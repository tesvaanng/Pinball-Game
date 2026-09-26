using TMPro;
using UnityEngine;

namespace Pinball.UI
{
    public class FloatingText : MonoBehaviour
    {
        private enum Mode
        {
            None,
            Fade,
            HoldThenFly
        }

        private FloatingTextManager manager;
        private TextMeshProUGUI text;
        private RectTransform rect;

        private Mode mode = Mode.None;
        private float timer;
        private float duration;
        private Vector2 startScreen;
        private Vector2 targetScreen;
        private float holdDuration;
        private float flyDuration;
        private float riseOffset;
        private Color color;

        public void Initialize(FloatingTextManager owner, TextMeshProUGUI tmp, RectTransform rectTransform)
        {
            manager = owner;
            text = tmp;
            rect = rectTransform;
        }

        public void PlayFade(Vector2 screenPos, string content, Color textColor, float lifeTime, float rise, float fontSize)
        {
            mode = Mode.Fade;
            timer = 0f;
            duration = Mathf.Max(0.001f, lifeTime);
            startScreen = screenPos;
            targetScreen = screenPos + Vector2.up * rise;
            riseOffset = rise;
            color = textColor;
            SetContent(content, fontSize);
            rect.position = new Vector3(screenPos.x, screenPos.y, 0f);
            text.color = color;
            gameObject.SetActive(true);
        }

        public void PlayHoldThenFly(Vector2 screenPos, Vector2 target, string content, Color textColor, float hold, float fly, float fontSize)
        {
            mode = Mode.HoldThenFly;
            timer = 0f;
            startScreen = screenPos;
            targetScreen = target;
            holdDuration = Mathf.Max(0f, hold);
            flyDuration = Mathf.Max(0.001f, fly);
            color = textColor;
            SetContent(content, fontSize);
            rect.position = new Vector3(screenPos.x, screenPos.y, 0f);
            text.color = color;
            gameObject.SetActive(true);
        }

        public void FinishImmediately()
        {
            if (mode == Mode.None)
            {
                return;
            }

            mode = Mode.None;
            if (manager != null)
            {
                manager.Release(this);
            }
        }

        private void Update()
        {
            if (mode == Mode.None)
            {
                return;
            }

            timer += Time.deltaTime;

            if (mode == Mode.Fade)
            {
                float t = Mathf.Clamp01(timer / duration);
                rect.position = Vector2.Lerp(startScreen, targetScreen, t);

                Color faded = color;
                faded.a = 1f - t;
                text.color = faded;

                if (t >= 1f)
                {
                    FinishImmediately();
                }
                return;
            }

            if (timer < holdDuration)
            {
                rect.position = new Vector3(startScreen.x, startScreen.y, 0f);
                text.color = color;
                return;
            }

            float flyT = Mathf.Clamp01((timer - holdDuration) / flyDuration);
            rect.position = Vector2.Lerp(startScreen, targetScreen, flyT);

            Color flyColor = color;
            flyColor.a = 1f - flyT;
            text.color = flyColor;

            if (flyT >= 1f)
            {
                FinishImmediately();
            }
        }

        private void SetContent(string content, float fontSize)
        {
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
        }
    }
}
