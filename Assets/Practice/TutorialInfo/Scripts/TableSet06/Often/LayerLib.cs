using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Often
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
