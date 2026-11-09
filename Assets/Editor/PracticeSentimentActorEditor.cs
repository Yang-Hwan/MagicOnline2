using UnityEditor;
using UnityEngine;
using Assets.Scripts.Sight.Surface.Pavilion;

[CustomEditor(typeof(PracticeSentimentActor))]
public sealed class PracticeSentimentActorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var actor=(PracticeSentimentActor)target;
        if(actor.artwork) {
        EditorGUILayout.Space();
        var area=GUILayoutUtility.GetRect(250,160,GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area,new Color(.025f,.1f,.04f));
        float aspect=actor.artwork.width*actor.uv.width/(actor.artwork.height*actor.uv.height);
        float width=Mathf.Min(area.width-12,(area.height-12)*aspect);
        var imageRect=new Rect(area.center.x-width/2,area.center.y-width/aspect/2,width,width/aspect);
        GUI.DrawTextureWithTexCoords(imageRect,actor.artwork,actor.uv,true);
        }
        using(new EditorGUI.DisabledScope(!Application.isPlaying))
            if(GUILayout.Button("연출재생")) {
                var controller=Object.FindFirstObjectByType<PracticeSentimentController>();
                if(controller)controller.Play(actor,controller.PreviewPosition,true);
                else if(MultiplayerSentimentController.Active) {
                    var multiplayer=MultiplayerSentimentController.Instance;
                    multiplayer.Play(actor,multiplayer.PreviewPosition,true);
                }
            }
    }
}
