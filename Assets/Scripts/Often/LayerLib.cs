using UnityEngine;

namespace Assets.Scripts.Often
{

    public enum LayerKind
    {
        Cloth,
        Board,
        Ball,
        CueBall,
        Stuff,
        Item
    }

    public class LayerLib
    {
        public static int NameToInt(LayerKind layer) => 1 << LayerMask.NameToLayer(layer.ToString());
    }
}
