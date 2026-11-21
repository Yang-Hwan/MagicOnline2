using System;
using UnityEngine;
namespace Assets.Scripts.Often
{
    // No Update/LateUpdate work; discovery runs when visual children are added.
    public sealed class MatchReplayHierarchyObserver : MonoBehaviour
    {
        public event Action<Transform> Changed;
        void OnTransformChildrenChanged()=>Changed?.Invoke(transform);
    }
}
