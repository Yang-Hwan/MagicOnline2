using UnityEngine;

namespace Assets.Scripts.Often
{
    public sealed class DoubleTapCue
    {
        private static readonly int Charge = Shader.PropertyToID("_Charge");
        private static readonly int FlashStart = Shader.PropertyToID("_FlashStart");
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly MeshRenderer renderer;

        private DoubleTapCue(MeshRenderer renderer) => this.renderer = renderer;

        public static DoubleTapCue Equip(Transform slider)
        {
            var standard = slider ? slider.Find("Cue3D/Standard3DCue") : null;
            var prefab = Resources.Load<GameObject>("Gameplay/DoubleTap/DoubleTap");
            if (!standard || !prefab) return null;

            var bounds = standard.GetComponent<MeshFilter>().sharedMesh.bounds;
            var skin = Object.Instantiate(prefab, standard, false);
            skin.transform.localScale = Vector3.one * bounds.size.z;
            skin.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            standard.GetComponent<MeshRenderer>().enabled = false;
            return new DoubleTapCue(skin.GetComponentInChildren<MeshRenderer>());
        }

        public void SetCharge(float strength)
        {
            properties.SetFloat(Charge, Mathf.Clamp01(strength));
            renderer.SetPropertyBlock(properties);
        }

        public void Remove()
        {
            var skin = renderer.transform.parent.gameObject;
            skin.SetActive(false);
            Object.Destroy(skin);
        }

        public void Strike()
        {
            properties.SetFloat(FlashStart, Time.time);
            SetCharge(0);
        }
    }
}
