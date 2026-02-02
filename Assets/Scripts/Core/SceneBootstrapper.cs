using UnityEngine;
using MergeDrop.Data;
using MergeDrop.UI;
using MergeDrop.Audio;
using MergeDrop.Effects;

namespace MergeDrop.Core
{
    public class SceneBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            SetupScreen();
            SetupCamera();
            SetupPhysics();
            CreateManagers();
            CreateBackground();
        }

        private void SetupScreen()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
        }

        private void SetupCamera()
        {
            Camera cam;
            if (Camera.main == null)
            {
                var camObj = new GameObject("MainCamera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }
            else
            {
                cam = Camera.main;
            }

            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.transform.position = new Vector3(0f, 0.8f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void SetupPhysics()
        {
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Time.fixedDeltaTime = 0.01667f;
        }

        private void CreateManagers()
        {
            var config = GameConfig.Instance;

            CreateSingleton<GameManager>("GameManager");
            CreateSingleton<ObjectSpawner>("ObjectSpawner");
            CreateSingleton<MergeSystem>("MergeSystem");
            CreateSingleton<DropController>("DropController");
            CreateSingleton<ContainerBounds>("ContainerBounds");
            CreateSingleton<ScoreManager>("ScoreManager");

            // Phase 2-4: New singletons (order matters - before UI)
            CreateSingleton<DifficultyManager>("DifficultyManager");
            CreateSingleton<FeverManager>("FeverManager");
            CreateSingleton<SkillManager>("SkillManager");
            CreateSingleton<DailyChallengeManager>("DailyChallengeManager");
            CreateSingleton<BackgroundManager>("BackgroundManager");

            CreateSingleton<UIManager>("UIManager");
            CreateSingleton<AudioManager>("AudioManager");
            CreateSingleton<MergeEffect>("MergeEffect");
            CreateSingleton<ScreenShake>("ScreenShake");
        }

        private void CreateSingleton<T>(string name) where T : Component
        {
            if (FindAnyObjectByType<T>() == null)
            {
                var go = new GameObject(name);
                go.AddComponent<T>();
            }
        }

        private void CreateBackground()
        {
            var config = GameConfig.Instance;
            float halfW = config.containerWidth / 2f;
            float bottom = config.containerBottomY;
            float top = bottom + config.containerHeight;

            // 컨테이너 내부 어두운 배경
            var innerBg = new GameObject("ContainerBG");
            innerBg.transform.position = new Vector3(0f, (bottom + top) / 2f, 1f);
            var innerSr = innerBg.AddComponent<SpriteRenderer>();
            innerSr.sprite = CreateSquareSprite();
            innerSr.color = new Color(0.04f, 0.04f, 0.09f, 0.8f);
            innerSr.sortingOrder = -50;
            innerBg.transform.localScale = new Vector3(config.containerWidth - 0.1f, config.containerHeight + 0.5f, 1f);
        }

        private static Sprite CreateSquareSprite()
        {
            int res = 4;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[res * res];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        }
    }
}
