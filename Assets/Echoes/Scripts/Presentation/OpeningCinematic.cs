using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echoes
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed class OpeningCinematic : MonoBehaviour
    {
        private bool leaving;

        private void Start()
        {
            gameObject.AddComponent<TitleMenu>().Initialize(EnterDungeon);
        }

        private void EnterDungeon()
        {
            if (leaving) return;
            leaving = true;
            SceneManager.LoadSceneAsync("Prototype");
        }
    }
}
