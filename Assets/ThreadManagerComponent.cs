using Honey.Threading;
using UnityEngine;

namespace Honey.Unity
{
    public class ThreadManagerComponent : MonoBehaviour
    {
        public static ThreadManagerComponent Instance;

        [Range(1, 512)]
        public int ThreadCount = 16;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("Multiple ThreadManagers!");
                return;
            }

            Instance = this;
            ThreadManager.Init(ThreadCount);
        }

        void Update()
        {
            ThreadManager.Update();
        }

        void OnApplicationQuit()
        {
            ThreadManager.Shutdown();
        }
    }

}