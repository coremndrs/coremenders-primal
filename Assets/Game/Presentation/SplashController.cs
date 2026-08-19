using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation
{
    public class SplashController : MonoBehaviour
    {
        [SerializeField] private float _delay = 2f;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(_delay);
            SceneManager.LoadScene("MainMenu");
        }
    }
}
