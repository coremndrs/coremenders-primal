using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Escape opens/closes the in-game menu. The menu itself does not affect the clock —
    /// the explicit Pause/Resume button inside it does. Hidden automatically while a Skip
    /// is in progress (SkipManager shows its own exclusive-mode overlay).
    ///
    /// Menu contents (host-only buttons greyed for client):
    ///   Pause / Resume  → toggles clock
    ///   Save
    ///   Load Save
    ///   — Skip (Dev) —
    ///   Skip 1h / 4h / 8h → SkipManager.StartSkip(minutes)
    ///   Skip 8h (interrupt) → StartSkip(480, randomStep) — schedules a debug interrupt at a random tick
    ///   — Dev —
    ///   Checkpoint      → F1 atomic checkpoint
    ///   Revert          → F2 revert via warm-apply path
    ///   Close
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        private bool _visible;

        private WorldClockSync _clockSync;
        private SkipManager    _skipMgr;

        private WorldClockSync ClockSync =>
            _clockSync != null ? _clockSync : (_clockSync = FindFirstObjectByType<WorldClockSync>());

        private SkipManager SkipMgr =>
            _skipMgr != null ? _skipMgr : (_skipMgr = FindFirstObjectByType<SkipManager>());

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                _visible = !_visible;

            // The menu's buttons need a mouse; gameplay runs with the cursor locked (FP build b2).
            UiFocus.Set(this, _visible);
        }

        private void OnDisable() => UiFocus.Release(this);

        private void OnGUI()
        {
            // Hide the pause menu while a skip is in progress — SkipManager owns the screen.
            if (SkipMgr != null && SkipMgr.IsSkipping)
            {
                _visible = false;
                return;
            }

            if (!_visible) return;

            bool isHost   = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            bool isPaused = ClockSync != null && ClockSync.IsSpawned && ClockSync.IsPaused;
            bool canSkip  = isHost && (SkipMgr == null || !SkipMgr.IsSkipping);

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 100, Screen.height / 2f - 170, 200, 340));
            GUILayout.Label("— Menu —");

            GUI.enabled = isHost;
            if (GUILayout.Button(isPaused ? "Resume" : "Pause"))
                ClockSync?.SetPaused(!isPaused);
            if (GUILayout.Button("Save"))
                GameFlowManager.Instance.SaveGame();
            if (GUILayout.Button("Load Save"))
                GameFlowManager.Instance.LoadGame();

            GUILayout.Space(4);
            GUILayout.Label("— Skip —");
            GUI.enabled = canSkip;
            if (GUILayout.Button("Skip 1h"))  SkipMgr?.StartSkip(60);
            if (GUILayout.Button("Skip 4h"))  SkipMgr?.StartSkip(240);
            if (GUILayout.Button("Skip 8h"))  SkipMgr?.StartSkip(480);
            if (GUILayout.Button("Skip 8h (interrupt)"))
                SkipMgr?.StartSkip(480, UnityEngine.Random.Range(1, 481));

            GUILayout.Space(4);
            GUILayout.Label("— Dev —");
            GUI.enabled = isHost;
            if (GUILayout.Button("Checkpoint"))
                GameFlowManager.Instance.TakeCheckpoint();
            if (GUILayout.Button("Revert"))
                GameFlowManager.Instance.RevertToCheckpoint();
            GUI.enabled = true;

            GUILayout.Space(4);
            if (GUILayout.Button("Close"))
                _visible = false;

            GUILayout.EndArea();
        }
    }
}
