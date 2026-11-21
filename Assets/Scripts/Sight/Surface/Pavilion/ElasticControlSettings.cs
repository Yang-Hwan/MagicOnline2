using UnityEngine;
namespace Assets.Scripts.Sight.Surface.Pavilion
{
    [CreateAssetMenu(menuName="Magic/Elastic control appearance")]
    public sealed class ElasticControlSettings : ScriptableObject
    {
        [Tooltip("Off restores original sprites without changing shot physics or controls.")]
        public bool useNewAppearance = true;
    }
}
