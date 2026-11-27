using System;
using System.IO;
using Assets.Scripts.Often;
using UnityEditor;
using UnityEngine;

// A missing cue otherwise fails silently in a player build.
[InitializeOnLoad]
public static class GameAudioValidation
{
    [Serializable] sealed class Soundtrack { public string code; public float duration; public float[] events; }
    [Serializable] sealed class Soundtracks { public Soundtrack[] items; }
    const string Request = "Logs/Audio/run.request";
    static GameAudioValidation() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
        File.Delete(Request);
        Run();
    }

    [MenuItem("Tools/Magic Online/Validate Game Audio")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs/Audio");
        try
        {
            int checkedClips = 0;
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                bool variants = cue == SoundCue.Ball || cue == SoundCue.Cushion || cue >= SoundCue.Anger;
                if (variants) for (int i = 1; i <= 3; i++) Check(cue + "_" + i, ref checkedClips);
                else Check(cue.ToString(), ref checkedClips);
            }
            foreach (string name in new[] { "Lobby", "Lounge", "Match" }) Check(name, ref checkedClips);
            foreach (string code in SentimentAudioPlayer.Codes) Check("Sentiments/" + code, ref checkedClips);
            var manifest = Resources.Load<TextAsset>("Audio/Sentiments/manifest");
            if (!manifest) throw new Exception("Missing sentiment audio timing manifest");
            var tracks = JsonUtility.FromJson<Soundtracks>(manifest.text).items;
            if (tracks.Length != 12) throw new Exception("Expected 12 actor-specific soundtracks");
            foreach (string code in SentimentAudioPlayer.Codes)
            {
                var matches = Array.FindAll(tracks, track => track.code == code);
                if (matches.Length != 1) throw new Exception("Missing/duplicate actor soundtrack " + code);
                var clip = Resources.Load<AudioClip>("Audio/Sentiments/" + code);
                if (Mathf.Abs(clip.length - matches[0].duration) > .003f) throw new Exception("Soundtrack duration mismatch " + code);
            }
            void Times(string code, params float[] expected)
            {
                var actual = Array.Find(tracks, track => track.code == code).events;
                if (actual.Length != expected.Length) throw new Exception("Wrong impact count " + code);
                for (int i = 0; i < expected.Length; i++)
                    if (Mathf.Abs(actual[i] - expected[i]) > .001f) throw new Exception("Impact timing mismatch " + code);
            }
            Times("s1_4", Assets.Scripts.Sight.Surface.Pavilion.PracticeBombExplosion.ImpactTime,
                Assets.Scripts.Sight.Surface.Pavilion.PracticeBombExplosion.ImpactTime + Assets.Scripts.Sight.Surface.Pavilion.PracticeBombExplosion.BombInterval);
            Times("s5_10", Assets.Scripts.Sight.Surface.Pavilion.PracticeTableDemolition.ImpactTime(0),
                Assets.Scripts.Sight.Surface.Pavilion.PracticeTableDemolition.ImpactTime(1),
                Assets.Scripts.Sight.Surface.Pavilion.PracticeTableDemolition.ImpactTime(2));
            Times("s5_6", 0, .5f, 1.5f);
            Times("s5_8", 1.35f, 1.95f, 2.65f);
            File.WriteAllText("Logs/Audio/validation.txt", "PASS: " + checkedClips + " imported non-silent clips. All SoundCue mappings and 12 actor-specific mappings present. Bomb/hammer/gun/glass/storm timing verified.");
            Debug.Log("Game audio validation passed: " + checkedClips + " clips");
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/Audio/validation.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    static void Check(string name, ref int checkedClips)
    {
        var clip = Resources.Load<AudioClip>("Audio/" + name);
        if (!clip || clip.length <= .02f) throw new Exception("Missing or empty sound " + name);
        if (name == "Strike" || name.StartsWith("Ball_", StringComparison.Ordinal))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            if (clip.frequency != 44100 || importer.defaultSampleSettings.compressionFormat != AudioCompressionFormat.PCM)
                throw new Exception("Recorded contact must retain its 44.1 kHz PCM transient: " + name);
        }
        clip.LoadAudioData();
        var samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0)) throw new Exception("Unreadable sound " + name);
        float peak = 0;
        foreach (float sample in samples)
        {
            if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new Exception("Invalid sound " + name);
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        }
        if (peak < .01f || peak > 1) throw new Exception("Silent or clipping sound " + name);
        checkedClips++;
    }
}
