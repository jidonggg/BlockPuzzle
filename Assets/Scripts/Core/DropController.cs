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

        // 자동 드롭 타이머
        private float holdTimer;
        public float HoldTimeNormalized => currentObject != null
            ? Mathf.Clamp01(holdTimer / GameConfig.Instance.autoDropTime) : 0f;

        // 드롭 금지 구역
        private float lastDropX = float.NaN;
        private SpriteRenderer exclusionZoneVisual;
        private bool isInExclusionZone;
        private float exclusionFlashTimer;

        // 가이드라인
        private LineRenderer guideLine;
        private SpriteRenderer timerRing;

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
            CreateExclusionZoneVisual();
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

        private void CreateExclusionZoneVisual()
        {
            var go = new GameObject("ExclusionZone");
            go.transform.SetParent(transform);
            exclusionZoneVisual = go.AddComponent<SpriteRenderer>();
            exclusionZoneVisual.sprite = CreateExclusionSprite();
            exclusionZoneVisual.sortingOrder = 2;
            exclusionZoneVisual.enabled = false;
        }

        private static Sprite exclusionSprite;
        private static Sprite CreateExclusionSprite()
        {
            if (exclusionSprite != null) return exclusionSprite;
            int w = 4, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            exclusionSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
            return exclusionSprite;
        }

        private void UpdateExclusionZoneVisual()
        {
            if (exclusionZoneVisual == null) return;
            var config = GameConfig.Instance;

            if (float.IsNaN(lastDropX) || currentObject == null)
            {
                exclusionZoneVisual.enabled = false;
                return;
            }

            exclusionZoneVisual.enabled = true;
            float zoneWidth = config.dropExclusionRadius * 2f;
            float zoneHeight = config.dropY - config.containerBottomY;
            exclusionZoneVisual.transform.position = new Vector3(
                lastDropX, (config.dropY + config.containerBottomY) / 2f, 0f);
            exclusionZoneVisual.transform.localScale = new Vector3(zoneWidth, zoneHeight, 1f);

            // 빨간색 반투명, 금지 구역 안이면 더 강하게 깜빡
            float baseAlpha = 0.08f;
            if (isInExclusionZone)
            {
                exclusionFlashTimer += Time.deltaTime * 6f;
                float pulse = Mathf.PingPong(exclusionFlashTimer, 1f);
                baseAlpha = Mathf.Lerp(0.12f, 0.25f, pulse);
            }
            else
            {
                exclusionFlashTimer = 0f;
            }

            exclusionZoneVisual.color = new Color(1f, 0.15f, 0.15f, baseAlpha);
        }

        private bool IsPositionInExclusionZone(float x)
        {
            if (float.IsNaN(lastDropX)) return false;
            return Mathf.Abs(x - lastDropX) < GameConfig.Instance.dropExclusionRadius;
        }

        public void Activate()
        {
            isActive = true;
            lastDropX = float.NaN; // 첫 드롭은 제한 없음
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
            lastDropX = float.NaN;
            if (guideLine != null)
                guideLine.enabled = false;
            if (exclusionZoneVisual != null)
                exclusionZoneVisual.enabled = false;
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

            // 자동 드롭 타이머
            holdTimer += Time.deltaTime;
            UpdateTimerVisual();
            UpdateExclusionZoneVisual();

            // 현재 위치가 금지 구역인지 체크
            if (currentObject != null)
                isInExclusionZone = IsPositionInExclusionZone(currentObject.transform.position.x);

            if (holdTimer >= GameConfig.Instance.autoDropTime)
            {
                // 시간 초과 — 금지 구역이면 랜덤 유효 위치로 이동 후 드롭
                isDragging = false;
                if (guideLine != null) guideLine.enabled = false;
                if (isInExclusionZone && currentObject != null)
                {
                    float safeX = FindSafeDropX();
                    currentObject.SetDropPosition(safeX);
                }
                TryDrop();
                return;
            }

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
                float currentX = currentObject.transform.position.x;
                UpdateGuideLine(currentX, config);

                // 금지 구역이면 가이드라인 빨간색
                bool inZone = IsPositionInExclusionZone(currentX);
                if (inZone)
                {
                    guideLine.startColor = new Color(1f, 0.2f, 0.2f, 0.6f);
                    guideLine.endColor = new Color(1f, 0.2f, 0.2f, 0.2f);
                }
                else
                {
                    guideLine.startColor = new Color(1f, 1f, 1f, 0.4f);
                    guideLine.endColor = new Color(1f, 1f, 1f, 0.1f);
                }
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

            // 금지 구역 체크 (자동 드롭은 이미 FindSafeDropX로 보정됨)
            float dropX = currentObject.transform.position.x;
            if (IsPositionInExclusionZone(dropX))
            {
                // 드롭 거부 — 에러 피드백
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayRejectSound();
                // 금지 구역 시각 강조
                exclusionFlashTimer = 0.5f;
                return;
            }

            lastDropX = dropX;
            lastDropTime = Time.time;
            currentObject.Drop();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDropSound();

            currentObject = null;
            Invoke(nameof(PrepareNext), cooldown);
        }

        private float FindSafeDropX()
        {
            var config = GameConfig.Instance;
            float minX = config.dropMinX;
            float maxX = config.dropMaxX;
            float radius = config.dropExclusionRadius;

            if (float.IsNaN(lastDropX)) return 0f;

            // 금지 구역 밖에서 랜덤 위치 선택
            // 왼쪽 영역: [minX, lastDropX - radius]
            // 오른쪽 영역: [lastDropX + radius, maxX]
            float leftMin = minX;
            float leftMax = Mathf.Max(minX, lastDropX - radius);
            float rightMin = Mathf.Min(maxX, lastDropX + radius);
            float rightMax = maxX;

            float leftRange = Mathf.Max(0, leftMax - leftMin);
            float rightRange = Mathf.Max(0, rightMax - rightMin);
            float totalRange = leftRange + rightRange;

            if (totalRange <= 0.01f)
                return lastDropX > 0 ? minX : maxX; // 극단적 경우

            float rand = UnityEngine.Random.Range(0f, totalRange);
            if (rand < leftRange)
                return UnityEngine.Random.Range(leftMin, leftMax);
            else
                return UnityEngine.Random.Range(rightMin, rightMax);
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
            holdTimer = 0f;
            OnNextObjectReady?.Invoke(QueuedNextLevel);
        }

        private void UpdateGuideLine(float x, GameConfig config)
        {
            guideLine.SetPosition(0, new Vector3(x, config.dropY, 0f));
            guideLine.SetPosition(1, new Vector3(x, config.containerBottomY, 0f));
        }

        private void UpdateTimerVisual()
        {
            if (currentObject == null) return;

            // Create timer ring if needed
            if (timerRing == null)
            {
                var go = new GameObject("TimerRing");
                go.transform.SetParent(transform);
                timerRing = go.AddComponent<SpriteRenderer>();
                timerRing.sprite = CreateTimerSprite();
                timerRing.sortingOrder = 6;
            }

            float t = HoldTimeNormalized;
            if (t < 0.01f || currentObject == null)
            {
                timerRing.enabled = false;
                return;
            }

            timerRing.enabled = true;
            timerRing.transform.position = currentObject.transform.position;

            float size = GameConfig.GetSize(NextDropLevel) + 0.3f;
            timerRing.transform.localScale = new Vector3(size, size, 1f);

            // Green → Yellow → Red
            Color col;
            if (t < 0.5f)
                col = Color.Lerp(new Color(0.3f, 1f, 0.3f, 0.4f), new Color(1f, 1f, 0.2f, 0.6f), t * 2f);
            else
                col = Color.Lerp(new Color(1f, 1f, 0.2f, 0.6f), new Color(1f, 0.2f, 0.2f, 0.9f), (t - 0.5f) * 2f);

            // Pulse when urgent
            if (t > 0.7f)
            {
                float pulse = Mathf.PingPong(Time.time * 8f, 1f);
                float scaleBoost = 1f + pulse * 0.15f;
                timerRing.transform.localScale *= scaleBoost;
                col.a = Mathf.Lerp(col.a, 1f, pulse * 0.5f);
            }

            timerRing.color = col;
        }

        private static Sprite timerSprite;
        private static Sprite CreateTimerSprite()
        {
            if (timerSprite != null) return timerSprite;
            int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;
            float outerR = center;
            float innerR = center * 0.8f;

            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float outerA = Mathf.Clamp01((outerR - dist) * 2f);
                    float innerA = Mathf.Clamp01((dist - innerR) * 2f);
                    pixels[y * res + x] = new Color(1f, 1f, 1f, outerA * innerA);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            timerSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
            return timerSprite;
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            Vector3 pos = new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z);
            return Camera.main.ScreenToWorldPoint(pos);
        }
    }
}
