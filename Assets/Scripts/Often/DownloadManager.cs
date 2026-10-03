using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;

namespace Assets.Scripts.Often
{

    public delegate void DownloadHandler(DownloadManager.DownloadParameters parameters);

    public class DownloadManager : MonoBehaviour
    {

        static string upPath = Application.persistentDataPath + "/SavedData/Match";

        public enum DownloadType
        {
            AlwaysDownload = 0,
            Update,
            DownloadOrLoadFromDisc
        }

        public class DownloadParameters
        {
            public string URL
            {
                get;
                private set;
            }
            public string name
            {
                get;
                private set;
            }
            public string localURL
            {
                get;
                set;
            }
            public DownloadType downloadType
            {
                get;
                private set;
            }
            public bool isNull
            {
                get;
                set;
            }
            public string error
            {
                get;
                set;
            }
            public bool downloading
            {
                get;
                set;
            }
            public float progress
            {
                get;
                set;
            }
            public byte[] bytes
            {
                get;
                private set;
            }

            public string text
            {
                get;
                private set;
            }


            public Texture2D texture
            {
                get;
                private set;
            }
            public AssetBundle assetBundle
            {
                get;
                private set;
            }
            public DownloadParameters(string URL, string name, DownloadType downloadType = DownloadType.DownloadOrLoadFromDisc)
            {
                this.isNull = false;
                this.error = "";
                this.URL = URL;
                this.name = name;
                this.downloadType = downloadType;
            }

            public void SetData(byte[] bytes, string text, Texture2D texture, AssetBundle assetBundle)
            {
                this.bytes = bytes;
                this.text = text;
                this.texture = texture;
                this.assetBundle = assetBundle;
            }
        }

        public static event DownloadHandler OnStartDownload;
        public static event DownloadHandler OnEndDownload;
        public static event DownloadHandler OnDownloading;

        public static void DeleteDocumendsFromPersistentStorage()
        {
            string path = Application.persistentDataPath + "/SavedData/Documends";
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }


        public static void DeleteKeyValuFromPersistentStorage()
        {
            string path = Application.persistentDataPath + "/SavedData/KeyValueStorage";

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }






        public static void EmptyGameDataToDocumends(Dictionary<string, DataManager.Data> gameDataDictionary, int idx, string exceptKey)
        {
            if (gameDataDictionary == null)
            {
                return;
            }
            int m;
            foreach (KeyValuePair<string, DataManager.Data> item in gameDataDictionary)
            {
                string[] sp = item.Key.Split('_');
                if (sp.Length == 1) continue;
                if (exceptKey == sp[0])
                {
                    continue;
                }
                if (int.TryParse(sp[1], out m) && m == idx)
                {
                    SaveString(item.Value.typeId, item.Key, "");
                }
            }
        }

        public static void DropGameDataToDocumendsTEST(Dictionary<string, DataManager.Data> gameDataDictionary, int idx, string exceptKey)
        {
            if (gameDataDictionary == null)
            {
                return;
            }
            int m;
            //string path = GetKeyValueStoragePath(1, "BallsCount_0");
            //File.Delete(path);

            foreach (KeyValuePair<string, DataManager.Data> item in gameDataDictionary)
            {
                string[] sp = item.Key.Split('_');
                if (sp.Length == 1) continue;
                if (exceptKey == sp[0])
                {
                    continue;
                }
                Debug.Log($"(sp[1] : {item.Key}, idx : {idx}");
                if (int.TryParse(sp[1], out m) && m == idx)
                {
                    DropString(item.Value.typeId, item.Key);
                    //string path = GetKeyValueStoragePath(1, item.Key);
                    //File.Delete(path);
                }
                //if (int.TryParse(sp[1], out m) && m == idx)
                //{
                //    DropString(item.Value.typeId, item.Key);
                //}
            }
        }

        public static void DropGameDataToDocumends(Dictionary<string, DataManager.Data> gameDataDictionary, int idx, string exceptKey)
        {
            if (gameDataDictionary == null)
            {
                return;
            }
            int m;
            foreach (KeyValuePair<string, DataManager.Data> item in gameDataDictionary)
            {
                string[] sp = item.Key.Split('_');
                if (sp.Length == 1) continue;
                if (exceptKey == sp[0])
                {
                    continue;
                }
                if (int.TryParse(sp[1], out m) && m == idx)
                {
                    DropString(item.Value.typeId, item.Key);
                }
            }
        }

