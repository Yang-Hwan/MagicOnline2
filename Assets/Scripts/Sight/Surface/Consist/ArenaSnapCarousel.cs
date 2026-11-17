using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.Sight.Surface.Consist
{
    // Position is measured in card pitches, independently of the Canvas scale/resolution.
    public sealed class ArenaSnapCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float Pitch=350;
        public float Position{get;private set;}
        public float Velocity{get;private set;}
        public float Sway{get;private set;}
        public int Target{get;private set;}
        public int Count{get;private set;}
        public bool Dragging{get;private set;}
        public bool Loop=>Count>=3;
        public bool Settled=>!Dragging && Mathf.Abs(Position-Target)<.001f && Mathf.Abs(Velocity)<.008f;
        public Action Changed;
        float dragOrigin,positionOrigin,lastX,lastTime,swaySpeed;
        RectTransform rect;
        public int Index(int logical)=>Count==0?-1:Loop?(logical%Count+Count)%Count:Mathf.Clamp(logical,0,Count-1);
        public void Initialize(int count,int selected)
        {
            rect=(RectTransform)transform;Count=Mathf.Max(0,count);
            Position=Target=Mathf.Clamp(selected,0,Mathf.Max(0,Count-1));
            Velocity=0;Sway=0;swaySpeed=0;Dragging=false;Changed?.Invoke();
        }
        public void SnapTo(int logical)
        {
            if(Count==0)return;
            Dragging=false;Target=Loop?logical:Mathf.Clamp(logical,0,Count-1);
            Changed?.Invoke();
        }
        float PointerX(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out var point);
            return point.x;
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if(Count<2 || data.button!=PointerEventData.InputButton.Left)return;
            Dragging=true;dragOrigin=lastX=PointerX(data);positionOrigin=Position;
            lastTime=Time.unscaledTime;Velocity=0;Changed?.Invoke();
        }
        public void OnDrag(PointerEventData data)
        {
            if(!Dragging)return;
            float x=PointerX(data),raw=positionOrigin-(x-dragOrigin)/Pitch;
            if(!Loop)
            {
                float bound=Mathf.Clamp(raw,0,Count-1);
                raw=bound+(raw-bound)*.23f;
            }
            float dt=Mathf.Max(.001f,Time.unscaledTime-lastTime);
            Velocity=Mathf.Lerp(Velocity,-(x-lastX)/Pitch/dt,.65f);
            Position=raw;lastX=x;lastTime=Time.unscaledTime;Changed?.Invoke();
        }
        public void OnEndDrag(PointerEventData data)
        {
            if(!Dragging)return;
            // A short flick advances naturally; a slow release chooses the nearest magnet.
            if(Time.unscaledTime-lastTime>.12f)Velocity=0;
            Velocity=Mathf.Clamp(Velocity,-5,5);
            SnapTo(Mathf.RoundToInt(Position+Mathf.Clamp(Velocity,-3,3)*.15f));
        }
        public void Advance(float seconds)
        {
            float remaining=Mathf.Min(seconds,.1f);
            while(remaining>0)
            {
                float dt=Mathf.Min(remaining,1f/120);remaining-=dt;
                if(!Dragging)
                {
                    Velocity+=((Target-Position)*85-Velocity*16)*dt;
                    Position+=Velocity*dt;
                    if(Mathf.Abs(Position-Target)<.001f && Mathf.Abs(Velocity)<.008f){Position=Target;Velocity=0;}
                }
                float desired=Mathf.Clamp(Velocity*2,-7,7);
                swaySpeed+=((desired-Sway)*65-swaySpeed*11)*dt;Sway+=swaySpeed*dt;
                if(Mathf.Abs(Sway)<.01f && Mathf.Abs(swaySpeed)<.02f){Sway=0;swaySpeed=0;}
            }
            Changed?.Invoke();
        }
        void Update(){if(Count>0)Advance(Time.unscaledDeltaTime);}
        void OnDisable(){Dragging=false;Velocity=0;}
    }
}
