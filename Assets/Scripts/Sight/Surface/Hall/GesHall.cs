using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Hall
{
    public class GesHall : MonoBehaviour
    {
        public enum Pnl
        {
            Lobby,
            WaitRoom,
        }

        Pnl pnl;
        public GameObject[] pnls;

        void Awake()
        {
            AllClose();
            pnls = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++) pnls[i] = transform.GetChild(i).gameObject;
        }

        private void OnEnable()
        {
            Open(Pnl.Lobby, true);
        }

        void AllClose()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                bool a = transform.GetChild(i).gameObject.activeSelf;
                if (a)
                {
                    transform.GetChild(i).gameObject.SetActive(false);
                }
            }
        }

        public void Open(Pnl p, bool awake = false)
        {
            Debug.Log($"GesHall Open p : {p} ... pnl : {pnl}");
            if (p == pnl && !awake) return;
            pnls[(int)pnl].SetActive(false);
            pnl = p;
            pnls[(int)pnl].SetActive(true);
        }

    }

}
