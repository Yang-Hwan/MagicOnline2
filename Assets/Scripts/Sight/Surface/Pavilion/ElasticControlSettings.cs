using UnityEngine;
namespace Assets.Scripts.Sight.Surface.Pavilion
{
    [CreateAssetMenu(menuName="Magic/Elastic control appearance")]
    public sealed class ElasticControlSettings : ScriptableObject
    {
        [Tooltip("Off restores original sprites without changing shot physics or controls.")]
        public bool useNewAppearance = true;
        [Tooltip("Body height at 768px viewport height. Only the straight middle stretches; end caps, fasteners and display size stay fixed.")]
        [Min(240)] public float bodyHeight = 402;
        [Tooltip("Follow-through extension at 768px viewport height. Only the shaft middle stretches, never the dome or fixed display.")]
        [Min(20)] public float followTravel = 95;
    }
}
