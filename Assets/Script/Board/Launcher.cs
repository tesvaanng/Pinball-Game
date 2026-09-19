using UnityEngine;
using Pinball.Input;

namespace Pinball.Board
{
    /// <summary>
    /// 2D 發射器瞄準元件。
    /// 掛在發射器本體上，讓 aimTransform 的 local up 持續朝向滑鼠在世界空間的位置。
    /// BoardManager 發射時使用 shootPoint.up 當方向，所以把 shootPoint 指到這個 aimTransform 即可。
    /// </summary>
    public class Launcher : MonoBehaviour
    {
        [Header("Transforms")]
        [Tooltip("不旋轉的發射器本體，用來當預設方向參考。留空則使用自己。")]
        public Transform pivot;

        [Tooltip("會被旋轉的槍口 / 準心 Transform。留空則使用自己。")]
        public Transform aimTransform;

        [Header("Aim")]
        [Tooltip("用來把滑鼠螢幕座標轉成世界座標的攝影機。留空則使用 Camera.main。")]
        public Camera aimCamera;

        [Tooltip("相對預設方向的最大瞄準角度，避免往後打。")]
        [Range(0f, 89f)]
        public float maxAimAngle = 80f;

        [Tooltip("是否每幀追蹤滑鼠位置。")]
        public bool trackPointer = true;

        [Tooltip("如果發射方向與滑鼠位置相反，勾選這個。")]
        public bool invertDirection = false;

        private Vector2 defaultDirection;

        private void Awake()
        {
            if (pivot == null) pivot = transform;
            if (aimTransform == null) aimTransform = transform;
            if (aimCamera == null) aimCamera = Camera.main;

            Vector3 up = pivot.up;
            Vector2 direction = new Vector2(-up.x, -up.y);
            defaultDirection = direction.sqrMagnitude < 0.0001f ? Vector2.down : direction.normalized;
        }

        private void Start()
        {
            ApplyAimDirection(defaultDirection);
        }

        private void Update()
        {
            if (!trackPointer) return;
            if (!InputManager.IsInitialized) return;

            UpdateAim(InputManager.Instance.PointerScreenPosition);
        }

        public void UpdateAim(Vector2 screenPosition)
        {
            ApplyAimDirection(GetAimDirection(screenPosition));
        }

        public Vector2 GetAimDirection(Vector2 screenPosition)
        {
            if (aimCamera == null || aimTransform == null)
            {
                return defaultDirection;
            }

            float depth = Mathf.Abs(aimCamera.transform.position.z - aimTransform.position.z);
            Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, depth);
            Vector3 worldPoint = aimCamera.ScreenToWorldPoint(screenPoint);

            Vector2 direction = (Vector2)worldPoint - (Vector2)aimTransform.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return defaultDirection;
            }

            direction.Normalize();
            Vector2 clamped = ClampDirection(direction, defaultDirection);

            if (invertDirection)
            {
                clamped = -clamped;
            }

            return clamped;
        }

        private void ApplyAimDirection(Vector2 direction)
        {
            if (aimTransform == null) return;
            if (direction.sqrMagnitude < 0.0001f) return;

            // 讓 aimTransform.up 對準 direction；BoardManager 就是用 shootPoint.up 發射。
            float zAngle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            aimTransform.rotation = Quaternion.Euler(0f, 0f, zAngle);
        }

        private Vector2 ClampDirection(Vector2 direction, Vector2 reference)
        {
            float angle = Vector2.SignedAngle(reference, direction);
            angle = Mathf.Clamp(angle, -maxAimAngle, maxAimAngle);
            return Quaternion.Euler(0f, 0f, angle) * reference;
        }
    }
}
