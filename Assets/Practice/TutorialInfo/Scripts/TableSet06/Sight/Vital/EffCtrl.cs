using System.Collections;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using UnityEngine;
namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public class EffCtrl : MonoBehaviour
    {
        public bool isAutoDeactive = true;
        public float liveSec = 3f;
        public int OwnerPlayerId { get; private set; } = -1;
        public int Generation { get; private set; }
        public Material LineMaterial { get; private set; }
        bool authority;
        float born;
        ParticleSystem[] particles;
        TrailRenderer[] trails;
        LineRenderer line;
        void Awake()
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
            trails = GetComponentsInChildren<TrailRenderer>(true);
            line = GetComponent<LineRenderer>();
            if (line) LineMaterial = line.material;
        }
        void OnEnable()
        {
            Generation++; born = Time.time; authority = false; OwnerPlayerId = -1;
            foreach (var trail in trails) trail.Clear();
            foreach (var particle in particles) if (particle.gameObject.activeInHierarchy) { particle.Clear(); particle.Play(); }
            if (line) line.positionCount = 0;
            if (TryGetComponent<Rigidbody>(out var body)) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        public void Setup(int playerId, bool isAuthority) { OwnerPlayerId = playerId; authority = isAuthority; }
        public void Run(IEnumerator routine) => StartCoroutine(routine);
        void Update() { if (isAutoDeactive && Time.time - born >= liveSec) DeactiveDelay(); }
        public void DeactiveDelay() => gameObject.SetActive(false);
        void OnDisable()
        {
            Generation++; authority = false; StopAllCoroutines();
            if (ObjectPooler.instance) ObjectPooler.instance.ReturnToPool(gameObject);
        }
        void OnDestroy() { if (LineMaterial) Destroy(LineMaterial); }
        public void OnTriggerEnter(Collider other)
        {
            if (!authority || !gameObject.activeInHierarchy || other.gameObject.layer != LayerMask.NameToLayer("Stuff")) return;
            if (other.TryGetComponent<StuffBase>(out var item))
                ObjectPooler.instance.GetComponent<PhysicsMng>().TryCollectItem(item, OwnerPlayerId);
        }
    }
}
