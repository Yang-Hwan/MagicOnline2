using UnityEngine;
using UnityEngine.UI;
namespace Assets.Scripts.Often
{
    public static class GameplayActionButton
    {
        static readonly Sprite[] art=new Sprite[3];
        static readonly Material[] materials=new Material[3];
        public static Image Apply(GameObject target,int index)
        {
            if(!art[index]) {
                var texture=Resources.Load<Texture2D>("Gameplay/ActionButtons/ConceptAtlas");
                // Original concept sheet: exact approved angle, practice and score artwork.
                int[] left={1007,1181,1350};
                // Import/platform settings can resize a non-power-of-two atlas.
                // Convert the source-sheet coordinates into the loaded texture space.
                float sx=texture.width/1536f,sy=texture.height/1024f;
                var rect=new Rect(left[index]*sx,(1024-279)*sy,162*sx,158*sy);
                art[index]=Sprite.Create(texture,rect,Vector2.one*.5f,100,0,SpriteMeshType.FullRect);

            }
            var image=target.GetComponent<Image>();if(!image)image=target.AddComponent<Image>();
            image.sprite=art[index];image.useSpriteMesh=false;image.color=Color.white;image.preserveAspect=true;image.raycastTarget=true;
            image.type=Image.Type.Simple;
            if(!materials[index]) {
                materials[index]=new Material(Resources.Load<Shader>("Gameplay/ActionButtons/RoundedIcon"));
                var r=art[index].rect;var texture=art[index].texture;
                materials[index].SetVector("_IconRect",new Vector4(r.x/texture.width,r.y/texture.height,r.width/texture.width,r.height/texture.height));
            }
            image.material=materials[index];
            var button=target.GetComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,1,.9f);colors.pressedColor=new Color(.7f,.85f,.9f);button.colors=colors;
            return image;
        }
        public static void OpenAngle()
        {
            var small=GameObject.Find("PointSmall");if(!small)return;
            small.GetComponent<Button>()?.onClick.Invoke();
            var big=small.transform.parent.Find("PointBig");
            if(big&&big.gameObject.activeInHierarchy)big.GetComponent<CueAimPopup>()?.ShowMode(true);
        }
    }
}
