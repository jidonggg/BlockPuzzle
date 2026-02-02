using UnityEngine;
using System.Collections;

namespace MergeDrop.Effects
{
    public class ScreenShake : MonoBehaviour
    {
        public static ScreenShake Instance { get; private set; }

        private Vector3 originalCamPos;
        private Coroutine shakeCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Shake(float intensity, float duration)
        {
            if (Camera.main == null) return;

            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            originalCamPos = new Vector3(0f, 0.8f, -10f); // 카메라 기본 위치
            shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));
        }

        private IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / duration); // 시간에 따라 감쇠

                float offsetX = Random.Range(-1f, 1f) * intensity * t;
                float offsetY = Random.Range(-1f, 1f) * intensity * t;

                Camera.main.transform.position = originalCamPos + new Vector3(offsetX, offsetY, 0f);

                yield return null;
            }

            Camera.main.transform.position = originalCamPos;
            shakeCoroutine = null;
        }
    }
}
