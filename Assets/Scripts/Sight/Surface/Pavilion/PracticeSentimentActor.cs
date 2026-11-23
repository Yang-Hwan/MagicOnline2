using UnityEngine;
using DG.Tweening;
using Assets.Scripts.Sight.Vital.Pavilion;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    // Scene-authored artwork, loaded once and reused by the practice presenter.
    public sealed class PracticeSentimentActor : MonoBehaviour
    {
        [Range(1, 8)] public int category;
        public string phrase;
        public Texture2D artwork;
        public Rect uv = new Rect(0, 0, 1, 1);
        [Range(.5f, 4f)] public float duration = 1.6f;
        [Range(120, 500)] public float width = 300;
        public MovStoryAni animation;
        // 0: authored table effect, 1: world cue point, 2: local cloud point, 3: particle emitter at cue.
        [Range(0, 3)] public int animationAnchor;
        [Range(1, 12)] public float animationTimeout = 8;
        ParticleSystem[] particles;
        Transform[] parts;
        Vector3[] positions, scales;
        Quaternion[] rotations;
        PracticeTableDemolition demolition;
        PracticeBombExplosion explosion;
        PracticeTableStorm storm;
        PracticeSkullCurse skullCurse;
        PracticeTearFountain tearFountain;
        PracticeProudHalo proudHalo;
        public bool HasAnimation=>animation||demolition||explosion||storm||skullCurse||tearFountain||proudHalo;

        public void ConfigurePracticeTableEffects(Transform cloth,bool includeBomb=true)
        {
            if(name=="s8_5"&&cloth){
                proudHalo=GetComponent<PracticeProudHalo>()??gameObject.AddComponent<PracticeProudHalo>();
                proudHalo.Configure(cloth);
            }
            if(name=="s5_11"&&cloth){
                tearFountain=GetComponent<PracticeTearFountain>()??gameObject.AddComponent<PracticeTearFountain>();
                tearFountain.Configure(cloth);
            }
            if(name=="s5_9"&&cloth){
                skullCurse=GetComponent<PracticeSkullCurse>()??gameObject.AddComponent<PracticeSkullCurse>();
                skullCurse.Configure(cloth);
            }
            if(name=="s5_8"&&cloth){
                storm=GetComponent<PracticeTableStorm>()??gameObject.AddComponent<PracticeTableStorm>();
                storm.Configure(cloth);
            }
            if(name=="s1_4"&&includeBomb&&cloth){
                explosion=GetComponent<PracticeBombExplosion>()??gameObject.AddComponent<PracticeBombExplosion>();
                explosion.Configure(cloth);
            }
            if(name!="s5_10"||!cloth)return;
            demolition=GetComponent<PracticeTableDemolition>()??gameObject.AddComponent<PracticeTableDemolition>();
            demolition.Configure(cloth);
        }

        public void PrepareAnimation()
        {
            if (!animation || parts != null) return;
            parts = GetComponentsInChildren<Transform>(true);
            positions = new Vector3[parts.Length]; scales = new Vector3[parts.Length];
            rotations = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++) {
                positions[i] = parts[i].localPosition; scales[i] = parts[i].localScale;
                rotations[i] = parts[i].localRotation;
            }
            particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles) {
                var main = particle.main; main.playOnAwake = false;
            }
        }
        public void PlayAnimation(Vector3 worldPoint)
        {
            PrepareAnimation();
            gameObject.SetActive(true); // Original Awake binds child objects once.
            if(proudHalo){if(animation)animation.ReleaseSequence();proudHalo.Play(worldPoint);return;}
            if(tearFountain){animation.ReleaseSequence();tearFountain.Play(worldPoint);return;}
            if(skullCurse){animation.ReleaseSequence();skullCurse.Play(worldPoint);return;}
            if(storm){animation.ReleaseSequence();storm.Play();return;}
            if(explosion){animation.ReleaseSequence();explosion.Play();return;}
            if(demolition){animation.ReleaseSequence();demolition.Play();return;}
            animation.mySequence?.Kill();
            for (int i = 0; i < parts.Length; i++) {
                parts[i].localPosition = positions[i]; parts[i].localScale = scales[i];
                parts[i].localRotation = rotations[i];
            }
            var point = animationAnchor == 0 ? Vector3.zero : worldPoint;
            if (animationAnchor == 2) {
                var cloud = transform.Find("cloudPos");
                point = cloud.parent.InverseTransformPoint(worldPoint);
            }
            if (animationAnchor == 3) transform.Find("PS").position = worldPoint;
            animation.StartSequence(point);
        }
        public bool AnimationFinished(float elapsed)
        {
            if(proudHalo)return proudHalo.Finished;
            if(tearFountain)return tearFountain.Finished;
            if(skullCurse)return skullCurse.Finished;
            if(storm)return storm.Finished;
            if(explosion)return explosion.Finished;
            if(demolition)return demolition.Finished;
            if (elapsed >= animationTimeout) return true;
            if (elapsed < .1f || (animation.mySequence != null && animation.mySequence.IsActive() &&
                !animation.mySequence.IsComplete())) return false;
            foreach (var particle in particles) if (particle.IsAlive(true)) return false;
            return true;
        }
        public void StopAnimation()
        {
            if(proudHalo)proudHalo.Stop();
            if(tearFountain)tearFountain.Stop();
            if(skullCurse)skullCurse.Stop();
            if(storm)storm.Stop();
            if(explosion)explosion.Stop();
            if(demolition)demolition.Stop();
            if (!animation) return;
            animation.ReleaseSequence();
            if (particles != null) foreach (var particle in particles)
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable() { StopAnimation(); }
        void OnDestroy() { StopAnimation(); }
    }
}
