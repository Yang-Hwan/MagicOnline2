using System;
using System.IO;
using System.Reflection;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in, local-only play-mode regression. Restores the original editor scene.
[InitializeOnLoad]
public static class ComputerOpponentValidation
{
    const string Key="ComputerOpponentValidation.Running", Request="Logs/computer-opponent.request", Report="Logs/computer-opponent-validation.txt";
    static int phase, balls, checks, beforeShots;
    static double changed;
    static PhysicsMng manager;
    static ComputerOpponentValidation(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;}
    static void Check(bool value,string text){if(!value)throw new Exception(text);checks++;}
    static void Next(int value){phase=value;changed=EditorApplication.timeSinceStartup;SessionState.SetInt(Key+".Checks",checks);File.WriteAllText("Logs/computer-opponent-progress.txt",$"balls={balls}, phase={phase}, checks={checks}");}
    static void Configure(int count)
    {
        typeof(PracticeSceneFlow).GetField("<Configuration>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,MatchConfiguration.VersusComputer(count));
        typeof(PracticeSceneFlow).GetField("<SelectedMode>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,count==3?PracticeSceneFlow.Mode.ThreeBall:PracticeSceneFlow.Mode.FourBall);
        SceneManager.LoadScene(PracticeSceneFlow.ScenePath);balls=count;Next(1);
    }
    [MenuItem("Tools/Magic Online/Validate Computer Opponent")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save the current scene before validation.");
        SessionState.SetString(Key+".Scene",SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key+".Background",Application.runInBackground);
        SessionState.SetBool(Key,true);
        SessionState.SetBool(Key+".Failed",false);
        bool expert=File.Exists("Logs/computer-expert.request");
        SessionState.SetBool(Key+".Expert",expert);
        if(expert)File.Delete("Logs/computer-expert.request");
        bool strength=File.Exists("Logs/computer-strength.request");
        SessionState.SetBool(Key+".Strength",strength);
        if(strength)File.Delete("Logs/computer-strength.request");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.isPlaying=true;
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){Application.runInBackground=true;checks=0;Next(0);}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);Application.runInBackground=SessionState.GetBool(Key+".Background",false);
            string original=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
            if(!SessionState.GetBool(Key+".Failed",false))File.WriteAllText(Report,$"PASS: {SessionState.GetInt(Key+".Checks",0)} checks. 3/4 ball automatic computer shots, input lock, tools/replay suspension, opponent toggle, legacy practice configuration.");
        }
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!SessionState.GetBool(Key,false))
        {
            if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete(Request);Run();}return;
        }
        if(!EditorApplication.isPlaying)return;
        try
        {
            double elapsed=EditorApplication.timeSinceStartup-changed;
            if(elapsed>(phase==8?420:60))throw new Exception($"Computer opponent timed out: balls={balls}, phase={phase}");
            if(phase==0){Check(!MatchConfiguration.Practice(4).HasComputerOpponent,"Legacy practice changed");Configure(4);}
            else if(phase==1)
            {
                manager=UnityEngine.Object.FindAnyObjectByType<PhysicsMng>();
                if(!manager||manager.inMove||!PoolCoach.Instance.isMatchTimePlay)return;
                Check(manager.ballcs.Length==balls && manager.ComputerOpponentEnabled,"Computer configuration not applied");
                Check(PoolPlayer.turnId==0 && !manager.IsComputerTurn && ShotCtrl.canControl,"Human first turn locked");
                Check(PoolPlayer.players[1].name=="컴퓨터","Opponent name incorrect");
                beforeShots=manager.ComputerShotsPlayed;
                if(SessionState.GetBool(Key+".Expert",false)){Next(8);manager.StartCoroutine(ComputerExpertValidation.Run(manager));}
                else if(SessionState.GetBool(Key+".Strength",false)){Next(8);manager.StartCoroutine(ComputerStrengthValidation.Run(manager));}
                else {PoolCoach.Instance.ChangeTurnReady();Next(2);}
            }
            else if(phase==8 && (SessionState.GetBool(Key+".Expert",false)?ComputerExpertValidation.Finished:ComputerStrengthValidation.Finished))
            {
                Check(SessionState.GetBool(Key+".Expert",false)?ComputerExpertValidation.Passed:ComputerStrengthValidation.Passed,"Computer benchmark failed; see Logs/computer-expert-"+balls+".txt");
                beforeShots=manager.ComputerShotsPlayed;
                PoolCoach.Instance.ChangeTurnReady();Next(2);
            }
            else if(phase==2)
            {
                if(manager.ComputerShotsPlayed<=beforeShots)return;
                Check(manager.ComputerCandidatesEvaluated>0,"Computer did not evaluate candidates");
                Check(manager.inMove,"Computer shot did not move balls");
                Check(manager.IsComputerTurn && !ShotCtrl.canControl,"Computer input was not locked");Next(3);
            }
            else if(phase==3)
            {
                var shot=manager.GetComponent<ShotCtrl>();
                if(manager.inMove||shot.IsShotAnimating||!PoolCoach.Instance.calculateTime)return;
                manager.SetComputerOpponentEnabled(false);
                Check(!manager.ComputerIsThinking && !manager.IsComputerTurn && ShotCtrl.canControl,"Toggle did not restore manual input");
                PoolPlayer.SetTurn(1);PoolCoach.Instance.TurnChanged();beforeShots=manager.ComputerShotsPlayed;Next(4);
            }
            else if(phase==4 && elapsed>.8)
            {
                Check(manager.ComputerShotsPlayed==beforeShots,"Disabled computer still shot");
                typeof(PhysicsMng).GetMethod("TogglePracticeTools",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
                manager.SetComputerOpponentEnabled(true);Next(5);
            }
            else if(phase==5 && elapsed>.8)
            {
                Check(manager.PracticeToolsVisible && !manager.ComputerIsThinking && manager.ComputerShotsPlayed==beforeShots,"Tools did not suspend computer");
                manager.OpenReplaySlots();
                typeof(PhysicsMng).GetMethod("TogglePracticeTools",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);Next(6);
            }
            else if(phase==6 && elapsed>.8)
            {
                Check(manager.ReplayLoadMode && !manager.ComputerIsThinking && manager.ComputerShotsPlayed==beforeShots,"Replay browser did not suspend computer");
                manager.ExitPracticeReplay();
                if(balls==4)Configure(3);else{Next(7);EditorApplication.isPlaying=false;}
            }
        }
        catch(Exception e){SessionState.SetBool(Key+".Failed",true);File.WriteAllText(Report,"FAIL: "+e);Debug.LogException(e);EditorApplication.isPlaying=false;}
    }
}
