using Assets.Scripts.Sight.Vital.Pavilion;
using System;
using UnityEngine;


namespace Assets.Scripts.Often
{
    public class Utility
    {


        public static string CoinNumToStr(long _coin, sbyte grp = 2)
        {
            string vUnitNum, vFourNum, vHanAmt, vPartAmt;

            byte iNumIdx, iFourType, iCnt, iAmtLen;
            //long _coin = (long)(coin * 1000);
            string sCoin = _coin.ToString();
            iAmtLen = (byte)sCoin.Length;
            vUnitNum = vFourNum = vHanAmt = "";
            iCnt = 0;
            while (iCnt < iAmtLen)
            {
                vPartAmt = sCoin.Substring(iCnt, 1);
                iNumIdx = (byte)(iAmtLen - iCnt);  // 
                iFourType = 0;
                switch (iNumIdx)
                {
                    case 1: vUnitNum += "원"; grp--; break;
                    case 5: vUnitNum += "만"; grp--; break;
                    case 9: vUnitNum += "억"; grp--; break;
                    case 13: vUnitNum += "조"; grp--; break;
                    case 17: vUnitNum += "경"; grp--; break;
                    default:
                        iFourType = 1;
                        vFourNum = (Convert.ToInt16(vFourNum + vPartAmt)).ToString();
                        break;
                }
                if (grp < 0)
                {
                    break;
                }

                if (iFourType == 0)
                {
                    vFourNum = (Convert.ToInt16(vFourNum + vPartAmt)).ToString();
                    if (vFourNum == "0") vFourNum = vUnitNum = "";
                    vHanAmt += vFourNum + vUnitNum;
                    vFourNum = vUnitNum = "";
                }

                //Debug.Log($"iCnt : {iCnt}, iNumIdx : {iNumIdx}, g : {grp}, vFourNum : {vFourNum}, vHanAmt : {vHanAmt} ");


                iCnt++;
            }

            return vHanAmt;
        }




