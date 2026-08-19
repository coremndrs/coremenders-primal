using System;
using Game.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    // Per-option display data pushed once when the vote opens.
    // CapturedAtMinutes + Coverage give clients enough to render "Personal — 2.3h ago".
    // Never serialized directly over the wire; packed into VoteOptionsPayload below.
    public struct VoteOptionData
    {
        public float CapturedAtMinutes;
        public byte  Coverage;              // CheckpointCoverage cast to byte
    }

    // Host-authoritative pick state. PickA = slot-0's choice, PickB = slot-1's choice.
    // -1 = not yet chosen. sbyte keeps the payload at 2 bytes.
    internal struct VoteState : INetworkSerializable, IEquatable<VoteState>
    {
        public sbyte PickA;
        public sbyte PickB;

        public static VoteState Empty => new VoteState { PickA = -1, PickB = -1 };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref PickA);
            s.SerializeValue(ref PickB);
        }

        public bool Equals(VoteState other) => PickA == other.PickA && PickB == other.PickB;
        public override bool Equals(object obj) => obj is VoteState v && Equals(v);
        public override int GetHashCode() => (PickA << 8) | (byte)PickB;
    }

    // Fixed 4-slot option payload + active flag. Only the first Count slots are meaningful.
    // All fields are primitives — no strings anywhere on the wire.
    internal struct VoteOptionsPayload : INetworkSerializable
    {
        public bool  IsActive;
        public byte  Count;
        public float Opt0Minutes; public byte Opt0Cov;
        public float Opt1Minutes; public byte Opt1Cov;
        public float Opt2Minutes; public byte Opt2Cov;
        public float Opt3Minutes; public byte Opt3Cov;

        public VoteOptionData GetOption(int i) => i switch
        {
            0 => new VoteOptionData { CapturedAtMinutes = Opt0Minutes, Coverage = Opt0Cov },
            1 => new VoteOptionData { CapturedAtMinutes = Opt1Minutes, Coverage = Opt1Cov },
            2 => new VoteOptionData { CapturedAtMinutes = Opt2Minutes, Coverage = Opt2Cov },
            3 => new VoteOptionData { CapturedAtMinutes = Opt3Minutes, Coverage = Opt3Cov },
            _ => default,
        };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref IsActive);
            s.SerializeValue(ref Count);
            s.SerializeValue(ref Opt0Minutes); s.SerializeValue(ref Opt0Cov);
            s.SerializeValue(ref Opt1Minutes); s.SerializeValue(ref Opt1Cov);
            s.SerializeValue(ref Opt2Minutes); s.SerializeValue(ref Opt2Cov);
            s.SerializeValue(ref Opt3Minutes); s.SerializeValue(ref Opt3Cov);
        }
    }

    /// <summary>
    /// Generic agree-on-an-index consensus component (TDD §0.1.7b, task b1).
    ///
    /// Value-agnostic: the server activates it with N option display records (numbers only —
    /// no strings). Each dreamer slot submits a pick index; when both match,
    /// OnConsensusReached fires with the agreed index. The CALLER maps that index back to
    /// the actual value (e.g. checkpoint ID kept in DreamFlowManager._pendingCheckpointIds).
    ///
    /// The skip-start vote (deferred, TDD §4.5) reuses this component with duration display
    /// data in place of checkpoint display data.
    ///
    /// No Unity.Collections, no strings, no FixedString — all wire data is numeric.
    /// </summary>
    public class ConsensusVoteComponent : NetworkBehaviour
    {
        // ── Server-authoritative state ────────────────────────────────────────────

        private readonly NetworkVariable<VoteOptionsPayload> _options = new NetworkVariable<VoteOptionsPayload>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<VoteState> _voteState = new NetworkVariable<VoteState>(
            VoteState.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Server-side guard ─────────────────────────────────────────────────────

        private bool _fired;

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>Server-side. Fires with the agreed option index when consensus is reached.</summary>
        public event Action<int> OnConsensusReached;

        // ── Public queries ────────────────────────────────────────────────────────

        public bool IsActive    => _options.Value.IsActive;
        public int  OptionCount => _options.Value.IsActive ? _options.Value.Count : 0;
        public int  PickA       => _voteState.Value.PickA;
        public int  PickB       => _voteState.Value.PickB;
        public bool BothAgree   => _voteState.Value.PickA >= 0 && _voteState.Value.PickA == _voteState.Value.PickB;

        public VoteOptionData GetOption(int i) => _options.Value.GetOption(i);

        // ── Activation (server-only) ──────────────────────────────────────────────

        /// <summary>
        /// Server-only. Starts a vote round with the given display-data options.
        /// The caller retains the authoritative value list and maps the agreed index.
        /// Solo (1 connected client) auto-confirms index 0 immediately.
        /// </summary>
        public void Activate(VoteOptionData[] options)
        {
            if (!IsServer) return;

            var payload = new VoteOptionsPayload
            {
                IsActive = true,
                Count    = (byte)Math.Min(options.Length, 4),
            };
            for (int i = 0; i < payload.Count; i++)
            {
                switch (i)
                {
                    case 0: payload.Opt0Minutes = options[i].CapturedAtMinutes; payload.Opt0Cov = options[i].Coverage; break;
                    case 1: payload.Opt1Minutes = options[i].CapturedAtMinutes; payload.Opt1Cov = options[i].Coverage; break;
                    case 2: payload.Opt2Minutes = options[i].CapturedAtMinutes; payload.Opt2Cov = options[i].Coverage; break;
                    case 3: payload.Opt3Minutes = options[i].CapturedAtMinutes; payload.Opt3Cov = options[i].Coverage; break;
                }
            }

            _fired           = false;
            _options.Value   = payload;
            _voteState.Value = VoteState.Empty;
        }

        public void Deactivate()
        {
            if (!IsServer) return;
            _options.Value   = default;
            _voteState.Value = VoteState.Empty;
        }

        // ── Pick submission (any client → server) ────────────────────────────────

        [Rpc(SendTo.Server)]
        public void SubmitPickServerRpc(int optionIndex, RpcParams rpcParams = default)
        {
            var opts = _options.Value;
            if (!opts.IsActive || optionIndex < 0 || optionIndex >= opts.Count) return;

            ulong callerId = rpcParams.Receive.SenderClientId;
            if (!RuntimeDataManager.Instance.TryGetSlotForClient(callerId, out int slot)) return;

            var state = _voteState.Value;
            if (slot == 0) state.PickA = (sbyte)optionIndex;
            else           state.PickB = (sbyte)optionIndex;
            _voteState.Value = state;

            CheckConsensus();
        }

        // ── Consensus detection ───────────────────────────────────────────────────

        private void CheckConsensus()
        {
            if (!IsServer || _fired || !_options.Value.IsActive) return;
            var state = _voteState.Value;

            int agreedIndex;
            if (NetworkManager.Singleton.ConnectedClientsList.Count == 1)
            {
                // Solo: the single player's pick is immediately the selection — no partner needed.
                int pick = state.PickA >= 0 ? state.PickA : state.PickB;
                if (pick < 0 || pick >= _options.Value.Count) return;
                agreedIndex = pick;
            }
            else
            {
                // Co-op: both slots must agree on the same index.
                if (state.PickA < 0 || state.PickB < 0) return;
                if (state.PickA != state.PickB) return;
                if (state.PickA >= _options.Value.Count) return;
                agreedIndex = state.PickA;
            }

            _fired = true;
            var ended = _options.Value;
            ended.IsActive = false;
            _options.Value = ended;

            OnConsensusReached?.Invoke(agreedIndex);
        }

        // ── Debug OnGUI (all clients) ────────────────────────────────────────────

        private WorldClockSync _clockSync;

        private void OnGUI()
        {
            if (!IsSpawned) return;
            var opts = _options.Value;

            // The Wake Up panel is click-driven; gameplay runs with the cursor locked (FP build b2).
            UiFocus.Set(this, opts.IsActive && opts.Count > 0);

            if (!opts.IsActive || opts.Count == 0) return;

            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter };
            var btnStyle   = new GUIStyle(GUI.skin.button) { fontSize = 14 };

            float w = 440f, h = 60f + opts.Count * 38f + 60f;
            float x = Screen.width  / 2f - w / 2f;
            float y = Screen.height / 2f - h / 2f;

            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Label(new Rect(x, y + 8f, w, 24f), "— Wake Up —", labelStyle);

            if (_clockSync == null) _clockSync = FindFirstObjectByType<WorldClockSync>();
            float nowMinutes = _clockSync?.GetCurrentMinutes() ?? 0f;
            var   state      = _voteState.Value;
            float btnY       = y + 40f;

            for (int i = 0; i < opts.Count; i++)
            {
                var    opt    = opts.GetOption(i);
                var    cov    = (CheckpointCoverage)opt.Coverage;
                float  lost   = Mathf.Max(0f, nowMinutes - opt.CapturedAtMinutes);
                string covStr = cov == CheckpointCoverage.Shared ? "Shared" : "Personal";
                string lbl    = $"{covStr} — {lost / 60f:F1}h ago";

                var saved = GUI.color;
                if (state.PickA == i && state.PickB == i)                GUI.color = Color.green;
                else if ((state.PickA == i && IsOwnerOfSlot(0)) ||
                         (state.PickB == i && IsOwnerOfSlot(1)))         GUI.color = Color.yellow;

                if (GUI.Button(new Rect(x + 20f, btnY, w - 40f, 30f), lbl, btnStyle))
                    SubmitPickServerRpc(i);
                GUI.color = saved;
                btnY += 36f;
            }

            string p0lbl = state.PickA >= 0 ? $"#{state.PickA}" : "–";
            string p1lbl = state.PickB >= 0 ? $"#{state.PickB}" : "–";
            GUI.Label(new Rect(x, btnY + 4f, w, 24f),
                $"Dreamer 0: {p0lbl}   |   Dreamer 1: {p1lbl}", labelStyle);
        }

        private bool IsOwnerOfSlot(int slot) =>
            RuntimeDataManager.Instance != null &&
            RuntimeDataManager.Instance.GetOwner(slot) == NetworkManager.Singleton.LocalClientId;
    }
}
