using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections;
using System.Reflection;
using MergeDrop.Core;
using MergeDrop.Data;

namespace MergeDrop.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private static Font koreanFont;

        // UI 참조
        private Canvas canvas;
        private RectTransform canvasRT;
        private Text scoreText;
        private Text bestScoreText;
        private Text comboText;

        // 다음 미리보기
        private Image nextPreviewCircle;
        private Text nextPreviewLabel;

        // 패널
        private GameObject startPanel;
        private GameObject gameOverPanel;
        private GameObject pausePanel;
        private GameObject hudGroup;
        private Text gameOverScoreText;
        private Text gameOverBestText;
        private Text gameOverHighestLevelText;

        // F3: 피버 UI
        private Image[] feverBorders; // top, right, bottom, left
        private bool feverUIActive;

        // 색상 팔레트
        private static readonly Color panelDark = new Color(0.08f, 0.08f, 0.15f, 0.92f);
        private static readonly Color accentGold = new Color(1f, 0.84f, 0.0f);
        private static readonly Color accentGreen = new Color(0.25f, 0.85f, 0.45f);
        private static readonly Color accentBlue = new Color(0.3f, 0.55f, 0.95f);
        private static readonly Color accentRed = new Color(0.95f, 0.3f, 0.35f);
        private static readonly Color textDim = new Color(0.65f, 0.65f, 0.75f);

        // 원형 스프라이트 캐시
        private static Sprite circleSprite;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InitFont();
        }

        private static void InitFont()
        {
            if (koreanFont != null) return;

            string[] osFonts = Font.GetOSInstalledFontNames();
            string[] candidates = {
                "Malgun Gothic", "맑은 고딕", "NanumGothic",
                "AppleSDGothicNeo-Regular", "AppleGothic",
                "NotoSansCJK-Regular", "Arial"
            };

            foreach (string candidate in candidates)
            {
                foreach (string osFont in osFonts)
                {
                    if (osFont.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        candidate.IndexOf(osFont, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        koreanFont = Font.CreateDynamicFontFromOSFont(osFont, 32);
                        if (koreanFont != null) return;
                    }
                }
            }

            koreanFont = Font.CreateDynamicFontFromOSFont("Arial", 32);
        }

        public static Font GetFont()
        {
            if (koreanFont == null) InitFont();
            return koreanFont;
        }

        private void Start()
        {
            CreateCanvas();
            CreateHUD();
            CreateComboDisplay();
            CreateStartPanel();
            CreateGameOverPanel();
            CreatePauseButton();
            CreatePausePanel();
            CreateFeverBorders();

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += UpdateScore;
                ScoreManager.Instance.OnBestScoreChanged += UpdateBestScore;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnStateChanged;
            if (DropController.Instance != null)
            {
                DropController.Instance.OnNextObjectReady += UpdateNextPreview;
                DropController.Instance.OnQueuedLevelChanged += UpdateNextPreview;
            }
            if (MergeSystem.Instance != null)
                MergeSystem.Instance.OnMerge += OnMergeHappened;

            // F3: Fever events
            if (FeverManager.Instance != null)
            {
                FeverManager.Instance.OnFeverStart += OnFeverStart;
                FeverManager.Instance.OnFeverEnd += OnFeverEnd;
            }

            // F5: Skill UI
            InitializeSubUI<SkillUI>("SkillUI");
            // F8: Challenge UI
            InitializeSubUI<ChallengeUI>("ChallengeUI");
            // CC: Milestone UI
            InitializeSubUI<MilestoneUI>("MilestoneUI");

            OnStateChanged(GameManager.Instance != null ? GameManager.Instance.State : GameState.Ready);
        }

        private void InitializeSubUI<T>(string name) where T : MonoBehaviour
        {
            var existing = FindAnyObjectByType<T>();
            if (existing == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform);
                existing = go.AddComponent<T>();
            }

            // Call Initialize if it has one
            var method = typeof(T).GetMethod("Initialize",
                new System.Type[] { typeof(Canvas) });
            if (method != null)
                method.Invoke(existing, new object[] { canvas });
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= UpdateScore;
                ScoreManager.Instance.OnBestScoreChanged -= UpdateBestScore;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnStateChanged;
            if (DropController.Instance != null)
            {
                DropController.Instance.OnNextObjectReady -= UpdateNextPreview;
                DropController.Instance.OnQueuedLevelChanged -= UpdateNextPreview;
            }
            if (MergeSystem.Instance != null)
                MergeSystem.Instance.OnMerge -= OnMergeHappened;
            if (FeverManager.Instance != null)
            {
                FeverManager.Instance.OnFeverStart -= OnFeverStart;
                FeverManager.Instance.OnFeverEnd -= OnFeverEnd;
            }
        }

        // ────────────────────────────────────────
        //  Canvas
        // ────────────────────────────────────────
        private void CreateCanvas()
        {
            var canvasObj = new GameObject("UICanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasRT = canvas.GetComponent<RectTransform>();

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<InputSystemUIInputModule>();
            }
        }

        // ────────────────────────────────────────
        //  HUD (점수 + NEXT 미리보기)
        // ────────────────────────────────────────
        private void CreateHUD()
        {
            // HUD 그룹 (상단 영역)
            hudGroup = new GameObject("HUDGroup");
            hudGroup.transform.SetParent(canvas.transform, false);
            var hudRT = hudGroup.AddComponent<RectTransform>();
            hudRT.anchorMin = new Vector2(0f, 1f);
            hudRT.anchorMax = new Vector2(1f, 1f);
            hudRT.pivot = new Vector2(0.5f, 1f);
            hudRT.offsetMin = new Vector2(0f, -200f);
            hudRT.offsetMax = new Vector2(0f, 0f);

            // ── 점수 패널 (왼쪽) ──
            var scorePanel = CreateRoundedPanel("ScorePanel", hudGroup.transform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(28f, 0f), new Vector2(420f, 160f),
                new Vector2(0f, 0.5f), panelDark);

            // "SCORE" 라벨
            var scoreLabelTxt = CreateStyledText(scorePanel.transform, "ScoreLabel", "SCORE",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -8f), new Vector2(0f, 36f),
                22, TextAnchor.UpperLeft, textDim);
            scoreLabelTxt.rectTransform.offsetMin = new Vector2(24f, 0f);
            scoreLabelTxt.rectTransform.offsetMax = new Vector2(-12f, -8f);
            scoreLabelTxt.rectTransform.anchoredPosition = Vector2.zero;
            SetAnchorsAndStretch(scoreLabelTxt.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 1f), new Vector2(24f, 0f), new Vector2(-12f, 0f));

            // 점수 값
            scoreText = CreateStyledText(scorePanel.transform, "ScoreValue", "0",
                new Vector2(0f, 0f), new Vector2(1f, 0.72f),
                Vector2.zero, Vector2.zero,
                52, TextAnchor.MiddleLeft, Color.white);
            scoreText.rectTransform.offsetMin = new Vector2(24f, 0f);
            scoreText.rectTransform.offsetMax = new Vector2(-12f, 0f);
            AddOutline(scoreText.gameObject, new Color(0f, 0f, 0f, 0.6f), new Vector2(2f, -2f));

            // BEST 라벨 (점수 패널 아래)
            int bestVal = ScoreManager.Instance != null ? ScoreManager.Instance.BestScore : 0;
            bestScoreText = CreateStyledText(scorePanel.transform, "BestScore",
                $"BEST  {bestVal:N0}",
                new Vector2(0f, 0f), new Vector2(1f, 0.28f),
                Vector2.zero, Vector2.zero,
                20, TextAnchor.MiddleLeft, accentGold);
            bestScoreText.rectTransform.offsetMin = new Vector2(24f, 4f);
            bestScoreText.rectTransform.offsetMax = new Vector2(-12f, 0f);

            // ── NEXT 패널 (오른쪽) ──
            var nextPanel = CreateRoundedPanel("NextPanel", hudGroup.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-28f, 0f), new Vector2(200f, 160f),
                new Vector2(1f, 0.5f), panelDark);

            // "NEXT" 라벨
            CreateStyledText(nextPanel.transform, "NextLabel", "NEXT",
                new Vector2(0f, 0.7f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                22, TextAnchor.MiddleCenter, textDim);

            // 색상 원형 미리보기
            var circleObj = new GameObject("NextCircle");
            circleObj.transform.SetParent(nextPanel.transform, false);
            nextPreviewCircle = circleObj.AddComponent<Image>();
            nextPreviewCircle.sprite = GetCircleSprite();
            nextPreviewCircle.color = Color.white;
            var circleRT = nextPreviewCircle.rectTransform;
            circleRT.anchorMin = new Vector2(0.5f, 0.12f);
            circleRT.anchorMax = new Vector2(0.5f, 0.12f);
            circleRT.anchoredPosition = new Vector2(0f, 30f);
            circleRT.sizeDelta = new Vector2(64f, 64f);

            // 등급 이름 텍스트
            nextPreviewLabel = CreateStyledText(nextPanel.transform, "NextName", "",
                new Vector2(0f, 0f), new Vector2(1f, 0.2f),
                Vector2.zero, Vector2.zero,
                18, TextAnchor.MiddleCenter, Color.white);
        }

        // ────────────────────────────────────────
        //  콤보 표시 (화면 중앙 상단)
        // ────────────────────────────────────────
        private void CreateComboDisplay()
        {
            comboText = CreateStyledText(canvas.transform, "ComboText", "",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 350f), new Vector2(500f, 80f),
                56, TextAnchor.MiddleCenter, accentGold);
            AddOutline(comboText.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(3f, -3f));
        }

        // ────────────────────────────────────────
        //  시작 패널
        // ────────────────────────────────────────
        private void CreateStartPanel()
        {
            startPanel = CreateFullScreenOverlay("StartPanel", new Color(0.03f, 0.03f, 0.08f, 0.75f));

            // 타이틀
            var titleText = CreateStyledText(startPanel.transform, "TitleText", "합쳐봐!",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 180f), new Vector2(700f, 130f),
                84, TextAnchor.MiddleCenter, Color.white);
            AddOutline(titleText.gameObject, new Color(0.2f, 0.1f, 0.4f, 0.8f), new Vector2(4f, -4f));

            // 부제
            CreateStyledText(startPanel.transform, "SubTitle", "물리 머지 퍼즐",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 90f), new Vector2(500f, 50f),
                28, TextAnchor.MiddleCenter, textDim);

            // 시작 버튼
            var startBtn = CreateStyledButton(startPanel.transform, "StartButton", "게임 시작",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(380f, 90f),
                accentGreen, 36);

            startBtn.onClick.AddListener(() =>
            {
                GameManager.Instance.StartGame();
            });

            // 베스트 점수 표시
            int bestVal = ScoreManager.Instance != null ? ScoreManager.Instance.BestScore : 0;
            if (bestVal > 0)
            {
                CreateStyledText(startPanel.transform, "StartBest",
                    $"BEST: {bestVal:N0}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -150f), new Vector2(400f, 40f),
                    24, TextAnchor.MiddleCenter, accentGold);
            }
        }

        // ────────────────────────────────────────
        //  게임오버 패널
        // ────────────────────────────────────────
        private void CreateGameOverPanel()
        {
            gameOverPanel = CreateFullScreenOverlay("GameOverPanel", new Color(0.02f, 0.02f, 0.06f, 0.82f));

            // 중앙 카드 패널
            var card = CreateRoundedPanel("GOCard", gameOverPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 620f),
                new Vector2(0.5f, 0.5f), panelDark);

            // 제목
            var goTitle = CreateStyledText(card.transform, "GOTitle", "게임 오버",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(500f, 70f),
                52, TextAnchor.MiddleCenter, accentRed);
            AddOutline(goTitle.gameObject, new Color(0f, 0f, 0f, 0.5f), new Vector2(2f, -2f));

            // 구분선
            CreateDivider(card.transform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), 460f);

            // 점수
            gameOverScoreText = CreateStyledText(card.transform, "GOScore", "0",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -155f), new Vector2(500f, 70f),
                60, TextAnchor.MiddleCenter, Color.white);
            AddOutline(gameOverScoreText.gameObject, new Color(0f, 0f, 0f, 0.5f), new Vector2(2f, -2f));

            // 최고점수
            gameOverBestText = CreateStyledText(card.transform, "GOBest", "BEST: 0",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -220f), new Vector2(500f, 40f),
                26, TextAnchor.MiddleCenter, accentGold);

            // 최고 등급
            gameOverHighestLevelText = CreateStyledText(card.transform, "GOLevel", "",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -260f), new Vector2(500f, 36f),
                22, TextAnchor.MiddleCenter, textDim);

            // 구분선
            CreateDivider(card.transform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), 460f);

            // 부활 버튼
            var reviveBtn = CreateStyledButton(card.transform, "ReviveButton", "부활",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -360f), new Vector2(400f, 72f),
                new Color(0.85f, 0.65f, 0.1f), 30);

            reviveBtn.onClick.AddListener(() =>
            {
                GameManager.Instance.Revive();
            });

            // 다시하기 버튼
            var restartBtn = CreateStyledButton(card.transform, "RestartButton", "다시하기",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -448f), new Vector2(400f, 72f),
                accentBlue, 30);

            restartBtn.onClick.AddListener(() =>
            {
                GameManager.Instance.RestartGame();
            });

            // 공유 버튼
            var shareBtn = CreateStyledButton(card.transform, "ShareButton", "공유",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -536f), new Vector2(400f, 56f),
                new Color(0.35f, 0.35f, 0.45f), 24);

            shareBtn.onClick.AddListener(() =>
            {
                Debug.Log("[Share] " + ScoreManager.Instance.GetShareText());
            });

            gameOverPanel.SetActive(false);
        }

        // ────────────────────────────────────────
        //  일시정지 버튼 (좌하단)
        // ────────────────────────────────────────
        private void CreatePauseButton()
        {
            var pauseBtn = CreateStyledButton(canvas.transform, "PauseButton", "II",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(56f, 56f), new Vector2(80f, 80f),
                new Color(0.2f, 0.2f, 0.3f, 0.7f), 32);

            pauseBtn.onClick.AddListener(() =>
            {
                if (GameManager.Instance.State == GameState.Playing)
                {
                    GameManager.Instance.PauseGame();
                    pausePanel.SetActive(true);
                }
            });
        }

        // ────────────────────────────────────────
        //  일시정지 패널
        // ────────────────────────────────────────
        private void CreatePausePanel()
        {
            pausePanel = CreateFullScreenOverlay("PausePanel", new Color(0.02f, 0.02f, 0.06f, 0.8f));

            var card = CreateRoundedPanel("PauseCard", pausePanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(480f, 340f),
                new Vector2(0.5f, 0.5f), panelDark);

            CreateStyledText(card.transform, "PauseTitle", "일시정지",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -45f), new Vector2(400f, 60f),
                48, TextAnchor.MiddleCenter, Color.white);

            CreateDivider(card.transform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), 380f);

            var resumeBtn = CreateStyledButton(card.transform, "ResumeButton", "계속하기",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(360f, 72f),
                accentGreen, 30);

            resumeBtn.onClick.AddListener(() =>
            {
                GameManager.Instance.ResumeGame();
                pausePanel.SetActive(false);
            });

            var quitBtn = CreateStyledButton(card.transform, "QuitButton", "처음으로",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -240f), new Vector2(360f, 72f),
                accentRed, 30);

            quitBtn.onClick.AddListener(() =>
            {
                pausePanel.SetActive(false);
                GameManager.Instance.RestartGame();
            });

            pausePanel.SetActive(false);
        }

        // ────────────────────────────────────────
        //  이벤트 핸들러
        // ────────────────────────────────────────
        private void OnStateChanged(GameState state)
        {
            startPanel.SetActive(state == GameState.Ready);
            gameOverPanel.SetActive(state == GameState.GameOver);
            hudGroup.SetActive(state == GameState.Playing || state == GameState.Reviving);

            if (state == GameState.GameOver)
            {
                int score = ScoreManager.Instance.CurrentScore;
                gameOverScoreText.text = score.ToString("N0");
                gameOverBestText.text = $"BEST  {ScoreManager.Instance.BestScore:N0}";
                gameOverHighestLevelText.text =
                    $"최고 등급: {GameConfig.GetName(ScoreManager.Instance.HighestLevel)}";
            }

            if (state == GameState.Playing)
            {
                pausePanel.SetActive(false);
                comboText.text = "";
            }
        }

        private void UpdateScore(int score)
        {
            scoreText.text = score.ToString("N0");
            StartCoroutine(ScalePunch(scoreText.rectTransform, 1.25f));
        }

        private void UpdateBestScore(int best)
        {
            bestScoreText.text = $"BEST  {best:N0}";
        }

        private void UpdateNextPreview(int level)
        {
            if (nextPreviewCircle != null)
                nextPreviewCircle.color = GameConfig.GetColor(level);
            if (nextPreviewLabel != null)
                nextPreviewLabel.text = GameConfig.GetShortName(level);
        }

        private void OnMergeHappened(int newLevel, Vector3 worldPos, int combo)
        {
            ScoreManager.Instance.UpdateHighestLevel(newLevel);

            if (combo > 1)
            {
                comboText.text = $"x{combo} COMBO!";
                StartCoroutine(ScalePunch(comboText.rectTransform, 1.4f));
                StartCoroutine(FadeOutCombo());
            }

            int score = GameConfig.Instance.CalculateMergeScore(newLevel, combo);
            ShowFloatingScore(worldPos, score, GameConfig.GetColor(newLevel));
        }

        // ────────────────────────────────────────
        //  F3: 피버 UI
        // ────────────────────────────────────────
        private void CreateFeverBorders()
        {
            feverBorders = new Image[4];
            string[] names = { "FeverTop", "FeverRight", "FeverBottom", "FeverLeft" };
            // anchorMin, anchorMax, offsetMin, offsetMax
            Vector2[][] anchors = {
                new[] { new Vector2(0, 1), new Vector2(1, 1) }, // top
                new[] { new Vector2(1, 0), new Vector2(1, 1) }, // right
                new[] { new Vector2(0, 0), new Vector2(1, 0) }, // bottom
                new[] { new Vector2(0, 0), new Vector2(0, 1) }, // left
            };
            Vector2[][] offsets = {
                new[] { new Vector2(0, -20), new Vector2(0, 0) },    // top
                new[] { new Vector2(-20, 0), new Vector2(0, 0) },    // right
                new[] { new Vector2(0, 0), new Vector2(0, 20) },     // bottom
                new[] { new Vector2(0, 0), new Vector2(20, 0) },     // left
            };

            for (int i = 0; i < 4; i++)
            {
                var obj = new GameObject(names[i]);
                obj.transform.SetParent(canvas.transform, false);
                var rt = obj.AddComponent<RectTransform>();
                rt.anchorMin = anchors[i][0];
                rt.anchorMax = anchors[i][1];
                rt.offsetMin = offsets[i][0];
                rt.offsetMax = offsets[i][1];

                var img = obj.AddComponent<Image>();
                img.color = Color.clear;
                img.raycastTarget = false;
                feverBorders[i] = img;
                obj.SetActive(false);
            }
        }

        private void OnFeverStart()
        {
            feverUIActive = true;
            if (feverBorders != null)
            {
                foreach (var border in feverBorders)
                    if (border != null) border.gameObject.SetActive(true);
            }
        }

        private void OnFeverEnd()
        {
            feverUIActive = false;
            if (feverBorders != null)
            {
                foreach (var border in feverBorders)
                {
                    if (border != null)
                    {
                        border.color = Color.clear;
                        border.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void Update()
        {
            if (feverUIActive && feverBorders != null)
            {
                float hue = Mathf.PingPong(Time.time * 0.5f, 1f);
                Color rainbow = Color.HSVToRGB(hue, 0.8f, 1f);
                rainbow.a = 0.4f;

                foreach (var border in feverBorders)
                {
                    if (border != null)
                        border.color = rainbow;
                }
            }
        }

        // ────────────────────────────────────────
        //  플로팅 점수
        // ────────────────────────────────────────
        private void ShowFloatingScore(Vector3 worldPos, int score, Color color)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            var floatObj = new GameObject("FloatingScore");
            floatObj.transform.SetParent(canvas.transform, false);

            var txt = floatObj.AddComponent<Text>();
            txt.text = $"+{score}";
            txt.fontSize = 40;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (koreanFont != null) txt.font = koreanFont;
            AddOutline(floatObj, new Color(0f, 0f, 0f, 0.7f), new Vector2(1.5f, -1.5f));

            var rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240f, 60f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, null, out Vector2 localPoint);
            rt.anchoredPosition = localPoint;

            StartCoroutine(FloatAndFade(floatObj, rt, txt));
        }

        private IEnumerator FloatAndFade(GameObject obj, RectTransform rt, Text txt)
        {
            float duration = 1.0f;
            float elapsed = 0f;
            Vector2 startPos = rt.anchoredPosition;
            Color startColor = txt.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float ease = 1f - (1f - t) * (1f - t); // easeOutQuad

                rt.anchoredPosition = startPos + new Vector2(0f, ease * 120f);
                float scale = 1f + t * 0.3f;
                rt.localScale = new Vector3(scale, scale, 1f);
                txt.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t * t);

                yield return null;
            }

            Destroy(obj);
        }

        private IEnumerator ScalePunch(RectTransform rt, float punchScale)
        {
            Vector3 original = Vector3.one;
            Vector3 punch = original * punchScale;
            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // overshoot bounce
                float ease = t < 0.5f
                    ? 4f * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                rt.localScale = Vector3.Lerp(punch, original, ease);
                yield return null;
            }

            rt.localScale = original;
        }

        private IEnumerator FadeOutCombo()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (comboText != null)
                comboText.text = "";
        }

        // ════════════════════════════════════════
        //  UI 생성 헬퍼
        // ════════════════════════════════════════

        private GameObject CreateFullScreenOverlay(string name, Color bgColor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(canvas.transform, false);

            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.color = bgColor;

            return obj;
        }

        private GameObject CreateRoundedPanel(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Vector2 pivot, Color bgColor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = obj.AddComponent<Image>();
            img.color = bgColor;

            // 테두리 효과 (밝은 엣지)
            var borderObj = new GameObject("Border");
            borderObj.transform.SetParent(obj.transform, false);
            var borderRT = borderObj.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = Vector2.zero;
            borderRT.offsetMax = Vector2.zero;

            var borderOutline = borderObj.AddComponent<Outline>();
            borderOutline.effectColor = new Color(1f, 1f, 1f, 0.08f);
            borderOutline.effectDistance = new Vector2(1f, -1f);

            // 배경을 위한 Image (Outline이 동작하려면 Graphic 필요)
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = Color.clear;

            return obj;
        }

        private void CreateDivider(Transform parent, Vector2 anchor, Vector2 pos, float width)
        {
            var divObj = new GameObject("Divider");
            divObj.transform.SetParent(parent, false);

            var rt = divObj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 2f);

            var img = divObj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.1f);
        }

        private Text CreateStyledText(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta,
            int fontSize, TextAnchor alignment, Color color)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            var txt = textObj.AddComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            txt.fontStyle = FontStyle.Bold;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (koreanFont != null) txt.font = koreanFont;

            var rt = txt.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            return txt;
        }

        private void SetAnchorsAndStretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private Button CreateStyledButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Color bgColor, int fontSize = 30)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            var rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;

            // 버튼 텍스트
            var textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            var txt = textObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontStyle = FontStyle.Bold;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (koreanFont != null) txt.font = koreanFont;
            AddOutline(textObj, new Color(0f, 0f, 0f, 0.4f), new Vector2(1f, -1f));

            var textRt = txt.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return btn;
        }

        private static void AddOutline(GameObject obj, Color color, Vector2 distance)
        {
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        // ── 원형 스프라이트 (NEXT 미리보기용) ──
        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;

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
                    float alpha = Mathf.Clamp01(center - dist);
                    pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            circleSprite = Sprite.Create(tex, new Rect(0, 0, res, res),
                new Vector2(0.5f, 0.5f), res);
            return circleSprite;
        }
    }
}
