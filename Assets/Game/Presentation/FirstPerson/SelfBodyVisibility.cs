using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// FP build, Group F — the owner never sees their own body, but still casts a shadow.
    ///
    /// Driven by <see cref="FirstPersonRig"/> (the single ownership check). Applied only on the
    /// owning client's copy of the dreamer: a remote client's copy of the same dreamer keeps its
    /// renderers untouched, so other players see a normal full body.
    ///
    /// <b>Mechanism:</b> <see cref="ShadowCastingMode.ShadowsOnly"/> on the body renderers, which
    /// satisfies both f1 (no visible self-mesh — and no near-plane clipping through the head) and f2
    /// (shadow preserved) with one switch. The layer + camera-culling-mask variant is deliberately
    /// NOT used: in URP an object outside the camera's culling mask is dropped from that camera's
    /// shadow pass too, which would take the shadow with it and break f2.
    /// </summary>
    public class SelfBodyVisibility : MonoBehaviour
    {
        [Tooltip("Renderers to hide from the owner. Leave empty to auto-collect every renderer under " +
                 "this dreamer at spawn.")]
        [SerializeField] private Renderer[] _bodyRenderers;

        private ShadowCastingMode[] _originalModes;
        private bool                _applied;

        /// <summary>
        /// Called once by FirstPersonRig with the result of its ownership check.
        /// Non-owners are left completely alone.
        /// </summary>
        public void ApplyOwnerState(bool isOwner)
        {
            if (isOwner) Hide();
            else         Restore();
        }

        private void Hide()
        {
            if (_applied) return;

            if (_bodyRenderers == null || _bodyRenderers.Length == 0)
                _bodyRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            _originalModes = new ShadowCastingMode[_bodyRenderers.Length];
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                var r = _bodyRenderers[i];
                if (r == null) continue;
                _originalModes[i]  = r.shadowCastingMode;
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            _applied = true;
        }

        private void Restore()
        {
            if (!_applied || _bodyRenderers == null) return;

            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                var r = _bodyRenderers[i];
                if (r == null || _originalModes == null || i >= _originalModes.Length) continue;
                r.shadowCastingMode = _originalModes[i];
            }

            _applied = false;
        }

        // Ownership never transfers in this project, but a despawn/respawn of the same instance
        // must not leave the mesh hidden for a client that is no longer the owner.
        private void OnDisable() => Restore();
    }
}
