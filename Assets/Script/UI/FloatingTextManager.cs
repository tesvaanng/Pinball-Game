using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Pinball.Core;

namespace Pinball.UI
{
    public class FloatingTextManager : Singleton<FloatingTextManager>
    {
        public Canvas canvas;
        public float defaultFontSize = 36f;

        private readonly List<FloatingText> active = new List<FloatingText>();
        private readonly Stack<FloatingText> pool = new Stack<FloatingText>();

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            EnsureCanvas();
        }

        public static FloatingTextManager EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject go = new GameObject("FloatingTextManager");
            return go.AddComponent<FloatingTextManager>();
        }

        public FloatingText SpawnFadeWorld(Vector3 worldPos, string text, Color color, float duration, float riseOffset = 30f, float fontSize = 0f)
        {
            return SpawnFadeScreen(WorldToScreen(worldPos), text, color, duration, riseOffset, fontSize);
        }

        public FloatingText SpawnFadeScreen(Vector2 screenPos, string text, Color color, float duration, float riseOffset = 30f, float fontSize = 0f)
        {
            FloatingText ft = GetOrCreate();
            ft.PlayFade(screenPos, text, color, duration, riseOffset, fontSize <= 0f ? defaultFontSize : fontSize);
            active.Add(ft);
            return ft;
        }

        public FloatingText SpawnHoldThenFlyWorld(Vector3 worldPos, Vector2 targetScreenPos, string text, Color color, float holdDuration, float flyDuration, float fontSize = 0f)
        {
            return SpawnHoldThenFlyScreen(WorldToScreen(worldPos), targetScreenPos, text, color, holdDuration, flyDuration, fontSize);
        }

        public FloatingText SpawnHoldThenFlyScreen(Vector2 screenPos, Vector2 targetScreenPos, string text, Color color, float holdDuration, float flyDuration, float fontSize = 0f)
        {
            FloatingText ft = GetOrCreate();
            ft.PlayHoldThenFly(screenPos, targetScreenPos, text, color, holdDuration, flyDuration, fontSize <= 0f ? defaultFontSize : fontSize);
            active.Add(ft);
            return ft;
        }

        public void Release(FloatingText ft)
        {
            if (ft == null)
            {
                return;
            }

            active.Remove(ft);
            ft.gameObject.SetActive(false);
            pool.Push(ft);
        }

        public void CancelAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                FloatingText ft = active[i];
                if (ft != null)
                {
                    ft.FinishImmediately();
                }
            }
        }

        private FloatingText GetOrCreate()
        {
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            return CreateText();
        }

        private FloatingText CreateText()
        {
            GameObject go = new GameObject("FloatingText", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            if (tmp.font == null)
            {
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                {
                    tmp.font = font;
                }
            }

            tmp.fontSize = defaultFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            tmp.color = Color.white;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 80f);

            FloatingText ft = go.AddComponent<FloatingText>();
            ft.Initialize(this, tmp, rect);
            go.SetActive(false);
            return ft;
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject go = new GameObject("FloatingTextCanvas");
            go.transform.SetParent(transform, false);

            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
        }

        private static Vector2 WorldToScreen(Vector3 worldPos)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            return new Vector2(screen.x, screen.y);
        }
    }
}
