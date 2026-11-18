using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Coach=Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolCoach;
using Player=Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolPlayer;

[InitializeOnLoad]
public static class InningScoreValidation
{
    const string Root="Logs/InningScores/",Key="InningScoreValidation";
    static int step;
    static double next,deadline;
    static Vector3 fixedName,fixedTotal;
    static InningScoreValidation(){EditorApplication.update+=Tick;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        if(File.Exists(Root+"run.request"))
        {
            File.Delete(Root+"run.request");SessionState.SetBool(Key,true);step=0;deadline=EditorApplication.timeSinceStartup+150;
            if(File.Exists(Root+"result.json"))File.Delete(Root+"result.json");
            if(File.Exists(Root+"error.txt"))File.Delete(Root+"error.txt");
            if(!EditorApplication.isPlaying)
            {
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){File.WriteAllText(Root+"error.txt","Unsaved scene");SessionState.SetBool(Key,false);return;}
                EditorSceneManager.OpenScene("Assets/Practice/Scenes/TableSet07.unity");EditorApplication.isPlaying=true;return;
            }
        }
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+150;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Inning score validation timed out");
            if(step==0)
            {
                Logic();MatchResultProtocolValidation.Run(3);MatchResultProtocolValidation.Run(4);MatchRecoveryValidation.Run(3);MatchRecoveryValidation.Run(4);
                PracticeSceneFlow.Enter(PracticeSceneFlow.Mode.ThreeBall);step=1;next=EditorApplication.timeSinceStartup+3;return;
            }
            var view=UnityEngine.Object.FindAnyObjectByType<InningScoreView>();
            if(!view || Coach.Instance.InningScores.Active<0 || Coach.Instance.InningScores.Count(Coach.Instance.InningScores.Active)==0)return;
            if(step==1 || step==4)
            {
                var hud=UnityEngine.Object.FindAnyObjectByType<MatchHudView>();
                Check(hud,"practice HUD absent");int oldTarget=hud.Counters[0].Target;
                hud.ResetScores(7);hud.SetScore(0,6,false);hud.SetScore(0,8);
                Check(hud.Counters[0].Actual==8 && hud.Counters[0].Desired==7 && hud.Counters[0].FromScore==6 && hud.Counters[0].ToScore==7,"6 to 8 should move only the final bead");
                Check(hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.name=="ActualScore" && t.text=="8 / 7"),"actual overshoot score label missing");
                hud.ResetScores(oldTarget);
                var model=Coach.Instance.InningScores;Check(model.Total(0)==0 && model.Total(1)==0,"new match inherited score history");
                if(Player.turnId!=0)Player.ChangeTurn();
                if(step==1)
                {
                    var state=Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolLogic.gameState;
                    state.hitKinds.Add(Assets.TutorialInfo.Scripts.TableSet06.Often.HitKind.Board_T);
                    int points=Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolLogic.Instance.ShotPoints;
                    Check(points==2,"local cushion-first shot was not worth two");
                    Player.SetHitAdd(0,Coach.Instance.hitReward*points,points:points);
                    Check(Player.players[0].matchCoin==Coach.Instance.hitReward*2,"bank base coins not doubled");
                    Coach.Instance.ScoreChanged(true,Coach.Instance.hitReward*points,Assets.TutorialInfo.Scripts.TableSet06.Often.RunPath.None);
                    Check(Player.players[0].hitCnt==2 && Player.players[0].matchCoin==(Coach.Instance.hitReward+200)*2 && model.Total(0)==2,"local bank score/coin/innings disagree");
                    Player.SetHitMinus(0);Player.SetHitMinus(0);
                    Player.SetHitAdd(0,(int)-Player.players[0].matchCoin,false);state.hitKinds.Clear();model.Reset(0);
                    hud.ResetScores(oldTarget);
                }
                Player.SetHitAdd(0,0);Coach.Instance.ScoreChanged(true,0,Assets.TutorialInfo.Scripts.TableSet06.Often.RunPath.None);
                Player.SetHitAdd(0,0);Coach.Instance.ScoreChanged(true,0,Assets.TutorialInfo.Scripts.TableSet06.Often.RunPath.None);
                Check(model.Score(0,0)==2,"practice score event not connected");
                Player.ChangeTurn();Check(model.Score(1,0)==0,"zero-point visit absent");Player.ChangeTurn();
                Player.SetHitAdd(0,0);Coach.Instance.ScoreChanged(true,0,Assets.TutorialInfo.Scripts.TableSet06.Often.RunPath.None);
                Check(model.Score(0,1)==1 && model.Total(0)==3,"consecutive innings merged");
                for(int i=0;i<34;i++)model.Activate(1-model.Active);
                view.IconButton.GetComponent<Button>().onClick.Invoke();Check(view.IsOpen,"score icon did not open panel");
                next=EditorApplication.timeSinceStartup+2.5;step++;return;
            }
            if(step==2 || step==5)
            {
                var corners=new Vector3[4];view.Panel.GetWorldCorners(corners);
                Check(corners.All(c=>{var p=RectTransformUtility.WorldToScreenPoint(null,c);return p.x>=0 && p.x<=Screen.width && p.y>=0 && p.y<=Screen.height;}),"opened score panel is off screen");
                Check(view.InningsScroll.content.rect.width>view.InningsScroll.viewport.rect.width,"long innings do not scroll");
                Check(view.InningsScroll.horizontalNormalizedPosition>.99f,"opening did not reveal current inning");
                fixedName=view.Panel.Find("Fixed Player 0").position;fixedTotal=view.Panel.Find("Fixed Total 0").position;
                Check(InningScoreView.ContainsPointer(RectTransformUtility.WorldToScreenPoint(null,view.IconButton.position)),"icon lets table input through");
                Capture(step==2?"three-open":"four-open");
                var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                UnityEngine.EventSystems.ExecuteEvents.Execute(view.InningsScroll.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
                view.InningsScroll.horizontalNormalizedPosition=0;
                UnityEngine.EventSystems.ExecuteEvents.Execute(view.InningsScroll.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
                next=EditorApplication.timeSinceStartup+.5;step++;return;
            }
            if(step==3 || step==6)
            {
                Check(Vector3.Distance(fixedName,view.Panel.Find("Fixed Player 0").position)<.1f &&
                    Vector3.Distance(fixedTotal,view.Panel.Find("Fixed Total 0").position)<.1f,"fixed columns scrolled");
                Capture(step==3?"three-scrolled":"four-scrolled");
                float scrollX=view.InningsScroll.content.anchoredPosition.x;
                Player.SetHitAdd(0,0);Coach.Instance.ScoreChanged(true,0,Assets.TutorialInfo.Scripts.TableSet06.Often.RunPath.None);
                view.SendMessage("Update");view.SendMessage("LateUpdate");
                Check(Mathf.Abs(scrollX-view.InningsScroll.content.anchoredPosition.x)<.1f,"new score interrupted history browsing");
                view.IconButton.GetComponent<Button>().onClick.Invoke();Check(!view.IsOpen,"second tap did not close panel");
                if(step==3){PracticeSceneFlow.Enter(PracticeSceneFlow.Mode.FourBall);step=4;next=EditorApplication.timeSinceStartup+3;return;}
                Coach.Instance.MatchReset();File.WriteAllText(Root+"result.json","{\"passed\":true,\"threeBall\":true,\"fourBall\":true,\"bankScoring\":true,\"overshootBeads\":true,\"twoReplicaProtocol\":true,\"recovery\":true,\"toggle\":true,\"fixedColumns\":true,\"longScroll\":true}");
                SessionState.SetBool(Key,false);
            }
        }
        catch(Exception ex){File.WriteAllText(Root+"error.txt",ex.ToString());Debug.LogException(ex);SessionState.SetBool(Key,false);}
    }
    static void Capture(string name){ScreenCapture.CaptureScreenshot(Root+name+".png");}
    static void Logic()
    {
        Check(Assets.TutorialInfo.Scripts.TableSet06.Often.Utility.CoinNumToStr(-3000).StartsWith("-"),"negative practice coin display throws");
        var h=new InningScoreHistory();h.Reset(1);Check(h.Score(0,0)==null && h.Score(1,0)==0,"unplayed and zero conflated");
        h.SetTotals(0,2);h.Activate(1);Check(h.Count(1)==1 && h.Score(1,0)==2,"consecutive score started another inning");
        h.Activate(0);h.SetTotals(0,2);h.Activate(1);h.SetTotals(0,1);
        Check(h.Score(1,1)==-1 && h.Total(1)==1,"foul totals incorrect");
        var saved=h.Capture();h.Activate(0);h.SetTotals(3,1);h.Restore(saved);
        Check(h.Total(0)==0 && h.Total(1)==1 && h.Count(0)==1,"undo did not restore innings");
        var json=JsonUtility.ToJson(saved);var copy=JsonUtility.FromJson<InningScoreSnapshot>(json);
        Check(InningScoreHistory.Valid(copy,1,0,1),"valid recovery ledger rejected");copy.row1[0]++;
        Check(!InningScoreHistory.Valid(copy,1,0,1),"tampered ledger accepted");
        h.Finish();h.Activate(0);Check(h.Active==1,"finished match added empty inning");h.Reset(0);Check(h.Columns==1 && h.Total(1)==0,"rematch did not reset");
        for(int i=0;i<3000;i++){h.SetTotals(i/2,i/3);h.Activate(1-h.Active);}Check(h.Columns==1501,"long match truncated");
    }
}
