using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProphecyCentury.UI
{
    public sealed class BootstrapSceneController : MonoBehaviour
    {
        [SerializeField] private string runSceneName = "RunScene";

        private void Start()
        {
            SceneManager.LoadScene(runSceneName);
        }
    }
}
