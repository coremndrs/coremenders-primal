using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Host-side spawner. Listens for OnClientConnectedCallback and spawns the correct
    /// dreamer slot for each connecting client (slot 0 = host, slot 1 = first joiner).
    /// Does nothing on the client instance (IsServer guard).
    /// </summary>
    public class DreamerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _dreamerPrefab;

        private void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Host fires this callback for itself (slot 0) and for each joiner (slot 1).
            int slot = (clientId == NetworkManager.Singleton.LocalClientId) ? 0 : 1;
            SpawnSlot(slot, clientId);
        }

        private void SpawnSlot(int slot, ulong ownerId)
        {
            var record = RuntimeDataManager.Instance.GetDreamer(slot);
            if (record == null)
            {
                Debug.LogError($"[DreamerSpawner] No DreamerRecord for slot {slot}");
                return;
            }

            var go = Instantiate(_dreamerPrefab, record.position.ToVector3(), Quaternion.identity);
            go.GetComponent<DreamerNetworkAdapter>().Initialize(slot);
            go.GetComponent<NetworkObject>().SpawnWithOwnership(ownerId);
            RuntimeDataManager.Instance.SetOwnership(slot, ownerId);
        }
    }
}
