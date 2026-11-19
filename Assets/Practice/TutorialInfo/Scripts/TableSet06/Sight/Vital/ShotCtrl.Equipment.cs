using System;
using UnityEngine;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Network;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class ShotCtrl
    {
        private DoubleTapCue doubleTap;
        private PremiumCueWorld premiumCue;
        private string displayedCue;
        private MeshRenderer standardCue;
        private Material standardFinish;
        private Material shopFinish;

        void RefreshCueAppearance()
        {
            if(!standardCue && cueSlider){
                var standard=cueSlider.Find("Cue3D/Standard3DCue");
                if(standard){standardCue=standard.GetComponent<MeshRenderer>();if(standardCue)standardFinish=standardCue.sharedMaterial;}
            }
            string key = MatchEquipmentRuntime.Shooter.CueVisual ?? "";
            if (key == displayedCue || !standardCue) return;
            displayedCue = key;
            if(premiumCue){premiumCue.Remove();premiumCue=null;}
            doubleTap?.Remove();
            doubleTap = null;
            standardCue.enabled = true;
            standardCue.sharedMaterial = standardFinish;
            if(PremiumCueDesign.Style(key)!=PremiumCueStyle.None)
            {
                premiumCue=PremiumCueWorld.Equip(cueSlider,key);return;
            }
            if (key == "Gameplay/DoubleTap/DoubleTap")
            {
                doubleTap = DoubleTapCue.Equip(cueSlider);
                return;
            }
            if (!key.StartsWith("Gameplay/ShopCues/", StringComparison.Ordinal)) return;
            var texture = Resources.Load<Texture2D>(key);
            var shader = Resources.Load<Shader>("Gameplay/ShopCues/CueFinish");
            if (!texture || !shader) return;
            if (!shopFinish) shopFinish = new Material(shader);
            var bounds = standardCue.GetComponent<MeshFilter>().sharedMesh.bounds;
            shopFinish.mainTexture = texture;
            shopFinish.SetVector("_CueAxis", new Vector4(bounds.min.z, bounds.size.z, 0, 0));
            standardCue.sharedMaterial = shopFinish;
        }


        void UpdateCueAppearance()
        {
            RefreshCueAppearance();
            doubleTap?.SetCharge(IsShotAnimating ? 0 : force);
            premiumCue?.SetCharge(IsShotAnimating ? 0 : force);
        }
        void StrikeCueAppearance(){doubleTap?.Strike();premiumCue?.Strike();}
        void DisposeCueAppearance(){if(shopFinish)Destroy(shopFinish);}
    }
}
