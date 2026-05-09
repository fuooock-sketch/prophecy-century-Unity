using UnityEngine;

namespace ProphecyCentury.Core
{
    public sealed class BootstrapInstaller : MonoBehaviour
    {
        [SerializeField] private bool createRunOnStart = true;

        private void Awake()
        {
            if (ProphecyGameSession.Instance != null)
            {
                return;
            }

            var sessionObject = new GameObject("ProphecyGameSession");
            var session = sessionObject.AddComponent<ProphecyGameSession>();
            if (!createRunOnStart)
            {
                return;
            }

            session.StartNewRun();
        }
    }
}
