using UnityEngine;

namespace MergeDrop.Core
{
    public class GoldenObject : MonoBehaviour
    {
        public bool IsGolden { get; private set; }
        public bool IsTransferredGolden { get; private set; }

        private SpriteRenderer outlineRing;
        private ParticleSystem sparkleTrail;
        private SpriteRenderer mainRenderer;

        private Color originalColor;
        private static readonly Color goldenTint = new Color(1f, 0.84f, 0f);

        private static Sprite ringSprite;

        public void MakeGolden()
        {
            IsGolden = true;
            IsTransferredGolden = false;
            mainRenderer = GetComponent<SpriteRenderer>();
            if (mainRenderer != null)
                originalColor = mainRenderer.color;
            ApplyGoldenVisuals();
        }

        public void MakeTransferredGolden()
        {
            IsGolden = true;
            IsTransferredGolden = true;
            mainRenderer = GetComponent<SpriteRenderer>();
            if (mainRenderer != null)
                originalColor = mainRenderer.color;
            ApplyGoldenVisuals();
        }

        public void ClearGolden()
        {
            IsGolden = false;
            IsTransferredGolden = false;
            if (outlineRing != null)
                outlineRing.gameObject.SetActive(false);
            if (sparkleTrail != null)
                sparkleTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public int GetScoreMultiplier()
        {
            return IsGolden ? 5 : 1;
        }

        /// <summary>
        /// Returns true if golden should transfer to the merge result.
        /// Only original golden transfers; transferred golden does not.
        /// </summary>
        public bool ShouldTransfer()
        {
            return IsGolden && !IsTransferredGolden;
        }

        private void ApplyGoldenVisuals()
        {
            // Tint the main sprite golden
            if (mainRenderer != null)
            {
                Color tinted = Color.Lerp(mainRenderer.color, goldenTint, 0.5f);
                mainRenderer.color = tinted;
            }

            // Create outline ring
            EnsureRingSprite();
            if (outlineRing == null)
            {
                var existing = transform.Find("GoldenRing");
                if (existing != null)
                {
                    outlineRing = existing.GetComponent<SpriteRenderer>();
                }
                else
                {
                    var ringObj = new GameObject("GoldenRing");
                    ringObj.transform.SetParent(transform, false);
                    ringObj.transform.localPosition = Vector3.zero;
                    ringObj.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
                    outlineRing = ringObj.AddComponent<SpriteRenderer>();
                    outlineRing.sortingOrder = 0;
                }
            }
            outlineRing.sprite = ringSprite;
            outlineRing.color = new Color(1f, 0.84f, 0f, 0.6f);
            outlineRing.gameObject.SetActive(true);

            // Create sparkle trail
            if (sparkleTrail == null)
            {
                var existing = transform.Find("SparkleTrail");
                if (existing != null)
                {
                    sparkleTrail = existing.GetComponent<ParticleSystem>();
                }
                else
                {
                    var sparkleObj = new GameObject("SparkleTrail");
                    sparkleObj.transform.SetParent(transform, false);
                    sparkleObj.transform.localPosition = Vector3.zero;
                    sparkleTrail = sparkleObj.AddComponent<ParticleSystem>();
                    SetupSparkleParticles(sparkleTrail);
                }
            }
            sparkleTrail.Play();
        }

        private void Update()
        {
            if (!IsGolden) return;

            // Pulsing outline ring
            if (outlineRing != null && outlineRing.gameObject.activeSelf)
            {
                float pulse = Mathf.PingPong(Time.time * 2f, 1f);
                float alpha = Mathf.Lerp(0.3f, 0.8f, pulse);
                outlineRing.color = new Color(1f, 0.84f, 0f, alpha);

                float scale = Mathf.Lerp(1.1f, 1.2f, pulse);
                outlineRing.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void SetupSparkleParticles(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.5f;
            main.startSize = 0.04f;
            main.maxParticles = 15;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.2f;
            main.startColor = goldenTint;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(goldenTint, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 5;
        }

        private static void EnsureRingSprite()
        {
            if (ringSprite != null) return;

            int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;
            float outerR = center;
            float innerR = center * 0.85f;

            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float outerAlpha = Mathf.Clamp01((outerR - dist) * 2f);
                    float innerAlpha = Mathf.Clamp01((dist - innerR) * 2f);
                    float alpha = outerAlpha * innerAlpha;

                    pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            ringSprite = Sprite.Create(tex, new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f), res);
        }
    }
}
