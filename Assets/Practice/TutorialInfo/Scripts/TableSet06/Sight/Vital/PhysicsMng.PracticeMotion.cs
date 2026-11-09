using UnityEngine;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        public bool IsLocalPractice => !externalItemSession &&
            PoolCoach.Instance.Configuration.Execution != Assets.Scripts.Often.MatchExecutionMode.OnlineMatch;

        public static string ClassifyPracticeMotion(Vector3 velocity, Vector3 angularVelocity, float radius)
        {
            float speed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
            if (speed < .008f && angularVelocity.magnitude < .15f) return "정지";
            if (speed < .008f) return "제자리 회전";
            Vector3 slip = Vector3.ProjectOnPlane(velocity + Vector3.Cross(angularVelocity, Vector3.down * radius), Vector3.up);
            return slip.magnitude <= Mathf.Max(.02f, speed * .08f) ? "구름" : "미끄러짐";
        }

    }
}
