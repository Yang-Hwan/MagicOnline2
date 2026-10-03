using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Come
{
    public class GesMember : MonoBehaviour
    {

        public enum Pnl
        {
            Login,
            Entry,
            FindId,
            FindPw
        }

        Pnl pnl;
        public GameObject[] pnls;

        private void Awake()
        {
            AllClose();
            pnls = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                pnls[i] = transform.GetChild(i).gameObject;
            }
        }

        private void OnEnable()
        {
            Open(Pnl.Login, true);
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
            if (p == pnl && !awake) return;
            pnls[(int)pnl].SetActive(false);
            pnl = p;
            pnls[(int)pnl].SetActive(true);
        }

    }
}
