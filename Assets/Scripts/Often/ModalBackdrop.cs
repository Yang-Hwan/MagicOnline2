using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    // Shared modal contract: full-canvas dimming, outside click dismissal,
    // and a raycast surface shielding controls and empty space inside the card.
    public sealed class ModalBackdrop : MonoBehaviour,IPointerClickHandler,IPointerDownHandler,IBeginDragHandler,IDragHandler
    {
        static readonly System.Collections.Generic.HashSet<ModalBackdrop> active=new System.Collections.Generic.HashSet<ModalBackdrop>();
        static int dismissedFrame=-10;
        public static bool BlocksGameplayInput=>active.Count>0 || Time.frameCount<=dismissedFrame+1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState(){active.Clear();dismissedFrame=-10;}
        void OnEnable(){active.Add(this);}
        void OnDisable(){active.Remove(this);}
        void OnDestroy(){active.Remove(this);}
        Action dismiss;
        RectTransform surface;
        Image shade;
        Vector2 down;
        bool dragged;
        readonly Vector3[] corners=new Vector3[4];
        public static ModalBackdrop Attach(Image image,RectTransform card,Action close)
        {
            var modal=image.GetComponent<ModalBackdrop>()??image.gameObject.AddComponent<ModalBackdrop>();
            modal.shade=image;modal.surface=card;modal.dismiss=close;
            image.color=new Color(0,0,0,.68f);image.raycastTarget=true;
            AttachSurface(card);
            modal.Fit();return modal;
        }
        public static void AttachSurface(RectTransform card)
        {
            if(!card)return;
            var graphic=card.GetComponent<Graphic>();
            if(!graphic){var blocker=card.gameObject.AddComponent<Image>();blocker.color=Color.clear;graphic=blocker;}
            graphic.raycastTarget=true;
            if(!card.GetComponent<ModalSurface>())card.gameObject.AddComponent<ModalSurface>();
        }
        public static ModalBackdrop Create(RectTransform card,Action close)
        {
            var rect=new GameObject("Modal Backdrop",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(card.parent,false);rect.SetSiblingIndex(card.GetSiblingIndex());
            var modal=Attach(rect.GetComponent<Image>(),card,close);
            var life=card.GetComponent<ModalBackdropOwner>()??card.gameObject.AddComponent<ModalBackdropOwner>();
            life.Backdrop=rect.gameObject;
            return modal;
        }
        void LateUpdate(){Fit();}
        void Fit()
        {
            var canvas=GetComponentInParent<Canvas>();
            if(!canvas || !transform.parent)return;
            var root=(RectTransform)canvas.rootCanvas.transform;root.GetWorldCorners(corners);
            var parent=transform.parent;
            var min=parent.InverseTransformPoint(corners[0]);var max=parent.InverseTransformPoint(corners[2]);
            var rect=(RectTransform)transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
            rect.sizeDelta=new Vector2(Mathf.Abs(max.x-min.x),Mathf.Abs(max.y-min.y));
            rect.localPosition=(min+max)*.5f;
        }
        public void OnPointerDown(PointerEventData e){down=e.position;dragged=false;}
        public void OnBeginDrag(PointerEventData e){dragged=true;}
        public void OnDrag(PointerEventData e){dragged=true;}
        public void OnPointerClick(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left || dragged || (e.position-down).sqrMagnitude>64)return;
            if(surface && RectTransformUtility.RectangleContainsScreenPoint(surface,e.position,e.pressEventCamera))return;
            dismissedFrame=Time.frameCount;
            dismiss?.Invoke();
        }
    }
    public sealed class ModalSurface : MonoBehaviour,IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e){} // Consume blank-space clicks within the popup.
    }
    public sealed class ModalBackdropOwner : MonoBehaviour
    {
        public GameObject Backdrop;
        void OnEnable(){if(Backdrop)Backdrop.SetActive(true);}
        void OnDisable(){if(Backdrop)Backdrop.SetActive(false);}
        void OnDestroy(){if(Backdrop){if(Application.isPlaying)Destroy(Backdrop);else DestroyImmediate(Backdrop);}}
    }
}
