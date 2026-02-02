using UnityEngine;
using MergeDrop.Core;

namespace MergeDrop.Effects
{
    public class BackgroundManager : MonoBehaviour
    {
        public static BackgroundManager Instance { get; private set; }

        // Score tier colors
        private static readonly Color tier0Color = new Color(0.03f, 0.03f, 0.10f); // 짙은 네이비
        private static readonly Color tier1Color = new Color(0.08f, 0.02f, 0.12f); // 딥 퍼플
        private static readonly Color tier2Color = new Color(0.02f, 0.04f, 0.14f); // 미드나잇 블루
        // tier3 = 오로라 (HSV 순환)

        private static readonly Color tier0Wall = new Color(0.35f, 0.40f, 0.55f, 0.8f);
        private static readonly Color tier1Wall = new Color(0.50f, 0.30f, 0.55f, 0.8f);
        private static readonly Color tier2Wall = new Color(0.30f, 0.40f, 0.65f, 0.8f);
        private static readonly Color tier3Wall = new Color(0.55f, 0.50f, 0.70f, 0.8f);

        private Color currentBgColor;
        private Color targetBgColor;
        private Color currentWallColor;

        private ParticleSystem starParticles;
        private bool starsActive;
        private int currentTier = -1;

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
            currentBgColor = tier0Color;
            targetBgColor = tier0Color;
            currentWallColor = tier0Wall;

            if (Camera.main != null)
                Camera.main.backgroundColor = currentBgColor;

            CreateStarParticles();

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
        }

        private void Update()
        {
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            int tier = GetTier(score);

            if (tier >= 3)
            {
                // 오로라: HSV 색조 순환
                float hue = Mathf.PingPong(Time.time * 0.05f, 1f);
                targetBgColor = Color.HSVToRGB(hue, 0.3f, 0.12f);
                currentWallColor = Color.Lerp(currentWallColor, tier3Wall, Time.deltaTime * 2f);
            }

            currentBgColor = Color.Lerp(currentBgColor, targetBgColor, Time.deltaTime * 2f);

            if (Camera.main != null)
                Camera.main.backgroundColor = currentBgColor;

            // Update wall colors
            if (ContainerBounds.Instance != null)
                UpdateWallColors();

            // Star particle visibility
            if (tier >= 2 && !starsActive)
            {
                starsActive = true;
                if (starParticles != null) starParticles.Play();
            }
            else if (tier < 2 && starsActive)
            {
                starsActive = false;
                if (starParticles != null) starParticles.Stop();
            }
        }

        private void OnScoreChanged(int score)
        {
            int tier = GetTier(score);
            if (tier == currentTier) return;
            currentTier = tier;

            switch (tier)
            {
                case 0:
                    targetBgColor = tier0Color;
                    currentWallColor = tier0Wall;
                    break;
                case 1:
                    targetBgColor = tier1Color;
                    currentWallColor = tier1Wall;
                    break;
                case 2:
                    targetBgColor = tier2Color;
                    currentWallColor = tier2Wall;
                    break;
                // tier 3+ handled in Update (aurora)
            }
        }

        private static int GetTier(int score)
        {
            if (score >= 10000) return 3;
            if (score >= 5000) return 2;
            if (score >= 2000) return 1;
            return 0;
        }

        private void UpdateWallColors()
        {
            var bounds = ContainerBounds.Instance;
            if (bounds == null) return;

            // Find wall LineRenderers
            var lineRenderers = bounds.GetComponentsInChildren<LineRenderer>();
            foreach (var lr in lineRenderers)
            {
                if (lr.gameObject.name == "GameOverLine" || lr.gameObject.name == "GuideLine") continue;

                Color wallColor = currentWallColor;
                // Floor is brighter
                if (lr.gameObject.name == "Floor")
                {
                    wallColor = new Color(
                        Mathf.Min(1f, currentWallColor.r + 0.15f),
                        Mathf.Min(1f, currentWallColor.g + 0.15f),
                        Mathf.Min(1f, currentWallColor.b + 0.15f),
                        0.9f);
                }

                lr.startColor = Color.Lerp(lr.startColor, wallColor, Time.deltaTime * 2f);
                lr.endColor = Color.Lerp(lr.endColor, wallColor, Time.deltaTime * 2f);
            }
        }

        private void CreateStarParticles()
        {
            var go = new GameObject("StarParticles");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(0f, 3f, 0f);

            starParticles = go.AddComponent<ParticleSystem>();
            starParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = starParticles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startColor = new Color(1f, 1f, 1f, 0.6f);

            var emission = starParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 5f;

            var shape = starParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 12f, 0f);

            // Twinkle via size over lifetime
            var sizeOverLifetime = starParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.5f, 0.3f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0f)));

            // Color over lifetime - fade in/out
            var colorOverLifetime = starParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.6f, 0.3f),
                    new GradientAlphaKey(0.6f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = -40;
        }
    }
}
