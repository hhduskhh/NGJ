using UnityEngine;
using UnityEngine.SceneManagement;

namespace Echoes
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed class OpeningCinematic : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        private bool leaving;

        private void Start()
        {
            gameObject.AddComponent<TitleMenu>().Initialize(EnterDungeon, config);
        }

        private void EnterDungeon(Hero hero)
        {
            if (leaving) return;
            leaving = true;
            RunController.NextCharacter = hero;
            SceneManager.LoadSceneAsync("Prototype");
        }
    }
}
