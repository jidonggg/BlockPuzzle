using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MergeDrop.Core;

namespace MergeDrop.UI
{
    public class SkillUI : MonoBehaviour
    {
        public static SkillUI Instance { get; private set; }

        private Canvas canvas;
        private GameObject skillPanel;
        private GameObject selectionOverlay;
        private Image[] chargeGauges = new Image[3];
        private Image[] buttonBgs = new Image[3];
        private Button[] buttons = new Button[3];
        private Text selectionText;

        private static readonly string[] skillNames = { "흔들기", "강등", "폭탄" };
        private static readonly string[] skillIcons = { "~", "▼", "✸" };
        private static readonly Color readyGlow = new Color(1f, 0.84f, 0f, 0.9f);
        private static readonly Color notReadyColor = new Color(0.3f, 0.3f, 0.4f, 0.8f);

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
            CreateSkillButtons();
            CreateSelectionOverlay();

            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.OnChargeChanged += OnChargeChanged;
                SkillManager.Instance.OnSkillReady += OnSkillReady;
                SkillManager.Instance.OnSelectionModeChanged += OnSelectionModeChanged;
            }

            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (SkillManager.Instance != null)
            {
                SkillManager.Instance.OnChargeChanged -= OnChargeChanged;
                SkillManager.Instance.OnSkillReady -= OnSkillReady;
                SkillManager.Instance.OnSelectionModeChanged -= OnSelectionModeChanged;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= OnStateChanged;
        }

        private void CreateSkillButtons()
        {
            skillPanel = new GameObject("SkillPanel");
            skillPanel.transform.SetParent(canvas.transform, false);
            var panelRT = skillPanel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0f);
            panelRT.anchorMax = new Vector2(0.5f, 0f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = new Vector2(0f, 20f);
            panelRT.sizeDelta = new Vector2(360f, 90f);

            float spacing = 110f;
            float startX = -spacing;

            for (int i = 0; i < 3; i++)
            {
                int idx = i; // capture for closure
                var btnObj = new GameObject($"Skill_{skillNames[i]}");
                btnObj.transform.SetParent(skillPanel.transform, false);

                var btnRT = btnObj.AddComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(0.5f, 0.5f);
                btnRT.anchorMax = new Vector2(0.5f, 0.5f);
                btnRT.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                btnRT.sizeDelta = new Vector2(80f, 80f);

                // Background (circular feel)
                var bgImg = btnObj.AddComponent<Image>();
                bgImg.color = notReadyColor;
                buttonBgs[i] = bgImg;

                var btn = btnObj.AddComponent<Button>();
                btn.targetGraphic = bgImg;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
                colors.fadeDuration = 0.05f;
                btn.colors = colors;
                btn.onClick.AddListener(() => OnSkillButtonClicked(idx));
                buttons[i] = btn;

                // Icon text
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(btnObj.transform, false);
                var iconTxt = iconObj.AddComponent<Text>();
                iconTxt.text = skillIcons[i];
                iconTxt.fontSize = 30;
                iconTxt.alignment = TextAnchor.MiddleCenter;
                iconTxt.color = Color.white;
                iconTxt.fontStyle = FontStyle.Bold;
                if (UIManager.GetFont() != null) iconTxt.font = UIManager.GetFont();
                var iconRT = iconTxt.rectTransform;
                iconRT.anchorMin = Vector2.zero;
                iconRT.anchorMax = Vector2.one;
                iconRT.offsetMin = new Vector2(0f, 15f);
                iconRT.offsetMax = Vector2.zero;

                // Name text
                var nameObj = new GameObject("Name");
                nameObj.transform.SetParent(btnObj.transform, false);
                var nameTxt = nameObj.AddComponent<Text>();
                nameTxt.text = skillNames[i];
                nameTxt.fontSize = 14;
                nameTxt.alignment = TextAnchor.MiddleCenter;
                nameTxt.color = new Color(0.8f, 0.8f, 0.9f);
                if (UIManager.GetFont() != null) nameTxt.font = UIManager.GetFont();
                var nameRT = nameTxt.rectTransform;
                nameRT.anchorMin = new Vector2(0f, 0f);
                nameRT.anchorMax = new Vector2(1f, 0.25f);
                nameRT.offsetMin = Vector2.zero;
                nameRT.offsetMax = Vector2.zero;

                // Charge gauge (fill overlay)
                var gaugeObj = new GameObject("Gauge");
                gaugeObj.transform.SetParent(btnObj.transform, false);
                var gaugeImg = gaugeObj.AddComponent<Image>();
                gaugeImg.color = new Color(1f, 0.84f, 0f, 0.3f);
                gaugeImg.type = Image.Type.Filled;
                gaugeImg.fillMethod = Image.FillMethod.Vertical;
                gaugeImg.fillOrigin = 0; // bottom
                gaugeImg.fillAmount = 0f;
                gaugeImg.raycastTarget = false;
                chargeGauges[i] = gaugeImg;
                var gaugeRT = gaugeImg.rectTransform;
                gaugeRT.anchorMin = Vector2.zero;
                gaugeRT.anchorMax = Vector2.one;
                gaugeRT.offsetMin = Vector2.zero;
                gaugeRT.offsetMax = Vector2.zero;
            }

            skillPanel.SetActive(false);
        }