        public static void SaveGameDataToDocumends(Dictionary<string, DataManager.Data> gameDataDictionary)
        {
            if (gameDataDictionary == null)
            {
                return;
            }
            DeleteKeyValuFromPersistentStorage();
            CheckKeyValueStorageDirectory();
            foreach (KeyValuePair<string, DataManager.Data> item in gameDataDictionary)
            {
                SaveString(item.Value.typeId, item.Key, item.Value.value);
            }
        }



        public static Dictionary<string, DataManager.Data> LoadGameDataFromDocumends()
        {
            CheckKeyValueStorageDirectory();
            Dictionary<string, DataManager.Data> gameDataDictionary = new Dictionary<string, DataManager.Data>();

            string intPath = Application.persistentDataPath + "/SavedData/KeyValueStorage/Int/";

            DirectoryInfo intDirectoryInfo = new DirectoryInfo(intPath);
            foreach (FileInfo item in intDirectoryInfo.GetFiles())
            {
                gameDataDictionary.Add(item.Name.Replace(".dat", ""), new DataManager.Data(1, File.ReadAllText(intPath + item.Name, System.Text.Encoding.UTF8)));
            }
            string floatPath = Application.persistentDataPath + "/SavedData/KeyValueStorage/Float/";
            DirectoryInfo floatDirectoryInfo = new DirectoryInfo(floatPath);
            foreach (FileInfo item in floatDirectoryInfo.GetFiles())
            {
                gameDataDictionary.Add(item.Name.Replace(".dat", ""), new DataManager.Data(2, File.ReadAllText(floatPath + item.Name, System.Text.Encoding.UTF8)));
            }
            string stringPath = Application.persistentDataPath + "/SavedData/KeyValueStorage/String/";
            DirectoryInfo stringDirectoryInfo = new DirectoryInfo(stringPath);
            foreach (FileInfo item in stringDirectoryInfo.GetFiles())
            {
                gameDataDictionary.Add(item.Name.Replace(".dat", ""), new DataManager.Data(3, File.ReadAllText(stringPath + item.Name, System.Text.Encoding.UTF8)));
            }
            return gameDataDictionary;
        }



