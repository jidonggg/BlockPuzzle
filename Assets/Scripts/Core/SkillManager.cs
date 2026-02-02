using UnityEngine;
using System;
using System.Collections.Generic;

namespace MergeDrop.Core
{
    public enum SkillType { Shake, Downgrade, Bomb }

    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }

        public event Action<SkillType, int> OnChargeChanged; // (skill, currentCharge)
        public event Action<SkillType> OnSkillReady;
        public event Action<bool> OnSelectionModeChanged; // true=enter, false=exit

        public bool IsSelectionMode { get; private set; }
        public SkillType? PendingSkill { get; private set; }

        // Charge costs
        private static readonly int[] chargeCosts = { 10, 15, 20 };
        private int[] charges = new int[3];
        private int totalMergeCount;

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
            totalMergeCount++;

            for (int i = 0; i < 3; i++)
            {
                int prevCharge = charges[i];
                charges[i]++;
                OnChargeChanged?.Invoke((SkillType)i, charges[i]);

                if (prevCharge < chargeCosts[i] && charges[i] >= chargeCosts[i])
                    OnSkillReady?.Invoke((SkillType)i);
            }
        }

        public int GetCharge(SkillType skill) => charges[(int)skill];
        public int GetMaxCharge(SkillType skill) => chargeCosts[(int)skill];
        public bool IsReady(SkillType skill) => charges[(int)skill] >= chargeCosts[(int)skill];

        public void ActivateSkill(SkillType skill)
        {
            if (!IsReady(skill)) return;

            if (skill == SkillType.Shake)
            {
                ExecuteShake();
                ConsumeCharge(skill);
            }
            else
            {
                // Enter selection mode for Downgrade/Bomb
                EnterSelectionMode(skill);
            }
        }

        public void OnObjectSelected(MergeableObject target)
        {
            if (!IsSelectionMode || PendingSkill == null) return;

            switch (PendingSkill.Value)
            {
                case SkillType.Downgrade:
                    ExecuteDowngrade(target);
                    break;
                case SkillType.Bomb:
                    ExecuteBomb(target);
                    break;
            }

            ConsumeCharge(PendingSkill.Value);
            ExitSelectionMode();
        }

        public void CancelSelection()
        {
            ExitSelectionMode();
        }

        private void EnterSelectionMode(SkillType skill)
        {
            IsSelectionMode = true;
            PendingSkill = skill;

            // Suppress DropController input
            if (DropController.Instance != null)
                DropController.Instance.InputSuppressed = true;

            OnSelectionModeChanged?.Invoke(true);
        }

        private void ExitSelectionMode()
        {
            IsSelectionMode = false;
            PendingSkill = null;

            if (DropController.Instance != null)
                DropController.Instance.InputSuppressed = false;

            OnSelectionModeChanged?.Invoke(false);
        }

        private void ConsumeCharge(SkillType skill)
        {
            int idx = (int)skill;
            charges[idx] = 0;
            OnChargeChanged?.Invoke(skill, 0);

            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlaySkillSound();
        }

        private void ExecuteShake()
        {
            if (ObjectSpawner.Instance == null) return;
            var objects = ObjectSpawner.Instance.GetActiveObjects();
            foreach (var obj in objects)
            {
                if (obj == null || obj.IsMerging) continue;
                var rb = obj.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                {
                    Vector2 force = new Vector2(
                        UnityEngine.Random.Range(-3f, 3f),
                        UnityEngine.Random.Range(1f, 4f));
                    rb.AddForce(force, ForceMode2D.Impulse);
                }
            }

            if (Effects.ScreenShake.Instance != null)
                Effects.ScreenShake.Instance.Shake(0.3f, 0.3f);
        }

        private void ExecuteDowngrade(MergeableObject target)
        {
            if (target == null) return;
            target.DowngradeLevel(1);
        }

        private void ExecuteBomb(MergeableObject target)
        {
            if (target == null || ObjectSpawner.Instance == null) return;

            Vector3 targetPos = target.transform.position;

            // Find nearest object
            MergeableObject nearest = null;
            float nearestDist = float.MaxValue;
            var objects = ObjectSpawner.Instance.GetActiveObjects();
            foreach (var obj in objects)
            {
                if (obj == null || obj == target) continue;
                float dist = Vector3.Distance(obj.transform.position, targetPos);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = obj;
                }
            }

            // Remove target
            ObjectSpawner.Instance.ReturnToPool(target);

            // Remove nearest
            if (nearest != null)
                ObjectSpawner.Instance.ReturnToPool(nearest);

            if (Effects.ScreenShake.Instance != null)
                Effects.ScreenShake.Instance.Shake(0.25f, 0.2f);
        }

        /// <summary>
        /// F8: Grant one free charge (reward from daily challenge)
        /// </summary>
        public void GrantFreeCharge(SkillType skill)
        {
            int idx = (int)skill;
            charges[idx] = chargeCosts[idx];
            OnChargeChanged?.Invoke(skill, charges[idx]);
            OnSkillReady?.Invoke(skill);
        }

        public void ResetSkills()
        {
            for (int i = 0; i < 3; i++)
                charges[i] = 0;
            totalMergeCount = 0;
            IsSelectionMode = false;
            PendingSkill = null;
        }
    }
}
