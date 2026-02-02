using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using MergeDrop.Core;
using MergeDrop.Data;

namespace MergeDrop.UI
{
    public class MilestoneUI : MonoBehaviour
    {
        public static MilestoneUI Instance { get; private set; }

        private Canvas canvas;
        private GameObject bannerObj;
        private Text bannerText;
        private Image bannerBg;
        private Image flashOverlay;

        private readonly HashSet<int> shownLevelMilestones = new HashSet<int>();
        private readonly HashSet<int> shownScoreMilestones = new HashSet<int>();
        private static readonly int[] scoreMilestones = { 1000, 5000, 10000, 20000, 50000 };

        private static readonly Color milestoneGold = new Color(1f, 0.84f, 0f);
        private static readonly Color milestoneBg = new Color(0.15f, 0.12f, 0.02f, 0.92f);

        private Coroutine currentBanner;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(Canvas parentCanvas)
        {
            canvas = parentCanvas;
            CreateBanner();
            CreateFlashOverlay();

            if (MergeSystem.Instance != null)
                MergeSystem.Instance.OnMerge += OnMerge;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (MergeSystem.Instance != null)
                MergeSystem.Instance.OnMerge -= OnMerge;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnStateChanged;
        }

        private void CreateBanner()
        {
            bannerObj = new GameObject("MilestoneBanner");
            bannerObj.transform.SetParent(canvas.transform, false);

            var rt = bannerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 150f);
            rt.sizeDelta = new Vector2(550f, 90f);

            bannerBg = bannerObj.AddComponent<Image>();
            bannerBg.color = milestoneBg;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(bannerObj.transform, false);
            bannerText = textObj.AddComponent<Text>();
            bannerText.fontSize = 36;
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.color = milestoneGold;
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (UIManager.GetFont() != null) bannerText.font = UIManager.GetFont();

            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textRT = bannerText.rectTransform;
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            bannerObj.SetActive(false);
        }

        private void CreateFlashOverlay()
        {
            var flashObj = new GameObject("MilestoneFlash");
            flashObj.transform.SetParent(canvas.transform, false);

            var rt = flashObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            flashOverlay = flashObj.AddComponent<Image>();
            flashOverlay.color = Color.clear;
            flashOverlay.raycastTarget = false;
            flashObj.SetActive(false);
        }

        private void OnMerge(int newLevel, Vector3 pos, int combo)
        {
            // Level milestone: Lv5+ first reach
            if (newLevel >= 5 && !shownLevelMilestones.Contains(newLevel))
            {
                shownLevelMilestones.Add(newLevel);
                string name = GameConfig.GetName(newLevel);
                ShowMilestone($"{name} 달성!", true);
            }
        }

        private void OnScoreChanged(int score)
        {
            foreach (int milestone in scoreMilestones)
            {
                if (score >= milestone && !shownScoreMilestones.Contains(milestone))
                {
                    shownScoreMilestones.Add(milestone);
                    ShowMilestone($"{milestone:N0}점 돌파!", true);
                    break;
                }
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                ShowNearMissMessage();
            }
            else if (state == GameState.Playing || state == GameState.Ready)
            {
                shownLevelMilestones.Clear();
                shownScoreMilestones.Clear();
            }
        }

        private void ShowNearMissMessage()
        {
            if (ScoreManager.Instance == null) return;

            int current = ScoreManager.Instance.CurrentScore;
            int best = ScoreManager.Instance.BestScore;

            // Check if within 15% of best score (but didn't beat it)
            if (current < best && best > 0)
            {
                float ratio = (float)(best - current) / best;
                if (ratio <= 0.15f)
                {
                    int diff = best - current;
                    ShowMilestone($"역대 최고 기록까지 {diff:N0}점 남았어요!", false);
                }
            }
        }

        private void ShowMilestone(string text, bool withFlash)
        {
            if (currentBanner != null)
                StopCoroutine(currentBanner);

            currentBanner = StartCoroutine(ShowBannerCoroutine(text, withFlash));
        }

        private IEnumerator ShowBannerCoroutine(string text, bool withFlash)
        {
            bannerText.text = text;
            bannerObj.SetActive(true);

            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayMilestoneSound();

            // Flash effect
            if (withFlash && flashOverlay != null)
            {
                flashOverlay.gameObject.SetActive(true);
                float flashDuration = 0.3f;
                float elapsed = 0f;

                while (elapsed < flashDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / flashDuration;
                    float alpha = Mathf.Lerp(0.4f, 0f, t);
                    flashOverlay.color = new Color(1f, 0.9f, 0.5f, alpha);
                    yield return null;
                }

                flashOverlay.color = Color.clear;
                flashOverlay.gameObject.SetActive(false);
            }

            // Scale punch animation
            var rt = bannerObj.GetComponent<RectTransform>();
            float punchDuration = 0.2f;
            float punchElapsed = 0f;
            while (punchElapsed < punchDuration)
            {
                punchElapsed += Time.unscaledDeltaTime;
                float t = punchElapsed / punchDuration;
                float scale = t < 0.5f
                    ? 1f + 0.3f * (t / 0.5f)
                    : 1f + 0.3f * (1f - (t - 0.5f) / 0.5f);
                rt.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;

            // Hold for 2 seconds
            yield return new WaitForSecondsRealtime(2f);

            // Fade out
            float fadeDuration = 0.5f;
            float fadeElapsed = 0f;
            Color bgColor = bannerBg.color;
            Color txtColor = bannerText.color;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float t = fadeElapsed / fadeDuration;
                bannerBg.color = new Color(bgColor.r, bgColor.g, bgColor.b, bgColor.a * (1f - t));
                bannerText.color = new Color(txtColor.r, txtColor.g, txtColor.b, txtColor.a * (1f - t));
                yield return null;
            }

            bannerObj.SetActive(false);
            bannerBg.color = milestoneBg;
            bannerText.color = milestoneGold;
            currentBanner = null;
        }
    }
}
