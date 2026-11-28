using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Often
{
    public enum SoundCue
    {
        Click, Open, Close, Error, Notify, Request, Accept, Cancel, Chat,
        Strike, Ball, Cushion, Pull, Aim, Turn, Start, Timeout, FinishReady,
        Score, Combo, Foul, Victory, Defeat, Draw, Count, CountDone, Coin,
        LevelUp, Reward, Attendance, Equip, Spark, Pulse, Flame,
        Anger, Encourage, Disappoint, Grumble, Despair, Joy, Thanks, Relief, ScoreCoin
    }

    // All sounds are local presentation. Never consume Unity's gameplay random state.
    [DefaultExecutionOrder(-10000)]
    public sealed class GameAudio : MonoBehaviour
    {
        static GameAudio instance;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<SoundCue, float> lastPlayed = new Dictionary<SoundCue, float>();
        readonly AudioSource[] voices = new AudioSource[12];
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly System.Random random = new System.Random();
        AudioSource music;
        string musicName;
        float musicGain, duckUntil;
        bool focused = true;
        Button pressed;
        Vector2 pressPosition;
        int nextVoice;
        float saveAt = -1;

        public static float MusicVolume => PlayerPrefs.GetFloat("Audio.Music", .3f);
        public static float GameVolume => PlayerPrefs.GetFloat("Audio.Game", .8f);
        public static float UiVolume => PlayerPrefs.GetFloat("Audio.Ui", .65f);
        public static bool Muted => PlayerPrefs.GetInt("Audio.Muted", 0) != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!instance) new GameObject("Game Audio").AddComponent<GameAudio>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            focused = Application.isFocused;
            DontDestroyOnLoad(gameObject);
            music = Source(true);
            for (int i = 0; i < voices.Length; i++) voices[i] = Source(false);
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio")) clips[clip.name] = clip;
            SceneManager.activeSceneChanged += SceneChanged;
            SetSceneMusic(SceneManager.GetActiveScene().name);
        }

        AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.volume = 0;
            return source;
        }

        public static void SetVolume(string channel, float value)
        {
            PlayerPrefs.SetFloat("Audio." + channel, Mathf.Clamp01(value));
            if (instance) { instance.saveAt = Time.unscaledTime + .5f; instance.StopEffects(); }
            else PlayerPrefs.Save();
        }

        public static void SetMuted(bool muted)
        {
            PlayerPrefs.SetInt("Audio.Muted", muted ? 1 : 0);
            PlayerPrefs.Save();
            if (instance) instance.StopEffects();
        }

        public static void Play(SoundCue cue, float strength = 1, float pitch = 1)
        {
            if (!Application.isPlaying || !instance || Muted || !instance.focused) return;
            instance.PlayCue(cue, strength, pitch);
        }

        static bool IsGame(SoundCue cue) => (cue >= SoundCue.Strike && cue <= SoundCue.Foul) || cue == SoundCue.ScoreCoin;

        void PlayCue(SoundCue cue, float strength, float pitch)
        {
            float now = Time.unscaledTime;
            bool impact = cue == SoundCue.Ball || cue == SoundCue.Cushion;
            float interval = impact ? .025f : (cue == SoundCue.Count || cue == SoundCue.ScoreCoin) ? .07f : cue == SoundCue.Coin ? .09f : .12f;
            if (lastPlayed.TryGetValue(cue, out float last) && now - last < interval) return;
            string key = cue.ToString();
            if (impact || (cue >= SoundCue.Anger && cue <= SoundCue.Relief)) key += "_" + random.Next(1, 4);
            if (!clips.TryGetValue(key, out var clip)) return;
            lastPlayed[cue] = now;
            AudioSource voice = null;
            for (int i = 0; i < voices.Length; i++)
            {
                var candidate = voices[(nextVoice + i) % voices.Length];
                if (!candidate.isPlaying) { voice = candidate; break; }
            }
            if (!voice) voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.Stop();
            voice.clip = clip;
            // Small presentation-only variation avoids a repeated sampled/musical tone.
            float contactPitch = cue == SoundCue.Strike || cue == SoundCue.Ball
                ? 1 + (float)(random.NextDouble() * .03 - .015) : 1;
            voice.pitch = Mathf.Clamp(pitch * contactPitch, .7f, 1.5f);
            voice.volume = Mathf.Clamp01(strength) * (IsGame(cue) ? GameVolume : UiVolume);
            if (cue >= SoundCue.Anger && cue <= SoundCue.Relief && lastPlayed.TryGetValue(SoundCue.Score, out float scored) && now - scored < .5f)
                voice.volume *= .5f;
            voice.Play();
            if (cue == SoundCue.Victory || cue == SoundCue.Defeat || cue == SoundCue.Draw || cue == SoundCue.LevelUp)
                duckUntil = now + clip.length;
        }

        public static void Impact(Component ball, Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < .08f) return;
            bool otherBall = collision.collider.gameObject.layer == LayerMask.NameToLayer("Ball");
            // Cushion/table contacts are silent; only ball-to-ball contacts play audio.
            if (!otherBall) return;
            // A fast grazing pass is a soft contact: loudness follows the closing
            // velocity along the contact normal, not the tangential ball speed.
            if (collision.contactCount > 0)
            {
                speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
                if (speed < .08f) return;
            }
            // Both balls receive the same collision callback.
            if (ball.gameObject.GetEntityId() > collision.collider.gameObject.GetEntityId()) return;
            Play(SoundCue.Ball, Mathf.Clamp(speed / 5f, .08f, .85f));
        }

        public static void Emotion(int category, string actorCode = null, GameObject actor = null)
        {
            if (!instance || category < 1 || category > 8) return;
            if (actor && SentimentAudioPlayer.Supports(actorCode) && instance.clips.TryGetValue(actorCode, out var clip))
            {
                if (Application.isPlaying && !Muted && instance.focused) SentimentAudioPlayer.Play(actor, clip);
                return;
            }
            if (Time.unscaledTime < instance.nextEmotion) return;
            instance.nextEmotion = Time.unscaledTime + 1.1f;
            Play((SoundCue)((int)SoundCue.Anger + category - 1), .4f);
        }
        float nextEmotion;

        public static void Music(string name)
        {
            if (!instance || instance.musicName == name) return;
            instance.musicName = name;
            instance.musicGain = 0;
            instance.music.Stop();
            instance.music.volume = 0;
            if (instance.clips.TryGetValue(name, out var clip))
            {
                instance.music.clip = clip;
                instance.music.Play();
            }
        }

        void SetSceneMusic(string scene)
        {
            Music(scene == "Hall" ? "Lounge" : scene == "Pavilion" || scene.StartsWith("TableSet") ? "Match" : "Lobby");
        }

        void SceneChanged(Scene previous, Scene current)
        {
            StopEffects();
            pressed = null;
            nextEmotion = 0;
            SetSceneMusic(current.name);
        }

        void StopEffects()
        {
            SentimentAudioPlayer.StopAll();
            foreach (var voice in voices) if (voice) voice.Stop();
            lastPlayed.Clear();
        }

        void Update()
        {
            if (saveAt >= 0 && Time.unscaledTime >= saveAt) { PlayerPrefs.Save(); saveAt = -1; }
            float target = Muted || !focused ? 0 : MusicVolume * (Time.unscaledTime < duckUntil ? .25f : 1);
            musicGain = Mathf.MoveTowards(musicGain, target, Time.unscaledDeltaTime * .7f);
            music.volume = musicGain;
            // Only raycast on input transitions, including dynamically created popup buttons.
            if (Input.GetMouseButtonDown(0)) { pressPosition = Input.mousePosition; pressed = ButtonAt(pressPosition); }
            if (Input.GetMouseButtonUp(0))
            {
                if (pressed && pressed.IsInteractable() && pressed == ButtonAt(Input.mousePosition) &&
                    Vector2.Distance(pressPosition, Input.mousePosition) < 15) Play(SoundCue.Click, .65f);
                pressed = null;
            }
            if (Input.GetButtonDown("Submit") && EventSystem.current)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                var button = selected ? selected.GetComponent<Button>() : null;
                if (button && button.IsInteractable()) Play(SoundCue.Click, .65f);
            }
        }

        Button ButtonAt(Vector2 position)
        {
            var events = EventSystem.current;
            if (!events) return null;
            hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = position }, hits);
            return hits.Count == 0 ? null : hits[0].gameObject.GetComponentInParent<Button>();
        }

        void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) { StopEffects(); music.Pause(); }
            else music.UnPause();
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            if (instance == this) instance = null;
        }

        void OnApplicationQuit() { if (saveAt >= 0) PlayerPrefs.Save(); }
    }
}
