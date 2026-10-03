using UnityEngine;


namespace Assets.Scripts.Sight.Surface.Consist
{
    public class ConsistMng : MonoBehaviour
    {

        [SerializeField] Camera _camera;

        private void Awake()
        {
            //Rect rect = _camera.rect;
            //float scaleheight = ((float)Screen.width / Screen.height) / ((float)16 / 9);        // (가로 / 세로)
            //float scalewidth = 1f / scaleheight;
            //if (scaleheight < 1)
            //{
            //    rect.height = scaleheight;
            //    rect.y = (1f - scaleheight) / 2f;
            //}
            //else
            //{
            //    rect.width = scalewidth;
            //    rect.x = (1f - scalewidth) / 2f;
            //}
            //_camera.rect = rect;
        }


    }
}
