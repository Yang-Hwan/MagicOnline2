using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class RingBall : MonoBehaviour
    {

        public string hostName;
        public Material ringMat;
        public Material dirMat;

        private void Awake()
        {
            ringMat = transform.GetChild(0).GetComponent<MeshRenderer>().material;
            dirMat = transform.GetChild(1).GetComponent<MeshRenderer>().material;
        }

        public void SetInfo(string _name)
        {
            this.hostName = _name;
        }

 
        public void FigureVal(float f = 1f)
        {
            //Debug.Log($"FigureVal : {f}");
            ringMat.SetFloat("_Figure", f);
        }
 
        public void PercentageVal(float f = 1f)
        {
            ringMat.SetFloat("_Percentage", f);
        }

        [ContextMenu("Percentage01")]
        public void PercentageVal()
        {
            ringMat.SetFloat("_Percentage", 0.5f);
        }

        [ContextMenu("Figure02")]
        public void ZeroVal02()
        {
            ringMat.SetFloat("_Figure", 0.5f);
        }


        public void ZeroVal()
        {
            ringMat.SetFloat("_Percentage", 0);
            ringMat.SetFloat("_Figure", 0);
            dirMat.SetFloat("_Dir", 0);
        }

        public void ArrowDir(float dir)
        {
            DetailRotationArrowFade(dir).Forget();
        }

        // 세부조절시 방향 표시하기
        async UniTaskVoid DetailRotationArrowFade(float dir)
        {
            float a = 1f;
            float timer = 0;
            dirMat.SetFloat("_Dir", dir);
            //Debug.Log($"dir : {dir} ============ ");
            while (0 < a)
            {
                timer += Time.deltaTime;
                //Debug.Log($"dir : {dir}, a : {a} ");
                dirMat.SetFloat("_Alpha", a);
                a -= 0.05f;
                await UniTask.Yield();
            }
            //Debug.Log($"timer : {timer} .... ");

        }
    }
}
