using UnityEngine;

namespace MergeDrop.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "MergeDrop/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        private static GameConfig instance;
        public static GameConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameConfig>("GameConfig");
                    if (instance == null)
                    {
                        instance = CreateInstance<GameConfig>();
                        instance.name = "GameConfig_Runtime";
                    }
                }
                return instance;
            }
        }

        // ── 물리 (속도감 있게) ──
        public float gravityScale = 5.0f;
        public float physicsFriction = 0.3f;
        public float physicsBounciness = 0.3f;
        public float linearDrag = 0.2f;
        public float angularDrag = 0.3f;

        // ── 컨테이너 (카메라에 꽉 차게) ──
        public float containerWidth = 5.0f;
        public float containerHeight = 8.5f;
        public float containerBottomY = -3.8f;
        public float gameOverLineY = 4.0f;
        public float gameOverDelay = 2.0f;

        // ── 드롭 ──
        public float dropY = 4.8f;
        public float dropCooldown = 0.3f;
        public float dropMinX = -2.3f;
        public float dropMaxX = 2.3f;

        // ── 머지 ──
        public float mergeAnimDuration = 0.1f;
        public float comboTimeWindow = 1.5f;
        public float chainBonusBase = 5f;

        // ── 이펙트 ──
        public float screenShakeIntensity = 0.15f;
        public float screenShakeDuration = 0.2f;

        // ── 등급 데이터 ──
        public static readonly int MaxLevel = 10;

        public static readonly string[] levelNames =
        {
            "체리", "딸기", "포도", "귤", "사과",
            "배", "복숭아", "파인애플", "멜론", "수박", "★대박"
        };

        public static readonly string[] levelShortNames =
        {
            "체리", "딸기", "포도", "귤", "사과",
            "배", "복숭아", "파인", "멜론", "수박", "★"
        };

        public static readonly float[] levelSizes =
        {
            0.45f, 0.58f, 0.72f, 0.88f, 1.05f,
            1.25f, 1.45f, 1.68f, 1.92f, 2.20f, 2.50f
        };

        public static readonly int[] levelScores =
        {
            1, 3, 6, 10, 15,
            21, 28, 36, 45, 55, 100
        };

        public static readonly string[] levelColorHex =
        {
            "#FF6B6B", "#FF8EB4", "#9B59B6", "#F39C12", "#E74C3C",
            "#C6E84D", "#FFAA88", "#FFD93D", "#2ECC71", "#27AE60", "#FFD700"
        };

        // ── 드롭 가중치 (등급 0~4) ──
        public static readonly float[] dropWeights = { 35f, 30f, 20f, 10f, 5f };
        public static readonly int maxDropLevel = 4;

        // ── 헬퍼 메서드 ──

        public static float GetSize(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            return levelSizes[level];
        }

        public static int GetScore(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            return levelScores[level];
        }

        public static Color GetColor(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            if (ColorUtility.TryParseHtmlString(levelColorHex[level], out Color color))
                return color;
            return Color.white;
        }

        public static string GetName(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            return levelNames[level];
        }

        public static string GetShortName(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            return levelShortNames[level];
        }

        public static int GetRandomDropLevel()
        {
            float total = 0f;
            for (int i = 0; i < dropWeights.Length; i++)
                total += dropWeights[i];

            float rand = Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < dropWeights.Length; i++)
            {
                cumulative += dropWeights[i];
                if (rand <= cumulative) return i;
            }
            return 0;
        }

        public int CalculateMergeScore(int newLevel, int combo)
        {
            int baseScore = GetScore(newLevel);
            float comboMultiplier = 1f + Mathf.Max(0, combo - 1) * 0.5f;
            float chainBonus = chainBonusBase * Mathf.Max(0, combo - 1);
            return Mathf.RoundToInt(baseScore * comboMultiplier + chainBonus);
        }
    }
}