        public static void DrawBallHitLine(LineRenderer[] cueBallLines, float cueBallRadius, int ballLayer, int boardLayer, ref Vector3[] find_pos_lst, ref int is_find_idx, int target_id, int cnt, Vector3 origin, Vector3 direction, float line_len, bool isBallHit = false, bool isCueball = true)
        {
            LineRenderer[] lines = cueBallLines;
            //pos_ball = pos_ball == Vector3.zero ? Vector3.zero : pos_ball;

            if (cnt == 0)
            {
                for (int i = cnt; i < lines.Length; i++)
                {
                    lines[i].positionCount = 0;
                }
            }

            RaycastHit targetShapeHit;

            if (Physics.SphereCast(origin, cueBallRadius, direction, out targetShapeHit, cnt == 0 && isCueball ? 3f : line_len, isBallHit || !isCueball ? boardLayer : (ballLayer | boardLayer)))
            {
                Ball listener = targetShapeHit.collider.gameObject.GetComponent<Ball>();

                // 적구와 충돌시 
                if (listener)
                {
                    isBallHit = true;       // 적구는 한번만 체크 함.
                    // 적구타격시수구위치 = 적구위치 + 수구반지름 * 타격방향
                    Vector3 positionInHit = targetShapeHit.point + cueBallRadius * targetShapeHit.normal;
                    // 볼체커위치설정 ...
                    Vector3 targetHit_normal = Vector3.ProjectOnPlane(targetShapeHit.normal, Vector3.up).normalized;
                    Vector3 new_direction = Vector3.Cross(Vector3.up, targetHit_normal);
                    float tangent = Vector3.Dot(Vector3.ProjectOnPlane(direction, Vector3.up), new_direction);
                    if (tangent < 0) new_direction = -new_direction;
                    float moved_line_dist = Vector3.Distance(origin, positionInHit);                    // 출발점에서 충돌지점간 거리
                    float line_remain = cnt == 0 ? line_len : line_len - moved_line_dist;                 // 첫시작에서는 남은거리 계산 필요없음.
                    float new_line_len = Mathf.Clamp(Mathf.Abs(tangent), 0.2f, 1.0f) * line_remain;     // 적구와 충돌시 데미지 적용
                    float figure = 1 - Mathf.Abs(tangent);

                    //lines[cnt].positionCount = 2;
                    //lines[cnt].SetPosition(0, origin);
                    //lines[cnt].SetPosition(1, positionInHit);


                    //SetBallChecker(positionInHit, listener.id);

                    if (target_id == listener.id)
                    {
                        is_find_idx = cnt;
                        find_pos_lst[cnt] = positionInHit;
                    }

                    //Debug.Log($"DrawBallHitLine cnt : {cnt}, origin : {origin} .. ");
                    float cueTiling = lineDistToTiling(moved_line_dist);
                    //lines[cnt].material.SetVector("_Tiling", new Vector2(cueTiling, 1));

                    //Vector3 targetPosition = listener.GetComponent<Rigidbody>().position;
                    //Vector3 targetDirection = (listener.GetComponent<Rigidbody>().position - positionInHit).normalized;
                    //float target_len = Mathf.Clamp(1.0f - Mathf.Abs(tangent), 0.2f, 1.0f) * line_len;



                    cnt++;

                    DrawBallHitLine(cueBallLines, cueBallRadius, ballLayer, boardLayer, ref find_pos_lst, ref is_find_idx, target_id, cnt, positionInHit, new_direction, new_line_len, isBallHit, isCueball);

                }
                // 보더와 충돌시
                else
                {

                    Vector3 positionInHit = targetShapeHit.point + cueBallRadius * targetShapeHit.normal;

                    //lines[cnt].positionCount = 2;
                    //lines[cnt].SetPosition(0, origin);
                    //lines[cnt].SetPosition(1, positionInHit);


                    find_pos_lst[cnt] = positionInHit;

                    Vector3 projectOnNormal = Vector3.Project(Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.ProjectOnPlane(targetShapeHit.normal, Vector3.up).normalized);
                    Vector3 new_direction = Vector3.ProjectOnPlane(direction, Vector3.up) - 2.0f * projectOnNormal;

                    float moved_line_dist = Vector3.Distance(origin, positionInHit);                    // 출발점에서 충돌지점간 거리
                    float line_remain = cnt == 0 && isCueball ? line_len : line_len - moved_line_dist;                 // 첫시작에서는 남은거리 계산 필요없음.
                    float new_line_len = line_remain;                                                   // 

                    float cueTiling = lineDistToTiling(moved_line_dist);
                    //lines[cnt].material.SetVector("_Tiling", new Vector2(cueTiling, 1));




                    cnt++;
                    DrawBallHitLine(cueBallLines, cueBallRadius, ballLayer, boardLayer, ref find_pos_lst, ref is_find_idx, target_id, cnt, positionInHit, new_direction, new_line_len, isBallHit, isCueball);

                }



            }
            // 충돌이 없다면
            else
            {

                Vector3 line_end_position = origin + line_len * Vector3.ProjectOnPlane(direction, Vector3.up);
                //lines[cnt].positionCount = 2;
                //lines[cnt].SetPosition(0, origin);
                //lines[cnt].SetPosition(1, line_end_position);

                float moved_line_dist = Vector3.Distance(origin, line_end_position);                    // 출발점에서 종료지점간 거리
                //Debug.Log($"xxxxxx DrawBallHitLine cnt : {cnt}, origin : {origin} .. ");


                // 처음이 아니면 화살표의 개수를 지정
                //if ((cnt > 0 && isCueball) || !isCueball)
                //{
                //    float cueTiling = lineDistToTiling(moved_line_dist);
                //    lines[cnt].material.SetVector("_Tiling", new Vector2(cueTiling, 1));
                //}

                // 종료 후 남은 라인들은 초기화
                cnt++;
                //for (int i = cnt; i < lines.Length; i++)
                //{
                //    //Debug.Log($"cueball line reset {i}");
                //    lines[i].positionCount = 0;
                //}

            }



        }



        static float lineDistToTiling(float dist)
        {
            float ret = 1;
            ret = dist * 20;

            return ret;
        }


    }
}
