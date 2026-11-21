using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // Opt-in measurement in the actual Windows player, using unscaled wall time.
    public sealed class MatchReplayPerformanceProbe : MonoBehaviour
    {
        public static long Captures;public static double CaptureMilliseconds,MaxCaptureMilliseconds;
        public static long Profiles;public static double DiscoveryMilliseconds,StateMilliseconds,MaterialMilliseconds,HeavyNodeMilliseconds;public static string HeavyNode="";
        [Serializable] sealed class Report {public string mode;public int frames,recordedFrames;public double averageFrameMs,p95FrameMs,p99FrameMs,averageCaptureMs,maxCaptureMs,discoveryMs,stateMs,materialMs,heavyNodeMs;public string unityVersion,heavyNode;}
        readonly List<double> times=new List<double>();string output;double previous,next,start;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install(){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-replay-performance");if(at<0||at+1>=args.Length)return;var go=new GameObject("Replay performance probe");DontDestroyOnLoad(go);go.AddComponent<MatchReplayPerformanceProbe>().output=args[at+1];}
        void Update(){double now=Time.realtimeSinceStartupAsDouble;if(previous>0&&Captures>0)times.Add((now-previous)*1000);previous=now;if(now>=next){next=now+2;Write();}}
        void Write(){if(string.IsNullOrEmpty(output)||times.Count==0)return;var sorted=times.ToArray();Array.Sort(sorted);double sum=0;foreach(double t in times)sum+=t;var report=new Report{mode=Array.IndexOf(Environment.GetCommandLineArgs(),"-replay-recording-off")>=0?"baseline":"state-events",frames=times.Count,recordedFrames=(int)Captures,averageFrameMs=sum/times.Count,p95FrameMs=sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*.95))],p99FrameMs=sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*.99))],averageCaptureMs=Captures>0?CaptureMilliseconds/Captures:0,maxCaptureMs=MaxCaptureMilliseconds,unityVersion=Application.unityVersion,discoveryMs=Profiles>0?DiscoveryMilliseconds/Profiles:0,stateMs=Profiles>0?StateMilliseconds/Profiles:0,materialMs=Profiles>0?MaterialMilliseconds/Profiles:0,heavyNode=HeavyNode,heavyNodeMs=HeavyNodeMilliseconds};Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));File.WriteAllText(output,JsonUtility.ToJson(report,true));}
        void OnApplicationQuit()=>Write();
    }
}
