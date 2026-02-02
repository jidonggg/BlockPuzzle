using UnityEngine;

namespace MergeDrop.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource sfxSource;
        private AudioSource bgmSource;

        private bool isMuted;
        private float sfxVolume = 0.7f;
        private float bgmVolume = 0.3f;

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
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = sfxVolume;

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
        }

        public void PlayDropSound()
        {
            if (isMuted) return;
            var clip = GenerateToneClip(300f, 0.08f, 0.4f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayMergeSound(int level)
        {
            if (isMuted) return;
            float freq = 400f + level * 50f;
            var clip = GenerateMergeToneClip(freq, 0.15f, 0.6f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayMaxMergeSound()
        {
            if (isMuted) return;
            // C 메이저 화음 (C4=261.6, E4=329.6, G4=392.0)
            var clip = GenerateChordClip(
                new float[] { 523.25f, 659.25f, 783.99f },
                0.4f, 0.7f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayGameOverSound()
        {
            if (isMuted) return;
            // 하강톤
            var clip = GenerateDescendingToneClip(400f, 150f, 0.5f, 0.5f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayNewLevelSound()
        {
            if (isMuted) return;
            // 상승톤
            var clip = GenerateAscendingToneClip(400f, 800f, 0.3f, 0.5f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // F3: 피버 사운드 (상승 아르페지오)
        public void PlayFeverSound()
        {
            if (isMuted) return;
            var clip = GenerateArpeggioClip(
                new float[] { 523.25f, 659.25f, 783.99f, 1046.50f },
                0.5f, 0.6f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // F5: 스킬 사운드
        public void PlaySkillSound()
        {
            if (isMuted) return;
            var clip = GenerateChordClip(
                new float[] { 440f, 554.37f, 659.25f },
                0.2f, 0.5f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // CC: 마일스톤 사운드
        public void PlayMilestoneSound()
        {
            if (isMuted) return;
            var clip = GenerateArpeggioClip(
                new float[] { 440f, 554.37f, 659.25f, 880f },
                0.4f, 0.5f);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            sfxSource.mute = isMuted;
            bgmSource.mute = isMuted;
        }

        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            sfxSource.volume = sfxVolume;
        }

        public void SetBGMVolume(float vol)
        {
            bgmVolume = Mathf.Clamp01(vol);
            bgmSource.volume = bgmVolume;
        }

        // ── 프로시저럴 오디오 생성 ──

        /// <summary>
        /// 기본톤 + 하모닉 생성.
        /// </summary>
        private AudioClip GenerateToneClip(float freq, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / duration); // 감쇠 엔벨로프
                envelope *= envelope; // 빠른 감쇠

                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.3f;
                samples[i] = (wave + harmonic) * envelope * volume;
            }

            var clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 머지 사운드 — 약간의 주파수 변조 포함.
        /// </summary>
        private AudioClip GenerateMergeToneClip(float freq, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / duration);
                envelope = envelope * envelope * envelope;

                // 주파수가 약간 상승하는 효과
                float modFreq = freq + (t / duration) * freq * 0.3f;
                float wave = Mathf.Sin(2f * Mathf.PI * modFreq * t);
                float harmonic1 = Mathf.Sin(2f * Mathf.PI * modFreq * 1.5f * t) * 0.25f;
                float harmonic2 = Mathf.Sin(2f * Mathf.PI * modFreq * 3f * t) * 0.1f;

                samples[i] = (wave + harmonic1 + harmonic2) * envelope * volume;
            }

            var clip = AudioClip.Create("MergeTone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 화음 (여러 주파수 합성).
        /// </summary>
        private AudioClip GenerateChordClip(float[] frequencies, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float perNote = volume / frequencies.Length;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / duration);
                envelope *= envelope;

                float sum = 0f;
                foreach (float freq in frequencies)
                    sum += Mathf.Sin(2f * Mathf.PI * freq * t);

                samples[i] = sum * perNote * envelope;
            }

            var clip = AudioClip.Create("Chord", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 하강톤 — 게임오버.
        /// </summary>
        private AudioClip GenerateDescendingToneClip(float startFreq, float endFreq, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float envelope = 1f - progress;

                float freq = Mathf.Lerp(startFreq, endFreq, progress);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.3f;

                samples[i] = (wave + harmonic) * envelope * volume;
            }

            var clip = AudioClip.Create("Descending", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 아르페지오 — 피버/마일스톤.
        /// </summary>
        private AudioClip GenerateArpeggioClip(float[] notes, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float noteLength = duration / notes.Length;

            for (int n = 0; n < notes.Length; n++)
            {
                int noteStart = Mathf.RoundToInt(n * noteLength * sampleRate);
                int noteEnd = Mathf.Min(sampleCount, Mathf.RoundToInt((n + 1) * noteLength * sampleRate));
                float freq = notes[n];

                for (int i = noteStart; i < noteEnd; i++)
                {
                    float t = (float)(i - noteStart) / sampleRate;
                    float localDur = noteLength;
                    float envelope = 1f - (t / localDur);
                    envelope = Mathf.Max(0f, envelope);

                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                    float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
                    samples[i] += (wave + harmonic) * envelope * volume / notes.Length * 2f;
                }
            }

            var clip = AudioClip.Create("Arpeggio", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 상승톤 — 새 등급 도달.
        /// </summary>
        private AudioClip GenerateAscendingToneClip(float startFreq, float endFreq, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float envelope = 1f - progress * 0.5f; // 느린 감쇠

                float freq = Mathf.Lerp(startFreq, endFreq, progress);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);

                samples[i] = wave * envelope * volume;
            }

            var clip = AudioClip.Create("Ascending", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
