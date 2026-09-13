using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// An authored spawn marker. Drop an empty GameObject anywhere in the Action scene, add this
    /// component, and <see cref="DreamerSpawnPoints"/> will spawn dreamers here instead of at the
    /// template position.
    ///
    /// Place as many as you like — name them ("Camp", "Lake", "Ridge") and pick the active one by
    /// id on the <see cref="DreamerSpawnPoints"/> manager. That is the testing workflow: move a
    /// marker, or switch which marker is active, without touching the templates.
    ///
    /// The marker's <b>position</b> is the spawn point (optionally dropped onto the ground by the
    /// manager) and its <b>yaw</b> is the facing the dreamer starts with — FirstPersonLook adopts
    /// the body's authored heading on enable, so aiming the blue arrow aims the camera.
    ///
    /// Pure authoring data: no networking, no simulation state, never saved. It only decides where
    /// a body is placed at spawn; from that moment on position is owned by the save as usual.
    /// </summary>
    public sealed class DreamerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Label used to select this point on the DreamerSpawnPoints manager. Leave blank to " +
                 "use the GameObject's name.")]
        [SerializeField] private string _id = "";

        [Tooltip("Dreamer slot this marker is reserved for: 0 = host dreamer, 1 = joining dreamer, " +
                 "-1 = either (the manager then side-steps the second dreamer so the two do not " +
                 "spawn inside each other).")]
        [SerializeField] private int _slot = -1;

        [Header("Gizmo")]
        [SerializeField] private float _gizmoHeight = 1.8f;
        [SerializeField] private float _gizmoRadius = 0.35f;

        /// <summary>Selection label — falls back to the GameObject name when no id is authored.</summary>
        public string Id => string.IsNullOrWhiteSpace(_id) ? gameObject.name : _id.Trim();

        /// <summary>Slot this marker is reserved for, or -1 for either dreamer.</summary>
        public int Slot => _slot;

        // ── Gizmos ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()      => DrawGizmo(new Color(0.35f, 0.7f, 1f, 0.8f));
        private void OnDrawGizmosSelected() => DrawGizmo(new Color(0.4f, 1f, 0.6f, 1f));

        private void DrawGizmo(Color color)
        {
            Gizmos.color = color;

            Vector3 p  = transform.position;
            Vector3 up = Vector3.up * _gizmoHeight;

            // Body footprint + height, so the marker reads at terrain-sculpting zoom.
            DrawCircle(p, _gizmoRadius);
            DrawCircle(p + up, _gizmoRadius);
            Gizmos.DrawLine(p, p + up);

            // Facing arrow — the yaw the dreamer starts with.
            Vector3 eye  = p + Vector3.up * (_gizmoHeight * 0.9f);
            Vector3 tip  = eye + transform.forward * 1.2f;
            Gizmos.DrawLine(eye, tip);
            Gizmos.DrawLine(tip, tip - transform.forward * 0.3f + transform.right * 0.15f);
            Gizmos.DrawLine(tip, tip - transform.forward * 0.3f - transform.right * 0.15f);

#if UNITY_EDITOR
            string label = _slot < 0 ? Id : $"{Id} (slot {_slot})";
            UnityEditor.Handles.Label(p + up + Vector3.up * 0.2f, label);
#endif
        }

        private static void DrawCircle(Vector3 center, float radius, int segments = 24)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
