using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Often
{
    // Imported effect meshes can have Read/Write disabled. Read their display
    // buffers once instead of requiring changes to model import settings.
    internal static class MatchReplayMeshReadback
    {
        static byte[] Buffer(GraphicsBuffer buffer)
        {
            using(buffer){var request=AsyncGPUReadback.Request(buffer);request.WaitForCompletion();if(request.hasError)throw new InvalidOperationException("연출 메시의 GPU 읽기에 실패했습니다.");return request.GetData<byte>().ToArray();}
        }
        static float Half(ushort bits)
        {
            int sign=(bits&0x8000)!=0?-1:1,exponent=(bits>>10)&31,mantissa=bits&1023;
            if(exponent==31)return mantissa==0?sign*65504:0;
            return sign*(exponent==0?mantissa/1024f*Mathf.Pow(2,-14):(1+mantissa/1024f)*Mathf.Pow(2,exponent-15));
        }
        public static Mesh Read(Mesh source)
        {
            if(!SystemInfo.supportsAsyncGPUReadback)throw new InvalidOperationException("이 장치에서는 연출 메시 기록을 지원하지 않습니다.");
            var streams=new Dictionary<int,byte[]>();
            Vector4 Attribute(VertexAttribute attribute,int vertex,Vector4 fallback)
            {
                if(!source.HasVertexAttribute(attribute))return fallback;
                int stream=source.GetVertexAttributeStream(attribute);
                if(!streams.TryGetValue(stream,out var bytes)){bytes=Buffer(source.GetVertexBuffer(stream));streams.Add(stream,bytes);}
                int offset=vertex*source.GetVertexBufferStride(stream)+source.GetVertexAttributeOffset(attribute);
                int count=source.GetVertexAttributeDimension(attribute);var format=source.GetVertexAttributeFormat(attribute);var value=Vector4.zero;
                for(int i=0;i<count;i++){
                    switch(format){
                        case VertexAttributeFormat.Float32:value[i]=BitConverter.ToSingle(bytes,offset);offset+=4;break;
                        case VertexAttributeFormat.Float16:value[i]=Half(BitConverter.ToUInt16(bytes,offset));offset+=2;break;
                        case VertexAttributeFormat.UNorm8:value[i]=bytes[offset++]/255f;break;
                        case VertexAttributeFormat.SNorm8:value[i]=Mathf.Max(-1,(sbyte)bytes[offset++]/127f);break;
                        case VertexAttributeFormat.UNorm16:value[i]=BitConverter.ToUInt16(bytes,offset)/65535f;offset+=2;break;
                        case VertexAttributeFormat.SNorm16:value[i]=Mathf.Max(-1,BitConverter.ToInt16(bytes,offset)/32767f);offset+=2;break;
                        default:throw new InvalidOperationException("지원하지 않는 연출 메시 형식: "+format);
                    }
                }
                return value;
            }
            var mesh=new Mesh{indexFormat=source.indexFormat};int n=source.vertexCount;
            var positions=new Vector3[n];var colors=new Color[n];var uv=new List<Vector4>[4];for(int c=0;c<4;c++)uv[c]=new List<Vector4>(n);
            for(int i=0;i<n;i++){
                positions[i]=Attribute(VertexAttribute.Position,i,Vector4.zero);colors[i]=(Color)Attribute(VertexAttribute.Color,i,Vector4.one);
                for(int c=0;c<4;c++)uv[c].Add(Attribute((VertexAttribute)((int)VertexAttribute.TexCoord0+c),i,Vector4.zero));
            }
            mesh.vertices=positions;mesh.colors=colors;for(int c=0;c<4;c++)mesh.SetUVs(c,uv[c]);
            var indices=Buffer(source.GetIndexBuffer());mesh.subMeshCount=source.subMeshCount;
            for(int sub=0;sub<source.subMeshCount;sub++){
                var desc=source.GetSubMesh(sub);if(desc.topology!=MeshTopology.Triangles)continue;var triangles=new int[desc.indexCount];
                for(int i=0;i<triangles.Length;i++){int index=desc.indexStart+i;triangles[i]=(source.indexFormat==IndexFormat.UInt16?BitConverter.ToUInt16(indices,index*2):(int)BitConverter.ToUInt32(indices,index*4))+desc.baseVertex;}
                mesh.SetTriangles(triangles,sub);
            }
            return mesh;
        }
    }
}
