using Assets.Scripts.Often;
using System;
using UnityEngine;

namespace Assets.Scripts.Sight.Surface.Pavilion
{
    [Serializable] public sealed class ReplayShotClockBell {public int stage;public float volume;}
    public sealed class ShotClockAlarmSound : MonoBehaviour
    {
        AudioSource source;
        AudioClip[] clips;
        public int BellRequests {get;private set;}
        public void Bell(int stage,float volume)
        {
            BellRequests++;
            if(!Application.isPlaying || GameAudio.Muted || !Application.isFocused)return;
            if(!source){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=false;}
            if(clips==null){clips=new AudioClip[3];for(int n=0;n<3;n++){
                const int rate=22050;var samples=new float[(int)(rate*.28f)];
                for(int i=0;i<samples.Length;i++){float t=i/(float)rate,envelope=Mathf.Min(1,t/.005f)*Mathf.Exp(-t*18);float hz=1050+n*100;samples[i]=envelope*(Mathf.Sin(t*hz*2*Mathf.PI)*.48f+Mathf.Sin(t*hz*2.76f*2*Mathf.PI)*.18f);}
                clips[n]=AudioClip.Create("Shot clock bell "+n,samples.Length,1,rate,false);clips[n].SetData(samples,0);
            }}
            source.PlayOneShot(clips[Mathf.Clamp(stage,0,2)],Mathf.Clamp01(volume) * GameAudio.GameVolume);
        }
        void Update(){if(source)source.volume = GameAudio.Muted || !Application.isFocused ? 0 : 1;}
        public void Silence(){if(source)source.Stop();}
        void OnDisable(){Silence();}
        void OnDestroy(){if(clips!=null)foreach(var clip in clips)if(clip){if(Application.isPlaying)Destroy(clip);else DestroyImmediate(clip);}}
    }
}
