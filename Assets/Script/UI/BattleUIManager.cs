using TMPro;
using UnityEngine;
using Pinball.Core;

namespace Pinball.UI
{
    public class BattleUIManager : Singleton<BattleUIManager>
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI chargeText;
        public TextMeshProUGUI monsterNameText;
        public TextMeshProUGUI monsterHpText;
        public TextMeshProUGUI playerHpText;
        public TextMeshProUGUI resultText;
        public TextMeshProUGUI fireChargeText;

        private Color normalChargeColor = Color.white;

        public void ShowBallScore(BigNumber score)
        {
            if (scoreText != null)
            {
                scoreText.text = "Ball: " + score.ToString();
            }
        }

        public void ShowCharge(BigNumber charge, BigNumber capacity)
        {
            if (chargeText != null)
            {
                chargeText.text = "(" + charge.ToString() + "/" + capacity.ToString() + ")";
                chargeText.color = normalChargeColor;
            }
        }

        public void ShowMonsterName(string name)
        {
            if (monsterNameText != null)
            {
                monsterNameText.text = name;
            }
        }

        public void ShowMonsterHp(BigNumber hp)
        {
            if (monsterHpText != null)
            {
                monsterHpText.text = "Monster HP: " + hp.ToString();
            }
        }

        public void ShowPlayerHp(BigNumber hp)
        {
            if (playerHpText != null)
            {
                playerHpText.text = "Player HP: " + hp.ToString();
            }
        }

        public void ShowResult(string text)
        {
            if (resultText != null)
            {
                resultText.text = text;
            }
        }

        public void ShowFireCharge(float charge)
        {
            if (fireChargeText != null)
            {
                fireChargeText.text = "Fire Charge: " + charge.ToString();
            }
        }

        public Vector2 GetChargeScreenPosition()
        {
            return GetScreenPosition(chargeText != null ? chargeText.rectTransform : null);
        }

        public Vector2 GetMonsterHpScreenPosition()
        {
            return GetScreenPosition(monsterHpText != null ? monsterHpText.rectTransform : null);
        }

        private Vector2 GetScreenPosition(RectTransform rect)
        {
            if (rect == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, rect.position);
        }
    }
}
