using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
{


    public class DrawStuffPos : MonoBehaviour
    {

        public Transform st;
        public Transform en;
        [Range(1, 10)]
        public int p;
        public float radius = 0.03f;
        public List<Vector3> stuffListPos;
        public PhysicsMng physicsMng;
        private int index = 0;
        float dist_belt_min = 0.4f;

        public int effectIng { get; private set; }
        public int effectStuffEa { get; set; }

        private int point_effect = 10;              // 효과로 얻는 포인트
        public string stuff_data
        {
            get
            {
                string stuffData = string.Empty;
                // 상대턴 일때 현재 스터프 정보를 보냄.
                List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
                for (int i = 0; i < liveLst.Count; i++)
                {
                    int pid = liveLst[i].positionId;
                    int tid = liveLst[i].touchId;
                    stuffData += $"({pid},{tid})";
                }
                return stuffData;
            }

        }

        public void stuff_array(string str_data)
        {
            string[] stuffRows = DataManager.ConvertArrayDataToBraceArray(str_data);
            List<int[]> other = new List<int[]>();
            Debug.Log(str_data);

            for (int i = 0; i < stuffRows.Length; i++)
            {
                if (stuffRows[i] == string.Empty) continue;
                if (stuffRows[i].IndexOf(',') < 0) continue;
                string[] stuff = DataManager.ConvertDataToBraceArray(stuffRows[i]);
                int pId = int.Parse(stuff[0]);
                int tId = int.Parse(stuff[1]);
                other.Add(new int[2] { pId, tId });
            }
            List<int[]> other_ord = other.OrderBy(r => r[0]).ToList();

            //Debug.Log($"other_ord cnt : {other_ord.Count}    =====================");
            //for (int i = 0; i < other_ord.Count; i++)
            //{
            //    Debug.Log($"{i} : pid : {other_ord[i][0]}, tid : {other_ord[i][1]}");
            //}

            List<int[]> self = new List<int[]>();
            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            for (int i = 0; i < liveLst.Count; i++)
            {
                int pid = liveLst[i].positionId;
                int tid = liveLst[i].touchId;
                self.Add(new int[2] { pid, tid });
            }
            List<int[]> self_ord = self.OrderBy(r => r[0]).ToList();
            Debug.Log($"self_ord cnt : {self_ord.Count}     ===================");
            bool isSame = true;
            int noSameIdx = -1;
            for (int i = 0; i < self_ord.Count; i++)
            {
                if (self_ord[i][0] != other_ord[i][0])
                {
                    noSameIdx = i;
                    isSame = false;
                    //Debug.Log($"{i} : self_ord_pid : {self_ord[i][0]}, self_ord_tid : {self_ord[i][1]}   ...  other_ord_pid : {other_ord[i][0]}, other_ord_tid : {other_ord[i][1]}");
                }
            }

            //Debug.Log($"   stuffLogs.Count : {stuffLogs.Count}    ********* ");
            for (int i = 0; i < stuffLogs.Count; i++)
            {
               // Debug.Log($" tid : {stuffLogs[i].tid}  ..  pids : {stuffLogs[i].pids}  ");
            }

            if (!isSame)
            {
                //Debug.Log($" ************************************** NO SAME   ******************************************************");
                //Debug.Log($"{noSameIdx} : self_ord_pid : {self_ord[noSameIdx][0]}, self_ord_tid : {self_ord[noSameIdx][1]}   ...  other_ord_pid : {other_ord[noSameIdx][0]}, other_ord_tid : {other_ord[noSameIdx][1]}");
                //Debug.Log($"**********************************************************************************************");
            }

            stuffLogs.Clear();

        }

        //public struct StuffReport
        //{
        //    public int pid_0;
        //    public int tid_0;
        //    public int pid_1;
        //    public int tid_1;
        //    public int status;      // 0:  ,  1:s만 있다, 2: 불일치, 3:r만 있다, 4:s만 있다, 5:불일치, 6:r만 있다
        //    public 
        //}


        public struct StuffLog
        {
            public int tid;
            public string pids;

            public StuffLog(int tid, string pids)
            {
                this.tid = tid;
                this.pids = pids;
            }
        }

        public List<StuffLog> stuffLogs = new List<StuffLog>();

        private PnlMatch pnlMatch;

        private void Awake()
        {
            Debug.Log($"{this.GetType().Name} ===== ");

            physicsMng = FindObjectOfType<PhysicsMng>();

            st = GameObject.Find("Table/Addition/Position/st_table").transform;
            en = GameObject.Find("Table/Addition/Position/en_table").transform;

            float w = en.position.x - st.position.x;
            float h = en.position.z - st.position.z;
            float d = w / 2 * 1 / p;

            SetStuffPos();
            effectIng = 0;
        }

        private void OnEnable()
        {
            pnlMatch = pnlMatch ?? PnlMatch.FindObjectOfType<PnlMatch>();


        }

        private void OnDisable()
        {


        }




        public void SetStuffPos()
        {
            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;

            for (int w = 0; w < 2 * p; w++)
            {
                float _x = st.position.x + w * dist;
                for (int h = 0; h < p; h++)
                {
                    float _z = st.position.z - h * dist;
                    float dist_c = dist * .5f;
                    Vector3 cen = new Vector3(_x + dist_c, 0, _z - dist_c);
                    stuffListPos.Add(cen);
                    //Debug.Log($"w : {w}, h : {h}, cen : {cen}");
                }
            }

        }


        int[] StuffPosIdAdd(int req_cnt = 5)
        {
            int add_cnt = req_cnt;


            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            int remain_cnt = stuffListPos.Count - liveLst.Count;
            // 남은개수가 작다면 요청한 개수를 변경한다.
            if (remain_cnt < add_cnt)
            {
                add_cnt = remain_cnt;
            }
            int[] lst_tmp = new int[add_cnt];

            //Debug.Log("StuffPosIdAdd " + stuffListPos.Count + ", req cnt : " + add_cnt + ", liveLst cnt : " + liveLst.Count);



            //return lst;
            if (liveLst.Count >= stuffListPos.Count)
            {
                //Debug.Log($"live cnt : {liveLst.Count}  ======= pos cnt : {stuffListPos.Count} add no ... full pos ");
                lst_tmp = new int[1] { -1 };
                return lst_tmp;
            }

            if (liveLst.Count + add_cnt >= stuffListPos.Count)
            {
                //Debug.Log($"req add cnt : {add_cnt} over old cnt ... livecnt : {liveLst.Count}, list pos : {stuffListPos.Count}  ======= cur cnt : {stuffListPos.Count - liveLst.Count} ");
                add_cnt = stuffListPos.Count - liveLst.Count;
                if (add_cnt == 0)
                {
                    lst_tmp = new int[1] { -1 };
                    //Debug.Log(" add NO ~~~~~~~~ ");
                    return lst_tmp;
                }
            }


            for (int i = 0; i < add_cnt; i++)
            {
                lst_tmp[i] = UnityEngine.Random.Range(0, stuffListPos.Count);

                // 이미 저장값이면 되돌림
                for (int j = 0; j < i; j++)
                {
                    if (lst_tmp[j] == lst_tmp[i])
                    {
                        i = i - 1;
                        break;
                    }
                }

                // 이미 존재하면 되돌림 
                for (int s = 0; s < liveLst.Count; s++)
                {
                    if (liveLst[s].positionId == lst_tmp[i])
                    {
                        i = i - 1;
                        break;
                    }
                }
            }
            return lst_tmp;
        }



        public void StuffCreate(int len, int itemCnt = 0)
        {

            int[] priodeSec = new int[3] { 15, 17, 19 };
            int[] posIds = StuffPosIdAdd(len);
            string stuffData = string.Empty;



            if (posIds.Length == 1)
            {
                if (posIds[0] == -1)
                {
                    Debug.Log($"StuffCreate add NO !! ");
                    return;
                }
            }
            if (len > posIds.Length)
            {
                len = posIds.Length;
            }
            string pos_str = $"{string.Join(",", posIds)}";
            //Debug.Log($"StuffCreate len : {len}, itemCnt : {itemCnt}, posIds   : { pos_str }");
            //return;

            int enum_item_len = Enum.GetValues(typeof(StuffType)).Length;
            for (int i = 0; i < len; i++)
            {
                int typeId;
                if (itemCnt > 0)
                {
                    int itemYnRnd = UnityEngine.Random.Range(0, 3); // 확률 20%
                    //Debug.Log($"itemYnRnd : {itemYnRnd}");
                    if (itemYnRnd == 0)
                    {
                        int itemRnd = UnityEngine.Random.Range(1, enum_item_len);
                        //Debug.Log($"itemRnd : {itemRnd}");
                        typeId = itemRnd;
                    }
                    else
                    {
                        typeId = 0;
                    }
                    itemCnt--;
                }
                else
                {
                    typeId = 0;
                }

                int posId = posIds[i];
                int secRnd = UnityEngine.Random.Range(0, priodeSec.Length);
                int sec = priodeSec[secRnd];
                Vector3 pos = stuffListPos[posId];
                StuffBase stuffItem = ObjectPooler.instance.SpawnFromPool(((StuffType)typeId).ToString(), pos).GetComponent<StuffBase>();
                //string mm = posId == 0 ? " ******************************************************* " : "";
                //Debug.Log($" {i} .. typeId : {typeId} , typeNm : {((StuffType)typeId).ToString()} ");
                //Debug.Log($" {i} .. name : {stuffItem.name}  ");
                //Debug.Log($" {i} .. itemCnt : {itemCnt}, posIds.LEN : {posIds.Length}, posId : {posId}, sec : {sec} ... {mm}");
                stuffItem.Setup((StuffType)typeId, posId, sec);

                stuffData += $"({typeId},{posId},{sec})";
            }

            if (stuffData != string.Empty)
            {
                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetStuffCreateFromNetwork), physicsMng.moveTime, stuffData);
            }

        }

        public async UniTaskVoid SetStuffCreateFromNetwork(float netMoveTime, string stuffData)
        {
            if (stuffData.IndexOf(')') < 0) return;
            while (!physicsMng.endFromNetwork && physicsMng.moveTime < netMoveTime)
            {
                await UniTask.WaitForFixedUpdate();
            }

            string[] stuffRows = DataManager.ConvertArrayDataToBraceArray(stuffData);
            for (int i = 0; i < stuffRows.Length; i++)
            {
                if (stuffRows[i] == string.Empty) continue;
                if (stuffRows[i].IndexOf(',') < 0) continue;
                string[] stuff = DataManager.ConvertDataToBraceArray(stuffRows[i]);
                int typeId = int.Parse(stuff[0]);
                int posId = int.Parse(stuff[1]);
                int sec = int.Parse(stuff[2]);
                Vector3 pos = stuffListPos[posId];

                StuffBase stuffItem = ObjectPooler.instance.SpawnFromPool(((StuffType)typeId).ToString(), pos).GetComponent<StuffBase>();
                stuffItem.Setup((StuffType)typeId, posId, sec);
            }


        }


        public void StuffVanishNotice(int stuffId, int coin_ch)
        {
            if (PoolLogic.controlInNetwork)
            {
                //pnlMatch.PoolCoach_OnSetGameInfo($"[보냄]StuffVanishNotice stuffId : {stuffId} , coin_ch : {coin_ch}");

                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetStuffVanishFromNetwork), physicsMng.moveTime, stuffId, coin_ch);
            }
        }


        // 상대로부터 소멸 요청 받음
        public async UniTaskVoid SetStuffVanishFromNetwork(float netMoveTime, int stuffId, int coin_ch)
        {

            //pnlMatch.PoolCoach_OnSetGameInfo($"[받음]StuffVanishNotice stuffId : {stuffId} , coin_ch : {coin_ch}");
            int turnId = PoolPlayer.turnId;
            while (!physicsMng.endFromNetwork && physicsMng.moveTime < netMoveTime)
            {
                await UniTask.WaitForFixedUpdate();
            }

            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            //Debug.Log($"SetStuffVanishFromNetwork liveLst cnt : {liveLst.Count} .. stuffId : {stuffId}");
            for (int s = 0; s < liveLst.Count; s++)
            {
                //Debug.Log($"SetStuffVanishFromNetwork liveLst.positionId : {liveLst[s].positionId} ");
                if (liveLst[s].positionId == stuffId)
                {

                    //pnlMatch.PoolCoach_OnSetGameInfo($"[받음]StuffVanishNotice 발견 stuffId : {stuffId} , coin_ch : {coin_ch}");
                    //Debug.Log($"SetStuffVanishFromNetwork stuffData : {stuffData}, coin_ch : {coin_ch}");
                    liveLst[s].BallTouch(turnId, coin_ch);
                    break;
                }
            }
        }

        public void CreateEff(int positionId, StuffType stuffType)
        {
            // 자신이 터치된 경우이므로 무조건 0
            int turnId = 0; // PoolPlayer.turnId;
            switch (stuffType)
            {
                case StuffType.StuffItem01:
                    AddEff03(positionId, turnId).Forget();
                    break;
                case StuffType.StuffItem02:
                    AddEff03(positionId, turnId).Forget();
                    break;//
                case StuffType.StuffItem03:
                    AddEff03(positionId, turnId).Forget();
                    break;
                case StuffType.StuffItem04:
                    AddEff03(positionId, turnId).Forget();
                    break;
            }


        }


        async UniTaskVoid AddEff01(int stuffId)
        {
            pnlMatch.PoolCoach_OnSetGameInfo($"[보냄]AddEff01 stuffId : {stuffId}");

            await UniTask.Yield();
            Vector3 pos = stuffListPos[stuffId];
            ObjectPooler.instance.SpawnFromPool(nameof(EffType.Effect01), pos);
            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetStuffEffect01FromNetwork), physicsMng.moveTime, stuffId);

        }

        public void SetStuffAllDeactive()
        {
            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            for (int i = 0; i < liveLst.Count; i++)
            {
                liveLst[i].DeactiveDelay();
            }

        }

        public async UniTaskVoid SetStuffEffect01FromNetwork(float netMoveTime, int stuffId)
        {
            pnlMatch.PoolCoach_OnSetGameInfo($"[받음]AddEff01 stuffId : {stuffId}");
            while (!physicsMng.endFromNetwork && physicsMng.moveTime < netMoveTime)
            {
                await UniTask.WaitForFixedUpdate();
            }
            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            for (int s = 0; s < liveLst.Count; s++)
            {
                if (liveLst[s].positionId == stuffId)
                {
                    Vector3 pos = stuffListPos[stuffId];
                    ObjectPooler.instance.SpawnFromPool(nameof(EffType.Effect01), pos);
                    liveLst[s].DeactiveDelay();
                    break;

                }
            }
        }

        async UniTaskVoid AddEff02(int stuffId)
        {
            await UniTask.Delay(200);
            Vector3 pos = stuffListPos[stuffId];
            Vector3 to = FindMoveSpot(pos);
            GameObject comet = ObjectPooler.instance.SpawnFromPool((EffType.Effect02).ToString(), pos);
            await UniTask.Delay(1000);

            int i = 0;
            Vector3 cur = comet.transform.position;
            while (Vector3.Distance(to, cur) > 0.005f)
            {
                cur = comet.transform.position;
                comet.transform.position = Vector3.Lerp(cur, to, 0.05f);
                await UniTask.Yield();
                i++;
            }
            pnlMatch.PoolCoach_OnSetGameInfo($"[보냄]AddEff02 stuffId : {stuffId}, to : {to}");

            NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetStuffEffect02FromNetwork), physicsMng.moveTime, stuffId, to);


        }

        public async UniTaskVoid SetStuffEffect02FromNetwork(float netMoveTime, int stuffId, Vector3 to)
        {
            pnlMatch.PoolCoach_OnSetGameInfo($"[받음]AddEff02 stuffId : {stuffId}, to : {to}");

            while (!physicsMng.endFromNetwork && physicsMng.moveTime < netMoveTime)
            {
                await UniTask.WaitForFixedUpdate();
            }
            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            for (int s = 0; s < liveLst.Count; s++)
            {
                if (liveLst[s].positionId == stuffId)
                {
                    liveLst[s].DeactiveDelay();

                    await UniTask.Delay(200);
                    Vector3 pos = stuffListPos[stuffId];
                    GameObject comet = ObjectPooler.instance.SpawnFromPool(nameof(EffType.Effect02), pos);
                    await UniTask.Delay(1000);
                    int i = 0;
                    Vector3 cur = comet.transform.position;
                    while (Vector3.Distance(to, cur) > 0.005f)
                    {
                        cur = comet.transform.position;
                        comet.transform.position = Vector3.Lerp(cur, to, 0.05f);
                        await UniTask.Yield();
                        i++;
                    }

                    break;
                }
            }
        }



        Vector3 FindMoveSpot(Vector3 pos)
        {

            //float width = drawStuffPos.en.position.x - drawStuffPos.st.position.x;
            //float height = drawStuffPos.en.position.z - drawStuffPos.st.position.z;
            //Rect rect = new Rect(drawStuffPos.st.position.x, drawStuffPos.st.position.z, width, height);

            Vector3 spot = Vector3.zero;
            float move_dist = 0.9f;
            List<Vector3> ava_pos = new List<Vector3>();
            for (int i = 0; i < stuffListPos.Count; i++)
            {
                Vector3 p = stuffListPos[i];
                float dist_temp = Vector3.Distance(p, pos);
                if (move_dist < dist_temp)
                {
                    ava_pos.Add(p);
                }
            }

            int rnd = UnityEngine.Random.Range(0, ava_pos.Count);
            spot = ava_pos[rnd];
            //Debug.Log($"FindMoveSpot cnt : {ava_pos.Count}, rnd : {rnd}, spot : {spot} ======================== ");
            //for (int i = 0; i < ava_pos.Count; i++)
            //{
            //    Debug.Log($"{i} : {ava_pos[i]}");
            //}

            return spot;
        }


        async UniTaskVoid AddEff03(int stuffId, int turnId)
        {

            effectIng++;

            float moveTime = physicsMng.moveTime;
            await UniTask.Yield();
            Vector3 pos = stuffListPos[stuffId];
            List<int> ord = new List<int>();
            List<StuffBase> stuffLst = new List<StuffBase>();
            int originId = PositionToPositionId(pos);
            ord.Add(originId);

            Vector3 origin = pos;

            GameObject eff = ObjectPooler.instance.SpawnFromPool(nameof(EffType.Effect03), Vector3.zero);
            LineRenderer belt = eff.GetComponent<LineRenderer>();
            belt.positionCount = 0;

            // 존재하는 스터프를 호출 
            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            for (int i = 0; i < liveLst.Count; i++)
            {
                if (ord.Count > 9) break;
                StuffBase stuff;
                int positionId_cur = FindNearbySpot(origin, ord.ToArray(), out stuff);
                if (positionId_cur == -1) break;
                ord.Add(positionId_cur);
                stuff.SetEffectAhead();
                stuffLst.Add(stuff);
                origin = stuffListPos[positionId_cur];
            }

            if(ord.Count > 0)
            {
                //string ord_str = string.Join(",", ord_real.ToArray());
                //pnlMatch.PoolCoach_OnSetGameInfo($"[보냄]AddEff03 stuffId : {stuffId}, ord : {string.Join(",", ord.ToArray())}");
                stuffLogs.Add(new StuffLog(originId, string.Join(",", ord.ToArray())));
                NetworkManager.network.SendRemoteMessage(nameof(NetworkManager.network.SetStuffEffect03FromNetwork), moveTime, stuffId, ord.ToArray());
            }

            //Debug.Log($"ord : { string.Join(",", ord?.ToArray())}");
            List<int> ord_real = new List<int>();
            ord_real.Add(originId);

            float c = 0.2f;
            belt.materials[0].color = Color.white * c;
            belt.positionCount = 0;

            // 
            if (ord.Count > 1)
            {
                // 벨트 위치 추가 
                for (int i = 0; i < ord.Count; i++)
                {
                    Vector3 spot = stuffListPos[ord[i]];
                    spot.y = .1f;
                    belt.positionCount++;
                    belt.SetPosition(belt.positionCount - 1, spot);
                    await UniTask.Delay(100);
                }

                // 벨트 색상 노랑을 변환 애니처리 
                while (c < 5)
                {
                    c += 0.2f;
                    belt.materials[0].color = Color.yellow * c;
                    await UniTask.Yield();
                }
                await UniTask.Delay(100);

                // 수집된 스터프에서 활성화된 것만 터치 처리하기.
                if (stuffLst.Count > 0)
                {
                    for (int i = 0; i < stuffLst.Count; i++)
                    {
                        //if (i == 0) continue;
                        if (stuffLst[i].gameObject != null && stuffLst[i].gameObject.activeSelf)
                        {
                            await stuffLst[i].EffectTouch(point_effect, originId, turnId);
                            //ord_real.Add(stuffLst[i].positionId);
                        }
                        else
                        {
                            //Debug.LogWarning($"AddEff03 DEACTIVE positionid : {stuffLst[i].positionId}, touchId : {stuffLst[i].touchId}, myTurn : {stuffLst[i].myTurn} , effectId_touch : {stuffLst[0].touchId}   ");
                        }
                        await UniTask.Delay(100);
                    }
                }
                while (c > .1f)
                {
                    c -= 0.1f;
                    belt.materials[0].color = Color.yellow * c;
                    await UniTask.Yield();
                }
                belt.positionCount = 0;
            }


            effectIng--;

            //Debug.Log($"AddEff03 MYTURN : {PoolLogic.controlInNetwork}  effectIng--  ({effectIng})=================================================== ");

        }

        public async UniTaskVoid SetStuffEffect03FromNetwork(float netMoveTime, int stuffId, int[] ord)
        {

            effectIng++;

            //pnlMatch.PoolCoach_OnSetGameInfo($"[받음]AddEff03 stuffId : {stuffId}, ord : {string.Join(",", ord)}");
            // 상대편이므로 무조건 1
            int turnId = 1; // PoolPlayer.turnId;

            while (!physicsMng.endFromNetwork && physicsMng.moveTime < netMoveTime)
            {
                await UniTask.WaitForFixedUpdate();
            }
            List<StuffBase> liveLst_from = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            for (int s = 0; s < liveLst_from.Count; s++)
            {
                if (liveLst_from[s].positionId == stuffId)
                {
                    liveLst_from[s].DeactiveDelay();
                    Vector3 pos = stuffListPos[stuffId];
                    Vector3 origin = pos;
                    StuffBase[] stuffLst = FindNearbySpotFrom(ord);

                    GameObject eff = ObjectPooler.instance.SpawnFromPool(nameof(EffType.Effect03), Vector3.zero);
                    LineRenderer belt = eff.GetComponent<LineRenderer>();
                    belt.positionCount = 0;

                    //Debug.Log($"SetStuffEffect03FromNetwork  === ord : {string.Join(",", ord)} .. stuffLst : {stuffLst.Length}  === ");

                    if (stuffLst.Length > 1)
                    {

                        float c = 0.2f;
                        belt.materials[0].color = Color.white * c;
                        if (ord.Length > 1)
                        {
                            belt.positionCount = 0;
                            for (int i = 0; i < ord.Length; i++)
                            {
                                Vector3 spot = stuffListPos[ord[i]];
                                spot.y = .1f;
                                belt.positionCount++;
                                belt.SetPosition(belt.positionCount - 1, spot);
                                await UniTask.Delay(100);
                            }

                            while (c < 5)
                            {
                                c += 0.2f;
                                belt.materials[0].color = Color.yellow * c;
                                await UniTask.Yield();
                            }
                            await UniTask.Delay(100);

                            // 0은 아이템임. 사라진 상태
                            //Debug.Log($" stuffLst.Length >>>> { stuffLst.Length}  ");
                            //if (stuffLst.Length > 1)
                            //{
                            for (int i = 1; i < stuffLst.Length; i++)
                            {
                                if (stuffLst[i] != null)
                                {
                                    if (stuffLst[i].gameObject != null)
                                    {
                                        if (stuffLst[i].gameObject.activeSelf)
                                        {
                                            //Debug.Log($"from i >>>> {i}, positionId : {stuffLst[i]?.positionId} , touchId : {stuffLst[i]?.touchId} ");
                                            await stuffLst[i].EffectTouch(10, ord[0], turnId);
                                            //coinCtrl.AddCoins(pos, 2, turnId);
                                        }
                                        else
                                        {
                                            //Debug.LogWarning($"AddEff03FROM DEACTIVE name : {stuffLst[i].gameObject.name} ..  positionid : {stuffLst[i].positionId}, touchId : {stuffLst[i].touchId}, myTurn : {stuffLst[i].myTurn}    ");
                                        }
                                    }
                                }
                                else
                                {
                                    //Debug.Log($" stuffLst[{i}] >>>> { stuffLst[i]?.name ?? "xx"}  ");
                                }
                                //Debug.Log($"{i} >> ord.pid : {ord[i]} .. stuffLst.name  {stuffLst[i]?.name ?? "xxx"}");
                                await UniTask.Delay(100);
                            }
                            //}

                            while (c > .1f)
                            {
                                c -= 0.1f;
                                belt.materials[0].color = Color.yellow * c;
                                await UniTask.Yield();
                            }
                            belt.positionCount = 0;
                        }
                        break;
                    }
                }
            }


            effectIng--;
            //Debug.Log($"SetStuffEffect03FromNetwork MYTURN : {PoolLogic.controlInNetwork}  effectIng--  ({effectIng})=================================================== ");



        }


        

        // 볼터치로 사라졌는데 검색되서 중복으로 사라지는 현상발생됨.
        int FindNearbySpot(Vector3 origin, int[] ord, out StuffBase stuffBase)
        {
            int idx = -1;
            float dist_near = dist_belt_min;
            stuffBase = null;
            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");
            //Debug.Log($"FindNearbySpot myturn : {PoolLogic.controlInNetwork} .. liveLst.Count : {liveLst.Count} ");

            for (int i = 0; i < liveLst.Count; i++)
            {
                if (liveLst[i].positionId == 0) continue;       // 0값은 제외
                if (liveLst[i].typeId != StuffType.StuffCoin01) continue;
                int i_old = ord.ToList().Find(o => o == liveLst[i].positionId);  // 일치되는값이 없다면 0... 반환 주의
                if (i_old != 0)
                {
                    continue;
                }

                float dist_temp = Vector3.Distance(origin, liveLst[i].transform.position);
                if (dist_temp > dist_near) continue;
                dist_near = dist_temp;
                idx = liveLst[i].positionId;
                stuffBase = liveLst[i];
            }

            return idx;
        }

        StuffBase[] FindNearbySpotFrom(int[] ord)
        {
            StuffBase[] stuffBase = new StuffBase[ord.Length];
            List<StuffBase> liveLst = ObjectPooler.instance.GetActivePools<StuffBase>("Stuff");

            for (int o = 0; o < ord.Length; o++)
            {
                for (int i = 0; i < liveLst.Count; i++)
                {
                    if (ord[o] == liveLst[i].positionId)
                    {
                        stuffBase[o] = liveLst[i];
                        break;
                    }
                }
            }

            return stuffBase;
        }

        int PositionToPositionId(Vector3 pos)
        {
            int positionId = -1;
            // drawStuffPos.stuffListPos.Find(o => o == pos)

            for (int i = 0; i < stuffListPos.Count; i++)
            {
                if (stuffListPos[i] == pos)
                {
                    positionId = i;
                    break;
                }
            }

            return positionId;
        }

        public void AddEff04()
        {

        }






        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////



    }
}
