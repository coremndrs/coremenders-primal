using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Build 0.0.1b creation UI — replaced by proper UI in Build 0.0.1c.
    ///
    /// Finds the locally owned DreamerNetworkAdapter and shows three +/- fields
    /// (skin tone, hair style, hair color) with a Confirm button. Only the
    /// owning client can edit its own dreamer; the other dreamer's fields are hidden.
    /// On Confirm, calls ConfirmAppearanceServerRpc which syncs to all clients.
    /// </summary>
    public class DreamerCreationUI : MonoBehaviour
    {
        private const int SkinToneCount  = 6;
        private const int HairStyleCount = 4;
        private const int HairColorCount = 6;

        private DreamerNetworkAdapter _adapter;
        private int  _skinTone;
        private int  _hairStyle;
        private int  _hairColor;
        private bool _confirmed;

        private void Update()
        {
            if (_confirmed || _adapter != null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient) return;

            foreach (var adapter in FindObjectsByType<DreamerNetworkAdapter>(FindObjectsSortMode.None))
            {
                if (!adapter.IsOwner) continue;

                _adapter   = adapter;
                var dto    = adapter.AppearanceDto;
                _skinTone  = dto.SkinTone;
                _hairStyle = dto.HairStyle;
                _hairColor = dto.HairColor;
                break;
            }
        }

        private void OnGUI()
        {
            if (_confirmed || _adapter == null) return;

            GUILayout.BeginArea(new Rect(10, 100, 260, 175));
            GUILayout.Label($"Dreamer {_adapter.Slot} — Appearance");

            _skinTone  = CycleField("Skin Tone",  _skinTone,  SkinToneCount);
            _hairStyle = CycleField("Hair Style",  _hairStyle, HairStyleCount);
            _hairColor = CycleField("Hair Color",  _hairColor, HairColorCount);

            if (GUILayout.Button("Confirm"))
            {
                _adapter.ConfirmAppearanceServerRpc(new AppearanceDataDto
                {
                    SkinTone  = _skinTone,
                    HairStyle = _hairStyle,
                    HairColor = _hairColor
                });
                _confirmed = true;
            }

            GUILayout.EndArea();
        }

        private static int CycleField(string label, int value, int count)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            if (GUILayout.Button("◀", GUILayout.Width(26))) value = (value - 1 + count) % count;
            GUILayout.Label(value.ToString(), GUILayout.Width(20));
            if (GUILayout.Button("▶", GUILayout.Width(26))) value = (value + 1) % count;
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
