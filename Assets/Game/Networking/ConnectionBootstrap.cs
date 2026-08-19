using Unity.Netcode;
using UnityEngine;

namespace Coremenders.Networking
{
    /// <summary>
    /// Build 0.0.0 smoke-test bootstrap. Provides Start Host / Start Client buttons
    /// via OnGUI and logs every client connection on the host side.
    /// Scene wiring is manual: add a NetworkManager + UnityTransport to the scene,
    /// then attach this component to any GameObject.
    /// </summary>
    public class ConnectionBootstrap : MonoBehaviour
    {
        private void OnGUI()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 200, 80));

            if (GUILayout.Button("Start Host"))
                StartAsHost();

            if (GUILayout.Button("Start Client"))
                StartAsClient();

            GUILayout.EndArea();
        }

        private void StartAsHost()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartHost();
            Debug.Log($"[Bootstrap] Started as Host. Local client ID: {NetworkManager.Singleton.LocalClientId}");
        }

        private void StartAsClient()
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("[Bootstrap] Started as Client.");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[Bootstrap] Client connected — ID: {clientId}");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
