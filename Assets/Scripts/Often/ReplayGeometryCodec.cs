using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace Assets.Scripts.Often
{
    public static class ReplayGeometryCodec
    {
        static int Count(BinaryReader r,int stride){int n=r.ReadInt32();if(n<0||n>1000000||(long)n*stride>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("Invalid replay geometry size");return n;}
        static void Points(BinaryWriter w,Vector3[] values){w.Write(values?.Length??0);var bits=new int[3];if(values!=null)foreach(var v in values){float[] xyz={v.x,v.y,v.z};for(int i=0;i<3;i++){int next=BitConverter.SingleToInt32Bits(xyz[i]);w.Write(next^bits[i]);bits[i]=next;}}}
        static Vector3[] Points(BinaryReader r){var result=new Vector3[Count(r,12)];var bits=new int[3];for(int n=0;n<result.Length;n++){var v=Vector3.zero;for(int i=0;i<3;i++){bits[i]^=r.ReadInt32();v[i]=BitConverter.Int32BitsToSingle(bits[i]);if(float.IsNaN(v[i])||float.IsInfinity(v[i]))throw new InvalidDataException("Invalid replay geometry coordinate");}result[n]=v;}return result;}
        static void UV(BinaryWriter w,Vector4[] values){w.Write(values?.Length??0);if(values!=null)foreach(var v in values){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}}
        static Vector4[] UV(BinaryReader r){var values=new Vector4[Count(r,16)];for(int i=0;i<values.Length;i++)values[i]=new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());return values;}
        // Rendering-only polylines: remove redundant samples within 0.0001
        // world units. Ball snapshots, contacts, scores and times remain lossless.
        public static Vector3[] SimplifyLine(Vector3[] points){
            if(points==null||points.Length<3)return points;
            const float toleranceSquared=0.0001f*0.0001f;
            var keep=new bool[points.Length];keep[0]=keep[points.Length-1]=true;var ranges=new Stack<(int first,int last)>();ranges.Push((0,points.Length-1));
            while(ranges.Count>0){var range=ranges.Pop();var a=points[range.first];var delta=points[range.last]-a;float length=delta.sqrMagnitude,max=toleranceSquared;int split=-1;
                for(int i=range.first+1;i<range.last;i++){float t=length>0?Mathf.Clamp01(Vector3.Dot(points[i]-a,delta)/length):0;float distance=(points[i]-(a+delta*t)).sqrMagnitude;if(distance>max){max=distance;split=i;}}
                if(split>=0){keep[split]=true;ranges.Push((range.first,split));ranges.Push((split,range.last));}
            }
            var result=new List<Vector3>();for(int i=0;i<points.Length;i++)if(keep[i])result.Add(points[i]);return result.ToArray();
        }
        public static byte[] EncodeLine(string json){var line=JsonUtility.FromJson<ReplayLine>(json);using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){var points=SimplifyLine(line.positions);line.positions=null;w.Write(JsonUtility.ToJson(line));Points(w,points);return m.ToArray();}}
        public static string DecodeLine(byte[] data){using(var m=new MemoryStream(data))using(var r=new BinaryReader(m)){var line=JsonUtility.FromJson<ReplayLine>(r.ReadString());line.positions=Points(r);if(m.Position!=m.Length)throw new InvalidDataException();return JsonUtility.ToJson(line);}}
        public static byte[] EncodeMesh(string json){var mesh=JsonUtility.FromJson<ReplayLocalGeometry>(json);using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){Points(w,mesh.positions);w.Write(mesh.colors?.Length??0);if(mesh.colors!=null)foreach(var c in mesh.colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}UV(w,mesh.uv0);UV(w,mesh.uv1);UV(w,mesh.uv2);UV(w,mesh.uv3);w.Write(mesh.indices?.Length??0);int previous=0;if(mesh.indices!=null)foreach(int index in mesh.indices){w.Write(index-previous);previous=index;}return m.ToArray();}}
        public static string DecodeMesh(byte[] data){using(var m=new MemoryStream(data))using(var r=new BinaryReader(m)){var mesh=new ReplayLocalGeometry{positions=Points(r)};mesh.colors=new Color32[Count(r,4)];for(int i=0;i<mesh.colors.Length;i++)mesh.colors[i]=new Color32(r.ReadByte(),r.ReadByte(),r.ReadByte(),r.ReadByte());mesh.uv0=UV(r);mesh.uv1=UV(r);mesh.uv2=UV(r);mesh.uv3=UV(r);mesh.indices=new int[Count(r,4)];int previous=0;for(int i=0;i<mesh.indices.Length;i++){previous=checked(previous+r.ReadInt32());if(previous<0||previous>=mesh.positions.Length)throw new InvalidDataException("Invalid replay geometry index");mesh.indices[i]=previous;}if(m.Position!=m.Length)throw new InvalidDataException();return JsonUtility.ToJson(mesh);}}
    }
}
