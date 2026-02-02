using UnityEngine;
using MergeDrop.Data;

namespace MergeDrop.Core
{
    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        public float CurrentGravityScale { get; private set; }

        // Difficulty tiers
        private struct DifficultyTier
        {
            public int scoreThreshold;
            public int minDropLevel;
            public int maxDropLevel;
            public float gravityScale;
            public float[] dropWeights;
        }

        private static readonly DifficultyTier[] tiers = new DifficultyTier[]
        {
            new DifficultyTier {
                scoreThreshold = 0,
                minDropLevel = 0, maxDropLevel = 3,
                gravityScale = 5.0f,
                dropWeights = new float[] { 35f, 30f, 20f, 15f }
            },
            new DifficultyTier {
                scoreThreshold = 2000,
                minDropLevel = 0, maxDropLevel = 4,
                gravityScale = 5.3f,
                dropWeights = new float[] { 25f, 30f, 25f, 15f, 5f }
            },
            new DifficultyTier {
                scoreThreshold = 5000,
                minDropLevel = 1, maxDropLevel = 4,
                gravityScale = 5.7f,
                dropWeights = new float[] { 30f, 30f, 25f, 15f }
            },
            new DifficultyTier {
                scoreThreshold = 10000,
                minDropLevel = 1, maxDropLevel = 5,
                gravityScale = 6.5f,
                dropWeights = new float[] { 20f, 30f, 25f, 15f, 10f }
            }
        };

        private int currentTierIndex;

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

            currentTierIndex = newTierIndex;

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

            float rand = Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (rand <= cumulative)
                    return tier.minDropLevel + i;
            }
            return tier.minDropLevel;
        }

        public void ResetDifficulty()
        {
            currentTierIndex = 0;
            CurrentGravityScale = tiers[0].gravityScale;
        }
    }
}
