using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using MergeDrop.Data;
using MergeDrop.Effects;
using MergeDrop.Audio;

namespace MergeDrop.Core
{
    public class MergeSystem : MonoBehaviour
    {
        public static MergeSystem Instance { get; private set; }

        public event Action<int, Vector3, int> OnMerge; // (newLevel, position, combo)
        public event Action<Vector3> OnMaxMerge; // 최대등급 소멸

        private readonly HashSet<int> mergeInProgress = new HashSet<int>();
        private int comboCount;
        private float lastMergeTime;
        private readonly HashSet<int> reachedLevels = new HashSet<int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ResetCombo()
        {
            comboCount = 0;
            reachedLevels.Clear();
        }

        public void RequestMerge(MergeableObject a, MergeableObject b)
        {
            int idA = a.GetInstanceID();
            int idB = b.GetInstanceID();

            if (mergeInProgress.Contains(idA) || mergeInProgress.Contains(idB))
                return;

            mergeInProgress.Add(idA);
            mergeInProgress.Add(idB);
            StartCoroutine(ExecuteMerge(a, b));
        }

        public void RequestMaxMerge(MergeableObject a, MergeableObject b)
        {
            int idA = a.GetInstanceID();
            int idB = b.GetInstanceID();

            if (mergeInProgress.Contains(idA) || mergeInProgress.Contains(idB))
                return;

            mergeInProgress.Add(idA);
            mergeInProgress.Add(idB);
            StartCoroutine(ExecuteMaxMerge(a, b));
        }

        private IEnumerator ExecuteMerge(MergeableObject a, MergeableObject b)
        {
            if (a == null || b == null) yield break;

            int newLevel = a.Level + 1;
            if (newLevel > GameConfig.MaxLevel) yield break;

            // 1. 두 오브젝트 머지 시작
            a.StartMerge();
            b.StartMerge();

            Vector3 posA = a.transform.position;
            Vector3 posB = b.transform.position;
            Vector3 mergePos = (posA + posB) / 2f;

            Vector3 scaleA = a.transform.localScale;
            Vector3 scaleB = b.transform.localScale;

            float duration = GameConfig.Instance.mergeAnimDuration;
            float elapsed = 0f;

            // 2. 애니메이션: 두 오브젝트가 중앙으로 이동 + 축소
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseInBack(t);

                if (a != null)
                {
                    a.transform.position = Vector3.Lerp(posA, mergePos, eased);
                    a.transform.localScale = Vector3.Lerp(scaleA, scaleA * 0.3f, eased);
                }
                if (b != null)
                {
                    b.transform.position = Vector3.Lerp(posB, mergePos, eased);
                    b.transform.localScale = Vector3.Lerp(scaleB, scaleB * 0.3f, eased);
                }

                yield return null;
            }

            // 3. 콤보 계산
            float config_comboWindow = GameConfig.Instance.comboTimeWindow;
            if (Time.time - lastMergeTime < config_comboWindow)
                comboCount++;
            else
                comboCount = 1;
            lastMergeTime = Time.time;

            // 4. Capture golden state before pool return
            bool bWasGolden = b != null && b.IsGolden;
            bool bShouldTransfer = false;
            if (bWasGolden)
            {
                var goldenB = b.GetGoldenObject();
                bShouldTransfer = goldenB != null && goldenB.ShouldTransfer();
            }
            bool aWasGolden = a.IsGolden;
            bool aShouldTransfer = false;
            if (aWasGolden)
            {
                var goldenA = a.GetGoldenObject();
                aShouldTransfer = goldenA != null && goldenA.ShouldTransfer();
            }

            // 4b. B 오브젝트 풀 반환
            int idB = b.GetInstanceID();
            mergeInProgress.Remove(idB);
            ObjectSpawner.Instance.ReturnToPool(b);

            // 5. A 오브젝트를 상위 등급으로 업그레이드
            int idA = a.GetInstanceID();
            mergeInProgress.Remove(idA);
            a.transform.position = mergePos;
            a.UpgradeLevel(newLevel);

            // 6. 스케일 팝 애니메이션 (오버슈트 1.3x → 원래 크기)
            StartCoroutine(ScalePopAnimation(a, newLevel));

            // 7. 점수 계산: base → golden x5 → fever x2
            int score = GameConfig.Instance.CalculateMergeScore(newLevel, comboCount);

            // 첫 등급 도달 보너스
            bool isFirstReach = !reachedLevels.Contains(newLevel);
            if (isFirstReach)
            {
                reachedLevels.Add(newLevel);
                score += 100 * (newLevel + 1);
            }

