using UnityEngine;

namespace ProphecyCentury.Core
{
    public sealed class BootstrapInstaller : MonoBehaviour
    {
        [SerializeField] private bool createRunOnStart = true;

        private void Awake()
        {
            var session = ProphecyGameSession.EnsureInstance();
            if (!createRunOnStart || session.HasCurrentRun)
            {
                return;
            }

            session.StartNewRun();
        }
    }
}
