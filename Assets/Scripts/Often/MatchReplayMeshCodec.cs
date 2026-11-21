using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Assets.Scripts.Often
{
    // HUD timer meshes contain mostly unchanged artwork. Bounded delta chains
    // keep a full-length match small without rerunning any presentation logic.
    public static class MatchReplayMeshCodec
    {
        public const int MaxMeshBytes=8*1024*1024;
        public static byte[] Encode(byte[] raw,byte[] previous,int previousId)
        {
            if(raw.Length>MaxMeshBytes)throw new InvalidDataException("Replay mesh too large");
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
                var runs=new List<(int start,int length)>();int changed=0;
                if(previous!=null&&previous.Length==raw.Length){
                    for(int i=0;i<raw.Length;){if(raw[i]==previous[i]){i++;continue;}int begin=i++;while(i<raw.Length&&(raw[i]!=previous[i]||i+1<raw.Length&&raw[i+1]!=previous[i+1]))i++;runs.Add((begin,i-begin));changed+=i-begin;}
                }
                bool delta=previous!=null&&previous.Length==raw.Length&&changed+runs.Count*8+16<raw.Length;
                w.Write(delta);w.Write(raw.Length);
                if(!delta)w.Write(raw);
                else{w.Write(previousId);w.Write(runs.Count);foreach(var run in runs){w.Write(run.start);w.Write(run.length);w.Write(raw,run.start,run.length);}}
                var payload=s.ToArray();
                using(var packed=new MemoryStream()){
                    using(var zip=new DeflateStream(packed,System.IO.Compression.CompressionLevel.Fastest,true))zip.Write(payload,0,payload.Length);
                    bool compress=packed.Length<payload.Length;
                    using(var result=new MemoryStream())using(var output=new BinaryWriter(result)){
                        output.Write((byte)(compress?2:3));output.Write(delta);output.Write(payload.Length);output.Write(compress?packed.ToArray():payload);return result.ToArray();
                    }
                }
            }
        }
        public static byte[] Decode(byte[] encoded,int id,Func<int,byte[]> previous)
        {
            byte[] payload;
            using(var envelope=new BinaryReader(new MemoryStream(encoded))){
                int format=envelope.ReadByte();bool delta=envelope.ReadBoolean();int size=envelope.ReadInt32();if((format!=2&&format!=3)||size<5||size>MaxMeshBytes+16)throw new InvalidDataException("Invalid geometry envelope");
                using(var unpacked=new MemoryStream()){
                    if(format==2){using(var zip=new DeflateStream(envelope.BaseStream,CompressionMode.Decompress,true)){
                        var block=new byte[8192];int n;while((n=zip.Read(block,0,block.Length))>0){if(unpacked.Length+n>size)throw new InvalidDataException("Geometry expansion limit");unpacked.Write(block,0,n);}
                    }}else{var bytes=envelope.ReadBytes(size);unpacked.Write(bytes,0,bytes.Length);}
                    payload=unpacked.ToArray();if(payload.Length!=size||payload[0]!=(delta?(byte)1:(byte)0))throw new InvalidDataException("Invalid geometry envelope size");
                }
            }
            using(var r=new BinaryReader(new MemoryStream(payload))){
                bool delta=r.ReadBoolean();int length=r.ReadInt32();if(length<0||length>MaxMeshBytes)throw new InvalidDataException("Replay mesh expansion limit");byte[] raw;
                if(!delta){raw=r.ReadBytes(length);if(raw.Length!=length)throw new EndOfStreamException();}
                else{
                    int baseId=r.ReadInt32();if(baseId<0||baseId>=id)throw new InvalidDataException("Cyclic replay geometry");
                    raw=(byte[])previous(baseId).Clone();if(raw.Length!=length)throw new InvalidDataException("Replay delta size mismatch");
                    int count=r.ReadInt32(),end=0;if(count<0||count>length)throw new InvalidDataException();
                    for(int i=0;i<count;i++){int start=r.ReadInt32(),size=r.ReadInt32();if(start<end||size<0||start>length-size)throw new InvalidDataException();var patch=r.ReadBytes(size);if(patch.Length!=size)throw new EndOfStreamException();Buffer.BlockCopy(patch,0,raw,start,size);end=start+size;}
                }
                if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Trailing replay geometry");return raw;
            }
        }
        public static bool IsDelta(byte[] encoded)=>encoded.Length>1&&encoded[1]!=0;
    }
}
