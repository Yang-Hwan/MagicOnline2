using System.Collections.Generic;
using UnityEngine;
using System;
using Assets.Scripts.Often;

namespace Assets.Scripts.Sight.Vital.Pavilion
{
    public class ObjectPooler : MonoBehaviour
    {


        public static ObjectPooler instance;

        [Serializable]
        public class Pool
        {
            public ObjectPool tag;
            public GameObject prefab;
            public int size;
        }

        [SerializeField] Pool[] pools;
        List<GameObject> spawnObjects;
        public Dictionary<ObjectPool, Queue<GameObject>> poolDictionary;
        Transform _parent;
        [SerializeField] Transform parent;
        //{
        //    get
        //    {
        //        if(_parent == null)
        //            _parent = GameObject.Find("Table/Addition/Item").transform;
        //        return _parent;
        //    }
        //}

        readonly string INFO = " 오브젝트에 다음을 적으세요 \nvoid OnDisable()\n{\n" +
    "    ObjectPooler.ReturnToPool(gameObject);    // 한 객체에 한번만 \n" +
    "    CancelInvoke();    // Monobehaviour에 Invoke가 있다면 \n}";

        GameObject CreateNewObject(string tag, GameObject prefab)
        {
            var obj = Instantiate(prefab, parent);
            obj.name = tag;
            obj.SetActive(false);
            return obj;
        }


        void ArrangePool(GameObject obj)
        {
            bool isFind = false;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (i == parent.childCount - 1)
                {
                    obj.transform.SetSiblingIndex(i);
                    spawnObjects.Insert(i, obj);
                    break;
                }
                else if (parent.GetChild(i).name == obj.name)
                {
                    isFind = true;
                }
                else if (isFind)
                {
                    obj.transform.SetSiblingIndex(i);
                    spawnObjects.Insert(i, obj);
                    break;
                }
            }
        }


        GameObject _SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            ObjectPool tag_op = (ObjectPool)Enum.Parse(typeof(ObjectPool), tag);
            if (!poolDictionary.ContainsKey(tag_op))
                throw new Exception($"Pool with tag {tag} doesn't exist.");



            // 큐에 없으면 새로 추가
            Queue<GameObject> poolQueue = poolDictionary[tag_op];
            if (poolQueue.Count <= 0)
            {
                Pool pool = Array.Find(pools, x => x.tag == tag_op);
                var obj = CreateNewObject(pool.tag.ToString(), pool.prefab);
                ArrangePool(obj);
            }

            // 큐에서 꺼내서 사용
            GameObject objectToSpawn = poolQueue.Dequeue();
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
        public List<GameObject> GetAllPools(ObjectPool tag)
        {
            if (!poolDictionary.ContainsKey(tag))
                throw new Exception($"Pool with tag {tag} doesn't exist.");

            return spawnObjects.FindAll(x => x.name == nameof(tag));
        }

        public List<T> GetAllPools<T>(ObjectPool tag) where T : Component
        {
            List<GameObject> objects = GetAllPools(tag);
            if (objects.Count == 0)
                return null;
            if (!objects[0].TryGetComponent(out T component))
                throw new Exception("Component not found");
            return objects.ConvertAll(x => x.GetComponent<T>());
        }

        public List<GameObject> GetActivePools(string tag, bool isActive = true)
        {
            List<GameObject> objects = spawnObjects.FindAll(x => x != null && x.name.IndexOf(tag) != -1 && x.activeSelf == isActive);
            return objects;
        }


        public List<T> GetActivePools<T>(string tag, bool isActive = true)
        {
            List<GameObject> objects = GetActivePools(tag, isActive);
            return objects.ConvertAll(x => x.GetComponent<T>());
        }

        public void SetAllInactivePool()
        {
            spawnObjects.ForEach(x => { if (x.activeSelf) x.SetActive(false); });
        }


        public void ReturnToPool(GameObject obj)
        {
            ObjectPool op = (ObjectPool)Enum.Parse(typeof(ObjectPool), obj.name);
            if (!poolDictionary.ContainsKey(op ))
                throw new Exception($"Pool with tag {obj.name} doesn't exist.");

            poolDictionary[op].Enqueue(obj);

        }

        [ContextMenu("GetSpawnObjectsInfo")]
        void GetSpawnObjectsInfo()
        {
            foreach (var pool in pools)
            {
                int count = spawnObjects.FindAll(x => x.name == nameof(pool.tag)).Count;
            }
        }


        private void Awake()
        {

            parent = GameObject.Find("Table/Addition/Item").transform;
            instance = this;
            spawnObjects = new List<GameObject>();
            poolDictionary = new Dictionary<ObjectPool, Queue<GameObject>>();

            foreach (Pool pool in pools)
            {

                poolDictionary.Add(pool.tag, new Queue<GameObject>());

                for (int i = 0; i < pool.size; i++)
                {
                    var obj = CreateNewObject(pool.tag.ToString(), pool.prefab);
                    ArrangePool(obj);
                }

            }

            Debug.Log($"{ this.GetType().Name } ===== ");


        }



    }
}
