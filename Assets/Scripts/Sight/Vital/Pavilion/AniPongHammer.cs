using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class AniPongHammer : MovStoryAni
    {

        public Transform posTr;
        public Transform hammerTr;
        public ParticleSystem pongPs;
        public Vector3 pos_st;
        public Vector3 _hammerRot_St;
        public Vector3 _hammerRot_En;

        public override void Awake()
        {
            posTr = transform.Find("Pos");
            hammerTr = transform.Find("Pos/Hammer");
            pongPs = transform.Find("Pos/Pong").GetComponent<ParticleSystem>();
            pos_st = new Vector3(-2.5f, 0.2f, 0.0f);
            posTr.position = pos_st;
            _hammerRot_St = new Vector3(90f, -10f, 0);
            _hammerRot_En = new Vector3(90f, -60f, 0);
            base.Awake();
        }

        [ContextMenu(nameof(AniPlay))]
        public override void AniPlay(Vector3 pos, CancellationToken _cancellationToken)
        {
        }

        public override void SetupSequence()
        {
            posTr.position = pos_st;
            pongPs.Stop();
            mySequence = DOTween.Sequence();
            mySequence.AppendCallback(() => pongPs.Stop());
            mySequence.Append(posTr.DOMove(_pos, 0.0f));
            mySequence.Append(hammerTr.DORotate(_hammerRot_St, 0.0f));
            mySequence.AppendInterval(0.5f);
            mySequence.Append(hammerTr.DORotate(_hammerRot_En, 0.1f));
            mySequence.Append(hammerTr.DORotate(_hammerRot_St, 0.5f));
            mySequence.JoinCallback(() => pongPs.Play());
            mySequence.AppendInterval(0.5f);
            mySequence.Append(posTr.DOMove(pos_st, 0.0f));
            mySequence.SetAutoKill(false).Pause();
        }


    }
}
