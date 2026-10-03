using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using System.Threading;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class MovStoryCtrl : MonoBehaviour
    {

        [Serializable]
        public class PoolStory
        {
            public StoryIdentity identity;
            public MovStoryAni story;
            public MovStoryBias bias;
        }

        [SerializeField] List<PoolStory> pools;
        PoolStory actPool;

        [Range(1, 10)]
        public int p;

        public Transform st;
        public Transform en;
        public List<Vector3> rect4ListPos;
        public List<Vector3> rect2HListPos;
        public List<Vector3> rect2VListPos;

        public List<GameObject> signObjs_r;
        public List<GameObject> signObjs_b;
        public List<GameObject> signObjs_y;

        public GameObject signPrefab_r;
        public GameObject signPrefab_b;
        public GameObject signPrefab_y;

        PhysicsMng physicsMng;

        CancellationTokenSource _tokenSource;
        CancellationToken _cancellationToken;

        private void Awake()
        {
            p = 2;
            st = GameObject.Find("Table/Addition/Position/st_table").transform;
            en = GameObject.Find("Table/Addition/Position/en_table").transform;
            Transform parent = GameObject.Find("Table/Addition/Item").transform;
            float w = en.position.x - st.position.x;
            float h = en.position.z - st.position.z;
            float d = w / 2 * 1 / p;

            PoolStory pool;

            pool = new PoolStory();
            pool.identity = StoryIdentity.HeartNiceShot;
            pool.story = GameObject.Find("Table/Addition/MovStorys/HeartNiceShot").GetComponent<AniHeartNiceShot>();
            pool.bias = MovStoryBias.Achieve;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.GlassBroken;
            pool.story = GameObject.Find("Table/Addition/MovStorys/GlassBroken").GetComponent<AniGlassBroken>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.CrowFly;
            pool.story = GameObject.Find("Table/Addition/MovStorys/CrowFly").GetComponent<AniCrowFly>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.DarkCloud;
            pool.story = GameObject.Find("Table/Addition/MovStorys/DarkCloud").GetComponent<AniDarkCloud>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.HeHe;
            pool.story = GameObject.Find("Table/Addition/MovStorys/HeHe").GetComponent<AniHeHe>();
            pool.bias = MovStoryBias.KissWell;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Ehyu;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Ehyu").GetComponent<AniEhyu>();
            pool.bias = MovStoryBias.KissHate;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.DabDab;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Dabdab").GetComponent<AniDabdab>();
            pool.bias = MovStoryBias.BallFar;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.KissGamsa;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Kissgamsa").GetComponent<AniKissgamsa>();
            pool.bias = MovStoryBias.KissWell;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.ANumuhe;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Anumuhe").GetComponent<AniAnumuhe>();
            pool.bias = MovStoryBias.KissHate;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Ggeung;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Ggeung").GetComponent<AniGgeung>();
            pool.bias = MovStoryBias.BallFar;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Jjagjjag;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Jjagjjag").GetComponent<AniJjagjjag>();
            pool.bias = MovStoryBias.Achieve;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Buglbugl;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Buglbugl").GetComponent<AniBuglbugl>();
            pool.bias = MovStoryBias.KissHate;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Kissgamsa2;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Kissgamsa2").GetComponent<AniKissgamsa2>();
            pool.bias = MovStoryBias.KissWell;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Jogmman;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Jogmman").GetComponent<AniJogmman>();
            pool.bias = MovStoryBias.Boost;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Akissuu;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Akissuu").GetComponent<AniAkissuu>();
            pool.bias = MovStoryBias.KissHate;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Kiss;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Kiss").GetComponent<AniKiss>();
            pool.bias = MovStoryBias.Kiss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.DeadLeaves;
            pool.story = GameObject.Find("Table/Addition/MovStorys/DeadLeaves").GetComponent<AniDeadLeaves>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.LightEmit;
            pool.story = GameObject.Find("Table/Addition/MovStorys/LightEmit").GetComponent<AniLightEmit>();
            pool.bias = MovStoryBias.Achieve;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.DogBone;
            pool.story = GameObject.Find("Table/Addition/MovStorys/DogBone").GetComponent<AniDogBone>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.SnowStorm;
            pool.story = GameObject.Find("Table/Addition/MovStorys/SnowStorm").GetComponent<AniSnowStorm>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.BombDebris;
            pool.story = GameObject.Find("Table/Addition/MovStorys/BombDebris").GetComponent<AniBombDebris>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.SpotLight;
            pool.story = GameObject.Find("Table/Addition/MovStorys/SpotLight").GetComponent<AniSpotLight>();
            pool.bias = MovStoryBias.Achieve;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.MusicNote;
            pool.story = GameObject.Find("Table/Addition/MovStorys/MusicNote").GetComponent<AniMusicNote>();
            pool.bias = MovStoryBias.Achieve;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.PongHammer;
            pool.story = GameObject.Find("Table/Addition/MovStorys/PongHammer").GetComponent<AniPongHammer>();
            pool.bias = MovStoryBias.Miss;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.BellyLaugh;
            pool.story = GameObject.Find("Table/Addition/MovStorys/BellyLaugh").GetComponent<AniBellyLaugh>();
            pool.bias = MovStoryBias.Achieve;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.TearCry;
            pool.story = GameObject.Find("Table/Addition/MovStorys/TearCry").GetComponent<AniTearCry>();
            pool.bias = MovStoryBias.BallFar;
            pools.Add(pool);

            pool = new PoolStory();
            pool.identity = StoryIdentity.Foul;
            pool.story = GameObject.Find("Table/Addition/MovStorys/Foul").GetComponent<AniFoul>();
            pool.bias = MovStoryBias.Foul;
            pools.Add(pool);

            physicsMng = GetComponent<PhysicsMng>();
            physicsMng.OnBallMovStory += PhysicsMng_OnBallMovStory;
            
            //GameObject g_y = Instantiate(signPrefab_y, parent);
            //signObjs_y.Add(g_y);
            //g_y.transform.parent = parent;
            //for (int i = 0; i < 40; i++)
            //{
            //    GameObject g_r = Instantiate(signPrefab_r, parent);
            //    GameObject g_b = Instantiate(signPrefab_b, parent);
            //    g_r.transform.position = new Vector3(0, -1, 0);
            //    g_b.transform.position = new Vector3(0, -1, 0);

            //    g_r.transform.parent = parent;
            //    g_b.transform.parent = parent;
            //    signObjs_r.Add(g_r);
            //    signObjs_b.Add(g_b);
            //}

            SetStuffPosWide();
        }

        private void OnEnable()
        {
            physicsMng.OnChoiceStory += physicsMng_OnChoiceStory;
        }

        private void OnDisable()
        {
            physicsMng.OnChoiceStory -= physicsMng_OnChoiceStory;
        }

        public void SetStuffPosWide()
        {
            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;

            rect2HListPos.Clear();
            rect2VListPos.Clear();
            rect4ListPos.Clear();

            //Debug.Log($"P : {p} .. dist : {dist} .. height : {height} ");

            for (int w = 0; w < p * 2; w++)
            {
                //Debug.Log($"1111 P : {p} .. w : {w} .. dist : {dist} ");
                //Debug.Log($"1111 P : {p} .. w : {w} .. dist : {dist} ");

                float _x = st.position.x + w * dist * 2;
                for (int h = 0; h < p; h++)
                {
                    //Debug.Log($"2222 P : {p} .. h : {h} .. dist : {dist} ");
                    float _z = st.position.z - h * dist;
                    float dist_c = dist * .5f;
                    Vector3 cen = new Vector3(_x + (dist_c * 2), 0, _z - dist_c);
                    rect4ListPos.Add(cen);
                    //Debug.Log($"wc : {w}, h : {h}, cen : {cen}");
                }
            }

            float x0, z0;
            //signObjs_r.ForEach(o => o.transform.position = new Vector3(0, -1, 0));
            //signObjs_b.ForEach(o => o.transform.position = new Vector3(0, -1, 0));

            ////signObjs_r.ForEach(o => o.transform.position = );
            //for (int i = 0; i < rect4ListPos.Count; i++)
            //{
            //    signObjs_r[i].transform.position = rect4ListPos[i];
            //}

            // 8 - 4x 

        }

        Table_Rect_2H GetTableRect2H(int idx)
        {
            Table_Rect_2H table_rect = Table_Rect_2H.Left;
            switch (idx)
            {
                case 0: table_rect = Table_Rect_2H.Left; break;
                case 1: table_rect = Table_Rect_2H.Left; break;
                case 2: table_rect = Table_Rect_2H.Right; break;
                case 3: table_rect = Table_Rect_2H.Right; break;
            }
            return table_rect;
        }

        Table_Rect_2V GetTableRect2V(int idx)
        {
            Table_Rect_2V table_rect = Table_Rect_2V.Up;
            switch (idx)
            {
                case 0: table_rect = Table_Rect_2V.Up; break;
                case 1: table_rect = Table_Rect_2V.Down; break;
                case 2: table_rect = Table_Rect_2V.Up; break;
                case 3: table_rect = Table_Rect_2V.Down; break;
            }
            return table_rect;
        }

        Table_Rect_4 GetTableRect4(int idx)
        {
            Table_Rect_4 table_rect = Table_Rect_4.UpLeft;
            switch (idx)
            {
                case 0: table_rect = Table_Rect_4.UpLeft; break;
                case 1: table_rect = Table_Rect_4.DownLeft; break;
                case 2: table_rect = Table_Rect_4.UpRight; break;
                case 3: table_rect = Table_Rect_4.DownRight; break;
            }
            return table_rect;
        }

        public Vector3 SetRandPos4(Vector3 pos)
        {
            //Debug.Log($"SetRandPos4 rect4ListPos.Count : {rect4ListPos.Count}");
            if (rect4ListPos.Count == 0)
            {
                SetStuffPosWide();
            }

            //float x = UnityEngine.Random.Range(st.position.x, en.position.x);
            //float z = UnityEngine.Random.Range(en.position.z, st.position.z);
            //Debug.Log($"SetRandPos x:{x}, z:{z}");
            //Vector3 pos = new Vector3(x, 0, z);

            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;
            float dist_w = dist * 1.0f;
            float dist_h = dist * 0.5f;
            Table_Rect_4 table_rect = Table_Rect_4.UpLeft;
            //signObjs_y[0].transform.position = pos;
            int s = -1;
            for (int i = 0; i < rect4ListPos.Count; i++)
            {
                if (rect4ListPos[i].x - dist_w < pos.x && pos.x < rect4ListPos[i].x + dist_w &&
                   rect4ListPos[i].z - dist_h < pos.z && pos.z < rect4ListPos[i].z + dist_h)
                {
                    //signObjs_b[0].transform.position = stuffListPos[i];
                    s = i;
                    break;
                }
            }

            table_rect = GetTableRect4(s);
            int rnd = s;
            while (true)
            {
                rnd = UnityEngine.Random.Range(0, 4);
                if (rnd != s) break;
            }
            //signObjs_b[0].transform.position = rect4ListPos[rnd];
            //Debug.Log($"rnd : {rnd}, ");
            //Debug.Log($"rect4ListPos[0] : {rect4ListPos[0]}, ");
            //Debug.Log($"rect4ListPos[1] : {rect4ListPos[1]}, ");
            //Debug.Log($"rect4ListPos[2] : {rect4ListPos[2]}, ");
            //Debug.Log($"rect4ListPos[3] : {rect4ListPos[3]}, ");
            return rect4ListPos[rnd];
        }

        public Vector3 SetRandPos2H(Vector3 pos)
        {
            if (rect4ListPos.Count == 0)
            {
                SetStuffPosWide();
            }

            rect2HListPos.Clear();


            //float x = UnityEngine.Random.Range(st.position.x, en.position.x);
            //float z = UnityEngine.Random.Range(en.position.z, st.position.z);
            ////Debug.Log($"SetRandPos x:{x}, z:{z}");
            //Vector3 pos = new Vector3(x, 0, z);

            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;
            float dist_w = dist * 1.0f;
            float dist_h = dist * 0.5f;
            Table_Rect_4 table_rect = Table_Rect_4.UpLeft;
            //signObjs_y[0].transform.position = pos;

            int s = -1;
            for (int i = 0; i < rect4ListPos.Count; i++)
            {
                if (rect4ListPos[i].x - dist_w < pos.x && pos.x < rect4ListPos[i].x + dist_w &&
                   rect4ListPos[i].z - dist_h < pos.z && pos.z < rect4ListPos[i].z + dist_h)
                {
                    //signObjs_b[0].transform.position = stuffListPos[i];
                    s = i;
                    break;
                }
            }

            for (int i = 0; i < rect4ListPos.Count; i++)
            {
                if (i % 2 != 0) continue;
                float xh = rect4ListPos[i].x;
                float zh = rect4ListPos[i].z + ((rect4ListPos[i + 1].z - rect4ListPos[i].z) * 0.5f);
                Vector3 posH = new Vector3(xh, 0, zh);
                rect2HListPos.Add(posH);
            }

            //dist_w = dist_w * 0.5f;
            //dist_h = dist_h * 0.5f;
            dist_w = (rect2HListPos[1].x - rect2HListPos[0].x) * 0.5f;
            for (int i = 0; i < rect2HListPos.Count; i++)
            {
                if (rect2HListPos[i].x - dist_w < pos.x && pos.x < rect2HListPos[i].x + dist_w)
                {
                    s = i;
                }
                //signObjs_b[i].transform.position = rect2HListPos[i];
            }

            int rnd = s == 0 ? 1 : 0;
            //signObjs_b[0].transform.position = rect2HListPos[rnd];
            return rect2HListPos[rnd];
        }

        public Vector3 SetRandPos2V(Vector3 pos)
        {
            if (rect4ListPos.Count == 0)
            {
                SetStuffPosWide();
            }

            rect2VListPos.Clear();


            //float x = UnityEngine.Random.Range(st.position.x, en.position.x);
            //float z = UnityEngine.Random.Range(en.position.z, st.position.z);
            ////Debug.Log($"SetRandPos x:{x}, z:{z}");
            //Vector3 pos = new Vector3(x, 0, z);

            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;
            float dist_w = dist * 1.0f;
            float dist_h = dist * 0.5f;
            Table_Rect_4 table_rect = Table_Rect_4.UpLeft;
            //signObjs_y[0].transform.position = pos;
            int s = -1;


            for (int i = 0; i < rect4ListPos.Count - 2; i++)
            {
                if (i > 1) continue;
                float xv = rect4ListPos[0].x + ((rect4ListPos[2].x - rect4ListPos[0].x) * 0.5f);
                float zv = rect4ListPos[i].z;
                Vector3 posV = new Vector3(xv, 0, zv);
                rect2VListPos.Add(posV);
            }

            dist_h = (rect2VListPos[0].z - rect2VListPos[1].z) * 0.5f;
            for (int i = 0; i < rect2VListPos.Count; i++)
            {
                //Debug.Log($"dist_h : {dist_h} .. i : {i} .. rect2VListPos[i].z : {rect2VListPos[i].z}"); 
                if (rect2VListPos[i].z - dist_h < pos.z && pos.z < rect2VListPos[i].z + dist_h)
                {
                    s = i;
                }
                //signObjs_b[i].transform.position = rect2VListPos[i];
            }

            int rnd = s == 0 ? 1 : 0;
            //signObjs_b[0].transform.position = rect2VListPos[rnd];

            return rect2VListPos[rnd];
        }



        public void ReqMovStory(MovStoryBias bias, Vector3 pos)
        {
            if (bias == MovStoryBias.None) return;
            PoolStory p = pools.Where(p => p.bias == bias).OrderBy(o => Guid.NewGuid()).Take(1).FirstOrDefault();
            if(p == null)
            {
                Debug.LogWarning($"ReqMovStory bias : {bias} .. PoolStory NULL ");
                return;
            }

            StoryIdentity idx = p.identity;
            StoryAniPos sap = p.story.storyAniPos;
            Vector3 _pos = pos;
            switch (sap)
            {
                case StoryAniPos.Full:
                    _pos = Vector3.zero;
                    break;
                case StoryAniPos.FourSplit:
                    _pos = SetRandPos4(pos);
                    break;
                case StoryAniPos.TwoVertical:
                    _pos = SetRandPos2V(pos);
                    break;
                case StoryAniPos.TwoHorizontal:
                    _pos = SetRandPos2H(pos);
                    break;
            }

            // 상대에게 보냄.
            if (PoolLogic.controlInNetwork)
            {
                string storyData = $"[{(int)idx};{DataManager.Vector3ToString(_pos)}]";
                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetBallMovStoryFromNetwork), physicsMng.moveTime, storyData);
            }

            ChoiceMovStory(idx, _pos);
        }


        // 상대로부터 받음.
        public void physicsMng_OnChoiceStory(StoryIdentity idx, Vector3 pos)
        {
            ChoiceMovStory(idx, pos);
        }


        public void ChoiceMovStory(StoryIdentity identity, Vector3 pos)
        {
            //if (_tokenSource != null)
            //{
            //    _tokenSource.Cancel();
            //}
            //_tokenSource = new CancellationTokenSource();
            //_cancellationToken = _tokenSource.Token;

            if(actPool != null && actPool.story != null)
            {
                actPool.story.CancelSequence();
            }
            Ball ball = PoolCoach.Instance.balls[PoolPlayer.currentPlayer.playerId];

            //Debug.Log($"ChoiceMovStory pos : {pos} .. identity : {identity}, ball.id : {ball.id}, ball.isCueBall : {ball.isCueBall} ");
            
            actPool = pools.Where(p => p.identity == identity).FirstOrDefault();
            //actPool = pools.Where(p => p.identity == StoryIdentity.PongHammer).FirstOrDefault();
            actPool.story.StartSequence(pos);

            //p.story.AniPlay(pos, _cancellationToken);
        }

        void PhysicsMng_OnBallMovStory(BallMovingDigest digest, BallMovingStory story, int cnt, Vector3 pos)
        {
            MovStoryBias bias = MovStoryBias.None;

            switch(story)
            {
                case BallMovingStory.KissNormal:
                    bias = MovStoryBias.Kiss;
                    break;
                case BallMovingStory.TargetPosLook:
                    break;
                case BallMovingStory.TargetBallLook:
                    bias = MovStoryBias.Boost;
                    break;
                case BallMovingStory.TargetBallFar:
                    bias = MovStoryBias.BallFar;
                    break;
                case BallMovingStory.ForceOut:
                    bias = MovStoryBias.Miss;
                    break;
                case BallMovingStory.KissWeaken:
                    bias = MovStoryBias.KissHate;
                    break;
                case BallMovingStory.AchieveKiss:
                    bias = MovStoryBias.KissWell;
                    break;
                case BallMovingStory.AchieveNormal:
                case BallMovingStory.AchieveRelax:
                    bias = MovStoryBias.Achieve;
                    break;
            }

            ReqMovStory(bias, pos);
        }

        private void Update()
        {
            //if (Input.GetKeyDown(KeyCode.Alpha1))
            //{
            //    ReqMovStory(MovStoryBias.Achieve, new Vector3(-0.8f, 0, 0.2f));
            //}
            //else if (Input.GetKeyDown(KeyCode.Alpha2))
            //{
            //    if (actPool != null && actPool.story != null)
            //    {
            //        actPool.story.CancelSequence();
            //    }
            //}
            //else if (Input.GetKeyDown(KeyCode.Alpha3))
            //{
            //    ReqMovStory(MovStoryBias.Achieve, new Vector3(-0.8f, 0, 0.2f));
            //}

            //else if (Input.GetKeyDown(KeyCode.Alpha0))
            //{
            //    //ReqMovStory(MovStoryBias.Achieve, new Vector3(-0.8f, 0, 0.2f));

            //}
        }



    }
}
