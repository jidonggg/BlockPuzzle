using UnityEngine;
using System;

namespace MergeDrop.Core
{
    public class FeverManager : MonoBehaviour
    {
        public static FeverManager Instance { get; private set; }

        public event Action OnFeverStart;
        public event Action OnFeverEnd;
        public event Action<float> OnFeverTimerUpdate; // normalized 0~1

        public bool IsFever { get; private set; }

        private int consecutiveCombo;
        private float feverTimer;
        private float lastMergeTime;

        private const int ComboThreshold = 5;
        private const float FeverDuration = 3f;
        private const float ComboWindow = 1.5f;
        private const float ScoreMultiplier = 2f;
        private const float CooldownMultiplier = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void OnMergePerformed()
        {
            float now = Time.time;

            if (now - lastMergeTime < ComboWindow)
                consecutiveCombo++;
            else
                consecutiveCombo = 1;

            lastMergeTime = now;

            if (!IsFever && consecutiveCombo >= ComboThreshold)
            {
                StartFever();
            }
        }

        private void Update()
        {
            if (!IsFever) return;

            feverTimer -= Time.deltaTime;
            float normalized = Mathf.Clamp01(feverTimer / FeverDuration);
            OnFeverTimerUpdate?.Invoke(normalized);

            if (feverTimer <= 0f)
            {
                EndFever();
            }
        }

        private void StartFever()
        {
            IsFever = true;
            feverTimer = FeverDuration;
            consecutiveCombo = 0;

            OnFeverStart?.Invoke();

            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayFeverSound();
        }

        private void EndFever()
        {
            IsFever = false;
            feverTimer = 0f;

            OnFeverEnd?.Invoke();
        }

        public float GetScoreMultiplier()
        {
            return IsFever ? ScoreMultiplier : 1f;
        }

        public float GetCooldownMultiplier()
        {
            return IsFever ? CooldownMultiplier : 1f;
        }

        public void ResetFever()
        {
            IsFever = false;
            feverTimer = 0f;
            consecutiveCombo = 0;
            lastMergeTime = 0f;
        }
    }
}
