using System;
using UnityEngine;
using TMPro;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Cysharp.Threading.Tasks;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface
{
    public class WordBalloonCtrl : MonoBehaviour
    {

        [SerializeField] TextMeshProUGUI msgBox;
        [SerializeField] RectTransform rect;
        [SerializeField] PhysicsMng physicsManager;
        int _id = -1;
        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            msgBox = transform.Find("TxtMsg").GetComponent<TextMeshProUGUI>();
            physicsManager = FindObjectOfType<PhysicsMng>();

            //rect.anchoredPosition = new Vector2(500f, 300f);
            //rect.sizeDelta = new Vector2(200f, 80f);
        }

        public void MsgOpen(int id, string msg)
        {
            _id = id;
            physicsManager.ballcs[_id].OnCueBallMoveScreen += BallC_OnCueBallMoveScreen;
            MsgReceipt(msg);
            WaitMsg().Forget();
        }

        async UniTaskVoid WaitMsg()
        {

            await UniTask.Delay(3000);
            MsgClose();
        }

        public void MsgClose()
        {
            msgBox.text = "";
            physicsManager.ballcs[_id].OnCueBallMoveScreen -= BallC_OnCueBallMoveScreen;
            rect.anchoredPosition = new Vector2(-500f, 0f);
        }

        void BallC_OnCueBallMoveScreen(Vector3 pos)
        {
            rect.anchoredPosition = pos + new Vector3(60f, 20f, 0);
        }

        public void MsgReceipt(string msg)
        {
            if (msg.Length < 3)
            {
                rect.sizeDelta = new Vector2(110f, 80f);
            }
            else if (msg.Length < 5)
            {
                rect.sizeDelta = new Vector2(155f, 80f);
            }
            else if (msg.Length < 8)
            {
                rect.sizeDelta = new Vector2(220f, 80f);
            }
            else if (msg.Length < 14)
            {
                rect.sizeDelta = new Vector2(300f, 80f);
            }
            msgBox.text = msg;
        }



    }
}
