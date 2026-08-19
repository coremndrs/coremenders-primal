using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// NetworkBehaviour on every dreamer prefab instance.
    ///
    /// Owns two server-authoritative NetworkVariables:
    ///   _slotVar    — the dreamer slot index (0 or 1), set in OnNetworkSpawn on the server.
    ///   _appearance — the appearance DTO, set from the RDM on spawn and updated via ServerRpc.
    ///
    /// B5 (Phase 0): applies skin-tone color to the MeshRenderer as a placeholder until
    /// real materials/meshes exist. Hair style/color are stored but not yet visually applied.
    /// </summary>
    public class DreamerNetworkAdapter : NetworkBehaviour
    {
        // Predefined skin-tone colors (placeholder — replaced by real materials later).
        private static readonly Color[] SkinTones =
        {
            new Color(1.00f, 0.87f, 0.78f), // very light
            new Color(0.96f, 0.76f, 0.60f), // light
            new Color(0.88f, 0.64f, 0.44f), // medium-light
            new Color(0.74f, 0.48f, 0.28f), // medium
            new Color(0.54f, 0.33f, 0.15f), // medium-dark
            new Color(0.32f, 0.18f, 0.06f), // dark
        };

        // ── NetworkVariables ────────────────────────────────────────────────────

        private readonly NetworkVariable<int> _slotVar = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<AppearanceDataDto> _appearance = new NetworkVariable<AppearanceDataDto>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ── Pre-spawn host-only slot, set by DreamerSpawner before SpawnWithOwnership ──

        private int _localSlot = -1;

        // ── Public API ──────────────────────────────────────────────────────────

        public int Slot => _slotVar.Value;
        public AppearanceDataDto AppearanceDto => _appearance.Value;

        /// <summary>Called by DreamerSpawner immediately after Instantiate, before SpawnWithOwnership.</summary>
        public void Initialize(int slot) => _localSlot = slot;

        // ── Lifecycle ───────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                _slotVar.Value = _localSlot;

                var record = RuntimeDataManager.Instance.GetDreamer(_localSlot);
                if (record != null)
                    _appearance.Value = AppearanceDataDto.From(record.appearance);
            }

            // Register this transform so the save system can sample live position on demand (TDD §1.5).
            RuntimeDataManager.Instance.RegisterDreamerTransform(Slot, transform);

            // Guard prevents double-subscription if OnNetworkSpawn fires more than once on this instance.
            _appearance.OnValueChanged -= OnAppearanceChanged;
            _appearance.OnValueChanged += OnAppearanceChanged;

            // OnValueChanged does not fire for the initial synced value — apply it manually.
            ApplyAppearance(_appearance.Value);

            Debug.Log($"[Dreamer] Slot {Slot} spawned — ownerClient: {OwnerClientId}, isOwner: {IsOwner}");
        }

        public override void OnNetworkDespawn()
        {
            RuntimeDataManager.Instance?.UnregisterDreamerTransform(Slot);
            _appearance.OnValueChanged -= OnAppearanceChanged;
            base.OnNetworkDespawn();
        }

        // ── Authority override — position snap (D5, reused by 0.0.5 Wake Up) ────

        /// <summary>
        /// Sent by the server to the owner. The owner applies an immediate position snap
        /// (bypassing interpolation) then the owner-authoritative NetworkTransform broadcasts
        /// the new position to all other clients.
        ///
        /// The host calls this on its own dreamer directly (runs locally) and sends it to
        /// client-owned dreamers via the [Rpc(SendTo.Owner)] delivery.
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void SnapToPositionRpc(Vector3 position)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                transform.position = position;
                cc.enabled = true;
            }
            else
            {
                transform.position = position;
            }
        }

        // ── Appearance — server writes ───────────────────────────────────────────

        /// <summary>
        /// Called by the owning client to confirm their appearance choice during creation.
        /// The server writes the RDM and the NetworkVariable, replicating to all clients.
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void ConfirmAppearanceServerRpc(AppearanceDataDto dto)
        {
            _appearance.Value = dto;

            var record = RuntimeDataManager.Instance.GetDreamer(_slotVar.Value);
            if (record != null)
                record.appearance = dto.ToAppearanceData();

            Debug.Log($"[Dreamer] Slot {_slotVar.Value} appearance confirmed — " +
                      $"skin {dto.SkinTone}, hair {dto.HairStyle}/{dto.HairColor}");
        }

        /// <summary>
        /// Called server-side by the load system (D5 warm path) to reapply saved appearance.
        /// Sets the server-authoritative NetworkVariable, which replicates to all clients.
        /// The RDM record is already populated from the save before this is called.
        /// </summary>
        public void SetAppearanceFromServer(AppearanceDataDto dto)
        {
            if (!IsServer) return;
            _appearance.Value = dto;
        }

        // ── Appearance application (B5 placeholder) ─────────────────────────────

        private void OnAppearanceChanged(AppearanceDataDto _, AppearanceDataDto current)
            => ApplyAppearance(current);

        private void ApplyAppearance(AppearanceDataDto dto)
        {
            var meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer == null) return;

            int idx = System.Math.Clamp(dto.SkinTone, 0, SkinTones.Length - 1);
            meshRenderer.material.color = SkinTones[idx];

            // HairStyle and HairColor stored but not yet visually applied —
            // requires hair mesh variants and materials (added with proper assets).
        }
    }
}
