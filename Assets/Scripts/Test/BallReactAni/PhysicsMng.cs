using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Test.BallReactAni
{




    public class PhysicsMng : MonoBehaviour
    {

        public Transform st;
        public Transform en;
        public List<Vector3> rect4ListPos;
        public List<Vector3> rect2HListPos;
        public List<Vector3> rect2VListPos;
        public List<GameObject> signObjs_r;
        public List<GameObject> signObjs_b;
        public List<GameObject> signObjs_y;

        [Range(1, 10)]
        public int p;

        public GameObject signPrefab_r;
        public GameObject signPrefab_b;
        public GameObject signPrefab_y;

        private void Awake()
        {
            st = GameObject.Find("Table/Addition/Position/st_table").transform;
            en = GameObject.Find("Table/Addition/Position/en_table").transform;
            Transform parent = GameObject.Find("Table/Addition/Item").transform;
            float w = en.position.x - st.position.x;
            float h = en.position.z - st.position.z;
            float d = w / 2 * 1 / p;

            GameObject g_y = Instantiate(signPrefab_y, parent);
            signObjs_y.Add(g_y);


            for (int i = 0; i < 40; i++)
            {
                GameObject g_r = Instantiate(signPrefab_r, parent);
                GameObject g_b = Instantiate(signPrefab_b, parent);
                g_r.transform.position = new Vector3(0, -1, 0);
                g_b.transform.position = new Vector3(0, -1, 0);
                signObjs_r.Add(g_r);
                signObjs_b.Add(g_b);
            }
        }

        private void Update()
        {
            //if (Input.GetKeyDown(KeyCode.Alpha1))
            //{
            //    SetStuffPosWide();
            //}

            //if (Input.GetKeyDown(KeyCode.Alpha2))
            //{
            //    SetRandPos4();
            //}
            //if (Input.GetKeyDown(KeyCode.Alpha3))
            //{
            //    SetRandPos2H();
            //}
            //if (Input.GetKeyDown(KeyCode.Alpha4))
            //{
            //    SetRandPos2V();
            //}
        }


        public void SetRandPos4()
        {
            if(rect4ListPos.Count == 0)
            {
                SetStuffPosWide();
            }

            float x = UnityEngine.Random.Range(st.position.x, en.position.x);
            float z = UnityEngine.Random.Range(en.position.z, st.position.z);
            //Debug.Log($"SetRandPos x:{x}, z:{z}");
            Vector3 pos = new Vector3(x, 0, z);

            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;
            float dist_w = dist * 1.0f;
            float dist_h = dist * 0.5f;
            Table_Rect_4 table_rect = Table_Rect_4.UpLeft;
            signObjs_y[0].transform.position = pos;
            int s = -1;
            for (int i = 0; i < rect4ListPos.Count; i++)
            {
                if(rect4ListPos[i].x - dist_w < pos.x && pos.x < rect4ListPos[i].x + dist_w && 
                   rect4ListPos[i].z - dist_h < pos.z && pos.z < rect4ListPos[i].z + dist_h)
                {
                    //signObjs_b[0].transform.position = stuffListPos[i];
                    s = i;
                    break;
                }
            }

            table_rect = GetTableRect4(s);
            int rnd = s;
            while(true)
            {
                rnd = UnityEngine.Random.Range(0, 4);
                if(rnd != s) break;
            }
            signObjs_b[0].transform.position = rect4ListPos[rnd];

        }


        public void SetRandPos2H()
        {
            if (rect4ListPos.Count == 0)
            {
                SetStuffPosWide();
            }

            rect2HListPos.Clear();


            float x = UnityEngine.Random.Range(st.position.x, en.position.x);
            float z = UnityEngine.Random.Range(en.position.z, st.position.z);
            //Debug.Log($"SetRandPos x:{x}, z:{z}");
            Vector3 pos = new Vector3(x, 0, z);

            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;
            float dist_w = dist * 1.0f;
            float dist_h = dist * 0.5f;
            Table_Rect_4 table_rect = Table_Rect_4.UpLeft;
            signObjs_y[0].transform.position = pos;

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
            signObjs_b[0].transform.position = rect2HListPos[rnd];

        }

        public void SetRandPos2V()
        {
            if (rect4ListPos.Count == 0)
            {
                SetStuffPosWide();
            }

            rect2VListPos.Clear();


            float x = UnityEngine.Random.Range(st.position.x, en.position.x);
            float z = UnityEngine.Random.Range(en.position.z, st.position.z);
            //Debug.Log($"SetRandPos x:{x}, z:{z}");
            Vector3 pos = new Vector3(x, 0, z);

            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;
            float dist_w = dist * 1.0f;
            float dist_h = dist * 0.5f;
            Table_Rect_4 table_rect = Table_Rect_4.UpLeft;
            signObjs_y[0].transform.position = pos;
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
            signObjs_b[0].transform.position = rect2VListPos[rnd];


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
                case 0: table_rect = Table_Rect_4.UpLeft;break;
                case 1: table_rect = Table_Rect_4.DownLeft;break;
                case 2: table_rect = Table_Rect_4.UpRight;break;
                case 3: table_rect = Table_Rect_4.DownRight; break;
            }
            return table_rect;
        }



        public void SetStuffPosWide()
        {
            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;

            rect2HListPos.Clear();
            rect2VListPos.Clear();
            rect4ListPos.Clear();

            for (int w = 0; w < p * 2; w++)
            {
                float _x = st.position.x + w * dist * 2;
                for (int h = 0; h < p; h++)
                {
                    float _z = st.position.z - h * dist;
                    float dist_c = dist * .5f;
                    Vector3 cen = new Vector3(_x + (dist_c * 2), 0, _z - dist_c);
                    rect4ListPos.Add(cen);
                    //Debug.Log($"w : {w}, h : {h}, cen : {cen}");
                }
            }

            float x0, z0;
            signObjs_r.ForEach(o => o.transform.position = new Vector3(0, -1, 0));
            signObjs_b.ForEach(o => o.transform.position = new Vector3(0, -1, 0));




            // 8 - 4x 

        }




        public void SetStuffPos()
        {
            float height = en.position.z - st.position.z;
            float dist = Mathf.Abs(height) / p;

            rect4ListPos.Clear();

            for (int w = 0; w < 2 * p; w++)
            {
                float _x = st.position.x + w * dist;
                for (int h = 0; h < p; h++)
                {
                    float _z = st.position.z - h * dist;
                    float dist_c = dist * .5f;
                    Vector3 cen = new Vector3(_x + dist_c, 0, _z - dist_c);
                    rect4ListPos.Add(cen);
                    //Debug.Log($"w : {w}, h : {h}, cen : {cen}");
                }
            }

            signObjs_r.ForEach(o => o.transform.position = new Vector3(0, -1, 0));
            signObjs_b.ForEach(o => o.transform.position = new Vector3(0, -1, 0));

            for (int i = 0; i < rect4ListPos.Count; i++)
            {
                signObjs_r[i].transform.position = rect4ListPos[i];

                //if (i % 2  == 0)
                //{
                //    signObjs_r[i].transform.position = stuffListPos[i];
                //}
                //else
                //{
                //    signObjs_b[i].transform.position = stuffListPos[i];
                //}
            }

        }


    }
}
