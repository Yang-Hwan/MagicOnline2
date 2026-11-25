using TMPro;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // Presentation only: the server has already committed growth before this is created.
    public sealed class CueGrowthPlayback : MonoBehaviour
    {
        public const float Interval=.3f;
        int amount;
        RectTransform sweep;
        CanvasGroup group;
        TMP_Text value;
        public int Remaining {get;private set;}
        public float Duration=>amount*Interval;
        public float Progress {get;private set;}
        public float SweepX=>sweep?sweep.anchoredPosition.x:0;
        public void Configure(int count,RectTransform light,CanvasGroup opacity,TMP_Text label)
        {amount=count;sweep=light;group=opacity;value=label;Sample(0);}
        public void Sample(float elapsed)
        {
            elapsed=Mathf.Max(0,elapsed);Progress=Duration>0?Mathf.Clamp01(elapsed/Duration):1;
            Remaining=Mathf.Max(0,amount-Mathf.FloorToInt(elapsed/Interval));
            float phase=Mathf.Repeat(elapsed,Interval)/Interval;
            if(value)value.text=Remaining>0?"+"+Remaining:"0";
            if(group)group.alpha=Remaining>0?1-phase:0;
            if(sweep)sweep.anchoredPosition=new Vector2(Mathf.Lerp(540,-36,phase),0);
        }
    }
}
