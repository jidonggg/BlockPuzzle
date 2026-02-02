using UnityEngine;
using System;
using MergeDrop.Audio;

namespace MergeDrop.Core
{
    public enum GameState { Loading, Ready, Playing, GameOver, Reviving }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Loading;

        public event Action<GameState> OnStateChanged;

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
            SetState(GameState.Ready);
        }

        public void StartGame()
        {
            if (State != GameState.Ready && State != GameState.GameOver) return;

            ScoreManager.Instance.ResetCurrentScore();
            MergeSystem.Instance.ResetCombo();
            ObjectSpawner.Instance.ClearAllActive();

            // F2: Reset difficulty
            if (DifficultyManager.Instance != null)
                DifficultyManager.Instance.ResetDifficulty();

            // F3: Reset fever
            if (FeverManager.Instance != null)
                FeverManager.Instance.ResetFever();

            // F5: Reset skills
            if (SkillManager.Instance != null)
                SkillManager.Instance.ResetSkills();

            // F8: Consume daily challenge reward + reset
            if (DailyChallengeManager.Instance != null)
            {
                DailyChallengeManager.Instance.TryConsumeReward();
                DailyChallengeManager.Instance.ResetForNewGame();
            }

            SetState(GameState.Playing);

            DropController.Instance.Activate();
        }

        public void HandleGameOver()
        {
            if (State != GameState.Playing) return;

            DropController.Instance.Deactivate();
            ScoreManager.Instance.OnGameEnd();

            // 슬로모션 연출
            StartCoroutine(GameOverSequence());
        }

        private System.Collections.IEnumerator GameOverSequence()
        {
            // 슬로모션
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(0.8f);
            Time.timeScale = 1f;

            SetState(GameState.GameOver);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayGameOverSound();
        }

        public void Revive()
        {
            if (State != GameState.GameOver) return;

            SetState(GameState.Reviving);

            // 상단 오브젝트 제거 후 재개
            ContainerBounds.Instance.ClearAboveLine();

            SetState(GameState.Playing);
            DropController.Instance.Activate();
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            ObjectSpawner.Instance.ClearAllActive();
            SetState(GameState.Ready);
            StartGame();
        }

        public void PauseGame()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 1f;
        }

        private void SetState(GameState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(State);
        }
    }
}
