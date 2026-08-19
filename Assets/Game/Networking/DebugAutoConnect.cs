using System.Collections;
using Game.Persistence;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Multiplayer.Playmode;
#endif

namespace Game.Networking
{
    /// <summary>
    /// Debug bootstrap for SampleScene — bypasses the real scene flow (Build 0.0.1c+).
    ///
    /// Inactive by default (_active = false). Enable in the Inspector on SampleScene's
    /// DebugAutoConnect GameObject to restore the direct-connect behaviour for isolated testing.
    ///
    /// When active: detects MPPM instance via CurrentPlayer.IsMainEditor, auto-connects as
    /// host or client, loads template, and spawns dreamers in SampleScene without scene transitions.
    /// </summary>
    public class DebugAutoConnect : MonoBehaviour
    {
        [SerializeField] private bool _active = false;

        private IEnumerator Start()
        {
            if (!_active) yield break;

            // One-frame yield so all other Start() methods (e.g. DreamerSpawner) run first.
            yield return null;

            if (IsVirtualPlayer())
                ConnectAsClient();
            else
                ConnectAsHost();
        }

        private static bool IsVirtualPlayer()
        {
#if UNITY_EDITOR
            return !CurrentPlayer.IsMainEditor;
#else
            return false;
#endif
        }

        private void ConnectAsHost()
        {
            var (world, mapLayer) = SaveSystem.LoadTemplate();
            RuntimeDataManager.Instance.Populate(world);
            RuntimeDataManager.Instance.SetMapLayer(mapLayer);
            NetworkManager.Singleton.StartHost();
            Debug.Log("[DebugAutoConnect] Started as Host.");
        }

        private void ConnectAsClient()
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("[DebugAutoConnect] Started as Client.");
        }
    }
}
