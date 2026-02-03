using UnityEngine;
using System.Collections.Generic;
using MergeDrop.Data;

namespace MergeDrop.Core
{
    public class ObjectSpawner : MonoBehaviour
    {
        public static ObjectSpawner Instance { get; private set; }

        private readonly Queue<MergeableObject> pool = new Queue<MergeableObject>();
        private readonly List<MergeableObject> activeObjects = new List<MergeableObject>();

        private const int InitialPoolSize = 30;
        private const int MaxPoolSize = 60;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // 풀 초기화
            for (int i = 0; i < InitialPoolSize; i++)
            {
                var obj = CreateNewObject();
                obj.gameObject.SetActive(false);
                pool.Enqueue(obj);
            }
        }

        public MergeableObject SpawnObject(int level, float yPos, bool isDropping)
        {
            MergeableObject obj;

            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
                obj.gameObject.SetActive(true);
            }
            else
            {
                obj = CreateNewObject();
            }

            obj.transform.position = new Vector3(0f, yPos, 0f);
            obj.gameObject.name = $"Mergeable_Lv{level}_{GameConfig.GetName(level)}";
            obj.Initialize(level, isDropping);

            // F7: 3% chance to be golden when dropping
            if (isDropping && Random.value < 0.03f)
            {
                var golden = obj.GetComponent<GoldenObject>();
                if (golden != null)
                {
                    golden.MakeGolden();
                    obj.SetGoldenObject(golden);
                }
            }

            activeObjects.Add(obj);
            return obj;
        }

        public void ReturnToPool(MergeableObject obj)
        {
            if (obj == null) return;

            activeObjects.Remove(obj);
            obj.ResetObject();
            obj.gameObject.SetActive(false);

            if (pool.Count < MaxPoolSize)
                pool.Enqueue(obj);
            else
                Destroy(obj.gameObject);
        }

        public void ClearAllActive()
        {
            for (int i = activeObjects.Count - 1; i >= 0; i--)
            {
                if (activeObjects[i] != null)
                {
                    activeObjects[i].ResetObject();
                    activeObjects[i].gameObject.SetActive(false);
                    if (pool.Count < MaxPoolSize)
                        pool.Enqueue(activeObjects[i]);
                    else
                        Destroy(activeObjects[i].gameObject);
                }
            }
            activeObjects.Clear();
        }

        public void RemoveTopObjects(float aboveY)
        {
            for (int i = activeObjects.Count - 1; i >= 0; i--)
            {
                if (activeObjects[i] != null && activeObjects[i].transform.position.y > aboveY)
                {
                    ReturnToPool(activeObjects[i]);
                }
            }
        }

        public MergeableObject SpawnStone(float x, float y, float size)
        {
            MergeableObject obj;
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
                obj.gameObject.SetActive(true);
            }
            else
            {
                obj = CreateNewObject();
            }

            obj.transform.position = new Vector3(x, y, 0f);
            obj.gameObject.name = "Stone";
            obj.InitializeAsStone(size);
            activeObjects.Add(obj);
            return obj;
        }

        public List<MergeableObject> GetActiveObjects()
        {
            // 제거된 오브젝트 정리
            activeObjects.RemoveAll(o => o == null || !o.gameObject.activeInHierarchy);
            return activeObjects;
        }

        private MergeableObject CreateNewObject()
        {
            var go = new GameObject("Mergeable_Pooled");
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<CircleCollider2D>();
            var obj = go.AddComponent<MergeableObject>();
            // F7: Add GoldenObject component (inactive by default)
            go.AddComponent<GoldenObject>();
            return obj;
        }
    }
}
