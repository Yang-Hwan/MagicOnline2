using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // XOR float bits, then compress: no quantization or resimulation of ball motion.
    public static class MatchReplayMotionCodec
    {
        public static void Write(BinaryWriter target,IReadOnlyList<float> times,IReadOnlyList<MatchBallState[]> frames,int balls)
        {
            using(var raw=new MemoryStream())using(var w=new BinaryWriter(raw)){
                var previous=new int[1+balls*13];
                for(int f=0;f<frames.Count;f++){
                    int at=0;
                    void F(float value){int bits=BitConverter.SingleToInt32Bits(value);w.Write(bits^previous[at]);previous[at++]=bits;}
                    void V(Vector3 value){F(value.x);F(value.y);F(value.z);}
                    F(times[f]);foreach(var b in frames[f]){V(b.position);F(b.rotation.x);F(b.rotation.y);F(b.rotation.z);F(b.rotation.w);V(b.velocity);V(b.angularVelocity);}
                }
                using(var packed=new MemoryStream()){
                    using(var gzip=new GZipStream(packed,System.IO.Compression.CompressionLevel.Optimal,true)){raw.Position=0;raw.CopyTo(gzip);}
                    target.Write((int)raw.Length);target.Write((int)packed.Length);target.Write(packed.ToArray());
                }
            }
        }
        public static void Read(BinaryReader source,int count,int balls,List<float> times,List<MatchBallState[]> frames)
        {
            if(count<2||count>120000||balls<3||balls>4)throw new InvalidDataException("Invalid replay motion count");
            int expected=checked(count*(1+balls*13)*4),length=source.ReadInt32(),packedLength=source.ReadInt32();
            if(length!=expected||packedLength<1||packedLength>expected+65536)throw new InvalidDataException("Invalid replay motion size");
            var packed=source.ReadBytes(packedLength);if(packed.Length!=packedLength)throw new EndOfStreamException();
            using(var gzip=new GZipStream(new MemoryStream(packed),CompressionMode.Decompress))using(var r=new BinaryReader(gzip)){
                var previous=new int[1+balls*13];float last=-1;
                for(int f=0;f<count;f++){
                    int at=0;
                    float F(){int bits=r.ReadInt32()^previous[at];previous[at++]=bits;float value=BitConverter.Int32BitsToSingle(bits);if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Invalid replay motion value");return value;}
                    Vector3 V()=>new Vector3(F(),F(),F());
                    float time=F();if(time<0||time>86400||time<=last)throw new InvalidDataException("Invalid replay motion time");last=time;times.Add(time);
                    var frame=new MatchBallState[balls];for(int i=0;i<balls;i++){
                        var b=new MatchBallState{position=V(),rotation=new Quaternion(F(),F(),F(),F()),velocity=V(),angularVelocity=V()};
                        if(b.position.sqrMagnitude>1000000||Mathf.Abs(Quaternion.Dot(b.rotation,b.rotation)-1)>.1f)throw new InvalidDataException("Invalid replay ball transform");frame[i]=b;
                    }frames.Add(frame);
                }
                if(gzip.ReadByte()!=-1)throw new InvalidDataException("Trailing replay motion data");
            }
        }
    }
}
