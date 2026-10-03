using UnityEngine;
namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public class CosmosCtrl : MonoBehaviour
    {
        public float liveSec = 60f;
        public BallC[] ballCs;
        public PhysicsMng physicsManager;
        ParticleSystem ps;
        float born;
        void Awake() { ps = GetComponent<ParticleSystem>(); }
        void OnEnable()
        {
            born = Time.time;
            if (!physicsManager) physicsManager = FindAnyObjectByType<PhysicsMng>();
            ballCs = physicsManager.ballcs;
            var trigger = ps.trigger;
            for (int i = 0; i < ballCs.Length; i++) trigger.SetCollider(i, ballCs[i].transform.GetChild(0).GetComponent<Collider>());
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }
        void Update() { if (Time.time - born >= liveSec || !ps.IsAlive(true)) DeactiveDelay(); }
        public void DeactiveDelay() => gameObject.SetActive(false);
        void OnDisable()
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (ObjectPooler.instance) ObjectPooler.instance.ReturnToPool(gameObject);
        }
    }
}
