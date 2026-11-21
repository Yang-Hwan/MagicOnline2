using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // CPU simulation state, not rendered meshes. Preserves sub-emitter output
    // and custom shader streams without rerunning random/emission decisions.
    public sealed class ReplayParticleBuffer
    {
        public byte[] bytes;public int count,stride;public Vector4[] custom1,custom2;
        public bool Same(ReplayParticleBuffer other)=>other!=null&&count==other.count&&bytes.AsSpan().SequenceEqual(other.bytes)&&Same(custom1,other.custom1)&&Same(custom2,other.custom2);
        static bool Same(Vector4[] a,Vector4[] b)=>ReferenceEquals(a,b)||a!=null&&b!=null&&a.SequenceEqual(b);
        [Serializable] public sealed class Payload {public string data,unityVersion;public int count,stride;public Vector4[] custom1,custom2;}
        public string Serialize()=>JsonUtility.ToJson(new Payload{data=Convert.ToBase64String(bytes),unityVersion=Application.unityVersion,count=count,stride=stride,custom1=custom1,custom2=custom2});
        public static ReplayParticleBuffer Capture(ParticleSystem source,ref ParticleSystem.Particle[] scratch)
        {
            int count=source.particleCount;
            if(scratch==null||scratch.Length<count)scratch=new ParticleSystem.Particle[Mathf.Max(16,Mathf.NextPowerOfTwo(count))];
            count=source.GetParticles(scratch);int stride=Marshal.SizeOf<ParticleSystem.Particle>();
            var data=new ReplayParticleBuffer{count=count,stride=stride,bytes=MemoryMarshal.AsBytes(scratch.AsSpan(0,count)).ToArray()};
            if(source.customData.enabled&&count>0){var list=new List<Vector4>(count);source.GetCustomParticleData(list,ParticleSystemCustomData.Custom1);data.custom1=list.ToArray();list.Clear();source.GetCustomParticleData(list,ParticleSystemCustomData.Custom2);data.custom2=list.ToArray();}
            return data;
        }
        public static void Apply(ParticleSystem target,string json)
        {
            var payload=JsonUtility.FromJson<Payload>(json);int stride=Marshal.SizeOf<ParticleSystem.Particle>();
            if(payload.count<0||payload.count>100000||payload.stride!=stride||payload.unityVersion!=Application.unityVersion)throw new InvalidOperationException("리플레이 파티클 형식과 Unity 버전이 맞지 않습니다.");
            var bytes=Convert.FromBase64String(payload.data);if(bytes.Length!=(long)payload.count*stride)throw new InvalidOperationException("Invalid replay particle buffer size");
            var particles=MemoryMarshal.Cast<byte,ParticleSystem.Particle>(bytes.AsSpan()).ToArray();
            target.SetParticles(particles,particles.Length);
            if(payload.custom1!=null&&payload.custom1.Length>0)target.SetCustomParticleData(new List<Vector4>(payload.custom1),ParticleSystemCustomData.Custom1);
            if(payload.custom2!=null&&payload.custom2.Length>0)target.SetCustomParticleData(new List<Vector4>(payload.custom2),ParticleSystemCustomData.Custom2);
        }
    }
}
