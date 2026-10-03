using Assets.Scripts.Sight.Vital.Pavilion;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Often
{

    public delegate void gameStateChangedHandle(string state);

    public static class DataExtansion
    {
        public static string ToString4(this float value)
        {
            return ((int)(value * 10000f)).ToString();
        }

        public static float ToFloat4(this string value)
        {
            return ((float)int.Parse(value)) / 10000f;
        }
    }

    public class DataManager : MonoBehaviour
    {

        private static DataManager instance;
        private static bool gameDataSaved = false;

        private void Awake()
        {
            if (instance)
            {
                Destroy(instance);
            }
            else
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            //if (!gameDataSaved && instance == this)
            //{
            //    SaveGameData();
            //}
        }

        private void OnApplicationQuit()
        {
            //if (!gameDataSaved)
            //{
            //    SaveGameData();
            //}
        }



        public class Data
        {
            public int typeId { get; private set; }
            public string value { get; private set; }
            public Data(int typeId, string value)
            {
                this.typeId = typeId;
                this.value = value;
            }
        }
        private static Dictionary<string, Data> gameStateDictionary;
        private static Dictionary<string, Data> gameDataDictionary;

        public static string gameState;
        private static string _gameState;
        public static event gameStateChangedHandle OngameStateChanged;

        private static void SavegameState()
        {
            if (OngameStateChanged != null)
            {
                OngameStateChanged(gameState);
            }
            PlayerPrefs.Save();
        }

        public static void SaveGameData(int matchIdx = -1)
        {
            gameDataSaved = true;

            //SaveMatchData();
            //SaveRawData(matchIdx);
            DownloadManager.SaveGameDataToDocumends(gameDataDictionary);
        }



        private static void LoadGameData()
        {
            gameDataDictionary = DownloadManager.LoadGameDataFromDocumends();
        }

        private static void LoadGameData(int matchIdx = -1)
        {
            int m;
            Dictionary<string, Data> dic = DownloadManager.LoadGameDataFromDocumends();
            gameDataDictionary = new Dictionary<string, Data>();

            if (matchIdx != -1)
            {
                foreach (KeyValuePair<string, Data> d in dic)
                {
                    string[] sp = d.Key.Split('_');
                    if (sp.Length > 1 && int.TryParse(sp[1], out m) && m == matchIdx)
                    {
                        gameDataDictionary.Add(d.Key, d.Value);
                    }
                }
            }
            else
            {
                gameDataDictionary = DownloadManager.LoadGameDataFromDocumends();
            }
        }


        public static int GetGameDataCnt(string chkKey)
        {
            int ret = 0;
            if (gameDataDictionary == null)
            {
                LoadGameData();
            }
            foreach (KeyValuePair<string, Data> item in gameDataDictionary)
            {
                if (item.Key.IndexOf(chkKey) != -1)
                {
                    ret++;
                }
            }
            return ret;
        }




        public static void EmptyGameData(int matchIdx = -1)
        {
            if (gameDataDictionary == null)
            {
                LoadGameData();
            }
            string exceptKey = "ReplayInfo";
            DownloadManager.EmptyGameDataToDocumends(gameDataDictionary, matchIdx, exceptKey);
        }

        public static void DropGameData(int matchIdx = -1)
        {
            if (gameDataDictionary == null)
            {
                LoadGameData();
            }
            string exceptKey = "ReplayInfo";
            DownloadManager.DropGameDataToDocumends(gameDataDictionary, matchIdx, exceptKey);
        }

        private static string GetFromGameData(int typeId, string key)
        {
            if (gameDataDictionary == null)
            {
                LoadGameData();
            }
            if (gameDataDictionary == null)
            {
                return "";
            }
            foreach (KeyValuePair<string, Data> item in gameDataDictionary)
            {
                if (item.Key == key && item.Value.typeId == typeId)
                {
                    return gameDataDictionary[key].value;
                }
            }
            return "";
        }

        private static void AddToGameData(int typeId, string key, string value)
        {
            if (gameDataDictionary == null)
            {
                gameDataDictionary = new Dictionary<string, Data>();
            }
            if (gameDataDictionary.ContainsKey(key))
            {
                gameDataDictionary[key] = new Data(typeId, value);
            }
            else
            {
                gameDataDictionary.Add(key, new Data(typeId, value));
            }
        }

        private static bool AddToGameState(int typeId, string key, string value)
        {
            if (value.Contains("[") || value.Contains("]") || value.Contains(";") || value.Contains("{") || value.Contains("}"))
            {
                Debug.LogError(value + ": the value can not have characters such as [ ; { ");
                return false;
            }

            if (gameStateDictionary == null)
            {
                gameStateDictionary = new Dictionary<string, Data>();
            }
            if (gameStateDictionary.ContainsKey(key))
            {
                gameStateDictionary[key] = new Data(typeId, value);
            }
            else
            {
                gameStateDictionary.Add(key, new Data(typeId, value));
            }

            _gameState = "";
            foreach (KeyValuePair<string, Data> item in gameStateDictionary)
            {
                _gameState += "[" + item.Value.typeId + ";" + item.Key + ";" + item.Value.value + "]";
            }

            if (gameState != _gameState)
            {
                gameState = _gameState;
                SavegameState();
            }
            return true;
        }


        #region Key-Vakue using
        public static void SetInt(string key, int value)
        {
            if (AddToGameState(1, key, value.ToString()))
            {
                PlayerPrefs.SetInt(key, value);
            }
        }

        public static int GetInt(string key)
        {
            return PlayerPrefs.GetInt(key);
        }

        public static void SetFloat(string key, float value)
        {
            if (AddToGameState(2, key, value.ToString()))
            {
                PlayerPrefs.SetFloat(key, value);
            }
        }

        public static float GetFloat(string key)
        {
            return PlayerPrefs.GetFloat(key);
        }

        public static void SetString(string key, string value)
        {
            if (AddToGameState(3, key, value))
            {
                PlayerPrefs.SetString(key, value);
            }
        }

        public static string GetString(string key)
        {
            return PlayerPrefs.GetString(key);
        }
        public static void DeleteKey(string key)
        {
            if (gameStateDictionary.ContainsKey(key))
            {
                gameStateDictionary.Remove(key);
            }
            PlayerPrefs.DeleteKey(key);
        }
        #endregion

        #region Data using
        public static void SetIntOldData(string key, int value)
        {
            AddToGameData(1, key, value.ToString());
        }

        public static int GetIntOldData(string key)
        {
            int value = 0;
            if (int.TryParse(GetFromGameData(1, key), out value))
            {
                return value;
            }
            return value;
        }

        public static void SetFloatOldData(string key, float value)
        {
            AddToGameData(2, key, value.ToString());
        }

        public static float GetFloatOldData(string key)
        {
            float value = 0.0f;
            if (float.TryParse(GetFromGameData(2, key), out value))
            {
                return value;
            }
            return value;
        }

        public static void SetStringOldData(string key, string value)
        {
            AddToGameData(3, key, value.ToString());
        }

        public static string GetStringOldData(string key)
        {
            return GetFromGameData(3, key);
        }

        public static void SetColorData(string key, Color value)
        {
            SetStringOldData(key, ColorToString(value));
        }

        public static bool GetColorData(string key, out Color value)
        {
            string colorStr = GetStringOldData(key);

            if (!string.IsNullOrEmpty(colorStr))
            {
                value = ColorFromString(colorStr);
                return true;
            }
            else
            {
                value = new Color();
                return false;
            }
        }
        public static void DeleteKeyData(string key)
        {
            if (gameDataDictionary.ContainsKey(key))
            {
                gameDataDictionary.Remove(key);
            }
        }
        #endregion


        public static float CutValue(float value, int count = 3)
        {
            return value;
            //            float pow = Mathf.Pow(10.0f, (float)count);
            //            return  (float)((int)(pow * value)) / pow;
        }

        public static string Vec3To_Str(Vector3 v)
        {
            float x = CutValue(v.x);
            float y = CutValue(v.y);
            float z = CutValue(v.z);
            if (x == 0.0f && y == 0.0f && z == 0.0f)
            {
                return "z";
            }
            else if ((x == 1.0f && y == 0.0f && z == 0.0f))
            {
                return "r";
            }
            else if ((x == 0.0f && y == 1.0f && z == 0.0f))
            {
                return "u";
            }
            else if ((x == 0.0f && y == 0.0f && z == 1.0f))
            {
                return "f";
            }
            else if ((x == -1.0f && y == 0.0f && z == 0.0f))
            {
                return "l";
            }
            else if ((x == 0.0f && y == -1.0f && z == 0.0f))
            {
                return "d";
            }
            else if ((x == 0.0f && y == 0.0f && z == -1.0f))
            {
                return "b";
            }
            else if ((x == 1.0f && y == 1.0f && z == 1.0f))
            {
                return "o";
            }
            else
            {
                return x.ToString4() + "_" + y.ToString4() + "_" + z.ToString4();
            }
        }

        public static Vector3 Vec3From_Str(string s)
        {
            if (s == "" || s == "z")
            {
                return Vector3.zero;
            }
            else if (s == "r")
            {
                return Vector3.right;
            }
            else if (s == "u")
            {
                return Vector3.up;
            }
            else if (s == "f")
            {
                return Vector3.forward;
            }
            else if (s == "l")
            {
                return Vector3.left;
            }
            else if (s == "d")
            {
                return Vector3.down;
            }
            else if (s == "b")
            {
                return Vector3.back;
            }
            else if (s == "o")
            {
                return Vector3.one;
            }
            string strX = "";
            string strY = "";
            string strZ = "";

            int step = 1;
            foreach (char c in s)
            {
                if (c == '_')
                {
                    step++;
                    continue;
                }
                else if (c == '(' || c == ' ' || c == ')')
                {
                    continue;
                }
                if (step == 1)
                {
                    strX += c.ToString();
                }
                else if (step == 2)
                {
                    strY += c.ToString();
                }
                else if (step == 3)
                {
                    strZ += c.ToString();
                }
            }
            float x = strX.ToFloat4();
            float y = strY.ToFloat4();
            float z = strZ.ToFloat4();

            return new Vector3(x, y, z);
        }


        public static string Vector3ToString(Vector3 v)
        {
            float x = CutValue(v.x);
            float y = CutValue(v.y);
            float z = CutValue(v.z);
            if (x == 0.0f && y == 0.0f && z == 0.0f)
            {
                return "z";
            }
            else if ((x == 1.0f && y == 0.0f && z == 0.0f))
            {
                return "r";
            }
            else if ((x == 0.0f && y == 1.0f && z == 0.0f))
            {
                return "u";
            }
            else if ((x == 0.0f && y == 0.0f && z == 1.0f))
            {
                return "f";
            }
            else if ((x == -1.0f && y == 0.0f && z == 0.0f))
            {
                return "l";
            }
            else if ((x == 0.0f && y == -1.0f && z == 0.0f))
            {
                return "d";
            }
            else if ((x == 0.0f && y == 0.0f && z == -1.0f))
            {
                return "b";
            }
            else if ((x == 1.0f && y == 1.0f && z == 1.0f))
            {
                return "o";
            }
            else
            {
                return "(" + x.ToString4() + ", " + y.ToString4() + ", " + z.ToString4() + ")";
            }
        }


        public static Vector3 Vector3FromString(string s)
        {
            if (s == "" || s == "z")
            {
                return Vector3.zero;
            }
            else if (s == "r")
            {
                return Vector3.right;
            }
            else if (s == "u")
            {
                return Vector3.up;
            }
            else if (s == "f")
            {
                return Vector3.forward;
            }
            else if (s == "l")
            {
                return Vector3.left;
            }
            else if (s == "d")
            {
                return Vector3.down;
            }
            else if (s == "b")
            {
                return Vector3.back;
            }
            else if (s == "o")
            {
                return Vector3.one;
            }
            string strX = "";
            string strY = "";
            string strZ = "";

            int step = 1;
            foreach (char c in s)
            {
                if (c == ',')
                {
                    step++;
                    continue;
                }
                else if (c == '(' || c == ' ' || c == ')')
                {
                    continue;
                }
                if (step == 1)
                {
                    strX += c.ToString();
                }
                else if (step == 2)
                {
                    strY += c.ToString();
                }
                else if (step == 3)
                {
                    strZ += c.ToString();
                }
            }
            float x = strX.ToFloat4();
            float y = strY.ToFloat4();
            float z = strZ.ToFloat4();


            return new Vector3(x, y, z);
        }


        public static Color ColorFromString(string data)
        {
            string[] list = ConvertDataToStringArray(data);
            float r = list[0].ToFloat4();
            float g = list[1].ToFloat4();
            float b = list[2].ToFloat4();
            float a = list[3].ToFloat4();
            return new Color(r, g, b, a);
        }


        public static string ColorToString(Color value)
        {
            return "[" + value.r.ToString4() + ";" + value.g.ToString4() + ";" + value.b.ToString4() + ";" + value.a.ToString4() + "]";
        }





        public static string RandForceToString(RandForce4 randForce)
        {
            return "[" + Vector3ToString(randForce.pos1) + ";" + Vector3ToString(randForce.pos2) + ";" + Vector3ToString(randForce.pos3) + ";" + Vector3ToString(randForce.pos4) + "]";
        }

        public static RandForce4 RandForceFromString(string data)
        {
            string[] list = ConvertDataToStringArray(data);
            Vector3 pos1 = Vector3FromString(list[0]);
            Vector3 pos2 = Vector3FromString(list[1]);
            Vector3 pos3 = Vector3FromString(list[2]);
            Vector3 pos4 = Vector3FromString(list[3]);
            return new RandForce4(pos1, pos2, pos3, pos4);
        }

        public static string ImpulseToString(Impulse impulse)
        {
            return "[" + Vector3ToString(impulse.point) + ";" + Vector3ToString(impulse.impulse) + ";" + Vector3ToString(impulse.pushVec) + ";" + impulse.id + ";" + impulse.followThrough.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";" + impulse.spinPersistence.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "]";
        }

        public static Impulse ImpulseFromString(string data)
        {
            //Debug.Log($"ImpulseFromString : {data}");
            string[] list = ConvertDataToStringArray(data);
            Vector3 point = Vector3FromString(list[0]);
            Vector3 impulse = Vector3FromString(list[1]);
            Vector3 pushVec = Vector3FromString(list[2]);
            int id = int.Parse(list[3]);
            float follow = list.Length > 4 ? float.Parse(list[4], System.Globalization.CultureInfo.InvariantCulture) : -1f;
            float persistence = list.Length > 5 ? float.Parse(list[5], System.Globalization.CultureInfo.InvariantCulture) : -1f;
            return new Impulse(point, impulse, pushVec, id, follow, persistence);
        }

        /// <summary>
        /// [ value0; value2; value3;... ].
        /// </summary>
        public static string[] ConvertDataToStringArray(string data)
        {
            List<string> list = new List<string>(0);
            string value = "";
            foreach (char c in data)
            {
                if (c == ']')
                {
                    list.Add(value);
                    break;
                }
                if (c == ';')
                {
                    list.Add(value);
                    value = "";
                    continue;
                }
                if (c == '[' || c == ' ')
                {
                    continue;
                }
                value += c.ToString();
            }
            return list.ToArray();
        }

        /// <summary>
        /// [ value0; value2; value3;... ] [ value0; value2; value3;... ][ value0; value2; value3;... ].
        /// </summary>
        public static string[] ConvertArrayDataToStringArray(string data)
        {
            List<string> list = new List<string>(0);
            string value = "";
            foreach (char c in data)
            {
                if (c == ']')
                {
                    value += c;
                    list.Add(value);
                    value = "";
                    continue;
                }
                if (c == ' ')
                {
                    continue;
                }
                value += c;
            }
            return list.ToArray();
        }


        /// <summary>
        /// (value0,value2,value3,value4).
        /// </summary>
        public static string[] ConvertDataToBraceArray(string data)
        {
            List<string> list = new List<string>(0);
            string value = "";
            foreach (char c in data)
            {
                if (c == ')')
                {
                    list.Add(value);
                    break;
                }
                if (c == ',')
                {
                    list.Add(value);
                    value = "";
                    continue;
                }
                if (c == '(' || c == ' ')
                {
                    continue;
                }
                value += c.ToString();
            }
            return list.ToArray();
        }

        /// <summary>
        /// (value0,value2,value3)(value0,value2)(value0,value2,value3)
        /// </summary>
        public static string[] ConvertArrayDataToBraceArray(string data)
        {
            List<string> list = new List<string>(0);
            string value = "";
            foreach (char c in data)
            {
                if (c == ')')
                {
                    value += c;
                    list.Add(value);
                    value = "";
                    continue;
                }
                if (c == ' ')
                {
                    continue;
                }
                value += c;
            }
            return list.ToArray();
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////  

        public struct MatchData
        {
            public int typeId { get; private set; }
            public string value { get; private set; }

            public MatchData(int typeId, string value)
            {
                this.typeId = typeId;
                this.value = value;
            }
        }

        public struct RawData
        {
            public int matchIdx { get; private set; }
            public string value { get; private set; }

            public RawData(int matchIdx, string value)
            {
                this.matchIdx = matchIdx;
                this.value = value;
            }
        }


        private static Dictionary<string, MatchData> matchDic;
        private static Dictionary<string, RawData> rawDic;

        private static void AddToMatchData(int typeId, string docKey, string value)
        {
            if (matchDic == null)
            {
                matchDic = new Dictionary<string, MatchData>();
            }
            if (matchDic.ContainsKey(docKey))
            {
                matchDic[docKey] = new MatchData(typeId, value);
            }
            else
            {
                matchDic.Add(docKey, new MatchData(typeId, value));
            }
        }

        private static void AddToRawData(int rawIdx, string docKey, string value)
        {
            if (rawDic == null)
            {
                rawDic = new Dictionary<string, RawData>();
            }

            if (rawDic.ContainsKey(docKey))
            {
                rawDic[docKey] = new RawData(rawIdx, value);
            }
            else
            {
                rawDic.Add(docKey, new RawData(rawIdx, value));
            }
        }


        public static void SetIntData(string key, int value)
        {
            AddToMatchData(101, key, value.ToString());
        }

        public static int GetIntData(string key)
        {
            int value = 0;
            if (int.TryParse(GetFromMatchData(101, key), out value))
            {
                return value;
            }
            return value;
        }

        public static void SetFloatData(string key, float value)
        {
            AddToMatchData(102, key, value.ToString());
        }
        public static float GetFloatData(string key)
        {
            float value = 0f;
            if (float.TryParse(GetFromMatchData(102, key), out value))
            {
                return value;
            }
            return value;
        }


        public static void SetStringData(string key, string value, int matchIdx = -1)
        {
            if (matchIdx == -1)
            {
                //Debug.Log($"111. SetStringData key : {key}, matchIdx : {matchIdx} ");
                AddToMatchData(103, key, value);
            }
            else
            {
                //Debug.Log($"222. SetStringData key : {key}, matchIdx : {matchIdx} ");
                AddToRawData(matchIdx, key, value);
            }
        }

        public static string GetStringData(string key, int matchIdx = -1)
        {
            if (matchIdx == -1)
                return GetFromMatchData(103, key);
            else
                return GetFromRawData(matchIdx, key);
        }



        private static string GetFromMatchData(int typeId, string key)
        {
            if (matchDic == null)
            {
                LoadMatchData();
            }
            if (matchDic == null)
            {
                return "";
            }
            foreach (KeyValuePair<string, MatchData> item in matchDic)
            {
                if (item.Key == key && item.Value.typeId == typeId)
                {
                    return matchDic[key].value;
                }
            }
            return "";
        }

        private static string GetFromRawData(int matchIdx, string key)
        {
            if (rawDic == null)
            {
                LoadRawData(matchIdx);
            }
            if (rawDic == null)
            {
                return "";
            }
            foreach (KeyValuePair<string, RawData> item in rawDic)
            {
                if (item.Key == key && item.Value.matchIdx == matchIdx)
                {
                    return rawDic[key].value;
                }
            }
            return "";
        }


        private static void LoadMatchData()
        {
            matchDic = DownloadManager.LoadMatchFromDocumends(-1);
        }

        public static void SaveMatchData(int matchIdx = -1)
        {
            DownloadManager.SaveMatchToDocumends(matchDic, matchIdx);
        }

        public static void SaveRawData(int matchIdx = -1)
        {
            DownloadManager.SaveRawToDocumends(rawDic, matchIdx);
        }

        private static void LoadRawData(int matchIdx = -1)
        {
            rawDic = DownloadManager.LoadRawFromDocumends(matchIdx);
        }


    }
}
