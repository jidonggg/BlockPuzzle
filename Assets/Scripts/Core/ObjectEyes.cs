using UnityEngine;

namespace MergeDrop.Core
{
    public class ObjectEyes : MonoBehaviour
    {
        public enum EyeState { Idle, Dropping, Collision, Merging }

        private EyeState currentState = EyeState.Idle;

        private SpriteRenderer leftWhite, rightWhite;
        private SpriteRenderer leftPupil, rightPupil;

        private static Sprite eyeWhiteSprite;
        private static Sprite pupilSprite;

        private float perlinOffsetX;
        private float perlinOffsetY;
        private float collisionTimer;
        private float mergeRotation;

        private const float EyeLocalScale = 0.22f;
        private const float EyeOffsetX = 0.15f;
        private const float EyeOffsetY = 0.08f;
        private const float PupilRange = 0.08f;

        public void Setup(int level)
        {
            EnsureSprites();

            if (leftWhite == null) CreateEye("LeftEye", -EyeOffsetX, EyeOffsetY, out leftWhite, out leftPupil);
            if (rightWhite == null) CreateEye("RightEye", EyeOffsetX, EyeOffsetY, out rightWhite, out rightPupil);

            // Randomize perlin offset per object
            perlinOffsetX = Random.Range(0f, 100f);
            perlinOffsetY = Random.Range(0f, 100f);

            SetState(EyeState.Idle);
            SetEyesVisible(true);
        }

        public void SetState(EyeState state)
        {
            currentState = state;

            switch (state)
            {
                case EyeState.Dropping:
                    SetEyeScale(1.3f);
                    break;
                case EyeState.Collision:
                    collisionTimer = 0.2f;
                    SetEyeScale(0.6f);
                    break;
                case EyeState.Merging:
                    mergeRotation = 0f;
                    break;
                case EyeState.Idle:
                    SetEyeScale(1f);
                    break;
            }
        }

        public void ResetEyes()
        {
            currentState = EyeState.Idle;
            collisionTimer = 0f;
            mergeRotation = 0f;
            SetEyesVisible(false);
        }

        private void Update()
        {
            if (leftWhite == null || !leftWhite.gameObject.activeInHierarchy) return;

            switch (currentState)
            {
                case EyeState.Idle:
                    UpdateIdlePupils();
                    break;
                case EyeState.Dropping:
                    UpdateIdlePupils();
                    break;
                case EyeState.Collision:
                    collisionTimer -= Time.deltaTime;
                    if (collisionTimer <= 0f)
                        SetState(EyeState.Idle);
                    break;
                case EyeState.Merging:
                    mergeRotation += Time.deltaTime * 720f;
                    UpdateMergingPupils();
                    break;
            }
        }

        private void UpdateIdlePupils()
        {
            float time = Time.time * 0.5f;
            float px = (Mathf.PerlinNoise(time + perlinOffsetX, 0f) - 0.5f) * 2f * PupilRange;
            float py = (Mathf.PerlinNoise(0f, time + perlinOffsetY) - 0.5f) * 2f * PupilRange;

            if (leftPupil != null)
                leftPupil.transform.localPosition = new Vector3(px, py, 0f);
            if (rightPupil != null)
                rightPupil.transform.localPosition = new Vector3(px, py, 0f);
        }

        private void UpdateMergingPupils()
        {
            float rad = mergeRotation * Mathf.Deg2Rad;
            float px = Mathf.Cos(rad) * PupilRange;
            float py = Mathf.Sin(rad) * PupilRange;

            if (leftPupil != null)
                leftPupil.transform.localPosition = new Vector3(px, py, 0f);
            if (rightPupil != null)
                rightPupil.transform.localPosition = new Vector3(-px, -py, 0f);
        }

        private void SetEyeScale(float multiplier)
        {
            float s = EyeLocalScale * multiplier;
            Vector3 scale = new Vector3(s, s, 1f);
            if (leftWhite != null) leftWhite.transform.localScale = scale;
            if (rightWhite != null) rightWhite.transform.localScale = scale;
        }

        private void SetEyesVisible(bool visible)
        {
            if (leftWhite != null) leftWhite.gameObject.SetActive(visible);
            if (rightWhite != null) rightWhite.gameObject.SetActive(visible);
        }

        private void CreateEye(string name, float x, float y,
            out SpriteRenderer white, out SpriteRenderer pupil)
        {
            // Find existing or create
            var existing = transform.Find(name);
            GameObject eyeObj;
            if (existing != null)
            {
                eyeObj = existing.gameObject;
                white = eyeObj.GetComponent<SpriteRenderer>();
            }
            else
            {
                eyeObj = new GameObject(name);
                eyeObj.transform.SetParent(transform, false);
                white = eyeObj.AddComponent<SpriteRenderer>();
            }

            eyeObj.transform.localPosition = new Vector3(x, y, 0f);
            eyeObj.transform.localScale = new Vector3(EyeLocalScale, EyeLocalScale, 1f);
            white.sprite = eyeWhiteSprite;
            white.color = Color.white;
            white.sortingOrder = 3;

            // Pupil
            var pupilExisting = eyeObj.transform.Find("Pupil");
            GameObject pupilObj;
            if (pupilExisting != null)
            {
                pupilObj = pupilExisting.gameObject;
                pupil = pupilObj.GetComponent<SpriteRenderer>();
            }
            else
            {
                pupilObj = new GameObject("Pupil");
                pupilObj.transform.SetParent(eyeObj.transform, false);
                pupil = pupilObj.AddComponent<SpriteRenderer>();
            }

            pupilObj.transform.localPosition = Vector3.zero;
            pupilObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            pupil.sprite = pupilSprite;
            pupil.color = new Color(0.15f, 0.15f, 0.2f);
            pupil.sortingOrder = 4;
        }

        private static void EnsureSprites()
        {
            if (eyeWhiteSprite == null)
                eyeWhiteSprite = CreateCircleSprite(32, Color.white);
            if (pupilSprite == null)
                pupilSprite = CreateCircleSprite(16, Color.white);
        }

        private static Sprite CreateCircleSprite(int res, Color baseColor)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = res / 2f;
            float radius = center - 1f;

            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 1f - dist);
                    pixels[y * res + x] = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        }
    }
}
