using System;
using System.Collections.Generic;
using Assets.Scripts.Often;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public static class ComputerShotPlanner
    {
        struct Ranked { public MatchShotCommand shot; public float score; public int order; }
        public const int ShortlistCount=192;
        static readonly float[] Powers={.22f,.32f,.42f,.52f,.64f,.77f,.9f};
        static readonly Vector2[] Contacts={Vector2.zero,new Vector2(-.45f,0),new Vector2(.45f,0),new Vector2(0,-.35f),new Vector2(0,.35f),new Vector2(-.65f,0),new Vector2(.65f,0),new Vector2(-.45f,-.35f),new Vector2(.45f,-.35f),new Vector2(-.45f,.35f),new Vector2(.45f,.35f)};
        static Vector2 Flat(Vector3 p) => new Vector2(p.x,p.z);
        static Vector2 Perpendicular(Vector2 v) => new Vector2(-v.y,v.x);
        static float Radius(BallC ball) => ball.GetComponent<SphereCollider>().radius*Mathf.Abs(ball.transform.lossyScale.x);
        public static Rect PlayingArea(PhysicsMng manager)
        {
            float r=Radius(manager.ballcs[1]);
            return Rect.MinMaxRect(Mathf.Min(manager.min_x,manager.max_x)+r,Mathf.Min(manager.min_z,manager.max_z)+r,
                Mathf.Max(manager.min_x,manager.max_x)-r,Mathf.Max(manager.min_z,manager.max_z)-r);
        }
        // Reflected object-ball images supply tangent caroms after zero to four rails.
        // Geometry only orders candidates; actual local PhysX decides their final score.
        public static List<MatchShotCommand> Candidates(BallC[] balls,int seat,Rect area,int cushions)
        {
            var angles=new List<float>();var positions=new Vector2[balls.Length];var radii=new float[balls.Length];
            for(int i=0;i<balls.Length;i++){positions[i]=Flat(balls[i].body.position);radii[i]=Radius(balls[i]);}
            Vector2 cue=positions[seat];
            int targetMask=balls.Length==4 ? 12 : ((1<<balls.Length)-1)&~(1<<seat);
            foreach(var first in balls)
            {
                if((targetMask&(1<<first.id))==0)continue;
                Vector2 centre=positions[first.id], delta=centre-cue;
                float radius=radii[seat]+radii[first.id];
                float yaw=Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg;
                float spread=Mathf.Asin(Mathf.Clamp(radius/Mathf.Max(delta.magnitude,.001f),0,.99f))*Mathf.Rad2Deg;
                for(int cut=-39;cut<=39;cut++)angles.Add(yaw+spread*cut/40f);
                foreach(var second in balls)
                {
                    if(second.id==first.id || (targetMask&(1<<second.id))==0)continue;
                    for(int x=-6;x<=6;x++)for(int z=-6;z<=6;z++)
                    {
                        int rails=Math.Abs(x)+Math.Abs(z);
                        if(rails>6 || (cushions==0 && rails>1) || (cushions>0 && rails<cushions))continue;
                        Vector2 goal=new Vector2(Image(positions[second.id].x,area.xMin,area.xMax,x),Image(positions[second.id].y,area.yMin,area.yMax,z));
                        Vector2 v=goal-centre;float length2=v.sqrMagnitude;
                        if(length2<=radius*radius)continue;
                        Vector2 middle=centre+v*(radius*radius/length2);
                        Vector2 offset=Perpendicular(v)*(radius*Mathf.Sqrt(length2-radius*radius)/length2);
                        for(int side=-1;side<=1;side+=2)
                        {
                            Vector2 contact=middle+offset*side, incoming=(contact-cue).normalized, normal=(centre-contact).normalized;
                            if(Vector2.Dot(incoming,normal)<=0)continue; // far side is not an entry contact
                            Vector2 outgoing=incoming-normal*Vector2.Dot(incoming,normal);
                            if(Vector2.Dot(outgoing.normalized,(goal-contact).normalized)<.95f)continue;
                            float angle=Mathf.Atan2(incoming.x,incoming.y)*Mathf.Rad2Deg;
                            angles.Add(angle);angles.Add(angle-.12f);angles.Add(angle+.12f);
                        }
                    }
                }
                if(cushions>0)for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
                {
                    int rails=Math.Abs(x)+Math.Abs(z);if(rails<1||rails>3)continue;
                    Vector2 image=new Vector2(Image(centre.x,area.xMin,area.xMax,x),Image(centre.y,area.yMin,area.yMax,z));
                    Vector2 direction=image-cue;float angle=Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg;
                    float band=Mathf.Asin(Mathf.Clamp(radius/direction.magnitude,0,.99f))*Mathf.Rad2Deg;
                    angles.Add(angle+band*.6f);angles.Add(angle-band*.6f);
                }
            }
            for(int i=0;i<72;i++)angles.Add(i*5f);
            var ranked=new List<Ranked>();var seen=new HashSet<int>();int order=0;
            foreach(float angle in angles)
            {
                float yaw=Mathf.Repeat(angle,360);
                if(!seen.Add(Mathf.RoundToInt(yaw*100)))continue;
                foreach(float power in Powers)
                foreach(var contact in Contacts)
                {
                    var shot=new MatchShotCommand{seat=seat,yaw=yaw,power=power,contact=contact,followThrough=ShotCtrl.DefaultFollowThrough};
                    ranked.Add(new Ranked{shot=shot,score=QuickScore(shot,positions,radii,area,targetMask,cushions),order=order++});
                }
            }
            ranked.Sort((a,b)=>{int score=b.score.CompareTo(a.score);return score!=0?score:a.order.CompareTo(b.order);});
            var result=new List<MatchShotCommand>();
            // Keep different powers/spins/routes in the physical shortlist instead of
            // letting dozens of nearly identical tangent rays consume the turn.
            foreach(var candidate in ranked)
            {
                bool duplicate=false;
                foreach(var accepted in result)if(Mathf.Abs(Mathf.DeltaAngle(accepted.yaw,candidate.shot.yaw))<.25f &&
                    Mathf.Abs(accepted.power-candidate.shot.power)<.1f && Vector2.Distance(accepted.contact,candidate.shot.contact)<.2f){duplicate=true;break;}
                if(!duplicate)result.Add(candidate.shot);
                if(result.Count==ShortlistCount)break;
            }
            return result;
        }
        static float Image(float value,float min,float max,int tile) => tile*(max-min)+((tile&1)==0?value:min+max-value);
        static float QuickScore(MatchShotCommand shot,Vector2[] balls,float[] radii,Rect area,int targets,int cushions)
        {
            Vector2 position=balls[shot.seat];float angle=shot.yaw*Mathf.Deg2Rad;
            Vector2 velocity=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
            float travel=0,near=100,range=3+shot.power*shot.power*35;
            int mask=0,rails=0;float retained=1;
            for(int segment=0;segment<14 && travel<range && retained>.06f;segment++)
            {
                Vector2 direction=velocity.normalized;
                float x=direction.x>.00001f?(area.xMax-position.x)/direction.x:direction.x<-.00001f?(area.xMin-position.x)/direction.x:float.PositiveInfinity;
                float z=direction.y>.00001f?(area.yMax-position.y)/direction.y:direction.y<-.00001f?(area.yMin-position.y)/direction.y:float.PositiveInfinity;
                float distance=Mathf.Min(x,z);bool horizontal=x<z;int hit=-1;
                for(int i=0;i<balls.Length;i++)
                {
                    if(i==shot.seat || (mask&(1<<i))!=0)continue;
                    Vector2 delta=balls[i]-position;float forward=Vector2.Dot(delta,direction), radius=radii[i]+radii[shot.seat];
                    float side2=delta.sqrMagnitude-forward*forward;
                    if(forward>0 && side2<radius*radius)
                    {
                        float entry=forward-Mathf.Sqrt(Mathf.Max(0,radius*radius-side2));
                        if(entry>=-.0001f && entry<distance){distance=Mathf.Max(0,entry);hit=i;}
                    }
                }
                distance=Mathf.Clamp(distance,0,Mathf.Max(0,(range-travel)*retained));
                Vector2 end=position+direction*distance;
                if((mask&targets)!=0 && rails>=cushions)for(int i=0;i<balls.Length;i++)
                {
                    if((targets&(1<<i))==0 || (mask&(1<<i))!=0)continue;
                    Vector2 delta=balls[i]-position;float along=Mathf.Clamp(Vector2.Dot(delta,direction),0,distance);
                    near=Mathf.Min(near,Mathf.Max(0,(delta-direction*along).magnitude-radii[i]-radii[shot.seat]));
                }
                travel+=distance/Mathf.Max(retained,.05f);position=end;
                if(travel>=range-.0001f)break;
                if(hit>=0)
                {
                    if(balls.Length==4 && hit==1-shot.seat)return -10000-travel;
                    mask |= 1<<hit;
                    if((mask&targets)==targets)return rails>=cushions?10000-travel*2-shot.contact.magnitude*3:-1000;
                    Vector2 normal=(balls[hit]-position).normalized;
                    velocity=direction-normal*Vector2.Dot(direction,normal)+normal*(shot.contact.y*.2f);
                    retained*=Mathf.Clamp(velocity.magnitude,.01f,1);
                    if(velocity.sqrMagnitude<.0001f)break;
                }
                else
                {
                    rails++;velocity=horizontal?new Vector2(-direction.x,direction.y):new Vector2(direction.x,-direction.y);
                    velocity+=Perpendicular(velocity)*(shot.contact.x*.12f);retained*=.88f;
                }
                position+=velocity.normalized*.0001f;
            }
            return ((mask&targets)!=0?100:0)+80/(.05f+near)-travel*.1f-shot.contact.magnitude*2;
        }
        public static IEnumerable<MatchShotCommand> Refine(MatchShotCommand seed)
        {
            foreach(float delta in new[]{-.8f,-.35f,-.15f,-.06f,.06f,.15f,.35f,.8f})yield return Copy(seed,delta,0,Vector2.zero);
            foreach(float power in new[]{-.14f,-.06f,.06f,.14f})yield return Copy(seed,0,power,Vector2.zero);
            foreach(var spin in new[]{new Vector2(-.2f,0),new Vector2(.2f,0),new Vector2(0,-.25f),new Vector2(0,.25f)})yield return Copy(seed,0,0,spin);
        }
        // Preserve the strong planner; occasional human execution error softens
        // the default opponent without changing live physics or score rules.
        public static MatchShotCommand ApplyAccuracy(MatchShotCommand shot,System.Random random,float skill)
        {
            skill=float.IsNaN(skill)? .8f : Mathf.Clamp01(skill);
            if(skill>=1 || random.NextDouble()<skill)return shot;
            float sign=random.Next(2)==0?-1:1;
            float angle=sign*(.35f+(float)random.NextDouble()*.65f);
            float power=shot.power*((float)random.NextDouble()*.08f-.04f);
            var spin=new Vector2(((float)random.NextDouble()-.5f)*.05f,((float)random.NextDouble()-.5f)*.05f);
            return Copy(shot,angle,power,spin);
        }
        static MatchShotCommand Copy(MatchShotCommand seed,float angle,float power,Vector2 spin) => new MatchShotCommand {
            seat=seed.seat,yaw=Mathf.Repeat(seed.yaw+angle,360),power=Mathf.Clamp(seed.power+power,.15f,.95f),
            contact=Vector2.ClampMagnitude(seed.contact+spin,.8f),followThrough=seed.followThrough};
        public static float Score(ShotPrediction prediction,BallC[] balls,int seat,float power,int cushions)
        {
            int targets=balls.Length==4 ? 12 : ((1<<balls.Length)-1)&~(1<<seat);
            int hit=prediction.HitBallMask&targets;
            if(balls.Length==4 && (prediction.HitBallMask&(1<<(1-seat)))!=0)return -10000-power;
            if(hit==targets && (balls.Length==4 || prediction.RailsAtSecondBall>=cushions))return 10000+100*(1-power);
            if(hit==targets)return -100; // two balls before the required rails is a definite miss
            if(hit==0)return -1000+10/(.05f+prediction.ClosestFirstBallDistance);
            float near=prediction.ClosestSecondBallDistance;
            return 100+100/(.03f+near)+Mathf.Min(prediction.RailContacts,cushions)*8-power*2;
        }
    }
}
