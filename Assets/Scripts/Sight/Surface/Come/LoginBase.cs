using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

namespace Assets.Scripts.Sight.Surface.Come
{
    public class LoginBase : MonoBehaviour
    {

        [SerializeField] protected TextMeshProUGUI txtMsg;

        protected virtual void Awake()
        {
            txtMsg = GameObject.Find("TxtRetMsg").GetComponent<TextMeshProUGUI>();
        }


        protected void ResetUI(params Image[] images)
        {
            txtMsg.text = string.Empty;
            images.ToList().ForEach(r => r.color = Color.white);
        }

        protected void SetMessage(string msg) => txtMsg.text = msg;

        protected void GuideForIncorrectlyEnteredData(Image image, string msg)
        {
            txtMsg.text = msg;
            image.color = Color.red;
        }

        protected bool IsFieldDataEmpty(Image image, string field, string result)
        {
            if (field.Trim().Equals(""))
            {
                GuideForIncorrectlyEnteredData(image, $"\"{result}\" 필드를 채워주세요.");
                return true;
            }

            return false;
        }

    }
}
