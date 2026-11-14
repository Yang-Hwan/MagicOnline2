using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Assets.Scripts.Exert.Match;
using MatchShot = Assets.Scripts.Sight.Vital.Pavilion.ShotCtrl;
using MatchPhysics = Assets.Scripts.Sight.Vital.Pavilion.PhysicsMng;
using PracticeShot = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl;
using PracticePhysics = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;

public static class ShotInputValidation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    [MenuItem("Tools/Magic Online/Validate Shot Input Lock")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play mode before shot input validation.");
        var root = new GameObject("Shot input validation"); root.SetActive(false);
        var previousPlayers = PoolPlayer.players;
        bool matchControl = MatchShot.canControl, practiceControl = PracticeShot.canControl;
        int checks = 0;
        try
        {
            MatchShot.canControl = PracticeShot.canControl = true;
            PoolPlayer.players = new[] { new PoolPlayer(0, "fixture", 0, "fixture", null, "") };
            Set(PoolPlayer.players[0], "myTurn", true);
            var match = root.AddComponent<MatchShot>(); var matchPhysics = root.AddComponent<MatchPhysics>();
            SetField(match, "physicsManager", matchPhysics);
            var practice = root.AddComponent<PracticeShot>(); var practicePhysics = root.AddComponent<PracticePhysics>();
            SetField(practice, "physicsManager", practicePhysics);
            foreach (string phase in new[] { "animation", "movement", "resolution", "stroke" })
            {
                Set(match, "IsShotAnimating", phase == "animation"); Set(match, "inMove", phase == "stroke");
                Set(matchPhysics, "inMove", phase == "movement"); Set(matchPhysics, "IsResolvingShot", phase == "resolution");
                BlockedGesture(match, "multiplayer " + phase);
                matchPhysics.moveTime = 42; matchPhysics.endFromNetwork = true;
                // Direct network/local entry must not restart an already active shot either.
                match.WaitAndStartShot("duplicate");
                Check(matchPhysics.moveTime == 42 && matchPhysics.endFromNetwork && !match.inShot, "duplicate shot preserves active simulation " + phase);
            }
            Set(match, "IsShotAnimating", false); Set(match, "inMove", false);
            Set(matchPhysics, "inMove", false); Set(matchPhysics, "IsResolvingShot", false);
            Event(match, "Up"); Check(match.force == .65f && !match.IsShotAnimating, "held input released after unlock does not shoot");
            foreach (bool animation in new[] { true, false })
            {
                Set(practice, "IsShotAnimating", animation); Set(practicePhysics, "inMove", !animation);
                BlockedGesture(practice, animation ? "practice animation" : "practice movement");
            }
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText("UserSettings/ShotInputValidation.json", "{\"Ok\":true,\"Checks\":"+checks+"}");
            Debug.Log("[ShotInputValidation] PASS " + checks + " checks");
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
            void BlockedGesture(object shot, string phase)
            {
                var target = shot.GetType().GetField("_targetEquip", Flags);
                target.SetValue(shot, Enum.Parse(target.FieldType, "SlidePower")); Set(shot, "force", .65f);
                foreach (string state in new[] { "Down", "PressAndMove", "PressAndStay", "Up" })
                {
                    Event(shot, state);
                    Check(target.GetValue(shot).ToString() == "None" && (float)shot.GetType().GetProperty("force", Flags).GetValue(shot) == .65f,
                        phase + " rejected " + state + " without changing power or retaining drag");
                }
            }
        }
        finally
        {
            PoolPlayer.players = previousPlayers;
            MatchShot.canControl = matchControl; PracticeShot.canControl = practiceControl;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static void Set(object instance, string name, object value) => instance.GetType().GetProperty(name, Flags).SetValue(instance, value);
    static void SetField(object instance, string name, object value) => instance.GetType().GetField(name, Flags).SetValue(instance, value);
    static void Event(object instance, string name)
    {
        var method = instance.GetType().GetMethod("InputOutput_OnMouseState", Flags);
        method.Invoke(instance, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, name) });
    }
}
