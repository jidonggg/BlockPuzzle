using UnityEngine;
using System.Collections;

namespace MergeDrop.Effects
{
    public class MergeEffect : MonoBehaviour
    {
        public static MergeEffect Instance { get; private set; }

        private ParticleSystem mergeParticles;
        private ParticleSystem maxMergeParticles;

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
            mergeParticles = CreateParticleSystem("MergeParticles", 40);
            maxMergeParticles = CreateParticleSystem("MaxMergeParticles", 80);
        }

        public void PlayMergeEffect(Vector3 position, Color color, float size)
        {
            PlayParticles(mergeParticles, position, color, size, 25);
            StartCoroutine(FlashEffect(position, color, size * 0.8f));
            StartCoroutine(RingEffect(position, color, size * 0.6f));
        }

        public void PlayNewLevelEffect(Vector3 position, Color color, float size)
        {
            PlayParticles(mergeParticles, position, color, size, 35);
            StartCoroutine(FlashEffect(position, color, size));
            StartCoroutine(RingEffect(position, color, size));
            StartCoroutine(RingEffect(position, Color.white, size * 0.7f));
            StartCoroutine(ScreenFlash(color, 0.15f));
        }

        public void PlayMaxMergeEffect(Vector3 position)
        {
            Color gold = new Color(1f, 0.84f, 0f);
            PlayParticles(maxMergeParticles, position, gold, 3.5f, 80);
            StartCoroutine(FlashEffect(position, gold, 4f));
            StartCoroutine(RingEffect(position, gold, 4f));
            StartCoroutine(RingEffect(position, Color.white, 3f));
            StartCoroutine(ScreenFlash(gold, 0.3f));
        }

        /// <summary>
        /// Called from MergeSystem on every combo — bigger effects for bigger combos
        /// </summary>
        public void PlayComboEffect(Vector3 position, Color color, int combo)
        {
            if (combo >= 3)
            {
                StartCoroutine(ScreenFlash(color, 0.08f + combo * 0.03f));
                StartCoroutine(RingEffect(position, color, 1f + combo * 0.3f));
            }
            if (combo >= 5)
            {
                // Double ring for big combos
                StartCoroutine(RingEffect(position, Color.white, 2f + combo * 0.2f));
            }
        }

        private void PlayParticles(ParticleSystem ps, Vector3 position, Color color, float size, int count)
        {
            ps.transform.position = position;

            var main = ps.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.08f, size * 0.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(size * 1.5f, size * 4f);

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, count)
            });

            ps.Play();
        }

        private ParticleSystem CreateParticleSystem(string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = 4f;
            main.startSize = 0.15f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.8f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, maxParticles)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.3f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 10;

            return ps;
        }

        private IEnumerator FlashEffect(Vector3 position, Color color, float size)
        {
            var flashObj = new GameObject("Flash");
            flashObj.transform.position = position;

            var sr = flashObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.color = new Color(1f, 1f, 1f, 0.9f);
            sr.sortingOrder = 8;

            float duration = 0.25f;
            float elapsed = 0f;
            float startScale = size * 0.3f;
            float endScale = size * 2.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale = Mathf.Lerp(startScale, endScale, t);
                flashObj.transform.localScale = new Vector3(scale, scale, 1f);

                float alpha = Mathf.Lerp(0.9f, 0f, t * t);
                sr.color = new Color(color.r, color.g, color.b, alpha);

                yield return null;
            }

            Destroy(flashObj);
        }

        private IEnumerator RingEffect(Vector3 position, Color color, float size)
        {
            var ringObj = new GameObject("Ring");
            ringObj.transform.position = position;

            var sr = ringObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetRingSprite();
            sr.color = color;
            sr.sortingOrder = 9;

            float duration = 0.5f;
            float elapsed = 0f;
            float startScale = size * 0.2f;
            float endScale = size * 3.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float eased = 1f - (1f - t) * (1f - t); // easeOutQuad
                float scale = Mathf.Lerp(startScale, endScale, eased);
                ringObj.transform.localScale = new Vector3(scale, scale, 1f);

                float alpha = Mathf.Lerp(0.8f, 0f, t);
                sr.color = new Color(color.r, color.g, color.b, alpha);

                yield return null;
            }

            Destroy(ringObj);
        }

        /// <summary>
        /// Full-screen color flash
        /// </summary>
        private IEnumerator ScreenFlash(Color color, float intensity)
        {
            var flashObj = new GameObject("ScreenFlash");
            flashObj.transform.position = new Vector3(0f, 0.8f, -2f);

            var sr = flashObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite();
            sr.sortingOrder = 45;

            float duration = 0.3f;
            float elapsed = 0f;
            flashObj.transform.localScale = new Vector3(20f, 20f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = Mathf.Lerp(intensity, 0f, t);
                sr.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            Destroy(flashObj);
        }

        // ── Sprites ──
        private static Sprite cachedCircle;
        private static Sprite GetCircleSprite()
        {
            if (cachedCircle != null) return cachedCircle;

            int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;

            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((center - dist) * 2f);
                    pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            cachedCircle = Sprite.Create(tex, new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f), res);
            return cachedCircle;
        }

        private static Sprite cachedRing;
        private static Sprite GetRingSprite()
        {
            if (cachedRing != null) return cachedRing;

            int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;
            float outerR = center;
            float innerR = center * 0.7f;

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

            cachedRing = Sprite.Create(tex, new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f), res);
            return cachedRing;
        }
    }
}
