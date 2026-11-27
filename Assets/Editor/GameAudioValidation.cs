using System;
using System.IO;
using Assets.Scripts.Often;
using UnityEditor;
using UnityEngine;

// A missing cue otherwise fails silently in a player build.
[InitializeOnLoad]
public static class GameAudioValidation
{
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
            File.WriteAllText("Logs/Audio/validation.txt", "PASS: " + checkedClips + " imported non-silent clips. All SoundCue mappings present.");
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
