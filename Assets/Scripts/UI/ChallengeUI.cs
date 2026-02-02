using UnityEngine;
using UnityEngine.UI;
using MergeDrop.Core;

namespace MergeDrop.UI
{
    public class ChallengeUI : MonoBehaviour
    {
        public static ChallengeUI Instance { get; private set; }

        private Canvas canvas;
        private GameObject bannerObj;
        private Text challengeText;
        private Image progressBar;
        private Image progressBg;
        private Text progressText;
        private GameObject completeBanner;

        private static readonly Color bannerBg = new Color(0.1f, 0.1f, 0.2f, 0.85f);
        private static readonly Color progressColor = new Color(0.3f, 0.7f, 1f);
        private static readonly Color completeColor = new Color(1f, 0.84f, 0f);

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
            CreateCompleteBanner();

            if (DailyChallengeManager.Instance != null)
            {
                DailyChallengeManager.Instance.OnProgressChanged += OnProgressChanged;
                DailyChallengeManager.Instance.OnChallengeCompleted += OnChallengeCompleted;
                UpdateDisplay();
            }

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (DailyChallengeManager.Instance != null)
            {
                DailyChallengeManager.Instance.OnProgressChanged -= OnProgressChanged;
                DailyChallengeManager.Instance.OnChallengeCompleted -= OnChallengeCompleted;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnStateChanged;
        }

        private void CreateBanner()
        {
            bannerObj = new GameObject("ChallengeBanner");
            bannerObj.transform.SetParent(canvas.transform, false);

            var rt = bannerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -210f);
            rt.sizeDelta = new Vector2(600f, 70f);

            var bg = bannerObj.AddComponent<Image>();
            bg.color = bannerBg;

            // "일일 도전" label + description
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(bannerObj.transform, false);
            challengeText = labelObj.AddComponent<Text>();
            challengeText.fontSize = 20;
            challengeText.alignment = TextAnchor.MiddleLeft;
            challengeText.color = Color.white;
            challengeText.fontStyle = FontStyle.Bold;
            if (UIManager.GetFont() != null) challengeText.font = UIManager.GetFont();
            var labelRT = challengeText.rectTransform;
            labelRT.anchorMin = new Vector2(0f, 0.5f);
            labelRT.anchorMax = new Vector2(0.65f, 1f);
            labelRT.offsetMin = new Vector2(16f, 0f);
            labelRT.offsetMax = Vector2.zero;

            // Progress background
            var progressBgObj = new GameObject("ProgressBg");
            progressBgObj.transform.SetParent(bannerObj.transform, false);
            progressBg = progressBgObj.AddComponent<Image>();
            progressBg.color = new Color(0.2f, 0.2f, 0.3f);
            var bgRT = progressBg.rectTransform;
            bgRT.anchorMin = new Vector2(0.05f, 0.1f);
            bgRT.anchorMax = new Vector2(0.75f, 0.4f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Progress fill
            var progressFillObj = new GameObject("ProgressFill");
            progressFillObj.transform.SetParent(progressBgObj.transform, false);
            progressBar = progressFillObj.AddComponent<Image>();
            progressBar.color = progressColor;
            progressBar.type = Image.Type.Filled;
            progressBar.fillMethod = Image.FillMethod.Horizontal;
            progressBar.fillAmount = 0f;
            var fillRT = progressBar.rectTransform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            // Progress text (percentage)
            var pTextObj = new GameObject("ProgressText");
            pTextObj.transform.SetParent(bannerObj.transform, false);
            progressText = pTextObj.AddComponent<Text>();
            progressText.fontSize = 22;
            progressText.alignment = TextAnchor.MiddleRight;
            progressText.color = progressColor;
            progressText.fontStyle = FontStyle.Bold;
            if (UIManager.GetFont() != null) progressText.font = UIManager.GetFont();
            var pRT = progressText.rectTransform;
            pRT.anchorMin = new Vector2(0.75f, 0f);
            pRT.anchorMax = new Vector2(1f, 1f);
            pRT.offsetMin = Vector2.zero;
            pRT.offsetMax = new Vector2(-12f, 0f);

            bannerObj.SetActive(false);
        }

        private void CreateCompleteBanner()
        {
            completeBanner = new GameObject("CompleteBanner");
            completeBanner.transform.SetParent(canvas.transform, false);

            var rt = completeBanner.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 100f);
            rt.sizeDelta = new Vector2(500f, 80f);

            var bg = completeBanner.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.12f, 0.02f, 0.92f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(completeBanner.transform, false);
            var txt = textObj.AddComponent<Text>();
            txt.text = "일일 도전 완료!";
            txt.fontSize = 32;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = completeColor;
            txt.fontStyle = FontStyle.Bold;
            if (UIManager.GetFont() != null) txt.font = UIManager.GetFont();
            var textRT = txt.rectTransform;
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            completeBanner.SetActive(false);
        }

        private void UpdateDisplay()
        {
            var mgr = DailyChallengeManager.Instance;
            if (mgr == null) return;

            challengeText.text = $"도전: {mgr.ChallengeDescription}";
            progressBar.fillAmount = mgr.Progress;
            progressText.text = $"{Mathf.RoundToInt(mgr.Progress * 100)}%";

            if (mgr.IsCompleted)
            {
                progressBar.color = completeColor;
                progressText.color = completeColor;
            }
        }

        private void OnProgressChanged(float progress)
        {
            if (progressBar != null) progressBar.fillAmount = progress;
            if (progressText != null) progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }

        private void OnChallengeCompleted()
        {
            if (progressBar != null) progressBar.color = completeColor;
            if (progressText != null)
            {
                progressText.color = completeColor;
                progressText.text = "완료!";
            }

            // Show completion banner briefly
            if (completeBanner != null)
            {
                completeBanner.SetActive(true);
                StartCoroutine(HideAfterDelay(completeBanner, 2f));
            }
        }

        private System.Collections.IEnumerator HideAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (obj != null) obj.SetActive(false);
        }

        private void OnStateChanged(GameState state)
        {
            if (bannerObj != null)
                bannerObj.SetActive(state == GameState.Playing);
        }
    }
}
