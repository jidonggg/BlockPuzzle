using UnityEngine;
using System.Collections;
using MergeDrop.Data;

namespace MergeDrop.Core
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class MergeableObject : MonoBehaviour
    {
        public int Level { get; private set; }
        public bool CanMerge { get; private set; }
        public bool IsMerging { get; set; }
        public bool IsStone { get; private set; }

        private Rigidbody2D rb;
        private CircleCollider2D circleCollider;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer innerHighlight;
        private bool isDropping;

        // F4: Squash & Stretch
        private Vector3 baseScale;
        private Coroutine squashCoroutine;

        // F1: Eyes
        private ObjectEyes eyes;

        // F7: Golden
        private GoldenObject goldenObject;

        // Level label
        private TextMesh levelLabel;

        private static Sprite sharedCircleSprite;
        private static Sprite sharedHighlightSprite;

        public void Initialize(int level, bool dropping)
        {
            Level = level;
            isDropping = dropping;
            CanMerge = false;
            IsMerging = false;

            rb = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            ApplyVisuals();
            ApplyPhysics();
            SetupEyes();
            SetupLevelLabel();

            if (dropping)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                if (eyes != null) eyes.SetState(ObjectEyes.EyeState.Dropping);
            }
        }

        private void ApplyVisuals()
        {
            if (sharedCircleSprite == null)
                sharedCircleSprite = CreateCircleSprite(256);
            if (sharedHighlightSprite == null)
                sharedHighlightSprite = CreateHighlightSprite(128);

            spriteRenderer.sprite = sharedCircleSprite;
            spriteRenderer.color = GameConfig.GetColor(Level);
            spriteRenderer.sortingOrder = 1;

            float size = GameConfig.GetSize(Level);
            baseScale = new Vector3(size, size, 1f);
            transform.localScale = baseScale;

            // 내부 하이라이트 (입체감)
            SetupHighlight();
        }

        private void SetupHighlight()
        {
            if (innerHighlight == null)
            {
                var hlObj = transform.Find("Highlight");
                if (hlObj == null)
                {
                    var go = new GameObject("Highlight");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(-0.12f, 0.12f, 0f);
                    go.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                    innerHighlight = go.AddComponent<SpriteRenderer>();
                    innerHighlight.sortingOrder = 2;
                }
                else
                {
                    innerHighlight = hlObj.GetComponent<SpriteRenderer>();
                }
            }

            innerHighlight.sprite = sharedHighlightSprite;
            innerHighlight.color = new Color(1f, 1f, 1f, 0.35f);
        }

        private void ApplyPhysics()
        {
            var config = GameConfig.Instance;

            circleCollider.radius = 0.5f;

            var mat = new PhysicsMaterial2D("MergeableMat")
            {
                friction = config.physicsFriction,
                bounciness = config.physicsBounciness
            };
            circleCollider.sharedMaterial = mat;

            rb.gravityScale = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.CurrentGravityScale
                : config.gravityScale;
            rb.linearDamping = config.linearDrag;
            rb.angularDamping = config.angularDrag;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void SetDropPosition(float x)
        {
            if (!isDropping) return;
            var config = GameConfig.Instance;
            float halfSize = GameConfig.GetSize(Level) * 0.5f;
            x = Mathf.Clamp(x, config.dropMinX + halfSize, config.dropMaxX - halfSize);
            transform.position = new Vector3(x, config.dropY, 0f);
        }

        public void Drop()
        {
            isDropping = false;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.CurrentGravityScale
                : GameConfig.Instance.gravityScale;
            rb.linearVelocity = Vector2.zero;

            if (eyes != null) eyes.SetState(ObjectEyes.EyeState.Idle);

            Invoke(nameof(EnableMerge), 0.2f);
        }

        private void EnableMerge()
        {
            CanMerge = true;
        }

        public void StartMerge()
        {
            IsMerging = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (eyes != null) eyes.SetState(ObjectEyes.EyeState.Merging);
        }

        public void UpgradeLevel(int newLevel)
        {
            Level = newLevel;
            IsMerging = false;
            ApplyVisuals();
            SetupEyes();
            SetupLevelLabel();

            circleCollider.radius = 0.5f;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.CurrentGravityScale
                : GameConfig.Instance.gravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (eyes != null) eyes.SetState(ObjectEyes.EyeState.Idle);
        }

        public void ResetObject()
        {
            Level = 0;
            CanMerge = false;
            IsMerging = false;
            isDropping = false;
            IsStone = false;
            CancelInvoke();

            // F4: Reset squash
            if (squashCoroutine != null)
            {
                StopCoroutine(squashCoroutine);
                squashCoroutine = null;
            }
            baseScale = Vector3.one;
            transform.localScale = Vector3.one;

            // Reset label
            if (levelLabel != null) levelLabel.gameObject.SetActive(false);

            // F1: Reset eyes
            if (eyes != null) eyes.ResetEyes();

            // F7: Reset golden
            ClearGolden();

            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public bool IsAboveGameOverLine()
        {
            float halfSize = GameConfig.GetSize(Level) * 0.5f;
            return transform.position.y + halfSize > GameConfig.Instance.gameOverLineY;
        }

        public bool IsSettled()
        {
            if (rb == null) return false;
            return rb.bodyType == RigidbodyType2D.Dynamic &&
                   rb.linearVelocity.magnitude < 0.5f;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // F4: Squash & Stretch
            ApplySquashStretch(collision);

            // F1: Eyes collision reaction
            if (eyes != null) eyes.SetState(ObjectEyes.EyeState.Collision);

            if (!CanMerge || IsMerging || IsStone) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            var other = collision.gameObject.GetComponent<MergeableObject>();
            if (other == null || !other.CanMerge || other.IsMerging || other.IsStone) return;
            if (other.Level != Level) return;

            if (Level >= GameConfig.MaxLevel)
            {
                if (GetInstanceID() < other.GetInstanceID())
                    MergeSystem.Instance.RequestMaxMerge(this, other);
                return;
            }

            if (GetInstanceID() < other.GetInstanceID())
                MergeSystem.Instance.RequestMerge(this, other);
        }

        // ── F4: Squash & Stretch ──
        private void ApplySquashStretch(Collision2D collision)
        {
            if (baseScale.x < 0.01f) return;

            ContactPoint2D contact = collision.GetContact(0);
            Vector2 normal = contact.normal;
            float velocity = collision.relativeVelocity.magnitude;
            float magnitude = Mathf.Clamp01(velocity / 15f) * 0.35f;

            float squashX, squashY;
            if (Mathf.Abs(normal.y) > Mathf.Abs(normal.x))
            {
                // 수직 충돌: Y 압축, X 확장
                squashX = 1f + magnitude;
                squashY = 1f - magnitude;
            }
            else
            {
                // 수평 충돌: X 압축, Y 확장
                squashX = 1f - magnitude;
                squashY = 1f + magnitude;
            }

            transform.localScale = new Vector3(baseScale.x * squashX, baseScale.y * squashY, 1f);

            if (squashCoroutine != null)
                StopCoroutine(squashCoroutine);
            squashCoroutine = StartCoroutine(RestoreScale());
        }

        private IEnumerator RestoreScale()
        {
            float duration = 0.15f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, baseScale, t);
                yield return null;
            }

            transform.localScale = baseScale;
            squashCoroutine = null;
        }

        // ── F1: Eyes Setup ──
        private void SetupEyes()
        {
            if (eyes == null)
            {
                eyes = GetComponent<ObjectEyes>();
                if (eyes == null)
                    eyes = gameObject.AddComponent<ObjectEyes>();
            }
            eyes.Setup(Level);
        }

        // ── Level Label ──
        private void SetupLevelLabel()
        {
            if (levelLabel == null)
            {
                var labelObj = transform.Find("LevelLabel");
                if (labelObj == null)
                {
                    var go = new GameObject("LevelLabel");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                    levelLabel = go.AddComponent<TextMesh>();
                    levelLabel.alignment = TextAlignment.Center;
                    levelLabel.anchor = TextAnchor.MiddleCenter;
                    levelLabel.fontStyle = FontStyle.Bold;
                    levelLabel.color = Color.white;
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sortingOrder = 4;
                }
                else
                {
                    levelLabel = labelObj.GetComponent<TextMesh>();
                }
            }

            // Scale relative to parent — fixed world size regardless of ball size
            float parentSize = GameConfig.GetSize(Level);
            float labelWorldSize = 0.4f; // fixed readable size
            float relativeScale = labelWorldSize / Mathf.Max(parentSize, 0.1f);
            levelLabel.transform.localScale = new Vector3(relativeScale, relativeScale, 1f);

            levelLabel.text = Level <= 9 ? Level.ToString() : "★";
            levelLabel.fontSize = 64;
            levelLabel.characterSize = 0.15f;
            levelLabel.color = new Color(1f, 1f, 1f, 0.9f);
            levelLabel.gameObject.SetActive(true);
        }

        // ── Stone (Obstacle) ──
        public void InitializeAsStone(float size)
        {
            Level = -1;
            isDropping = false;
            CanMerge = false;
            IsMerging = false;
            IsStone = true;

            rb = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (sharedCircleSprite == null)
                sharedCircleSprite = CreateCircleSprite(256);

            spriteRenderer.sprite = sharedCircleSprite;
            spriteRenderer.color = new Color(0.4f, 0.4f, 0.45f);
            spriteRenderer.sortingOrder = 1;

            baseScale = new Vector3(size, size, 1f);
            transform.localScale = baseScale;

            // Hide highlight for stones
            SetupHighlight();
            if (innerHighlight != null)
                innerHighlight.color = new Color(0.6f, 0.6f, 0.6f, 0.15f);

            circleCollider.radius = 0.5f;
            var config = GameConfig.Instance;
            var mat = new PhysicsMaterial2D("StoneMat")
            {
                friction = config.physicsFriction * 1.5f,
                bounciness = config.physicsBounciness * 0.5f
            };
            circleCollider.sharedMaterial = mat;

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.CurrentGravityScale * 1.2f
                : config.gravityScale * 1.2f;
            rb.linearDamping = config.linearDrag * 2f;
            rb.angularDamping = config.angularDrag;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.mass = 3f; // heavier than normal

            // No eyes or label on stones
            if (eyes != null) eyes.gameObject.SetActive(false);
            if (levelLabel != null) levelLabel.gameObject.SetActive(false);

            CanMerge = false;
        }

        // ── F5: Skill - Downgrade Level ──
        public void DowngradeLevel(int levels)
        {
            int newLevel = Mathf.Max(0, Level - levels);
            if (newLevel == Level) return;
            Level = newLevel;
            ApplyVisuals();
            SetupEyes();
            SetupLevelLabel();
            circleCollider.radius = 0.5f;
        }

        // ── F7: Golden ──
        public GoldenObject GetGoldenObject()
        {
            if (goldenObject == null)
                goldenObject = GetComponent<GoldenObject>();
            return goldenObject;
        }

        public void SetGoldenObject(GoldenObject g) { goldenObject = g; }

        public bool IsGolden => goldenObject != null && goldenObject.IsGolden;

        public void ClearGolden()
        {
            if (goldenObject != null)
                goldenObject.ClearGolden();
        }

        // ── F2: Update gravity from DifficultyManager ──
        public void UpdateGravity(float gravityScale)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                rb.gravityScale = gravityScale;
        }

        // ── 메인 원형 스프라이트 (더 높은 해상도, 부드러운 테두리) ──
        private static Sprite CreateCircleSprite(int res)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;
            float radius = center - 1f;

            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius + 1f)
                    {
                        float alpha = Mathf.Clamp01(radius + 1f - dist);

                        // 테두리 어둡게
                        float edge = Mathf.Clamp01(dist / radius);
                        float rim = Mathf.Lerp(1f, 0.55f, edge * edge * edge);

                        pixels[y * res + x] = new Color(rim, rim, rim, alpha);
                    }
                    else
                    {
                        pixels[y * res + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f), res);
        }

        // ── 하이라이트 스프라이트 (좌상단 반사광) ──
        private static Sprite CreateHighlightSprite(int res)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;

            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - dist) * Mathf.Clamp01(1f - dist);
                    pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f), res);
        }
    }
}
