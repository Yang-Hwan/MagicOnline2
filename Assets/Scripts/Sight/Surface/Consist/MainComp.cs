using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public class MainComp : MonoBehaviour
    {

        public bool _open;// { get; private set; }
        public int _num;// { get; private set; }
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] GameObject content;

        [SerializeField] TextMeshProUGUI txt03;
        [SerializeField] TextMeshProUGUI txt04;

        private void Awake()
        {
            _open = false;
            //scrollRect = transform.Find("RoomScrollView").GetComponent<ScrollRect>();
            scrollRect = transform.GetComponentInChildren<ScrollRect>();
            if (scrollRect != null)
            {
                Debug.Log($"{scrollRect.transform.GetChild(0).name}");
                content = scrollRect.transform.GetChild(0).GetChild(0).gameObject;
                txt03 = transform.Find("Info/Txt03").GetComponent<TextMeshProUGUI>();
                txt04 = transform.Find("Info/Txt04").GetComponent<TextMeshProUGUI>();
            }

            //content = scrollRect != null ? scrollRect.transform.GetChild(0).GetChild(0).gameObject: null;
        }

        private void Start()
        {
            if (scrollRect != null)
            {
                string msg = scrollRect.transform.GetChild(0).GetChild(0).name;
                msg += "; RoomSwipe : " + scrollRect.transform.GetChild(0).GetChild(0).GetComponent<RoomSwipe>().enabled;
                txt03.text = msg;
            }
        }

        public void SetTxt(int num)
        {
            this._num = num;
        }


        public void CompOpen(bool isOpen)
        {
            if (_open && !isOpen)
            {

                //Debug.Log($"Open num : {_num}  ...  _open : {_open}, IsOpen : {isOpen}.. scrollRect : {scrollRect != null}   ... blue");
            }
            else
            {
                //Debug.Log($"Open num : {_num}  ...  _open : {_open}, IsOpen : {isOpen}.. scrollRect : {scrollRect != null}   ... yellow");
            }

            if (scrollRect != null)
            {
                scrollRect.gameObject.SetActive(isOpen);
                bool isAct = scrollRect.gameObject.activeSelf;
                //Debug.Log($"_num : {_num} , isOpen : {isOpen}, isAct : {isAct}");

                //content.SetActive(isOpen);
            }
            //this.enabled = isOpen;
            _open = isOpen;
        }

    }
}
