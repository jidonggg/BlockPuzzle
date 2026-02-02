using UnityEngine;
using System;
using MergeDrop.Data;

namespace MergeDrop.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        public int CurrentScore { get; private set; }
        public int BestScore { get; private set; }
        public int TotalGames { get; private set; }
        public int HighestLevel { get; private set; }

        public event Action<int> OnScoreChanged;
        public event Action<int> OnBestScoreChanged;
        public event Action<int> OnHighestLevelChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // PlayerPrefs에서 로드
            BestScore = PlayerPrefs.GetInt("BestScore", 0);
            TotalGames = PlayerPrefs.GetInt("TotalGames", 0);
            HighestLevel = PlayerPrefs.GetInt("HighestLevel", 0);
        }

        public void AddScore(int amount)
        {
            CurrentScore += amount;
            OnScoreChanged?.Invoke(CurrentScore);

            if (CurrentScore > BestScore)
            {
                BestScore = CurrentScore;
                PlayerPrefs.SetInt("BestScore", BestScore);
                OnBestScoreChanged?.Invoke(BestScore);
            }
        }

        public void UpdateHighestLevel(int level)
        {
            if (level > HighestLevel)
            {
                HighestLevel = level;
                PlayerPrefs.SetInt("HighestLevel", HighestLevel);
                OnHighestLevelChanged?.Invoke(HighestLevel);
            }
        }

        public void ResetCurrentScore()
        {
            CurrentScore = 0;
            OnScoreChanged?.Invoke(CurrentScore);
        }

        public void OnGameEnd()
        {
            TotalGames++;
            PlayerPrefs.SetInt("TotalGames", TotalGames);
            PlayerPrefs.Save();
        }

        public string GetShareText()
        {
            return $"합쳐봐!\n점수: {CurrentScore:N0}\n최고 등급: {GameConfig.GetName(HighestLevel)}\n최고 점수: {BestScore:N0}";
        }
    }
}
