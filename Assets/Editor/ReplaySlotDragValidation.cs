using System;
using System.IO;
using System.Reflection;
using Assets.Scripts.Often;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ReplaySlotDragValidation
{
    const string Request="Logs/replay-drag.request";
    static ReplaySlotDragValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying)return;
        File.Delete(Request);Run();
    }
    static FieldInfo Field(string name)=>typeof(ReplaySlotDrag).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
    static bool Flag(ReplaySlotDrag drag,string name)=>(bool)Field(name).GetValue(drag);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    [MenuItem("Tools/Magic Online/Validate Replay Slot Drag")]
    public static void Run()
    {
        GameObject root=null;
        try
        {
            root=new GameObject("Temporary Replay Drag Validation",typeof(RectTransform),typeof(ScrollRect));
            root.hideFlags=HideFlags.HideAndDontSave;
            var scroll=root.GetComponent<ScrollRect>();
            scroll.viewport=(RectTransform)root.transform;
            var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root.transform,false);scroll.content=content;
            var slot=new GameObject("Slot",typeof(RectTransform),typeof(Image),typeof(Button),typeof(ReplaySlotDrag));
            slot.transform.SetParent(content,false);
            var drag=slot.GetComponent<ReplaySlotDrag>();drag.Initialize(0,()=>true,(a,b)=>{});
            var pointer=new PointerEventData(null){button=PointerEventData.InputButton.Left,position=Vector2.zero};
            drag.OnPointerDown(pointer);
            Field("pressed").SetValue(drag,Time.unscaledTime-.4f);
            typeof(ReplaySlotDrag).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(drag,null);
            Check(Flag(drag,"armed"),"Long press did not arm");
            Check(slot.transform.localScale.x>1,"Long press has no visual feedback");
            drag.OnBeginDrag(pointer);Check(Flag(drag,"moving"),"Long press did not start move");
            drag.SendMessage("OnDisable");
            drag.OnPointerDown(pointer);pointer.position=new Vector2(8,0);drag.OnBeginDrag(pointer);
            Check(Flag(drag,"pending"),"Small jitter incorrectly began scrolling");
            Field("pressed").SetValue(drag,Time.unscaledTime-.4f);drag.OnDrag(pointer);
            Check(Flag(drag,"moving"),"Long press after jitter did not start move");
            drag.SendMessage("OnDisable");pointer.position=Vector2.zero;drag.OnPointerDown(pointer);
            pointer.position=new Vector2(40,0);drag.OnBeginDrag(pointer);
            Check(Flag(drag,"scrolling")&&!Flag(drag,"moving"),"Quick swipe did not scroll");
            drag.OnEndDrag(pointer);Check(slot.transform.localScale==Vector3.one,"Feedback was not reset");
            File.WriteAllText("Logs/replay-drag-validation.txt","PASS: long press feedback, drag start, jitter tolerance, quick swipe scrolling, cancellation cleanup.");
        }
        catch(Exception e){File.WriteAllText("Logs/replay-drag-validation.txt","FAIL: "+e);Debug.LogException(e);}
        finally{if(root)UnityEngine.Object.DestroyImmediate(root);}
    }
}
