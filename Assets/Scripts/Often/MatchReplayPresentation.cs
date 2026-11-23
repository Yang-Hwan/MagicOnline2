using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // Immutable display data. No gameplay commands, delegates, component types or
    // network credentials are serialized or executed by the replay reader.
    public sealed class MatchReplayPresentation
    {
        public const int Version=3, MaxBytes=256*1024*1024;
        public int width,height,localSeat,winner=-3,score0,score1;
        public string matchId="",applicationVersion="",presentationVersion="hud-mesh-1",result="";
        public readonly List<byte[]> textures=new List<byte[]>();
        public readonly List<byte[]> materials=new List<byte[]>();
        public readonly List<byte[]> meshes=new List<byte[]>();
        public readonly List<Draw> draws=new List<Draw>();
        public readonly List<int[]> frames=new List<int[]>();
        public readonly List<View> views=new List<View>();
        public readonly List<Event> events=new List<Event>();
        struct Pending {public float time;public string kind;public object value;}
        readonly List<Pending> pending=new List<Pending>();
        public void Queue(float time,string kind,object value){if(pending.Count+events.Count>=2000000)throw new InvalidDataException("시합 표시 이벤트의 저장 한도를 초과했습니다.");pending.Add(new Pending{time=time,kind=kind,value=value});}
        public void FlushPending(){if(pending.Count==0)return;for(int i=0;i<pending.Count;i++){var p=pending[i];events.Add(new Event{time=p.time,kind=p.kind,value=p.value is string text?text:p.value is ReplayParticleBuffer particles?particles.Serialize():JsonUtility.ToJson(p.value)});pending[i]=default;}pending.Clear();var ordered=events.OrderBy(e=>e.time).ToArray();events.Clear();events.AddRange(ordered);}
        public struct Draw { public int mesh,material; }
        public struct Event { public float time; public string kind,value; }
        public struct View
        {
            public Vector3 position;public Quaternion rotation;public bool orthographic;public float size,fov,near,far;public Rect rect;
            public static View Capture(Camera c)=>c?new View{position=c.transform.position,rotation=c.transform.rotation,orthographic=c.orthographic,size=c.orthographicSize,fov=c.fieldOfView,near=c.nearClipPlane,far=c.farClipPlane,rect=c.rect}:new View{position=new Vector3(0,0,-10),rotation=Quaternion.identity,orthographic=true,size=1,fov=60,near=.1f,far=100,rect=new Rect(0,0,1,1)};
            public void Apply(Camera c){if(!c)return;c.transform.SetPositionAndRotation(position,rotation);c.orthographic=orthographic;c.orthographicSize=size;c.fieldOfView=fov;c.nearClipPlane=near;c.farClipPlane=far;c.rect=rect;}
        }
        public void Write(BinaryWriter target)
        {
            FlushPending();
            using(var raw=new MemoryStream()){
                using(var w=new BinaryWriter(raw,System.Text.Encoding.UTF8,true)){
                    w.Write(Version);w.Write(width);w.Write(height);w.Write(localSeat);w.Write(winner);w.Write(score0);w.Write(score1);
                    w.Write(matchId??"");w.Write(applicationVersion??"");w.Write(presentationVersion??"");w.Write(result??"");
                    Blobs(w,textures);Blobs(w,materials);Blobs(w,meshes);
                    w.Write(draws.Count);foreach(var d in draws){w.Write(d.mesh);w.Write(d.material);}
                    w.Write(frames.Count);int[] previous=null;
                    foreach(var frame in frames){bool same=ReferenceEquals(previous,frame);w.Write(same);if(!same){w.Write(frame.Length);foreach(int id in frame)w.Write(id);}previous=frame;}
                    w.Write(views.Count);foreach(var v in views){w.Write(v.position.x);w.Write(v.position.y);w.Write(v.position.z);w.Write(v.rotation.x);w.Write(v.rotation.y);w.Write(v.rotation.z);w.Write(v.rotation.w);w.Write(v.orthographic);w.Write(v.size);w.Write(v.fov);w.Write(v.near);w.Write(v.far);w.Write(v.rect.x);w.Write(v.rect.y);w.Write(v.rect.width);w.Write(v.rect.height);}
                    w.Write(events.Count);
                    var previousEvents=new Dictionary<string,(int index,byte[] bytes)>();long expandedBytes=0;
                    for(int i=0;i<events.Count;i++) {
                        var e=events[i];byte encoding=e.kind=="visual-state"?(byte)1:e.kind.StartsWith("visual-part:",StringComparison.Ordinal)&&e.value.StartsWith("{\"positions\":",StringComparison.Ordinal)?(e.value.Contains("\"world\":")?(byte)2:(byte)3):(byte)0;
                        byte[] value=encoding==1?ReplayVisualStateCodec.Encode(e.value):encoding==2?ReplayGeometryCodec.EncodeLine(e.value):encoding==3?ReplayGeometryCodec.EncodeMesh(e.value):System.Text.Encoding.UTF8.GetBytes(e.value??"");
                        expandedBytes+=value.Length;if(value.Length>MaxBytes||expandedBytes>512L*1024*1024)throw new InvalidDataException("Replay event expansion limit");
                        string key=encoding+":"+EventDeltaKey(e);bool found=previousEvents.TryGetValue(key,out var previousEvent);
                        byte[] before=found?previousEvent.bytes:Array.Empty<byte>();int prefix=0,suffix=0,common=Math.Min(before.Length,value.Length);
                        while(prefix<common&&before[prefix]==value[prefix])prefix++;
                        while(suffix<common-prefix&&before[before.Length-1-suffix]==value[value.Length-1-suffix])suffix++;
                        w.Write(e.time);w.Write(e.kind??"");w.Write(encoding);w.Write(found?previousEvent.index:-1);
                        if(encoding>0&&before.Length==value.Length){
                            w.Write(-1);w.Write(0);w.Write(value.Length);for(int n=0;n<value.Length;n++)w.Write((byte)(value[n]^before[n]));
                        }else{w.Write(prefix);w.Write(suffix);int middle=value.Length-prefix-suffix;w.Write(middle);w.Write(value,prefix,middle);}
                        previousEvents[key]=(i,value);
                        if(raw.Length>MaxBytes)throw new InvalidDataException("Replay presentation exceeds its size limit");
                    }
                }
                if(raw.Length>MaxBytes)throw new InvalidDataException("Replay presentation exceeds its size limit");
                using(var compressed=new MemoryStream()){
                    using(var gzip=new GZipStream(compressed,System.IO.Compression.CompressionLevel.Optimal,true)){raw.Position=0;raw.CopyTo(gzip);}
                    target.Write((int)raw.Length);target.Write((int)compressed.Length);target.Write(compressed.ToArray());
                }
            }
        }
        static string EventDeltaKey(Event e)
        {
            string kind=e.kind??"",value=e.value??"";
            if(kind=="visual-state") {
                int start=value.IndexOf("\"id\":",StringComparison.Ordinal);
                if(start>=0) {start+=5;int end=start;while(end<value.Length&&(char.IsDigit(value[end])||value[end]=='-'||value[end]==' '))end++;
                    return kind+":"+value.Substring(start,end-start);}
            }
            int colon=kind.IndexOf(':');return colon<0?kind:kind.Substring(0,colon);
        }
        static void Blobs(BinaryWriter w,List<byte[]> blobs){w.Write(blobs.Count);foreach(var b in blobs){w.Write(b.Length);w.Write(b);}}
        static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException("Replay count out of range");return n;}
        static byte[] Bytes(BinaryReader r,int max){int n=Count(r,max);var bytes=r.ReadBytes(n);if(bytes.Length!=n)throw new EndOfStreamException();return bytes;}
        static void ReadBlobs(BinaryReader r,List<byte[]> target,int max){int count=Count(r,max);for(int i=0;i<count;i++)target.Add(Bytes(r,MaxBytes));}
        public static MatchReplayPresentation Read(BinaryReader source,int frameCount)
        {
            int length=Count(source,MaxBytes);byte[] packed=Bytes(source,MaxBytes);
            using(var raw=new MemoryStream()){
                using(var gzip=new GZipStream(new MemoryStream(packed),CompressionMode.Decompress)){
                    var buffer=new byte[65536];int n;while((n=gzip.Read(buffer,0,buffer.Length))>0){if(raw.Length+n>length)throw new InvalidDataException("Replay expansion limit");raw.Write(buffer,0,n);}
                }
                if(raw.Length!=length)throw new InvalidDataException("Truncated replay presentation");raw.Position=0;
                using(var r=new BinaryReader(raw)){
                    int version=r.ReadInt32();if(version<1||version>Version)throw new InvalidDataException("Unsupported presentation version");
                    var p=new MatchReplayPresentation{width=r.ReadInt32(),height=r.ReadInt32(),localSeat=r.ReadInt32(),winner=r.ReadInt32(),score0=r.ReadInt32(),score1=r.ReadInt32(),matchId=r.ReadString(),applicationVersion=r.ReadString(),presentationVersion=r.ReadString(),result=r.ReadString()};
                    if(p.width<1||p.width>16384||p.height<1||p.height>16384||p.localSeat<0||p.localSeat>1)throw new InvalidDataException("Invalid replay viewport or seat");
                    ReadBlobs(r,p.textures,8192);ReadBlobs(r,p.materials,100000);ReadBlobs(r,p.meshes,1000000);
                    int count=Count(r,1000000);for(int i=0;i<count;i++){var d=new Draw{mesh=r.ReadInt32(),material=r.ReadInt32()};if(d.mesh<0||d.mesh>=p.meshes.Count||d.material<0||d.material>=p.materials.Count)throw new InvalidDataException("Invalid presentation asset reference");p.draws.Add(d);}
                    count=Count(r,120000);if(count!=frameCount)throw new InvalidDataException("Presentation and motion timelines differ");
                    int[] previous=null;for(int i=0;i<count;i++){
                        if(r.ReadBoolean()){if(previous==null)throw new InvalidDataException();p.frames.Add(previous);continue;}
                        int size=Count(r,8192);var ids=new int[size];for(int j=0;j<size;j++){ids[j]=r.ReadInt32();if(ids[j]<0||ids[j]>=p.draws.Count)throw new InvalidDataException("Invalid draw reference");}p.frames.Add(ids);previous=ids;
                    }
                    count=Count(r,120000);if(count!=frameCount)throw new InvalidDataException("Replay camera timeline differs");
                    float F(){float f=r.ReadSingle();if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException("Invalid replay camera");return f;}
                    for(int i=0;i<count;i++){
                        var v=new View{position=new Vector3(F(),F(),F()),rotation=new Quaternion(F(),F(),F(),F()),orthographic=r.ReadBoolean(),size=F(),fov=F(),near=F(),far=F(),rect=new Rect(F(),F(),F(),F())};
                        if(v.position.sqrMagnitude>1000000||Mathf.Abs(Quaternion.Dot(v.rotation,v.rotation)-1)>.1f||v.size<=0||v.fov<1||v.fov>179||!v.orthographic&&v.near<=0||v.far<=v.near||v.rect.width<=0||v.rect.height<=0)throw new InvalidDataException("Invalid replay camera range");p.views.Add(v);
                    }
                    count=Count(r,2000000);float last=-1;long expandedCharacters=0,expandedBytes=0;var byteValues=new List<byte[]>();
                    for(int i=0;i<count;i++) {
                        float time=r.ReadSingle();if(float.IsNaN(time)||float.IsInfinity(time)||time<last)throw new InvalidDataException("Invalid presentation event time");last=time;
                        string kind=r.ReadString(),value;
                        if(version==1)value=r.ReadString();
                        else if(version==3){
                            byte encoding=r.ReadByte();if(encoding>3)throw new InvalidDataException("Invalid replay event encoding");int reference=r.ReadInt32(),prefix=r.ReadInt32(),suffix=r.ReadInt32();
                            if(reference< -1||reference>=i)throw new InvalidDataException("Invalid replay event reference");
                            byte[] before=reference<0?Array.Empty<byte>():byteValues[reference],middle=Bytes(r,MaxBytes);
                            bool xor=prefix==-1&&suffix==0&&encoding>0&&before.Length==middle.Length;
                            long size=xor?middle.Length:(long)prefix+middle.Length+suffix;
                            if(!xor&&(prefix<0||suffix<0||(long)prefix+suffix>before.Length)||size>MaxBytes||expandedBytes+size>512L*1024*1024)throw new InvalidDataException("Invalid replay byte delta");
                            var bytes=new byte[(int)size];
                            if(xor){for(int n=0;n<bytes.Length;n++)bytes[n]=(byte)(before[n]^middle[n]);}
                            else{Buffer.BlockCopy(before,0,bytes,0,prefix);Buffer.BlockCopy(middle,0,bytes,prefix,middle.Length);Buffer.BlockCopy(before,before.Length-suffix,bytes,prefix+middle.Length,suffix);}
                            byteValues.Add(bytes);expandedBytes+=size;
                            if((encoding==1)!=(kind=="visual-state")||encoding>=2&&!kind.StartsWith("visual-part:",StringComparison.Ordinal))throw new InvalidDataException("Invalid replay event encoding");
                            value=encoding==1?ReplayVisualStateCodec.Decode(bytes):encoding==2?ReplayGeometryCodec.DecodeLine(bytes):encoding==3?ReplayGeometryCodec.DecodeMesh(bytes):System.Text.Encoding.UTF8.GetString(bytes);
                        }
                        else {
                            int reference=r.ReadInt32(),prefix=r.ReadInt32(),suffix=r.ReadInt32();
                            if(reference< -1||reference>=i)throw new InvalidDataException("Invalid replay event reference");
                            string before=reference<0?"":p.events[reference].value,middle=r.ReadString();
                            if(prefix<0||suffix<0||(long)prefix+suffix>before.Length)throw new InvalidDataException("Invalid replay event delta");
                            long size=(long)prefix+middle.Length+suffix;
                            if(size>MaxBytes||expandedCharacters+size>512L*1024*1024)throw new InvalidDataException("Replay event expansion limit");
                            value=prefix==before.Length&&suffix==0&&middle.Length==0?before:before.Substring(0,prefix)+middle+before.Substring(before.Length-suffix);
                        }
                        expandedCharacters+=value.Length;
                        if(expandedCharacters>512L*1024*1024)throw new InvalidDataException("Replay event expansion limit");
                        p.events.Add(new Event{time=time,kind=kind,value=value});
                    }
                    if(raw.Position!=raw.Length)throw new InvalidDataException("Trailing presentation data");return p;
                }
            }
        }
    }
}
