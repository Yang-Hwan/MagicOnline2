using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using Assets.Scripts.Exert.Network;

namespace Assets.Scripts.Often
{
    public class ChkBox01 : MonoBehaviour
    {

        [SerializeField] bool isVal = false;
        [SerializeField] string Nm = "";
        [SerializeField] RectTransform point;
        [SerializeField] TextMeshProUGUI txtName;
        [SerializeField] Color color = Color.blue;


        bool isAct = false;

        void Awake()
        {
            //DontDestroyOnLoad(this.gameObject);

            if (!NetworkManager.initialized)
            {
                return;
            }

            Init();
            PointMotionAsync();//.Forget();
        }

        private void OnEnable()
        {
            isAct = gameObject.activeSelf;
            //Debug.Log($"ChkBox01 OnEnable isAct : {isAct}");
        }

        private void OnDisable()
        {
            isAct = gameObject.activeSelf;
            //Debug.Log($"ChkBox01 OnDisable isAct : {isAct}");
        }

        void Init()
        {
            if (point == null)
            {
                point = transform.Find("Point").GetComponent<RectTransform>();
                txtName = transform.Find("TxtName").GetComponent<TextMeshProUGUI>();
            }
        }

        public void SetInfo(string nm, bool val = false)
        {
            Init();
            //Debug.Log("SetInfo nm : " + nm);
            isVal = val;
            Nm = nm;
            txtName.text = Nm;
            PointMotionAsync();//.Forget();
        }

        public void SetCh()
        {
            Vector2 pos_new = new Vector2(0, 0);
            point.anchoredPosition = pos_new;
        }

        public void SetCh(bool val, string nm = "")
        {
            txtName.text = nm != "" ? nm : txtName.text;
            isVal = val;
            //Debug.Log($"00. val : {val} ... nm : {nm} ----------------");
            //Debug.Log($"11. gmo : {isAct}-----------------");
            if (isAct )
            {
                PointMotionAsync();//.Forget();
            }
        }



        private async void PointMotionAsync()
        {
            Vector2 pos = point.anchoredPosition;
            Vector2 pos_new = new Vector2(isVal ? 25f : -25f, 0);
            float duration = 0.1f;
            float time = 0;
            while (time < duration)
            {
                time += Time.deltaTime;
                point.anchoredPosition = Vector2.Lerp(pos, pos_new, time / duration);
                await Task.Yield();// UniTask.WaitForFixedUpdate();
            }
            Color c = isVal ? color : Color.gray;
            point.GetComponent<Image>().color = c;
        }

 
 

    }
}
