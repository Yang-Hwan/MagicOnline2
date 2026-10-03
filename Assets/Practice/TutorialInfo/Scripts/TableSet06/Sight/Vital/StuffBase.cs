using UnityEngine;
namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public enum StuffType { StuffCoin01, StuffItem01, StuffItem02, StuffItem03, StuffItem04 }
    public class StuffBase : MonoBehaviour
    {
        public StuffType typeId { get; private set; }
        public int positionId { get; private set; }
        public int lifeSec { get; private set; }
        public long SpawnId { get; private set; }
        protected PhysicsMng physicsManager;
        static readonly int MainColor = Shader.PropertyToID("_MainColor");
        Material mat;
        Vector3 resetScale;
        float born;
        public float RemainingLifetime => Mathf.Max(0, lifeSec - (Time.time - born));
        public void RestoreRemainingLifetime(float remaining) => born = Time.time - (lifeSec - remaining);
        bool bound;
        void Awake()
        {
            physicsManager = FindAnyObjectByType<PhysicsMng>();
            mat = transform.GetChild(0).GetComponent<MeshRenderer>().material;
            resetScale = transform.localScale;
        }
        public void Bind(PhysicsMng manager, ItemMatchState.Item item)
        {
            physicsManager = manager; SpawnId = item.id;
            Setup(item.type, item.positionId, item.lifeSeconds); bound = true;
        }
        public virtual void Setup(StuffType typeId, int positionId, int lifeSec)
        {
            this.typeId = typeId; this.positionId = positionId; this.lifeSec = lifeSec;
            transform.localScale = resetScale; born = Time.time;
            if (mat.HasProperty(MainColor)) mat.SetColor(MainColor, Color.white * 10f);
        }
        void Update()
        {
            if (!bound) return;
            float age = Time.time - born;
            // A time-based spawn flash followed by a subtle collectible pulse.
            float intensity = age < 1.4f ? Mathf.Lerp(10f, 1.6f, age / 1.4f) : 1.6f + .2f * Mathf.Sin(age * 3f);
            if (mat.HasProperty(MainColor)) mat.SetColor(MainColor, Color.white * intensity);
            if (physicsManager.HasItemAuthority && lifeSec > 0 && age >= lifeSec) physicsManager.Items.Expire(SpawnId);
        }
        public void ItemTouch() => physicsManager.TryCollectItem(this);
        public virtual void BallTouch(int coin_ch = 0, bool isSend = true) => physicsManager.TryCollectItem(this);
        public void EffectTouch() { if (physicsManager.HasItemAuthority) physicsManager.Items.Expire(SpawnId); }
        public void DeactiveDelay() => gameObject.SetActive(false);
        void OnDisable()
        {
            bound = false;
            if (ObjectPooler.instance) ObjectPooler.instance.ReturnToPool(gameObject);
        }
        void OnDestroy() { if (mat) Destroy(mat); }
    }
}
