#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Sight.Surface.Pavilion;
using Assets.Scripts.Sight.Vital.Pavilion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PreviewImpulse = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.Impulse;

public static class MatchEquipmentGameplayValidation
{
    [MenuItem("Tools/Local Game API/Validate match equipment gameplay")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before running equipment validation."); return; }
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("EquipmentGameplayValidation");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene); root.SetActive(false);
        int checks = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Debug.Log("[EquipmentGameplay] PASS " + label); }
        try
        {
            var shot = root.AddComponent<ShotCtrl>();
            var ballObject = new GameObject("Ball", typeof(SphereCollider), typeof(Rigidbody)); ballObject.transform.SetParent(root.transform);
            var ball = ballObject.AddComponent<Ball>(); var body = ballObject.GetComponent<Rigidbody>(); body.mass = .15f;
            typeof(Ball).GetProperty("body").SetValue(ball, body);
            typeof(ShotCtrl).GetProperty("cueBall").SetValue(shot, ball);
            Transform Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go.transform; }
            shot.cueSlider = Child("Slider"); shot.cueVertical = Child("Vertical"); shot.cueDisplacement = Child("Displacement");
            shot.cueDisplacement.position = new Vector3(.1f, 0, 0); shot.SetFollowThrough(.5f);
            var method = typeof(ShotCtrl).GetMethod("BuildImpulseForPower", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(float), typeof(MatchEquipmentProfile) }, null);
            PreviewImpulse Build(MatchEquipmentProfile p) => (PreviewImpulse)method.Invoke(shot, new object[] { .5f, p });
            var basic = Build(new MatchEquipmentProfile()); var powerful = Build(new MatchEquipmentProfile { Power = 20 });
            Check(Mathf.Abs(powerful.impulse.magnitude / basic.impulse.magnitude - 1.2f) < .00001f, "actual shot builder applies +20% power");
            var weak = Build(new MatchEquipmentProfile { Power = -40 });
            Check(Mathf.Abs(weak.impulse.magnitude / basic.impulse.magnitude - .6f) < .00001f, "actual shot builder applies -40% power");
            var follow = Build(new MatchEquipmentProfile { FollowThrough = 20 });
            Check(Mathf.Abs(follow.followThrough - .6f) < .00001f, "actual shot builder captures equipped follow-through");
            var again = Build(new MatchEquipmentProfile());
            Check(again.impulse == basic.impulse && again.point == basic.point && again.followThrough == basic.followThrough, "default cue preserves impulse and spin contact");
            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image)); portraitObject.transform.SetParent(root.transform);
            var image = portraitObject.GetComponent<Image>(); var portrait = portraitObject.AddComponent<EquipmentPortrait>();
            typeof(EquipmentPortrait).GetField("image", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(portrait, image);
            var neutral = Resources.Load<Sprite>("Gameplay/BandanaAvatar/Neutral"); var point = Resources.Load<Sprite>("Gameplay/BandanaAvatar/Point");
            Check(neutral && point, "real character resources exist");
            portrait.SetProfile(new MatchEquipmentProfile { CharacterId = 123, Emotions = new[] {
                new MatchEquipmentEmotion { Code="기본", Asset="Gameplay/BandanaAvatar/Neutral" },
                new MatchEquipmentEmotion { Code="환희", Asset="Gameplay/BandanaAvatar/Point" }
            }});
            portrait.Show("기본", null); Check(image.overrideSprite == neutral, "equipped neutral image reaches real UI Image");
            portrait.Show("환희", neutral); Check(image.overrideSprite == point, "equipped reaction image replaces neutral");
            portrait.Show("미안", point); Check(image.overrideSprite == neutral, "missing emotion falls back to equipped neutral");
            portrait.SetProfile(new MatchEquipmentProfile { Emotions = new[] { new MatchEquipmentEmotion { Code="기본", Asset="missing/equipment/test" } } });
            portrait.Show("기본", neutral); Check(image.overrideSprite == neutral, "missing asset keeps safe built-in portrait");
            var contract = new MatchEquipmentSnapshot { Players = new[] { new MatchEquipmentProfile { Messages = new[] { "선택한 메시지" } }, new MatchEquipmentProfile() } };
            Check(contract.MessageAt(0, 0) == "선택한 메시지" && contract.MessageAt(0, 1) == null && contract.MessageAt(1, 0) == null, "message lookup only permits selected pack indexes");
            Directory.CreateDirectory("UserSettings"); File.WriteAllText("UserSettings/MatchEquipmentGameplayValidation.json", "{\"Ok\":true,\"Checks\":" + checks + "}");
        }
        catch (Exception e) { Debug.LogError("[EquipmentGameplay] FAIL " + e); Directory.CreateDirectory("UserSettings"); File.WriteAllText("UserSettings/MatchEquipmentGameplayValidation.json", "{\"Ok\":false}"); }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
#endif
