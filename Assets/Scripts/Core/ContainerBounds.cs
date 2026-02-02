using UnityEngine;
using System.Collections;
using System;
using MergeDrop.Data;

namespace MergeDrop.Core
{
    public class ContainerBounds : MonoBehaviour
    {
        public static ContainerBounds Instance { get; private set; }

        public event Action OnGameOverTriggered;

        private LineRenderer gameOverLine;
        private SpriteRenderer vignetteOverlay;
        private float overflowTimer;
        private bool isWarning;

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
            CreateContainer();
            CreateGameOverLine();
            CreateVignetteOverlay();
        }

        private void CreateContainer()
        {
            var config = GameConfig.Instance;
            float halfW = config.containerWidth / 2f;
            float bottom = config.containerBottomY;
            float top = bottom + config.containerHeight;

            // U자형 EdgeCollider2D
            var containerObj = new GameObject("ContainerCollider");
            containerObj.transform.SetParent(transform);
            var edge = containerObj.AddComponent<EdgeCollider2D>();
            edge.points = new Vector2[]
            {
                new Vector2(-halfW, top + 3f),
                new Vector2(-halfW, bottom),
                new Vector2(halfW, bottom),
                new Vector2(halfW, top + 3f)
            };

            var mat = new PhysicsMaterial2D("ContainerMat")
            {
                friction = config.physicsFriction,
                bounciness = config.physicsBounciness * 0.3f
            };
            edge.sharedMaterial = mat;

            var spriteMat = new Material(Shader.Find("Sprites/Default"));

            // 좌벽 (그라디언트 느낌)
            CreateWallVisual("LeftWall",
                new Vector3(-halfW, bottom, 0f),
                new Vector3(-halfW, top + 1.5f, 0f),
                new Color(0.35f, 0.40f, 0.55f, 0.8f), 0.08f, spriteMat);

            // 우벽
            CreateWallVisual("RightWall",
                new Vector3(halfW, bottom, 0f),
                new Vector3(halfW, top + 1.5f, 0f),
                new Color(0.35f, 0.40f, 0.55f, 0.8f), 0.08f, spriteMat);

            // 바닥 (밝은 강조)
            CreateWallVisual("Floor",
                new Vector3(-halfW, bottom, 0f),
                new Vector3(halfW, bottom, 0f),
                new Color(0.5f, 0.55f, 0.7f, 0.9f), 0.12f, spriteMat);

            // 좌하단 코너 연결
            CreateWallVisual("CornerL",
                new Vector3(-halfW - 0.04f, bottom + 0.04f, 0f),
                new Vector3(-halfW + 0.04f, bottom - 0.04f, 0f),
                new Color(0.5f, 0.55f, 0.7f, 0.6f), 0.12f, spriteMat);

            // 우하단 코너 연결
            CreateWallVisual("CornerR",
                new Vector3(halfW - 0.04f, bottom - 0.04f, 0f),
                new Vector3(halfW + 0.04f, bottom + 0.04f, 0f),
                new Color(0.5f, 0.55f, 0.7f, 0.6f), 0.12f, spriteMat);
        }

        private void CreateWallVisual(string name, Vector3 start, Vector3 end, Color color, float width, Material mat)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            var lr = obj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.material = mat;
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingOrder = 3;
        }

        private void CreateGameOverLine()
        {
            var config = GameConfig.Instance;
            float halfW = config.containerWidth / 2f;

            var lineObj = new GameObject("GameOverLine");
            lineObj.transform.SetParent(transform);
            gameOverLine = lineObj.AddComponent<LineRenderer>();
            gameOverLine.positionCount = 2;
            gameOverLine.SetPosition(0, new Vector3(-halfW + 0.1f, config.gameOverLineY, 0f));
            gameOverLine.SetPosition(1, new Vector3(halfW - 0.1f, config.gameOverLineY, 0f));
            gameOverLine.startWidth = 0.04f;
            gameOverLine.endWidth = 0.04f;
            gameOverLine.material = new Material(Shader.Find("Sprites/Default"));
            gameOverLine.startColor = new Color(1f, 0.3f, 0.3f, 0.35f);
            gameOverLine.endColor = new Color(1f, 0.3f, 0.3f, 0.35f);
            gameOverLine.sortingOrder = 5;
        }

        private void CreateVignetteOverlay()
        {
            var overlayObj = new GameObject("VignetteOverlay");
            overlayObj.transform.SetParent(transform);
            overlayObj.transform.position = new Vector3(0f, 0.8f, -1f);

            vignetteOverlay = overlayObj.AddComponent<SpriteRenderer>();
            vignetteOverlay.sprite = CreateVignetteSprite();
            vignetteOverlay.color = new Color(1f, 0f, 0f, 0f);
            vignetteOverlay.sortingOrder = 50;
            overlayObj.transform.localScale = new Vector3(15f, 18f, 1f);
        }

        private Sprite CreateVignetteSprite()
        {
            int res = 64;
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
                    float dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    float alpha = Mathf.Clamp01((dist - 0.7f) / 0.3f) * 0.5f;
                    pixels[y * res + x] = new Color(1f, 0f, 0f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

            CheckOverflow();
            UpdateWarningVisuals();
        }

        private void CheckOverflow()
        {
            var objects = ObjectSpawner.Instance.GetActiveObjects();
            bool anyAbove = false;

            foreach (var obj in objects)
            {
                if (obj == null || obj.IsMerging || !obj.CanMerge) continue;
                if (obj.IsAboveGameOverLine() && obj.IsSettled())
                {
                    anyAbove = true;
                    break;
                }
            }

            if (anyAbove)
            {
                overflowTimer += Time.deltaTime;
                if (!isWarning)
                    isWarning = true;

                if (overflowTimer >= GameConfig.Instance.gameOverDelay)
                {
                    overflowTimer = 0f;
                    isWarning = false;
                    OnGameOverTriggered?.Invoke();
                    GameManager.Instance.HandleGameOver();
                }
            }
            else
            {
                overflowTimer = 0f;
                if (isWarning)
                    isWarning = false;
            }
        }

        private void UpdateWarningVisuals()
        {
            if (isWarning)
            {
                float blink = Mathf.PingPong(Time.time * 5f, 1f);
                float lineAlpha = Mathf.Lerp(0.2f, 1f, blink);
                gameOverLine.startColor = new Color(1f, 0.15f, 0.15f, lineAlpha);
                gameOverLine.endColor = new Color(1f, 0.15f, 0.15f, lineAlpha);

                float vigAlpha = Mathf.Lerp(0f, 0.5f, overflowTimer / GameConfig.Instance.gameOverDelay);
                vignetteOverlay.color = new Color(1f, 0f, 0f, vigAlpha);
            }
            else
            {
                gameOverLine.startColor = new Color(1f, 0.3f, 0.3f, 0.35f);
                gameOverLine.endColor = new Color(1f, 0.3f, 0.3f, 0.35f);
                vignetteOverlay.color = new Color(1f, 0f, 0f, 0f);
            }
        }

        public void ClearAboveLine()
        {
            ObjectSpawner.Instance.RemoveTopObjects(GameConfig.Instance.gameOverLineY);
            overflowTimer = 0f;
            isWarning = false;
        }
    }
}
