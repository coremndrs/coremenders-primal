using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// In-scene NetworkBehaviour for the CharacterCreation scene.
    ///
    /// Each player edits their own dreamer's appearance (skin tone, hair style, hair colour)
    /// then confirms and clicks Ready. The host sees a Start button once both slots are ready
    /// (or immediately in solo — one connected client).
    ///
    /// Appearance is written to the RDM via ServerRpc. Dreamers are not yet spawned here;
    /// they pick up the RDM appearance in DreamerNetworkAdapter.OnNetworkSpawn when Action loads.
    ///
    /// Requires a NetworkObject component on the same GameObject (in-scene NetworkObject —
    /// no entry in Network Prefabs needed).
    /// </summary>
    public class CharacterCreationController : NetworkBehaviour
    {
        private const int SkinToneCount  = 6;
        private const int HairStyleCount = 4;
        private const int HairColorCount = 6;

        // ── Server-authoritative ready flags ────────────────────────────────────

        private readonly NetworkVariable<bool> _slot0Ready = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _slot1Ready = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Local UI state (not networked) ───────────────────────────────────────

        private int  _skinTone, _hairStyle, _hairColor;
        private bool _appearanceConfirmed;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                _slot0Ready.Value = false;
                _slot1Ready.Value = false;
            }
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsSpawned) return;

            int mySlot = GetLocalSlot();
            if (mySlot < 0)
            {
                GUILayout.BeginArea(new Rect(10, 100, 260, 30));
                GUILayout.Label("Connecting...");
                GUILayout.EndArea();
                return;
            }

            if (!_appearanceConfirmed)
                DrawAppearancePanel(mySlot);
            else
                DrawReadyPanel(mySlot);

            DrawStartButton();
        }

        private void DrawAppearancePanel(int slot)
        {
            GUILayout.BeginArea(new Rect(10, 100, 270, 190));
            GUILayout.Label($"Dreamer {slot} — Appearance");
            _skinTone  = CycleField("Skin Tone",  _skinTone,  SkinToneCount);
            _hairStyle = CycleField("Hair Style",  _hairStyle, HairStyleCount);
            _hairColor = CycleField("Hair Colour", _hairColor, HairColorCount);

            if (GUILayout.Button("Confirm"))
            {
                ConfirmAppearanceRpc(new AppearanceDataDto
                {
                    SkinTone  = _skinTone,
                    HairStyle = _hairStyle,
                    HairColor = _hairColor
                });
                _appearanceConfirmed = true;
            }
            GUILayout.EndArea();
        }

        private void DrawReadyPanel(int slot)
        {
            bool myReady = slot == 0 ? _slot0Ready.Value : _slot1Ready.Value;

            GUILayout.BeginArea(new Rect(10, 100, 270, 60));
            GUILayout.Label(myReady ? "✓ Ready" : "Appearance confirmed — click Ready when set.");
            if (!myReady && GUILayout.Button("Ready"))
                SetReadyRpc();
            GUILayout.EndArea();
        }

        private void DrawStartButton()
        {
            if (!IsHost) return;

            bool soloMode  = NetworkManager.Singleton.ConnectedClients.Count == 1;
            bool bothReady = _slot0Ready.Value && _slot1Ready.Value;
            if (!soloMode && !bothReady) return;

            GUILayout.BeginArea(new Rect(10, Screen.height - 60, 200, 50));
            if (GUILayout.Button("Start Game"))
                GameFlowManager.Instance.StartGame();
            GUILayout.EndArea();
        }

        // ── ServerRpcs ───────────────────────────────────────────────────────────

        [Rpc(SendTo.Server)]
        private void ConfirmAppearanceRpc(AppearanceDataDto dto, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!RuntimeDataManager.Instance.TryGetSlotForClient(senderId, out int slot))
            {
                Debug.LogWarning($"[Creation] ConfirmAppearance from unknown client {senderId}");
                return;
            }

            var record = RuntimeDataManager.Instance.GetDreamer(slot);
            if (record != null) record.appearance = dto.ToAppearanceData();

            Debug.Log($"[Creation] Slot {slot} appearance confirmed — skin {dto.SkinTone}");
        }

        [Rpc(SendTo.Server)]
        private void SetReadyRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!RuntimeDataManager.Instance.TryGetSlotForClient(senderId, out int slot))
            {
                Debug.LogWarning($"[Creation] SetReady from unknown client {senderId}");
                return;
            }

            if (slot == 0) _slot0Ready.Value = true;
            else           _slot1Ready.Value = true;

            Debug.Log($"[Creation] Slot {slot} ready. Both: {_slot0Ready.Value && _slot1Ready.Value}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static int GetLocalSlot()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient) return -1;
            // Phase 0: host always owns slot 0, sole joining client owns slot 1.
            // The RDM ownership map is host-only and not synced to clients,
            // so we infer the slot from network role rather than looking it up.
            return nm.IsHost ? 0 : 1;
        }

        private static int CycleField(string label, int value, int count)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(85));
            if (GUILayout.Button("◀", GUILayout.Width(26))) value = (value - 1 + count) % count;
            GUILayout.Label(value.ToString(), GUILayout.Width(20));
            if (GUILayout.Button("▶", GUILayout.Width(26))) value = (value + 1) % count;
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
