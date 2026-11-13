using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Prack.BackSys.GameData;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    [RequireComponent(typeof(Image))]
    public sealed class EquipmentPortrait : MonoBehaviour
    {
        public static readonly string[] Emotions = { "기본", "감사", "미안", "미소", "환희", "짜증", "우울", "좌절", "분노" };
        readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        readonly HashSet<string> loading = new HashSet<string>();
        readonly List<Sprite> ownedSprites = new List<Sprite>();
        readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        MatchEquipmentProfile profile;
        Image image;
        string wanted;
        public string CurrentEmotion { get; private set; }
        void Awake() { image = GetComponent<Image>(); image.preserveAspect = true; }
        public void SetProfile(MatchEquipmentProfile value) { profile = value; }
        public void Show(string emotion, Sprite fallback)
        {
            CurrentEmotion = emotion;
            string key = profile?.Emotions?.FirstOrDefault(e => e.Code == emotion)?.Asset;
            if (string.IsNullOrWhiteSpace(key)) key = profile?.Emotions?.FirstOrDefault(e => e.Code == "기본")?.Asset;
            wanted = key;
            if (string.IsNullOrWhiteSpace(key)) { image.overrideSprite = fallback; return; }
            if (cache.TryGetValue(key, out var cached)) { image.overrideSprite = cached ? cached : fallback; return; }
            image.overrideSprite = fallback;
            if (!Uri.TryCreate(key, UriKind.Absolute, out var uri))
            {
                var sprite = Resources.Load<Sprite>(key);
                if (!sprite)
                {
                    var texture = Resources.Load<Texture2D>(key);
                    if (texture) { sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f); ownedSprites.Add(sprite); }
                }
                cache[key] = sprite;
                if (sprite) image.overrideSprite = sprite;
            }
            else if ((uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) && loading.Add(key))
                StartCoroutine(Download(key));
        }
        IEnumerator Download(string key)
        {
            using (var request = UnityWebRequestTexture.GetTexture(key))
            {
                request.timeout = 12;
                yield return request.SendWebRequest();
                loading.Remove(key);
                if (request.result != UnityWebRequest.Result.Success || !image) yield break;
                var texture = DownloadHandlerTexture.GetContent(request);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
                ownedSprites.Add(sprite); ownedTextures.Add(texture); cache[key] = sprite;
                if (wanted == key) image.overrideSprite = sprite;
            }
        }
        void OnDisable() { loading.Clear(); } // Unity stops coroutines when the object is deactivated.
        void OnDestroy()
        {
            foreach (var sprite in ownedSprites) if (sprite) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
            foreach (var texture in ownedTextures) if (texture) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        }
    }
}
