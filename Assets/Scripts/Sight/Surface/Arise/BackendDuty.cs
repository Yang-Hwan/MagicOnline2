using UnityEngine;
using BackEnd;

namespace Assets.Scripts.Sight.Surface.Arise
{
    public class BackendDuty : MonoBehaviour
    {
        public static BackendDuty Instance;

        public int mainP { get; private set; }
        public int subP { get; private set; }

        private void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(gameObject);
            BackendSetup();
        }

        public void ConsistPage(int m, int s = -1)
        {
            mainP = m;

            if (s == -1) return;
            subP = s;
        }

        void BackendSetup()
        {
            var bro = Backend.Initialize();
            if (!bro.IsSuccess())
            {
                Debug.LogError($"초기화 실패 : {bro}");
            }
        }



    }
}
