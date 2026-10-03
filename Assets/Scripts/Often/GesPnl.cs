using UnityEngine;

namespace Assets.Scripts.Often
{
    public class GesPnl : MonoBehaviour
    {


        public enum Pnl
        {
            Zero,
            One,
            Two,
        }

        public Pnl pnl { get; private set; }
        public GameObject[] pnls;


        void Awake()
        {
            //Debug.Log($"GesPnl Awake");
            pnls = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++) pnls[i] = transform.GetChild(i).gameObject;
            Open(pnl);
          

        }

        public void Open(Pnl p)
        {
            //Debug.Log($"GesPnl Open p: {(int)p}, pnl : {(int)pnl}   ... pnls let : {pnls.Length} ... objName : {transform.GetChild(0).gameObject.name} ");
            if (p == pnl)
            {
                if (pnls[(int)pnl] == null)
                {
                    Debug.Log(pnls[0]?.name??$"xxxx .. {transform.GetChild(0).gameObject.name} ");
                }
                else
                {
                    if (!pnls[(int)pnl].activeSelf)
                    {
                        pnls[(int)pnl].SetActive(true);
                    }
                    else
                    {

                    }
                }

                return;
            }
            
            pnls[(int)pnl].SetActive(false);
            pnl = p;
            pnls[(int)pnl].SetActive(true);
            Debug.Log($"GesPnl Open END END END ");
        }

        private void OnEnable()
        {
            //Open(GesPnl.Pnl.Zero);
        }

        private void OnDisable()
        {
            Open(GesPnl.Pnl.Zero);
        }
    }
}
