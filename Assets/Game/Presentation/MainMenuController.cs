using Game.Networking;
using Game.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation
{
    /// <summary>
    /// Main menu. Provides three entry points (D7):
    ///   New Game  → HostJoin scene (host creates a session, CharacterCreation follows).
    ///   Load Game → GameFlowManager.BeginLoad() — host-only, skips CharacterCreation.
    ///   Join      → GameFlowManager.BeginClient() — joins an existing host session.
    /// Load Game is greyed out when no save exists on this machine.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private void OnGUI()
        {
            bool hasSave = SaveSystem.SlotExists();

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 100, Screen.height / 2f - 60, 200, 120));

            if (GUILayout.Button("New Game"))
                SceneManager.LoadScene("HostJoin");

            GUI.enabled = hasSave;
            if (GUILayout.Button("Load Game"))
                GameFlowManager.Instance.BeginLoad();
            GUI.enabled = true;

            if (GUILayout.Button("Join"))
                GameFlowManager.Instance.BeginClient();

            GUILayout.EndArea();
        }
    }
}
