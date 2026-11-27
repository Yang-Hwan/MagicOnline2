using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    public sealed class ReplaySlotDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float HoldSeconds = .35f;
        int slot; float pressed; bool moving, held, pending, scrolling, armed; Func<bool> allowed; Action<int,int> move;
        Vector2 pressPosition;
        ScrollRect scroll; CanvasGroup ghost; readonly List<RaycastResult> hits=new List<RaycastResult>();
        public void Initialize(int index,Func<bool> canMove,Action<int,int> action){slot=index;allowed=canMove;move=action;}
        public void OnPointerDown(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;
            pressed=Time.unscaledTime;pressPosition=e.position;held=true;armed=false;
        }
        public void OnPointerUp(PointerEventData e){held=false;if(!moving)ResetVisual();}
        void Update()
        {
            if(held && !scrolling && !armed && Time.unscaledTime-pressed>=HoldSeconds && allowed())
            {
                armed=true;ShowReady();
            }
        }
        void ShowReady()
        {
            if(!ghost)ghost=GetComponent<CanvasGroup>();
            if(!ghost)ghost=gameObject.AddComponent<CanvasGroup>();
            ghost.alpha=.55f;transform.localScale=Vector3.one*1.1f;
        }
        void ResetVisual(){if(ghost)ghost.alpha=1;transform.localScale=Vector3.one;armed=false;}
        public void OnBeginDrag(PointerEventData e)
        {
            scroll=GetComponentInParent<ScrollRect>();
            if(e.button!=PointerEventData.InputButton.Left)return;
            moving=(armed || Time.unscaledTime-pressed>=HoldSeconds) && allowed();
            pending=!moving;scrolling=false;
            if(moving){ShowReady();if(scroll)scroll.StopMovement();}
            else OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if(pending)
            {
                // Small finger/mouse jitter must not turn a long press into scrolling.
                if((armed || Time.unscaledTime-pressed>=HoldSeconds) && allowed())
                {moving=true;pending=false;ShowReady();if(scroll)scroll.StopMovement();}
                else if(Vector2.Distance(e.position,pressPosition)>20 || !allowed())
                {pending=false;scrolling=true;ResetVisual();if(scroll)scroll.OnBeginDrag(e);}
                else return;
            }
            if(!moving){if(scrolling && scroll)scroll.OnDrag(e);return;}
            if(!scroll)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(scroll.viewport,e.position,e.pressEventCamera,out var point);
            var rect=scroll.viewport.rect;
            float direction=point.x<rect.xMin+48?-1:point.x>rect.xMax-48?1:0;
            float overflow=scroll.content.rect.width-rect.width;
            if(overflow>0)scroll.horizontalNormalizedPosition=Mathf.Clamp01(scroll.horizontalNormalizedPosition+direction*650*Time.unscaledDeltaTime/overflow);
        }
        public void OnEndDrag(PointerEventData e)
        {
            held=false;pending=false;
            if(!moving){if(scrolling && scroll)scroll.OnEndDrag(e);scrolling=false;ResetVisual();return;}
            moving=false;ResetVisual();
            hits.Clear();EventSystem.current.RaycastAll(e,hits);
            foreach(var hit in hits){var target=hit.gameObject.GetComponentInParent<ReplaySlotDrag>();if(target && target.GetComponentInParent<ScrollRect>()==scroll){move(slot,target.slot);break;}}
        }
        void OnDisable(){held=pending=scrolling=moving=false;ResetVisual();}
    }
}
