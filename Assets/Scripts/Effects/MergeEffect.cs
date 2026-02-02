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
            mergeParticles = CreateParticleSystem("MergeParticles", 20);
            maxMergeParticles = CreateParticleSystem("MaxMergeParticles", 50);
        }

        public void PlayMergeEffect(Vector3 position, Color color, float size)
        {
            PlayParticles(mergeParticles, position, color, size);
            StartCoroutine(FlashEffect(position, color, size));
        }

        public void PlayNewLevelEffect(Vector3 position, Color color, float size)
        {
            PlayParticles(mergeParticles, position, color, size);
            StartCoroutine(FlashEffect(position, color, size));
            StartCoroutine(RingEffect(position, color, size));
        }

        public void PlayMaxMergeEffect(Vector3 position)
        {
            Color gold = new Color(1f, 0.84f, 0f);
            PlayParticles(maxMergeParticles, position, gold, 3f);
            StartCoroutine(FlashEffect(position, gold, 3f));
            StartCoroutine(RingEffect(position, gold, 3f));
        }

        private void PlayParticles(ParticleSystem ps, Vector3 position, Color color, float size)
        {
            ps.transform.position = position;

            var main = ps.main;
            main.startColor = color;
            main.startSize = size * 0.15f;
            main.startSpeed = size * 2f;

            ps.Play();
        }

        private ParticleSystem CreateParticleSystem(string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 자동 재생 비활성화
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.1f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.5f;

            // Emission
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, maxParticles)
            });

            // Shape — 원형으로 퍼지게
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            // Color over lifetime — 페이드아웃
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            // Size over lifetime — 줄어듦
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            // Renderer
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
            sr.color = new Color(1f, 1f, 1f, 0.8f);
            sr.sortingOrder = 8;

            float duration = 0.2f;
            float elapsed = 0f;
            float startScale = size * 0.5f;
            float endScale = size * 1.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale = Mathf.Lerp(startScale, endScale, t);
                flashObj.transform.localScale = new Vector3(scale, scale, 1f);

                float alpha = Mathf.Lerp(0.8f, 0f, t);
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

            float duration = 0.4f;
            float elapsed = 0f;
            float startScale = size * 0.3f;
            float endScale = size * 2.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale = Mathf.Lerp(startScale, endScale, t);
                ringObj.transform.localScale = new Vector3(scale, scale, 1f);

                float alpha = Mathf.Lerp(0.7f, 0f, t);
                sr.color = new Color(color.r, color.g, color.b, alpha);

                yield return null;
            }

            Destroy(ringObj);
        }

        // ── 코드로 원형 스프라이트 생성 ──
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

        // ── 코드로 링 스프라이트 생성 ──
        private static Sprite cachedRing;
        private static Sprite GetRingSprite()
        {
            if (cachedRing != null) return cachedRing;

            int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;
            float outerR = center;
            float innerR = center * 0.75f;

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
