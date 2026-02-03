using UnityEngine;
using System;
using MergeDrop.Data;

namespace MergeDrop.Core
{
    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        public float CurrentGravityScale { get; private set; }
        public int CurrentTierIndex => currentTierIndex;
        public int TotalTiers => tiers.Length;

        public event Action<int> OnTierChanged; // tierIndex

        // Difficulty tiers
        private struct DifficultyTier
        {
            public int scoreThreshold;
            public int minDropLevel;
            public int maxDropLevel;
            public float gravityScale;
            public float[] dropWeights;
        }

        // Obstacle spawn interval (seconds) per tier; 0 = no obstacles
        public float CurrentObstacleInterval { get; private set; }

        private static readonly DifficultyTier[] tiers = new DifficultyTier[]
        {
            new DifficultyTier {  // Wave 1: 입문
                scoreThreshold = 0,
                minDropLevel = 0, maxDropLevel = 2,
                gravityScale = 5.0f,
                dropWeights = new float[] { 40f, 35f, 25f }
            },
            new DifficultyTier {  // Wave 2: 본격 시작
                scoreThreshold = 300,
                minDropLevel = 0, maxDropLevel = 3,
                gravityScale = 6.5f,
                dropWeights = new float[] { 25f, 30f, 25f, 20f }
            },
            new DifficultyTier {  // Wave 3: 압박 시작
                scoreThreshold = 1000,
                minDropLevel = 0, maxDropLevel = 4,
                gravityScale = 8.5f,
                dropWeights = new float[] { 15f, 25f, 30f, 20f, 10f }
            },
            new DifficultyTier {  // Wave 4: 고난이도
                scoreThreshold = 2500,
                minDropLevel = 1, maxDropLevel = 4,
                gravityScale = 11.0f,
                dropWeights = new float[] { 20f, 25f, 30f, 25f }
            },
            new DifficultyTier {  // Wave 5: 극한
                scoreThreshold = 5000,
                minDropLevel = 1, maxDropLevel = 5,
                gravityScale = 14.0f,
                dropWeights = new float[] { 10f, 20f, 30f, 25f, 15f }
            },
            new DifficultyTier {  // Wave 6: 지옥
                scoreThreshold = 10000,
                minDropLevel = 2, maxDropLevel = 5,
                gravityScale = 17.0f,
                dropWeights = new float[] { 15f, 25f, 30f, 30f }
            }
        };

        // Obstacle intervals per tier (seconds; 0 = none)
        private static readonly float[] obstacleIntervals = { 0f, 20f, 14f, 10f, 7f, 5f };

        private int currentTierIndex;
        private float obstacleTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CurrentGravityScale = GameConfig.Instance.gravityScale;
        }

        private void Start()
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (CurrentObstacleInterval <= 0f) return;

            obstacleTimer += Time.deltaTime;
            if (obstacleTimer >= CurrentObstacleInterval)
            {
                obstacleTimer = 0f;
                SpawnObstacle();
            }
        }

        private void SpawnObstacle()
        {
            if (ObjectSpawner.Instance == null) return;
            var config = GameConfig.Instance;
            float x = UnityEngine.Random.Range(config.dropMinX + 0.5f, config.dropMaxX - 0.5f);
            float y = config.dropY + 0.5f;
            float size = UnityEngine.Random.Range(0.5f, 0.9f);
            ObjectSpawner.Instance.SpawnStone(x, y, size);
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
        }

        private void OnScoreChanged(int score)
        {
            UpdateDifficulty(score);
        }

        private void UpdateDifficulty(int score)
        {
            // Find current tier
            int newTierIndex = 0;
            for (int i = tiers.Length - 1; i >= 0; i--)
            {
                if (score >= tiers[i].scoreThreshold)
                {
                    newTierIndex = i;
                    break;
                }
            }

            // Interpolate gravity between tiers
            if (newTierIndex < tiers.Length - 1)
            {
                float tierStart = tiers[newTierIndex].scoreThreshold;
                float tierEnd = tiers[newTierIndex + 1].scoreThreshold;
                float t = Mathf.InverseLerp(tierStart, tierEnd, score);
                CurrentGravityScale = Mathf.Lerp(
                    tiers[newTierIndex].gravityScale,
                    tiers[newTierIndex + 1].gravityScale, t);
            }
            else
            {
                CurrentGravityScale = tiers[newTierIndex].gravityScale;
            }

            // Obstacle interval
            CurrentObstacleInterval = obstacleIntervals[newTierIndex];

            if (newTierIndex != currentTierIndex)
            {
                currentTierIndex = newTierIndex;
                OnTierChanged?.Invoke(currentTierIndex);
            }
            else
            {
                currentTierIndex = newTierIndex;
            }

            // Update all active objects' gravity
            if (ObjectSpawner.Instance != null)
            {
                var activeObjects = ObjectSpawner.Instance.GetActiveObjects();
                foreach (var obj in activeObjects)
                {
                    if (obj != null)
                        obj.UpdateGravity(CurrentGravityScale);
                }
            }
        }

        public int GetRandomDropLevel()
        {
            var tier = tiers[currentTierIndex];
            float[] weights = tier.dropWeights;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];

            float rand = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (rand <= cumulative)
                    return tier.minDropLevel + i;
            }
            return tier.minDropLevel;
        }

        public int GetTierScoreThreshold(int tierIndex)
        {
            if (tierIndex < 0 || tierIndex >= tiers.Length) return 0;
            return tiers[tierIndex].scoreThreshold;
        }

        public int GetNextTierThreshold()
        {
            if (currentTierIndex < tiers.Length - 1)
                return tiers[currentTierIndex + 1].scoreThreshold;
            return -1; // no next tier
        }

        public void ResetDifficulty()
        {
            currentTierIndex = 0;
            CurrentGravityScale = tiers[0].gravityScale;
            CurrentObstacleInterval = obstacleIntervals[0];
            obstacleTimer = 0f;
        }
    }
}
