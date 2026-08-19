using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Pre-network UI for the HostJoin scene. Delegates to GameFlowManager which
    /// starts NGO and drives the scene transition to CharacterCreation.
    /// </summary>
    public class HostJoinController : MonoBehaviour
    {
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 100, Screen.height / 2f - 40, 200, 80));
            if (GUILayout.Button("Host Game")) GameFlowManager.Instance.BeginHost();
            if (GUILayout.Button("Join Game")) GameFlowManager.Instance.BeginClient();
            GUILayout.EndArea();
        }
    }
}