            // F7: Golden multiplier (using pre-captured state)
            bool anyGolden = aWasGolden || bWasGolden;
            bool shouldTransfer = aShouldTransfer || bShouldTransfer;

            if (anyGolden)
            {
                score *= 5;
            }

            // F3: Fever multiplier
            if (FeverManager.Instance != null)
                score = Mathf.RoundToInt(score * FeverManager.Instance.GetScoreMultiplier());

            ScoreManager.Instance.AddScore(score);

            // F7: Transfer golden to result
            if (shouldTransfer)
            {
                var resultGolden = a.GetComponent<GoldenObject>();
                if (resultGolden != null)
                {
                    resultGolden.MakeTransferredGolden();
                    a.SetGoldenObject(resultGolden);
                }
            }
            else
            {
                a.ClearGolden();
            }

            // F3: Notify FeverManager of merge
            if (FeverManager.Instance != null)
                FeverManager.Instance.OnMergePerformed();

            // F5: Notify SkillManager of merge
            if (SkillManager.Instance != null)
                SkillManager.Instance.OnMergePerformed();

            // 8. 이벤트 발사
            OnMerge?.Invoke(newLevel, mergePos, comboCount);

            // 이펙트
            if (MergeEffect.Instance != null)
            {
                if (isFirstReach && newLevel >= 5)
                    MergeEffect.Instance.PlayNewLevelEffect(mergePos, GameConfig.GetColor(newLevel), GameConfig.GetSize(newLevel));
                else
                    MergeEffect.Instance.PlayMergeEffect(mergePos, GameConfig.GetColor(newLevel), GameConfig.GetSize(newLevel));
            }

            // 모든 머지에 화면 흔들림 (등급 높을수록 강하게)
            if (ScreenShake.Instance != null)
            {
                float baseIntensity = 0.05f + newLevel * 0.03f;
                float comboBonusIntensity = Mathf.Min(comboCount * 0.02f, 0.15f);
                float intensity = baseIntensity + comboBonusIntensity;
                float dur = 0.1f + newLevel * 0.02f;
                ScreenShake.Instance.Shake(intensity, dur);
            }

            // 콤보 이펙트 (3콤보 이상)
            if (comboCount >= 3 && MergeEffect.Instance != null)
            {
                MergeEffect.Instance.PlayComboEffect(mergePos, GameConfig.GetColor(newLevel), comboCount);
            }

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMergeSound(newLevel);
        }

        private IEnumerator ExecuteMaxMerge(MergeableObject a, MergeableObject b)
        {
            if (a == null || b == null) yield break;

            a.StartMerge();
            b.StartMerge();

            Vector3 posA = a.transform.position;
            Vector3 posB = b.transform.position;
            Vector3 mergePos = (posA + posB) / 2f;

            float duration = GameConfig.Instance.mergeAnimDuration * 1.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseInBack(t);

                if (a != null)
                    a.transform.position = Vector3.Lerp(posA, mergePos, eased);
                if (b != null)
                    b.transform.position = Vector3.Lerp(posB, mergePos, eased);

                yield return null;
            }

            // 둘 다 소멸
            int idA = a.GetInstanceID();
            int idB = b.GetInstanceID();
            mergeInProgress.Remove(idA);
            mergeInProgress.Remove(idB);
            ObjectSpawner.Instance.ReturnToPool(a);
            ObjectSpawner.Instance.ReturnToPool(b);

            // 대박 점수: levelScores[10] × 5
            int megaScore = GameConfig.GetScore(GameConfig.MaxLevel) * 5;
            ScoreManager.Instance.AddScore(megaScore);

            // 이벤트
            OnMaxMerge?.Invoke(mergePos);

            // 이펙트
            if (MergeEffect.Instance != null)
                MergeEffect.Instance.PlayMaxMergeEffect(mergePos);

            if (ScreenShake.Instance != null)
                ScreenShake.Instance.Shake(GameConfig.Instance.screenShakeIntensity * 3f,
                    GameConfig.Instance.screenShakeDuration * 2f);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMaxMergeSound();
        }

        private IEnumerator ScalePopAnimation(MergeableObject obj, int level)
        {
            if (obj == null) yield break;

            float targetSize = GameConfig.GetSize(level);
            Vector3 targetScale = new Vector3(targetSize, targetSize, 1f);

            float duration = 0.2f;
            float elapsed = 0f;

            // 오버슈트
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutBack(t);

                if (obj != null)
                    obj.transform.localScale = Vector3.LerpUnclamped(targetScale * 0.5f, targetScale, eased);

                yield return null;
            }

            if (obj != null)
                obj.transform.localScale = targetScale;
        }

        // ── 이징 함수 ──
        private static float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