        private static void CheckDirectory()
        {
            if (!Directory.Exists(Application.persistentDataPath + "/SavedData"))
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/SavedData");
            }
#if UNITY_IOS
            UnityEngine.iOS.Device.SetNoBackupFlag(Application.persistentDataPath + "/SavedData");
#endif
        }
        private static void CheckDocumendsDirectory()
        {
            CheckDirectory();
            if (!Directory.Exists(Application.persistentDataPath + "/SavedData/Documends"))
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/SavedData/Documends");
            }
        }
        private static void CheckKeyValueStorageDirectory()
        {
            CheckDirectory();
            if (!Directory.Exists(Application.persistentDataPath + "/SavedData/KeyValueStorage/Int"))
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/SavedData/KeyValueStorage/Int");
            }
            if (!Directory.Exists(Application.persistentDataPath + "/SavedData/KeyValueStorage/Float"))
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/SavedData/KeyValueStorage/Float");
            }
            if (!Directory.Exists(Application.persistentDataPath + "/SavedData/KeyValueStorage/String"))
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/SavedData/KeyValueStorage/String");
            }
        }

        private static string GetKeyValueStoragePath(int typeId, string key)
        {
            CheckKeyValueStorageDirectory();
            return Application.persistentDataPath + "/SavedData/KeyValueStorage/" + (typeId == 1 ? "Int" : (typeId == 2 ? "Float" : "String")).ToString() + "/" + key + ".dat";
        }
        public static string LoadString(int typeId, string key)
        {
            string path = GetKeyValueStoragePath(typeId, key);
            string txt = File.ReadAllText(path);
            return txt;
            //byte[] bytes = File.ReadAllBytes(path);
            //return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public static void SaveString(int typeId, string key, string value)
        {
            string path = GetKeyValueStoragePath(typeId, key);
            File.WriteAllText(path, value);
            //byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            //File.WriteAllBytes(path, bytes);
        }

        public static void DropString(int typeId, string key)
        {
            string path = GetKeyValueStoragePath(typeId, key);
            Debug.Log($"typeId : {typeId}, key : {key}");
            File.Delete(path);

            //using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            //{
            //    File.Delete(path);
            //}

        }

        public static IEnumerator Download(DownloadParameters parameters, bool hasAssetBundle = false)
        {
            // Debug.Log(parameters.name + "  " + hasAssetBundle);
            if (parameters.downloadType == DownloadType.AlwaysDownload)
            {
                if (OnStartDownload != null)
                {
                    OnStartDownload(parameters);
                }
                yield return null;
                WWW downloadData = new WWW(parameters.URL);
                while (!downloadData.isDone)
                {
                    if (OnDownloading != null)
                    {
                        parameters.downloading = true;
                        parameters.progress = downloadData.progress;
                        OnDownloading(parameters);
                    }
                    yield return null;
                }
                yield return downloadData;
                if (string.IsNullOrEmpty(downloadData.error))
                {
                    parameters.SetData(downloadData.bytes, downloadData.text, downloadData.texture,
                        hasAssetBundle ? downloadData.assetBundle : null);
                }
                else
                {
                    parameters.isNull = true;
                    parameters.error = downloadData.error;
                }
                if (OnEndDownload != null)
                {
                    OnEndDownload(parameters);
                }
            }
            else
            {
                CheckDocumendsDirectory();

                string path = Application.persistentDataPath + "/SavedData/Documends/" +
                          parameters.URL.Replace("https://www.", "").Replace(".com", "").Replace("/", "").Replace("?dl=0", "").
                    Replace("https:", "").Replace("-", "").Replace("_", "").Replace("?", "").Replace(".", "").Replace("=", "").Replace("&", "").Replace("%20", "").Replace(" ", "") + ".dat";

                if (Application.internetReachability != NetworkReachability.NotReachable && (!File.Exists(path) || parameters.downloadType == DownloadType.Update))
                {
                    if (OnStartDownload != null)
                    {
                        OnStartDownload(parameters);
                    }
                    yield return null;
                    WWW downloadData = new WWW(parameters.URL);
                    while (!downloadData.isDone)
                    {
                        if (OnDownloading != null)
                        {
                            parameters.downloading = true;
                            parameters.progress = downloadData.progress;
                            OnDownloading(parameters);
                        }
                        yield return null;
                    }

                    yield return downloadData;
                    if (string.IsNullOrEmpty(downloadData.error))
                    {
                        parameters.SetData(downloadData.bytes, downloadData.text, downloadData.texture,
                        hasAssetBundle ? downloadData.assetBundle : null);
                        File.WriteAllBytes(path, downloadData.bytes);
                    }
                    else
                    {
                        parameters.error = downloadData.error;
                        parameters.isNull = true;
                    }
                    if (OnEndDownload != null)
                    {
                        OnEndDownload(parameters);
                    }
                }
                else if (File.Exists(path))
                {
                    if (OnStartDownload != null)
                    {
                        OnStartDownload(parameters);
                    }
                    parameters.localURL = "file://" + path;
                    //                    byte[] bytes = File.ReadAllBytes(path);
                    //                    Texture2D image = new Texture2D(2, 2);
                    //                    if(image.LoadImage(bytes))
                    //                    {
                    //                        image.Apply();
                    //                    }
                    //                    string text = System.Text.Encoding.UTF8.GetString(bytes);
                    //                    AssetBundle assetBundle = AssetBundle.LoadFromMemory(bytes);
                    //                    parameters.SetData(bytes, text, image, assetBundle);


                    WWW loadData = new WWW(parameters.localURL);
                    while (!loadData.isDone)
                    {
                        if (OnDownloading != null)
                        {
                            parameters.downloading = false;
                            parameters.progress = loadData.progress;
                            OnDownloading(parameters);
                        }
                        yield return null;
                    }
                    yield return loadData;
                    if (string.IsNullOrEmpty(loadData.error))
                    {
                        parameters.SetData(loadData.bytes, loadData.text, loadData.texture,
                        hasAssetBundle ? loadData.assetBundle : null);
                    }
                    else
                    {
                        parameters.isNull = true;
                        parameters.error = loadData.error;
                    }
                    if (OnEndDownload != null)
                    {
                        OnEndDownload(parameters);
                    }

                }
                else
                {
                    parameters.isNull = true;
                    parameters.error = "Some error";
                }
            }
        }




        //////////////////////////////////////////////////////////////////////////////////////////////////////////


        public static void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Debug.Log($"DeleteFolder path : {path}");
                Directory.Delete(path, true);
            }
        }

        public static void DeleteStorage(int matchIdx = -1)
        {
            if (matchIdx < 0)
            {
                DeleteFolder($"{upPath}/Int");
                DeleteFolder($"{upPath}/Float");
                DeleteFolder($"{upPath}/String");
            }
            else
            {
                DeleteFolder($"{upPath}/{matchIdx}");
            }
        }



        public static void SaveString(int typeId, string key, string value, int matchIdx = -1)
        {
            string path = GetKeyValueStoragePath(typeId, key, matchIdx);
            File.WriteAllText(path, value);
            //byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            //File.WriteAllBytes(path, bytes);
        }

        private static string GetKeyValueStoragePath(int typeId, string key, int matchIdx = -1)
        {
            CheckStorageDirectory();
            string path;
            if (matchIdx == -1)
                path = $"{upPath}/{(typeId == 101 ? "Int" : (typeId == 102 ? "Float" : "String"))}/{key}.dat";
            else
                path = $"{upPath}/{matchIdx}/{key}.dat";
            return path;
        }

        private static void CheckRootDirectory()
        {
            if (!Directory.Exists(upPath))
            {
                Directory.CreateDirectory(upPath);
            }
#if UNITY_IOS
            UnityEngine.iOS.Device.SetNoBackupFlag(upPath);
#endif
        }

        private static void CheckStorageDirectory(int matchIdx = -1)
        {
            CheckRootDirectory();
            if (!Directory.Exists(upPath + "/Int"))
            {
                Directory.CreateDirectory(upPath + "/Int");
            }
            if (!Directory.Exists(upPath + "/Float"))
            {
                Directory.CreateDirectory(upPath + "/Float");
            }
            if (!Directory.Exists(upPath + "/String"))
            {
                Directory.CreateDirectory(upPath + "/String");
            }
            if (matchIdx != -1)
            {
                if (!Directory.Exists(upPath + $"/{matchIdx}"))
                {
                    Debug.Log($"CheckStorageDirectory matchIdx : {matchIdx}");
                    Directory.CreateDirectory(upPath + $"/{matchIdx}");
                }
            }
        }

        public static string LoadString(int typeId, string key, int matchIdx = -1)
        {
            string path = GetKeyValueStoragePath(typeId, key);
            byte[] bytes = File.ReadAllBytes(path);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }


        // 매치값 없는 경우 루트정보, 매치값 있는 경우 루트정보 + 매치정보
        public static Dictionary<string, DataManager.MatchData> LoadMatchFromDocumends(int matchIdx = -1)
        {
            CheckStorageDirectory(matchIdx);

            Dictionary<string, DataManager.MatchData> gameDataDictionary = new Dictionary<string, DataManager.MatchData>();
            string intPath = $"{upPath}/Int/";
            DirectoryInfo intDirectoryInfo = new DirectoryInfo(intPath);
            foreach (FileInfo item in intDirectoryInfo.GetFiles())
            {
                string fileName = item.Name.Replace(".dat", "");
                //DocKey dk = (DocKey)Enum.Parse(typeof(DocKey), item.Name.Replace(".dat", ""));
                gameDataDictionary.Add(fileName, new DataManager.MatchData(101, File.ReadAllText(intPath + item.Name, System.Text.Encoding.UTF8)));
            }
            string floatPath = $"{upPath}/Float/";
            DirectoryInfo floatDirectoryInfo = new DirectoryInfo(floatPath);
            foreach (FileInfo item in floatDirectoryInfo.GetFiles())
            {
                string fileName = item.Name.Replace(".dat", "");
                //DocKey dk = (DocKey)Enum.Parse(typeof(DocKey), item.Name.Replace(".dat", ""));
                gameDataDictionary.Add(fileName, new DataManager.MatchData(102, File.ReadAllText(floatPath + item.Name, System.Text.Encoding.UTF8)));
            }
            string stringPath = $"{upPath}/String/";
            DirectoryInfo stringDirectoryInfo = new DirectoryInfo(stringPath);
            foreach (FileInfo item in stringDirectoryInfo.GetFiles())
            {
                string fileName = item.Name.Replace(".dat", "");
                //DocKey dk = (DocKey)Enum.Parse(typeof(DocKey), item.Name.Replace(".dat", ""));
                gameDataDictionary.Add(fileName, new DataManager.MatchData(103, File.ReadAllText(stringPath + item.Name, System.Text.Encoding.UTF8)));
            }

            return gameDataDictionary;



        }


        public static Dictionary<string, DataManager.RawData> LoadRawFromDocumends(int matchIdx = -1)
        {
            CheckStorageDirectory(matchIdx);

            Dictionary<string, DataManager.RawData> gameDataDictionary = new Dictionary<string, DataManager.RawData>();
            DirectoryInfo stringDirectoryInfoMatch = new DirectoryInfo(upPath);
            foreach (DirectoryInfo dir in stringDirectoryInfoMatch.GetDirectories())
            {

                int _matchIdx = 0;
                if (!int.TryParse(dir.Name, out _matchIdx)) continue;

                if (matchIdx == _matchIdx)
                {
                    string stringPathMatch = $"{upPath}/{matchIdx}/";
                    DirectoryInfo stringDirectoryInfoMatchView = new DirectoryInfo(stringPathMatch);
                    foreach (FileInfo item in stringDirectoryInfoMatchView.GetFiles())
                    {
                        string fileName = item.Name.Replace(".dat", "");
                        //Debug.Log($"dir.Name : {dir.Name}, fileName : {fileName}");

                        //DocKey dk = (DocKey)Enum.Parse(typeof(DocKey), item.Name.Replace(".dat", ""));
                        gameDataDictionary.Add(fileName, new DataManager.RawData(_matchIdx, File.ReadAllText(stringPathMatch + item.Name, System.Text.Encoding.UTF8)));
                    }
                }
            }
            return gameDataDictionary;
        }

        public static Dictionary<string, DataManager.MatchData> LoadMatchFromDocumends(string docKey = "")
        {
            CheckStorageDirectory();
            Dictionary<string, DataManager.MatchData> gameDataDictionary = new Dictionary<string, DataManager.MatchData>();
            string intPath = $"{upPath}/Int/";
            DirectoryInfo intDirectoryInfo = new DirectoryInfo(intPath);
            foreach (FileInfo item in intDirectoryInfo.GetFiles())
            {
                string fileName = item.Name.Replace(".dat", "");
                if (fileName.IndexOf(docKey) != -1)
                {
                    //DocKey dk = (DocKey)Enum.Parse(typeof(DocKey), fileName);
                    gameDataDictionary.Add(fileName, new DataManager.MatchData(1, File.ReadAllText(intPath + item.Name, System.Text.Encoding.UTF8)));
                }
            }
            string floatPath = $"{upPath}/Float/";
            DirectoryInfo floatDirectoryInfo = new DirectoryInfo(floatPath);
            foreach (FileInfo item in floatDirectoryInfo.GetFiles())
            {
                string fileName = item.Name.Replace(".dat", "");
                if (fileName.IndexOf(docKey) != -1)
                {
                    //DocKey dk = (DocKey)Enum.Parse(typeof(DocKey), fileName);
                    gameDataDictionary.Add(fileName, new DataManager.MatchData(2, File.ReadAllText(floatPath + item.Name, System.Text.Encoding.UTF8)));
                }
            }
            string stringPath = $"{upPath}/String/";
            DirectoryInfo stringDirectoryInfo = new DirectoryInfo(stringPath);
            foreach (FileInfo item in stringDirectoryInfo.GetFiles())
            {
                string fileName = item.Name.Replace(".dat", "");
                if (fileName.IndexOf(docKey) != -1)
                {
                    //DocKey dk = (DocKey)Enum.Parse(typeof(DocKey), fileName);
                    gameDataDictionary.Add(fileName, new DataManager.MatchData(3, File.ReadAllText(stringPath + item.Name, System.Text.Encoding.UTF8)));
                }
            }



            return gameDataDictionary;
        }


        public static void SaveMatchToDocumends(Dictionary<string, DataManager.MatchData> gameDataDictionary, int matchIdx = -1)
        {
            if (gameDataDictionary == null)
            {
                Debug.Log("gameDataDictionary == null");
                return;
            }
            //DeleteStorage(matchIdx);
            CheckStorageDirectory();        // 폴더가 없다면 생성
            foreach (KeyValuePair<string, DataManager.MatchData> item in gameDataDictionary)
            {
                SaveString(item.Value.typeId, item.Key, item.Value.value, matchIdx);
            }
        }

        public static void SaveRawToDocumends(Dictionary<string, DataManager.RawData> gameDataDictionary, int matchIdx = -1)
        {
            if (gameDataDictionary == null)
            {
                Debug.Log("gameDataDictionary == null");
                return;
            }
            DeleteStorage(matchIdx);
            CheckStorageDirectory(matchIdx);
            foreach (KeyValuePair<string, DataManager.RawData> item in gameDataDictionary)
            {
                SaveString(item.Value.matchIdx, item.Key, item.Value.value, matchIdx);
            }


        }

    }
}
