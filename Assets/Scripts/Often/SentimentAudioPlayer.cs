using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // One interruptible soundtrack per displayed actor. Never touches gameplay random/state.
    public sealed class SentimentAudioPlayer : MonoBehaviour
    {
        public static readonly string[] Codes = {
            "s1_4", "s5_10", "s5_11", "s5_4", "s5_5", "s5_6",
            "s5_7", "s5_8", "s5_9", "s6_5", "s8_4", "s8_5"
        };
        static readonly HashSet<SentimentAudioPlayer> active = new HashSet<SentimentAudioPlayer>();
        AudioSource voice;

        public static bool Supports(string code) => code != null && Array.IndexOf(Codes, code) >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => active.Clear();

        public static void Play(GameObject actor, AudioClip clip)
        {
            if (!actor || !actor.activeInHierarchy || !clip) return;
            var player = actor.GetComponent<SentimentAudioPlayer>();
            if (!player) player = actor.AddComponent<SentimentAudioPlayer>();
            if (!player.voice)
            {
                player.voice = actor.AddComponent<AudioSource>();
                player.voice.playOnAwake = false;
                player.voice.loop = false;
                player.voice.spatialBlend = 0;
                player.voice.priority = 160; // Cue/ball contacts keep their normal higher priority.
            }
            player.voice.Stop();
            player.voice.clip = clip;
            player.voice.pitch = 1; // Preserve the authored two/three-hit timing.
            player.voice.volume = GameAudio.GameVolume * .55f;
            player.voice.Play();
        }

        public static void StopFor(GameObject actor)
        {
            if (actor) actor.GetComponent<SentimentAudioPlayer>()?.Stop();
        }

        public static bool IsPlaying(GameObject actor)
        {
            if (!actor) return false;
            var player = actor.GetComponent<SentimentAudioPlayer>();
            return player && player.voice && player.voice.isPlaying;
        }

        public static void StopAll()
        {
            foreach (var player in active) if (player) player.Stop();
        }

        public void Stop() { if (voice) voice.Stop(); }
        void OnEnable() => active.Add(this);
        void OnDisable() { Stop(); active.Remove(this); }
        void OnDestroy() => active.Remove(this);
        void OnApplicationFocus(bool focused) { if (!focused) Stop(); }
        void OnApplicationPause(bool paused) { if (paused) Stop(); }
        void Update()
        {
            if (!voice || !voice.isPlaying) return;
            if (GameAudio.Muted || !Application.isFocused) Stop();
            else voice.volume = GameAudio.GameVolume * .55f;
        }
    }
}
