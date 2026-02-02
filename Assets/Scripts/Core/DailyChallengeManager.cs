using UnityEngine;
using System;

namespace MergeDrop.Core
{
    public enum ChallengeType { Score, CreateApples, Combo, NoSkill }

    public class DailyChallengeManager : MonoBehaviour
    {
        public static DailyChallengeManager Instance { get; private set; }

        public event Action<float> OnProgressChanged; // normalized 0~1
        public event Action OnChallengeCompleted;

        public ChallengeType CurrentChallenge { get; private set; }
        public string ChallengeDescription { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool RewardPending { get; private set; }
        public float Progress { get; private set; }

        private int targetValue;
        private int currentValue;
        private bool usedSkill;

        private static readonly ChallengeType[] challengeTypes = {
            ChallengeType.Score,
            ChallengeType.CreateApples,
            ChallengeType.Combo,
            ChallengeType.NoSkill
        };

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
            LoadOrGenerateChallenge();

            if (MergeSystem.Instance != null)
                MergeSystem.Instance.OnMerge += OnMerge;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
        }

        private void OnDestroy()
        {
            if (MergeSystem.Instance != null)
                MergeSystem.Instance.OnMerge -= OnMerge;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
        }

        private void LoadOrGenerateChallenge()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            string lastDate = PlayerPrefs.GetString("LastChallengeDate", "");

            if (lastDate == today)
            {
                // Load existing
                IsCompleted = PlayerPrefs.GetInt("ChallengeCompleted", 0) == 1;
                RewardPending = PlayerPrefs.GetInt("RewardPending", 0) == 1;
                int typeIdx = PlayerPrefs.GetInt("ChallengeType", 0);
                CurrentChallenge = challengeTypes[typeIdx % challengeTypes.Length];
                targetValue = PlayerPrefs.GetInt("ChallengeTarget", 3000);
            }
            else
            {
                // Generate new
                int seed = today.GetHashCode();
                var rng = new System.Random(seed);

                int typeIdx = rng.Next(0, challengeTypes.Length);
                CurrentChallenge = challengeTypes[typeIdx];

                switch (CurrentChallenge)
                {
                    case ChallengeType.Score:
                        targetValue = 3000;
                        break;
                    case ChallengeType.CreateApples:
                        targetValue = 3; // 사과(Lv4) 3개
                        break;
                    case ChallengeType.Combo:
                        targetValue = 5;
                        break;
                    case ChallengeType.NoSkill:
                        targetValue = 2000; // 스킬 미사용으로 2000점
                        break;
                }

                IsCompleted = false;
                RewardPending = false;

                PlayerPrefs.SetString("LastChallengeDate", today);
                PlayerPrefs.SetInt("ChallengeCompleted", 0);
                PlayerPrefs.SetInt("RewardPending", 0);
                PlayerPrefs.SetInt("ChallengeType", typeIdx);
                PlayerPrefs.SetInt("ChallengeTarget", targetValue);
                PlayerPrefs.Save();
            }

            ChallengeDescription = GetDescription();
            currentValue = 0;
            usedSkill = false;
            Progress = IsCompleted ? 1f : 0f;
        }

        private string GetDescription()
        {
            switch (CurrentChallenge)
            {
                case ChallengeType.Score:
                    return $"{targetValue:N0}점 달성";
                case ChallengeType.CreateApples:
                    return $"사과 {targetValue}개 만들기";
                case ChallengeType.Combo:
                    return $"{targetValue}콤보 달성";
                case ChallengeType.NoSkill:
                    return $"스킬 미사용 {targetValue:N0}점";
                default:
                    return "도전!";
            }
        }

        private void OnScoreChanged(int score)
        {
            if (IsCompleted) return;

            if (CurrentChallenge == ChallengeType.Score)
            {
                currentValue = score;
                UpdateProgress();
            }
            else if (CurrentChallenge == ChallengeType.NoSkill && !usedSkill)
            {
                currentValue = score;
                UpdateProgress();
            }
        }

        private void OnMerge(int newLevel, Vector3 pos, int combo)
        {
            if (IsCompleted) return;

            if (CurrentChallenge == ChallengeType.CreateApples && newLevel == 4)
            {
                currentValue++;
                UpdateProgress();
            }
            else if (CurrentChallenge == ChallengeType.Combo)
            {
                if (combo > currentValue)
                {
                    currentValue = combo;
                    UpdateProgress();
                }
            }
        }

        public void NotifySkillUsed()
        {
            usedSkill = true;
            if (CurrentChallenge == ChallengeType.NoSkill)
            {
                currentValue = 0;
                Progress = 0f;
                OnProgressChanged?.Invoke(0f);
            }
        }

        private void UpdateProgress()
        {
            Progress = Mathf.Clamp01((float)currentValue / targetValue);
            OnProgressChanged?.Invoke(Progress);

            if (currentValue >= targetValue && !IsCompleted)
            {
                CompleteChallenge();
            }
        }

        private void CompleteChallenge()
        {
            IsCompleted = true;
            RewardPending = true;
            Progress = 1f;

            PlayerPrefs.SetInt("ChallengeCompleted", 1);
            PlayerPrefs.SetInt("RewardPending", 1);
            PlayerPrefs.Save();

            OnChallengeCompleted?.Invoke();
        }

        /// <summary>
        /// Called at game start to consume pending reward.
        /// </summary>
        public bool TryConsumeReward()
        {
            if (!RewardPending) return false;

            RewardPending = false;
            PlayerPrefs.SetInt("RewardPending", 0);
            PlayerPrefs.Save();

            // Grant free skill charge
            if (SkillManager.Instance != null)
                SkillManager.Instance.GrantFreeCharge(SkillType.Shake);

            return true;
        }

        public void ResetForNewGame()
        {
            currentValue = 0;
            usedSkill = false;
            if (!IsCompleted)
            {
                Progress = 0f;
                OnProgressChanged?.Invoke(0f);
            }
        }
    }
}
