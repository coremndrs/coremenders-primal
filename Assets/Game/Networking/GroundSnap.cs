using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Shared "put this on the ground" resolve, used by every host-side placement: dreamer spawn
    /// markers (<see cref="DreamerSpawnPoints"/>) and world objects/items entering the world
    /// (<see cref="MapEntitySync"/>).
    ///
    /// <para><b>The rule.</b> Cast down through the position and prefer the <i>highest surface at or
    /// below</i> it — what the thing would fall onto. Only when there is none (the position is buried
    /// inside the terrain, e.g. a fallen tree yielded from a trunk whose base was sunk) fall back to
    /// the <i>lowest surface above</i> it, which lifts the object out onto the surface instead of
    /// leaving it underground. Preferring below-first is what keeps a drop under an overhang or
    /// inside a shelter landing on the floor rather than teleporting onto the roof.</para>
    ///
    /// <para>Dreamers are never ground — the probe starts above the position and would otherwise
    /// pass straight through the dreamer standing at it.</para>
    ///
    /// <para>Host-side and one-shot by design: the resolved position is written into authoritative
    /// state at placement time, so it saves, syncs and reverts like any other position. Nothing
    /// re-snaps on load — geometry is not re-queried after the fact.</para>
    /// </summary>
    public static class GroundSnap
    {
        /// <summary>Tolerance for "at or below" — a position resting exactly on a surface counts as below.</summary>
        private const float LevelEpsilon = 0.05f;

        /// <summary>
        /// Resolves <paramref name="position"/> onto the ground beneath (or above) it.
        /// Returns false and leaves <paramref name="grounded"/> = <paramref name="position"/> when
        /// nothing was hit — an authored height over a hole stays where it was authored.
        /// </summary>
        /// <param name="mask">Layers treated as ground.</param>
        /// <param name="probeAbove">How far above the position to start the downward cast. Also the
        /// depth from which a buried position can be rescued.</param>
        /// <param name="probeBelow">How far below the position to keep looking.</param>
        /// <param name="footOffset">Local-space Y of the object's base relative to its pivot —
        /// negative when the pivot sits above the base (the usual case). See
        /// <see cref="PrefabFootOffset"/> and the CharacterController equivalent at the call site.</param>
        /// <param name="clearance">Extra gap left above the surface.</param>
        /// <param name="ignoreRoot">Optional transform whose own colliders are not ground — the
        /// object being placed, when its visual already exists (a carried object being dropped).</param>
        public static bool TryResolve(Vector3 position, LayerMask mask, float probeAbove, float probeBelow,
            float footOffset, float clearance, Transform ignoreRoot, out Vector3 grounded)
        {
            grounded = position;

            var origin = position + Vector3.up * probeAbove;
            var hits   = Physics.RaycastAll(origin, Vector3.down, probeAbove + probeBelow,
                                            mask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool  found    = false;
            float surfaceY = 0f;

            foreach (var hit in hits)
            {
                if (!IsGround(hit, ignoreRoot)) continue;

                if (hit.point.y <= position.y + LevelEpsilon)
                {
                    // Sorted top-down, so this is the highest surface under the position: what it
                    // would land on. Nothing further down can beat it.
                    surfaceY = hit.point.y;
                    found    = true;
                    break;
                }

                // Above the position — keep the last one, i.e. the lowest ceiling/surface over it.
                // Only used if no surface below exists (the position is buried).
                surfaceY = hit.point.y;
                found    = true;
            }

            if (!found) return false;

            grounded = new Vector3(position.x, surfaceY - footOffset + clearance, position.z);
            return true;
        }

        private static bool IsGround(RaycastHit hit, Transform ignoreRoot)
        {
            var col = hit.collider;
            if (col == null) return false;

            // A dreamer is standing at most drop positions; it is never the floor.
            if (col.GetComponentInParent<DreamerNetworkAdapter>() != null) return false;

            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) return false;
            return true;
        }

        /// <summary>
        /// Local-space Y of a prefab's visual base relative to its pivot, from the combined renderer
        /// bounds — negative when the pivot is above the base (a centre-pivoted log), 0 when the
        /// pivot already sits at the base. Feed it to <see cref="TryResolve"/> as
        /// <c>footOffset</c> so the object rests on the surface instead of half-sinking into it.
        ///
        /// Returns 0 for a prefab with no renderers, and for a nonsense result (base above pivot, or
        /// an implausibly large extent) — placing at the pivot is the safe fallback.
        /// </summary>
        public static float PrefabFootOffset(GameObject prefab)
        {
            if (prefab == null) return 0f;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float offset = bounds.min.y - prefab.transform.position.y;
            if (offset > 0f || offset < -50f) return 0f;
            return offset;
        }
    }
}
