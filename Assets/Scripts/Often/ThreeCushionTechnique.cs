using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // Presentation classification, independent of score/coin/turn rules. Values are sent on the wire.
    public enum ThreeCushionTechnique { None, Side, Back, Front, Reverse, Grand, TwoBank, ThreeBank, Return, Bounding,
        Cut, Cross, Lengthwise, DoubleCushion, Ticky, Kick, OneBank, HorizontalHit, FrontGrand, OneBankReturn, ThreeBankReturn }

    public sealed class ThreeCushionShotTrace
    {
        const float NearNormalCosine = .9781476f; // within 12 degrees of the cushion normal
        public struct Contact
        {
            public int ball, rail; // rail: 0/1 = x ends, 2/3 = z ends; -1 for ball
            public Vector2 position, incoming, outgoing;
            public float spin;
            public Vector2 target;
            public float radius;
        }
        readonly List<Contact> contacts = new List<Contact>(24);
        readonly List<Vector2> path = new List<Vector2>(512);
        Bounds cloth;
        int cue, first = -1;
        bool active, complete, earlySecond, overflow;
        float length;
        Vector2 start, last;
        public IReadOnlyList<Contact> Contacts => contacts;
        public IReadOnlyList<Vector2> Path => path;
        public float TravelLength => length;
        public bool Complete => complete;
        public void Clear() { active=false;complete=false;contacts.Clear();path.Clear(); }

        public static Vector2 Plane(Vector3 v) => new Vector2(v.x, v.z);
        public void Begin(int cueId, Vector3 origin, Bounds bounds, Vector3 launchImpulse = default)
        {
            cue = cueId; cloth = bounds; first = -1; complete = earlySecond = overflow = false;
            active = true; contacts.Clear(); path.Clear(); length = 0; start = last = Plane(origin); path.Add(start);
        }
        public void Sample(Vector3 position)
        {
            if (!active || complete) return;
            var p = Plane(position);
            if ((p-last).sqrMagnitude < .000025f) return;
            length += Vector2.Distance(last,p); last=p;
            // Keep the full travelled distance even when a very long shot exceeds the visual path budget.
            if (path.Count < 4096) path.Add(p);
        }
        public int RailAt(Vector3 position)
        {
            var p=position-cloth.center;
            return Mathf.Abs(p.x/Mathf.Max(.001f,cloth.extents.x)) > Mathf.Abs(p.z/Mathf.Max(.001f,cloth.extents.z))
                ? (p.x<0?0:1) : (p.z<0?2:3);
        }
        public int RailAt(Vector3 position,Vector3 contactNormal)
        {
            if(Mathf.Abs(contactNormal.x)+Mathf.Abs(contactNormal.z)<.01f)return RailAt(position);
            return Mathf.Abs(contactNormal.x)>Mathf.Abs(contactNormal.z)
                ? (position.x<cloth.center.x?0:1) : (position.z<cloth.center.z?2:3);
        }
        public void Hit(int ballId, int rail, Vector3 position, Vector3 incoming, Vector3 outgoing, float spin,
            Vector3 targetPosition = default, float radius = 0)
        {
            if (!active || complete || ballId==cue && rail<0) return;
            Sample(position);
            if (contacts.Count>=128) {overflow=true;return;}
            contacts.Add(new Contact {ball=ballId,rail=rail,position=Plane(position),incoming=Plane(incoming),outgoing=Plane(outgoing),spin=spin,
                target=Plane(targetPosition),radius=radius});
            if (rail>=0) return;
            if (first<0) first=ballId;
            else if (ballId!=first)
            {
                complete=true;
                int cushions=0;foreach(var c in contacts)if(c.rail>=0)cushions++;
                earlySecond=cushions<3;
            }
        }
        public ThreeCushionTechnique Classify(bool confirmedPoint)
        {
            var technique=ClassifyBase(confirmedPoint);
            if(technique==ThreeCushionTechnique.Side || technique==ThreeCushionTechnique.Back ||
                technique==ThreeCushionTechnique.HorizontalHit)
            {
                // The trace freezes at the scoring object; later rail hits cannot upgrade the title.
                int cushions=0;foreach(var contact in contacts)if(contact.rail>=0)cushions++;
                if(technique==ThreeCushionTechnique.HorizontalHit && cushions>=4)return ThreeCushionTechnique.FrontGrand;
                if(cushions>=5)return ThreeCushionTechnique.Grand;
            }
            return technique;
        }
        ThreeCushionTechnique ClassifyBase(bool confirmedPoint)
        {
            if (!confirmedPoint || !complete || earlySecond || overflow || cloth.size.x<=0 || cloth.size.z<=0) return ThreeCushionTechnique.None;
            var rails=new List<Contact>();int leading=0,firstIndex=-1;
            for(int i=0;i<contacts.Count;i++)
            {
                var c=contacts[i];
                if(c.rail>=0){rails.Add(c);if(firstIndex<0)leading++;}
                else if(firstIndex<0)firstIndex=i;
                else if(i!=contacts.Count-1)return ThreeCushionTechnique.None; // kiss/extra object contact: ambiguous
            }
            if(leading==2)return ThreeCushionTechnique.TwoBank;
            if(leading>=3)
                return rails[0].rail/2!=rails[1].rail/2 && rails[0].rail==rails[2].rail
                    ? ThreeCushionTechnique.ThreeBankReturn : ThreeCushionTechnique.ThreeBank;
            if(firstIndex<0 || rails.Count<3)return ThreeCushionTechnique.None;
            var objectContact=contacts[firstIndex];
            if(leading==1)
            {
                var rail=rails[0];
                var offset=objectContact.target-rail.position;
                float normalGap=Mathf.Abs(rail.rail<2 ? offset.x : offset.y);
                float radius=objectContact.radius;
                // At cushion contact the cue centre is one radius from the face.
                // For equal-sized balls this perpendicular offset is the clearance to the object's edge.
                float maxGap=4f*radius;
                if(radius>0 && (normalGap<=maxGap || Mathf.Approximately(normalGap,maxGap)))
                {
                    if(!AimsIntoCushionGap(rail,objectContact)) return ThreeCushionTechnique.Kick;
                    Vector2 inward=InwardNormal(rail.rail);
                    float targetCenterGap=Vector2.Dot(offset,inward)+radius;
                    if(targetCenterGap<3.5f*radius && Vector2.Distance(rail.position,objectContact.position)<6f*radius &&
                        Vector2.Dot(objectContact.position-objectContact.target,inward)<-.25f*radius)
                        return ThreeCushionTechnique.Ticky;
                }
                // Include the leading bank cushion: bank -> object -> adjacent rail -> bank rail.
                // As with Return, later cushions before the scoring object do not erase the route.
                if(rails[0].rail/2!=rails[1].rail/2 && rails[0].rail==rails[2].rail)
                    return ThreeCushionTechnique.OneBankReturn;
                return ThreeCushionTechnique.OneBank;
            }
            if(leading!=0)return ThreeCushionTechnique.None;
            var a=rails[0];var b=rails[1];var d=rails[2];
            bool adjacent=(a.rail/2)!=(b.rail/2);
            if(adjacent && a.rail==d.rail)return ThreeCushionTechnique.Return;
            // Measure the object's edge clearance perpendicular to the first cushion.
            // Both equal-sized ball centres are one radius from their respective edges,
            // so the radius cancels against the recorded cue centre at cushion contact.
            float objectRailGap=Vector2.Dot(objectContact.target-a.position,InwardNormal(a.rail));
            // Compare the struck side as seen along the incoming cue-ball heading with
            // the turn around the cushions. Both signs reverse under reflection.
            // A thin hit alone cannot distinguish a side shot from a cut shot.
            float struckSide=Cross(objectContact.incoming.normalized,
                (objectContact.position-objectContact.target).normalized);
            float cushionTurn=Cross(InwardNormal(a.rail),InwardNormal(b.rail));
            bool knownSide=objectContact.radius>0 && Mathf.Abs(struckSide)>.02f && adjacent;
            bool oppositeSide=knownSide && struckSide*cushionTurn<0;
            // Near-normal travel from the first object to the first cushion is mandatory.
            // Proximity alone also accepts oblique back shots (practice slot 04, 2026-11-19).
            bool nearNormal=Vector2.Dot((a.position-objectContact.position).normalized,
                -InwardNormal(a.rail))>=NearNormalCosine;
            bool nearCushionCut=oppositeSide && objectRailGap>=-objectContact.radius && nearNormal;
            bool firstLong = cloth.size.x>=cloth.size.z ? a.rail>=2 : a.rail<2;
            bool frontRoute=!firstLong && adjacent && b.rail/2==d.rail/2 && b.rail!=d.rail;
            // Left/right end, adjoining long rail, then the opposite long rail.
            if(!knownSide && frontRoute)
                return ThreeCushionTechnique.Front;
            float diagonal=new Vector2(cloth.size.x,cloth.size.z).magnitude;
            // Bounding hits the same cushion again after curving back, without an intervening cushion.
            for(int i=1;i<rails.Count;i++)
            {
                var previous=rails[i-1];
                var next=rails[i];
                if(previous.rail!=next.rail || Mathf.Abs(previous.spin)<=.5f) continue;
                Vector2 from=previous.outgoing, to=next.incoming;
                float cross=from.x*to.y-from.y*to.x;
                float angle=Mathf.Atan2(Mathf.Abs(cross),Vector2.Dot(from,to))*Mathf.Rad2Deg;
                if(angle>=5f || Mathf.Approximately(angle,5f))
                    return ThreeCushionTechnique.Bounding;
            }
            // Reverse English must oppose the incoming tangent, not merely be a side-spin shot.
            Vector2 normal=a.rail==0?Vector2.right:a.rail==1?Vector2.left:a.rail==2?Vector2.up:Vector2.down;
            Vector2 tangent=new Vector2(-normal.y,normal.x);
            float along=Vector2.Dot(a.incoming,tangent),after=Vector2.Dot(a.outgoing,tangent);
            bool reverse=Mathf.Abs(a.spin)>.5f && a.spin*along<0 && Mathf.Abs(along)>.03f &&
                (along*after<0 || Mathf.Abs(after)<Mathf.Abs(along)*.55f);
            if(reverse && !adjacent && a.rail!=b.rail)return ThreeCushionTechnique.Reverse;
            if(!adjacent && a.rail!=b.rail)
            {
                if(b.rail/2==d.rail/2 && b.rail!=d.rail)
                    return (cloth.size.x>=cloth.size.z ? a.rail>=2 : a.rail<2)
                        ? ThreeCushionTechnique.Cross : ThreeCushionTechnique.Lengthwise;
                if(b.rail/2!=d.rail/2)return ThreeCushionTechnique.DoubleCushion;
            }
            if(!adjacent || a.rail==d.rail)return ThreeCushionTechnique.None;
            if(nearCushionCut)
                return ThreeCushionTechnique.Cut;
            // Short -> long -> opposite long is Front even when the struck side
            // agrees with the circuit. Check it before the short-first side-shot rule.
            if(frontRoute)return ThreeCushionTechnique.Front;
            // Matching struck side/circuit: long rail first is Side; short rail first
            // and the opposite short rail third is HorizontalHit.
            if(knownSide && !oppositeSide)
                return firstLong ? ThreeCushionTechnique.Side : a.rail/2==d.rail/2 && a.rail!=d.rail
                    ? ThreeCushionTechnique.HorizontalHit : ThreeCushionTechnique.None;
            if(a.rail/2!=d.rail/2)return ThreeCushionTechnique.None;
            if(knownSide)return ThreeCushionTechnique.Back;
            // A full-table circuit must not replace a recognised base technique.
            // Use Grand only when the struck-side evidence cannot identify that technique.
            if(rails.Count>=4 && length>diagonal*1.6f)
            {
                bool tour=true;for(int i=1;i<4;i++)if(rails[i].rail/2==rails[i-1].rail/2)tour=false;
                if(tour && a.rail!=d.rail && b.rail!=rails[3].rail)return ThreeCushionTechnique.Grand;
            }
            // Distinguish a draw/behind route from a side route using the actual object-ball approach,
            // outgoing heading and the side of the original approach line containing the first cushion.
            var approach=objectContact.incoming.normalized;
            float forward=Vector2.Dot((a.position-objectContact.position).normalized,approach);
            float behind=Vector2.Dot(a.position-objectContact.position,approach);
            if(forward<-.15f || behind<-diagonal*.04f)return ThreeCushionTechnique.Back;
            if(firstLong && Mathf.Abs(Vector2.Dot(approach,cloth.size.x>=cloth.size.z?Vector2.right:Vector2.up))>.65f)
                return ThreeCushionTechnique.Back;
            return ThreeCushionTechnique.Side;
        }
        static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
        static Vector2 InwardNormal(int rail) =>
            rail==0?Vector2.right:rail==1?Vector2.left:rail==2?Vector2.up:Vector2.down;
        static bool AimsIntoCushionGap(Contact rail,Contact ball)
        {
            // Extend the cue ball's approach at first-object contact as if the ball did not deflect it.
            // At the object's position along the rail, does this ray pass on its cushion side?
            Vector2 inward=InwardNormal(rail.rail);
            Vector2 along=new Vector2(-inward.y,inward.x);
            Vector2 direction=ball.incoming.normalized;
            if(direction==Vector2.zero) direction=(ball.position-rail.position).normalized;
            float speedAlong=Vector2.Dot(direction,along);
            float distanceAlong=Vector2.Dot(ball.target-ball.position,along);
            if(Mathf.Abs(speedAlong)<.0001f) return false;
            float travel=distanceAlong/speedAlong;
            if(travel<0) return false;
            Vector2 crossing=ball.position+travel*direction;
            // The rail contact records the cue centre, one radius inside the cushion face.
            return Vector2.Dot(crossing-rail.position,inward)>=-ball.radius &&
                   Vector2.Dot(crossing-ball.target,inward)<-ball.radius*.001f;
        }
        public static string Label(ThreeCushionTechnique kind)
        {
            switch(kind){case ThreeCushionTechnique.Side:return "옆돌리기";case ThreeCushionTechnique.Back:return "뒤돌리기";
                case ThreeCushionTechnique.Front:return "앞돌리기";case ThreeCushionTechnique.Reverse:return "리버스";
                case ThreeCushionTechnique.FrontGrand:return "앞돌리기 대회전";
                case ThreeCushionTechnique.OneBankReturn:return "원뱅크 되돌아오기";
                case ThreeCushionTechnique.ThreeBankReturn:return "쓰리뱅크 되돌아오기";
                case ThreeCushionTechnique.Grand:return "대회전";case ThreeCushionTechnique.TwoBank:return "투뱅크";
                case ThreeCushionTechnique.ThreeBank:return "쓰리뱅크";case ThreeCushionTechnique.Return:return "되돌아오기";
                case ThreeCushionTechnique.Bounding:return "바운딩샷";case ThreeCushionTechnique.Cut:return "비껴치기";
                case ThreeCushionTechnique.Cross:return "횡단샷";case ThreeCushionTechnique.Lengthwise:return "종단샷";
                case ThreeCushionTechnique.DoubleCushion:return "더블쿠션";case ThreeCushionTechnique.Ticky:return "넣어치기";
                case ThreeCushionTechnique.HorizontalHit:return "세워치기";case ThreeCushionTechnique.OneBank:return "원뱅크";case ThreeCushionTechnique.Kick:return "걸어치기";default:return "";}
        }
    }
}
