using System.Collections.Generic;
using UnityEngine;
namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public class DrawStuffPos : MonoBehaviour
    {
        public Transform st, en;
        [Range(1, 10)] public int p;
        public float radius = .03f;
        public List<Vector3> stuffListPos = new List<Vector3>();
        readonly List<int> free = new List<int>();
        readonly HashSet<int> occupied = new HashSet<int>();
        PhysicsMng physicsManager;
        void Awake() { physicsManager = GetComponent<PhysicsMng>(); SetStuffPos(); }
        public void SetStuffPos()
        {
            stuffListPos.Clear();
            int divisions = Mathf.Max(1, p);
            float dist = Mathf.Abs(en.position.z - st.position.z) / divisions;
            for (int x = 0; x < 2 * divisions; x++)
            for (int z = 0; z < divisions; z++)
                stuffListPos.Add(new Vector3(st.position.x + (x + .5f) * dist, 0, st.position.z - (z + .5f) * dist));
        }
        public void StuffCreate(int len, int itemCnt = 0)
        {
            if (!physicsManager.HasItemAuthority || !physicsManager.ItemsEnabled) return;
            occupied.Clear(); free.Clear();
            foreach (var item in physicsManager.ActiveItems) occupied.Add(item.positionId);
            for (int i = 0; i < stuffListPos.Count; i++) if (!occupied.Contains(i)) free.Add(i);
            int count = Mathf.Min(Mathf.Max(0, len), free.Count);
            for (int i = 0; i < count; i++)
            {
                int selected = Random.Range(i, free.Count);
                int pos = free[selected]; free[selected] = free[i]; free[i] = pos;
                // Preserve the original one-in-three special roll per eligible slot.
                StuffType type = i < itemCnt && Random.Range(0, 3) == 0 ? (StuffType)Random.Range(1, 5) : StuffType.StuffCoin01;
                physicsManager.Items.Spawn(type, pos, 15 + 2 * Random.Range(0, 3));
            }
        }
    }
}
