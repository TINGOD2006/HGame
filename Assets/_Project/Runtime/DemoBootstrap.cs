using UnityEngine;

namespace HGame.Unity
{
    public sealed class DemoBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true;
            if (FindFirstObjectByType<JrpgPresenter>() == null) gameObject.AddComponent<JrpgPresenter>();
        }
    }
}
