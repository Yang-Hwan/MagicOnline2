using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public class GesPopConsist : MonoBehaviour
    {

        public enum Pop
        {
            None,
            NickCh,
            Attendance,
        }

        Pop pop;
        public GameObject[] pops;
        public Image bg;


        void Awake()
        {
            bg = transform.GetComponent<Image>();
            pop = Pop.None;
            AllClose();
            pops = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++) pops[i] = transform.GetChild(i).gameObject;

        }

        private void OnEnable() => Close();

        private void OnDisable()
        {

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
            bg.enabled = false;
        }

        public void Open(Pop p)
        {
            if (p == pop) return;
            pops[(int)pop].SetActive(false);
            pop = p;
            pops[(int)pop].SetActive(true);
            bg.enabled = true;
        }

        public void Close()
        {
            pops[(int)pop].SetActive(false);
            bg.enabled = false;
            pop = Pop.None;
        }
    }
}