        private void CreateSelectionOverlay()
        {
            selectionOverlay = new GameObject("SelectionOverlay");
            selectionOverlay.transform.SetParent(canvas.transform, false);

            var overlayRT = selectionOverlay.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            var overlayImg = selectionOverlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.4f);
            overlayImg.raycastTarget = true;

            // "대상을 터치하세요" text
            var textObj = new GameObject("SelectionText");
            textObj.transform.SetParent(selectionOverlay.transform, false);
            selectionText = textObj.AddComponent<Text>();
            selectionText.text = "대상을 터치하세요";
            selectionText.fontSize = 36;
            selectionText.alignment = TextAnchor.MiddleCenter;
            selectionText.color = Color.white;
            selectionText.fontStyle = FontStyle.Bold;
            if (UIManager.GetFont() != null) selectionText.font = UIManager.GetFont();
            var textRT = selectionText.rectTransform;
            textRT.anchorMin = new Vector2(0.5f, 0.5f);
            textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.anchoredPosition = new Vector2(0f, 200f);
            textRT.sizeDelta = new Vector2(500f, 80f);

            // Cancel button
            var cancelObj = new GameObject("CancelButton");
            cancelObj.transform.SetParent(selectionOverlay.transform, false);
            var cancelRT = cancelObj.AddComponent<RectTransform>();
            cancelRT.anchorMin = new Vector2(0.5f, 0f);
            cancelRT.anchorMax = new Vector2(0.5f, 0f);
            cancelRT.anchoredPosition = new Vector2(0f, 100f);
            cancelRT.sizeDelta = new Vector2(200f, 60f);

            var cancelImg = cancelObj.AddComponent<Image>();
            cancelImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);

            var cancelBtn = cancelObj.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(() =>
            {
                if (SkillManager.Instance != null)
                    SkillManager.Instance.CancelSelection();
            });

            var cancelLabelObj = new GameObject("Label");
            cancelLabelObj.transform.SetParent(cancelObj.transform, false);
            var cancelLabel = cancelLabelObj.AddComponent<Text>();
            cancelLabel.text = "취소";
            cancelLabel.fontSize = 26;
            cancelLabel.alignment = TextAnchor.MiddleCenter;
            cancelLabel.color = Color.white;
            cancelLabel.fontStyle = FontStyle.Bold;
            if (UIManager.GetFont() != null) cancelLabel.font = UIManager.GetFont();
            var cancelLabelRT = cancelLabel.rectTransform;
            cancelLabelRT.anchorMin = Vector2.zero;
            cancelLabelRT.anchorMax = Vector2.one;
            cancelLabelRT.offsetMin = Vector2.zero;
            cancelLabelRT.offsetMax = Vector2.zero;

            selectionOverlay.SetActive(false);
        }

        private void Update()
        {
            if (SkillManager.Instance == null || !SkillManager.Instance.IsSelectionMode) return;

            // Handle touch/click to select object
            var pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                Vector2 screenPos = pointer.position.ReadValue();
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));

                var hit = Physics2D.OverlapCircle(worldPos, 0.3f);
                if (hit != null)
                {
                    var mergeable = hit.GetComponent<MergeableObject>();
                    if (mergeable != null && mergeable.CanMerge && !mergeable.IsMerging)
                    {
                        SkillManager.Instance.OnObjectSelected(mergeable);
                    }
                }
            }
        }

        private void OnSkillButtonClicked(int index)
        {
            if (SkillManager.Instance == null) return;
            SkillManager.Instance.ActivateSkill((SkillType)index);
        }

        private void OnChargeChanged(SkillType skill, int charge)
        {
            int idx = (int)skill;
            if (idx >= chargeGauges.Length || chargeGauges[idx] == null) return;

            int max = SkillManager.Instance.GetMaxCharge(skill);
            chargeGauges[idx].fillAmount = (float)charge / max;

            bool ready = charge >= max;
            buttonBgs[idx].color = ready ? readyGlow : notReadyColor;
        }

        private void OnSkillReady(SkillType skill)
        {
            int idx = (int)skill;
            if (idx >= buttonBgs.Length || buttonBgs[idx] == null) return;
            buttonBgs[idx].color = readyGlow;
        }

        private void OnSelectionModeChanged(bool entering)
        {
            if (selectionOverlay != null)
                selectionOverlay.SetActive(entering);
        }

        private void OnStateChanged(GameState state)
        {
            if (skillPanel != null)
                skillPanel.SetActive(state == GameState.Playing);

            if (state != GameState.Playing && selectionOverlay != null)
                selectionOverlay.SetActive(false);
        }
    }
}
