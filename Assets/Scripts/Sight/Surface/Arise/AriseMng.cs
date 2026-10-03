using Assets.Scripts.Often;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Arise
{

    public class AriseMng : MonoBehaviour
    {

        [SerializeField]
        BarProgress progress;
        [SerializeField]
        SceneNames nextScene;
        [SerializeField]
        Camera _camera;

        private void Awake()
        {
            //Camera camera = GetComponent<Camera>();
            Rect rect = _camera.rect;
            float scaleheight = ((float)Screen.width / Screen.height) / ((float)16 / 9);        // (가로 / 세로)
            float scalewidth = 1f / scaleheight;
            if(scaleheight < 1)
            {
                rect.height = scaleheight;
                rect.y = (1f - scaleheight) / 2f;
            }
            else
            {
                rect.width = scalewidth;
                rect.x = (1f - scalewidth) / 2f;
            }
            _camera.rect = rect;

            //Screen.SetResolution(960, 540, false);
            progress = GameObject.Find("Canvas/Progress").GetComponent<BarProgress>();
            nextScene = SceneNames.Come;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void Start()
        {
            SystemSetup();
        }

        void SystemSetup()
        {
            progress.Play(OnAfterProgress);
        }

        void OnAfterProgress()
        {
            SceneMove.LoadScene(nextScene);
        }
    }
}