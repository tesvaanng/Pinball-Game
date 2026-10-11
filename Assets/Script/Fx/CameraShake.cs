using UnityEngine;

namespace Pinball.Fx
{
    /// <summary>
    /// 輕量鏡頭抖動：用 Perlin 噪聲偏移 target 的 localPosition / localRotation，結束後精確還原。
    /// 不需要 Cinemachine，方便單獨測試（Inspector 右鍵「測試抖動」）。
    /// 之後若改用 Cinemachine，只要把 Shake() 的實作換成 ImpulseSource 即可。
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("要抖動的 Transform。留空 = Camera.main。")]
        public Transform target;

        [Tooltip("抖動頻率（越高越急促）。")]
        public float frequency = 22f;

        [Tooltip("旋轉抖動幅度（度）。")]
        public float rotationAmplitude = 1.2f;

        [Tooltip("勾選後不受 Time.timeScale 影響（慢動作時仍抖）。")]
        public bool useUnscaledTime;

        private float amplitude;
        private float duration;
        private float timer;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private bool active;

        private void Awake()
        {
            if (target == null && Camera.main != null)
            {
                target = Camera.main.transform;
            }

            CaptureBase();
        }

        public void Shake(float amplitude, float duration)
        {
            if (target == null && Camera.main != null)
            {
                target = Camera.main.transform;
                CaptureBase();
            }

            if (target == null || amplitude <= 0f || duration <= 0f)
            {
                return;
            }

            this.amplitude = Mathf.Max(this.amplitude, amplitude);
            this.duration = Mathf.Max(this.duration, duration);
            timer = 0f;
            active = true;
        }

        [ContextMenu("測試抖動")]
        private void TestShake()
        {
            Shake(0.2f, 0.4f);
        }

        private void LateUpdate()
        {
            if (!active || target == null)
            {
                return;
            }

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timer += dt;

            if (timer >= duration)
            {
                Restore();
                return;
            }

            float decay = 1f - (timer / duration);
            float noiseTime = (useUnscaledTime ? Time.unscaledTime : Time.time) * frequency;
            float offsetX = (Mathf.PerlinNoise(noiseTime, 0.13f) - 0.5f) * 2f;
            float offsetY = (Mathf.PerlinNoise(0.71f, noiseTime) - 0.5f) * 2f;

            target.localPosition = baseLocalPosition + new Vector3(offsetX, offsetY, 0f) * (amplitude * decay);

            float roll = (Mathf.PerlinNoise(noiseTime, 5.27f) - 0.5f) * 2f * rotationAmplitude * decay;
            target.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, roll);
        }

        private void CaptureBase()
        {
            if (target == null)
            {
                return;
            }

            baseLocalPosition = target.localPosition;
            baseLocalRotation = target.localRotation;
        }

        private void Restore()
        {
            if (target != null)
            {
                target.localPosition = baseLocalPosition;
                target.localRotation = baseLocalRotation;
            }

            active = false;
            amplitude = 0f;
            duration = 0f;
            timer = 0f;
        }
    }
}
