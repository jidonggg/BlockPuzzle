using UnityEngine;
using UnityEngine.InputSystem;
using System;
using MergeDrop.Data;
using MergeDrop.Audio;

namespace MergeDrop.Core
{
    public class DropController : MonoBehaviour
    {
        public static DropController Instance { get; private set; }

        public int NextDropLevel { get; private set; }
        public int QueuedNextLevel { get; private set; }

        public event Action<int> OnNextObjectReady;
        public event Action<int> OnQueuedLevelChanged;

        private MergeableObject currentObject;
        private float lastDropTime;
        private bool isDragging;
        private bool isActive;

        // 가이드라인
        private LineRenderer guideLine;

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
            CreateGuideLine();
        }

        private void CreateGuideLine()
        {
            var lineObj = new GameObject("GuideLine");
            lineObj.transform.SetParent(transform);
            guideLine = lineObj.AddComponent<LineRenderer>();
            guideLine.startWidth = 0.06f;
            guideLine.endWidth = 0.06f;

            guideLine.material = new Material(Shader.Find("Sprites/Default"));
            guideLine.startColor = new Color(1f, 1f, 1f, 0.4f);
            guideLine.endColor = new Color(1f, 1f, 1f, 0.1f);
            guideLine.positionCount = 2;
            guideLine.sortingOrder = 5;
            guideLine.enabled = false;
        }

        public void Activate()
        {
            isActive = true;
            NextDropLevel = GetDropLevel();
            QueuedNextLevel = GetDropLevel();
            OnQueuedLevelChanged?.Invoke(QueuedNextLevel);
            SpawnNextObject();
        }

        private int GetDropLevel()
        {
            if (DifficultyManager.Instance != null)
                return DifficultyManager.Instance.GetRandomDropLevel();
            return GameConfig.GetRandomDropLevel();
        }

        public void Deactivate()
        {
            isActive = false;
            isDragging = false;
            if (guideLine != null)
                guideLine.enabled = false;
            if (currentObject != null)
            {
                ObjectSpawner.Instance.ReturnToPool(currentObject);
                currentObject = null;
            }
            CancelInvoke();
        }

        // F5: Skill system can suppress input
        public bool InputSuppressed { get; set; }

        private void Update()
        {
            if (!isActive) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (currentObject == null) return;

            if (InputSuppressed) return;

            HandleInput();
        }

        public void HandleInput()
        {
            var config = GameConfig.Instance;

            // 새 Input System: Pointer (마우스 + 터치 통합)
            var pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 screenPos = pointer.position.ReadValue();
            Vector3 worldPos = ScreenToWorld(screenPos);

            if (pointer.press.wasPressedThisFrame)
            {
                isDragging = true;
                guideLine.enabled = true;
            }

            if (isDragging && currentObject != null)
            {
                currentObject.SetDropPosition(worldPos.x);
                UpdateGuideLine(currentObject.transform.position.x, config);
            }

            if (pointer.press.wasReleasedThisFrame && isDragging)
            {
                isDragging = false;
                guideLine.enabled = false;
                TryDrop();
            }
        }

        private void TryDrop()
        {
            if (currentObject == null) return;

            float cooldown = GameConfig.Instance.dropCooldown;
            // F3: Fever cooldown multiplier
            if (FeverManager.Instance != null)
                cooldown *= FeverManager.Instance.GetCooldownMultiplier();

            if (Time.time - lastDropTime < cooldown) return;

            lastDropTime = Time.time;
            currentObject.Drop();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDropSound();

            currentObject = null;
            Invoke(nameof(PrepareNext), cooldown);
        }

        private void PrepareNext()
        {
            if (!isActive || GameManager.Instance.State != GameState.Playing) return;

            NextDropLevel = QueuedNextLevel;
            QueuedNextLevel = GetDropLevel();
            OnQueuedLevelChanged?.Invoke(QueuedNextLevel);

            SpawnNextObject();
        }

        private void SpawnNextObject()
        {
            var config = GameConfig.Instance;
            currentObject = ObjectSpawner.Instance.SpawnObject(NextDropLevel, config.dropY, true);
            OnNextObjectReady?.Invoke(QueuedNextLevel);
        }

        private void UpdateGuideLine(float x, GameConfig config)
        {
            guideLine.SetPosition(0, new Vector3(x, config.dropY, 0f));
            guideLine.SetPosition(1, new Vector3(x, config.containerBottomY, 0f));
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            Vector3 pos = new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z);
            return Camera.main.ScreenToWorldPoint(pos);
        }
    }
}
