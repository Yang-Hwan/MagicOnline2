using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEditor;
using UnityEngine;

public static class PracticePointerValidation
{
    [MenuItem("Tools/Magic Online/Validate Pointer Release")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop play mode before pointer validation.");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(InputOutput);
        if (type.GetField("OnMouseState", flags).GetValue(null) != null)
            throw new InvalidOperationException("Pointer validation requires no active input listeners.");
        var saved = new Dictionary<FieldInfo, object>();
        foreach (var field in type.GetFields(flags)) saved[field] = field.GetValue(null);
        var cameraObject = new GameObject("Pointer validation camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.pixelRect = new Rect(0, 0, 1000, 600);
        camera.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));
        var events = new List<MouseState>();
        float travel = 0;
        const float step = .02f;
        InputOutput.OnMouse listener = state => {
            events.Add(state);
            if (state == MouseState.PressAndMove) travel += InputOutput.mouseWorldSpeed.z * step;
        };
        InputOutput.OnMouseState += listener;
        try
        {
            InputOutput.usedCamera = camera;
            var release = type.GetMethod("ReleasePointer", flags);
            var sample = type.GetMethod("SampleMovement", flags);
            var start = new Vector3(30, 350, 0);
            var end = new Vector3(30, 200, 0);
            void Reset()
            {
                type.GetField("_mouseScreenPosition", flags).SetValue(null, start);
                var world = camera.ScreenToWorldPoint(start); world.y = 0;
                type.GetField("_mouseWorldPosition", flags).SetValue(null, world);
                events.Clear(); travel = 0;
            }
            float expected = camera.ScreenToWorldPoint(end).z - camera.ScreenToWorldPoint(start).z;
            Reset();
            release.Invoke(null, new object[] { end, step });
            Require(events.Count == 2 && events[0] == MouseState.PressAndMove && events[1] == MouseState.Up &&
                expected < 0 && Mathf.Abs(travel - expected) < .0001f, "fast drag lost final power movement");
            Reset();
            release.Invoke(null, new object[] { start, step });
            Require(events.Count == 1 && events[0] == MouseState.Up && travel == 0, "click generated movement");
            Reset();
            sample.Invoke(null, new object[] { end, step });
            release.Invoke(null, new object[] { end, step });
            Require(events.Count == 1 && events[0] == MouseState.Up, "held movement counted twice on release");
            Reset();
            sample.Invoke(null, new object[] { Vector3.Lerp(start, end, .5f), step });
            release.Invoke(null, new object[] { end, step });
            Require(Mathf.Abs(travel - expected * .5f) < .0001f, "release did not preserve remaining movement");
            Debug.Log("[PracticePointerValidation] PASS: fast drag, click, no double count, final movement.");
        }
        finally
        {
            InputOutput.OnMouseState -= listener;
            foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Pointer release: " + message); }
}
