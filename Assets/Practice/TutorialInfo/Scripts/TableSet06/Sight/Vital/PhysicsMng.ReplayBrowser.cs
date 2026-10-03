using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        bool IsReplayBrowser => Assets.Scripts.Often.PracticeSceneFlow.SelectedMode == Assets.Scripts.Often.PracticeSceneFlow.Mode.Replay ||
            Assets.Scripts.Often.PracticeSceneFlow.SelectedMode == Assets.Scripts.Often.PracticeSceneFlow.Mode.MatchReplay;
        bool IsMatchReplayBrowser => Assets.Scripts.Often.PracticeSceneFlow.SelectedMode == Assets.Scripts.Often.PracticeSceneFlow.Mode.MatchReplay;
        bool IsMatchReplayLayout => IsMatchReplayBrowser || matchArchiveSaveMode;

        public async UniTask OpenReplayBrowser()
        {
            // Wait until BallC.Awake and ShotCtrl.Start have initialized the table.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            foreach (var ball in ballcs)
            {
                ball.body.linearVelocity = Vector3.zero;
                ball.body.angularVelocity = Vector3.zero;
                ball.body.Sleep();
            }
            inMove = false;
            ShotCtrl.canControl = false;
            shotController.CuePutAside();
            OpenReplaySlots();
        }
    }
}
