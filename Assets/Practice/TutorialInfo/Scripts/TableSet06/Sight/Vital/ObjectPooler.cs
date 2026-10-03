using System.Collections.Generic;
using UnityEngine;
using System;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler instance;

        [Serializable]
        public class Pool
        {
            public string tag;
            public GameObject prefab;
            public int size;
        }

        [SerializeField] Pool[] pools;
        List<GameObject> spawnObjects;
        readonly HashSet<GameObject> queued = new HashSet<GameObject>();
        readonly Dictionary<GameObject, string> owned = new Dictionary<GameObject, string>();
        readonly Dictionary<string, Pool> definitions = new Dictionary<string, Pool>();
        Transform staging;
        bool closing;
        public Dictionary<string, Queue<GameObject>> poolDictionary;
        [SerializeField] Transform parent;
        readonly string INFO = " 오브젝트에 다음을 적으세요 \nvoid OnDisable()\n{\n" +
            "    ObjectPooler.ReturnToPool(gameObject);    // 한 객체에 한번만 \n" +
            "    CancelInvoke();    // Monobehaviour에 Invoke가 있다면 \n}";


        GameObject CreateNewObject(string tag, GameObject prefab)
        {
            // Instantiate inactive so OnEnable never runs before ownership/setup.
            var obj = Instantiate(prefab, staging);
            obj.name = tag;
            obj.SetActive(false);
            obj.transform.SetParent(parent, false);
            owned.Add(obj, tag);
            ReturnToPool(obj);
            return obj;
        }
        void ArrangePool(GameObject obj) => spawnObjects.Add(obj);
        public GameObject TrySpawnVisual(string tag, Vector3 position, int maxActive = 32)
        {
            int active = 0;
            foreach (var obj in spawnObjects)
                if (obj && obj.name == tag && obj.activeSelf) active++;
            return active >= maxActive ? null : SpawnFromPool(tag, position);
        }

        GameObject _SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {

            if (!poolDictionary.ContainsKey(tag))
                throw new Exception($"Pool with tag {tag} doesn't exist.");



            // 큐에 없으면 새로 추가
            Queue<GameObject> poolQueue = poolDictionary[tag];
            if (poolQueue.Count <= 0)
            {
                Pool pool = definitions[tag];
                var obj = CreateNewObject(pool.tag, pool.prefab);
                ArrangePool(obj);
            }

            // 큐에서 꺼내서 사용
            GameObject objectToSpawn = poolQueue.Dequeue();
            queued.Remove(objectToSpawn);
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;
            objectToSpawn.SetActive(true);

            return objectToSpawn;
        }


        public GameObject SpawnFromPool(string tag, Vector3 position) => _SpawnFromPool(tag, position, Quaternion.identity);
        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation) => _SpawnFromPool(tag, position, rotation);
        public T SpawnFromPool<T>(string tag, Vector3 position) where T : Component
        {
            GameObject obj = _SpawnFromPool(tag, position, Quaternion.identity);
            if (obj.TryGetComponent(out T component))
                return component;
            else
            {
                obj.SetActive(false);
                throw new Exception($"Component not found");
            }
        }
        public T SpawnFromPool<T>(string tag, Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject obj = _SpawnFromPool(tag, position, rotation);
            if (obj.TryGetComponent(out T component))
                return component;
            else
            {
                obj.SetActive(false);
                throw new Exception($"Component not found");
            }
        }
        public List<GameObject> GetAllPools(string tag)
        {
            if (!poolDictionary.ContainsKey(tag))
                throw new Exception($"Pool with tag {tag} doesn't exist.");

            List<GameObject> results = new List<GameObject>();
            for (int i = 0; i < spawnObjects.Count; i++)
            {
                GameObject obj = spawnObjects[i];
                if (obj.name == tag)
                    results.Add(obj);
            }
            return results;
        }

        public List<T> GetAllPools<T>(string tag) where T : Component
        {
            List<T> results = new List<T>();
            for (int i = 0; i < spawnObjects.Count; i++)
            {
                GameObject obj = spawnObjects[i];
                if (obj.name != tag)
                    continue;
                if (!obj.TryGetComponent(out T component))
                    throw new Exception("Component not found");
                results.Add(component);
            }
            return results.Count == 0 ? null : results;
        }

        public List<GameObject> GetActivePools(string tag, bool isActive = true)
        {
            List<GameObject> results = new List<GameObject>();
            for (int i = 0; i < spawnObjects.Count; i++)
            {
                GameObject obj = spawnObjects[i];
                if (obj.name.IndexOf(tag, StringComparison.Ordinal) >= 0 && obj.activeSelf == isActive)
                    results.Add(obj);
            }
            return results;
        }


        public List<T> GetActivePools<T>(string tag, bool isActive = true)
        {
            List<T> results = new List<T>();
            for (int i = 0; i < spawnObjects.Count; i++)
            {
                GameObject obj = spawnObjects[i];
                if (obj.name.IndexOf(tag, StringComparison.Ordinal) < 0 || obj.activeSelf != isActive)
                    continue;
                if (obj.TryGetComponent(out T component))
                    results.Add(component);
            }
            return results;
        }

        public void SetAllInactivePool()
        {
            for (int i = 0; i < spawnObjects.Count; i++)
            {
                if (spawnObjects[i].activeSelf)
                    spawnObjects[i].SetActive(false);
            }
        }


        public void ReturnToPool(GameObject obj)
        {
            if (closing || !obj || obj.activeSelf || !owned.TryGetValue(obj, out var tag) || !queued.Add(obj)) return;
            poolDictionary[tag].Enqueue(obj);
        }
        void OnDestroy() { closing = true; if (instance == this) instance = null; }

        [ContextMenu("GetSpawnObjectsInfo")]
        void GetSpawnObjectsInfo()
        {
            foreach (var pool in pools)
            {
                int count = spawnObjects.FindAll(x => x.name == pool.tag).Count;
            }
        }


        private void Awake()
        {
            if (parent == null)
                parent = GameObject.Find("Table/Stuff/Item").transform;
            instance = this;
            staging = new GameObject("Pool staging").transform;
            staging.SetParent(transform, false);
            staging.gameObject.SetActive(false);
            spawnObjects = new List<GameObject>();
            poolDictionary = new Dictionary<string, Queue<GameObject>>();

            foreach (Pool pool in pools)
            {

                poolDictionary.Add(pool.tag, new Queue<GameObject>());
                definitions.Add(pool.tag, pool);

                for (int i = 0; i < pool.size; i++)
                {
                    var obj = CreateNewObject(pool.tag, pool.prefab);
                    ArrangePool(obj);
                }

            }



        }

    }
}
