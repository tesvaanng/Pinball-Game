using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Pinball.Core;

namespace Pinball.UI
{
    public class BattleUIManager : Singleton<BattleUIManager>
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI chargeText;

        [Tooltip("能量條的紅色底層 Image。Image Type 要設成 Filled，程式會用 fillAmount 顯示能量。在場景自己建立後拖進來。")]
        public Image chargeFillImage;

        [Tooltip("蓄力分層的顏色，依序給第 2、3、4… 層使用，超過就循環。Image 由程式複製 chargeFillImage 自動生成，不用手動建立。")]
        public Color[] overchargeColors = new Color[]
        {
            new Color(1f, 0.9f, 0.2f, 1f),
            new Color(0.35f, 1f, 0.35f, 1f),
            new Color(0.25f, 0.9f, 1f, 1f),
            new Color(0.35f, 0.5f, 1f, 1f),
            new Color(0.75f, 0.35f, 1f, 1f),
            new Color(1f, 0.35f, 0.75f, 1f),
            new Color(1f, 1f, 1f, 1f)
        };

        [Tooltip("目前倍率的顯示文字（例如 ×2.5）。位置隨你放，在場景建立 TMP 物件後拖進來；顏色會自動跟著當前分層。")]
        public TextMeshProUGUI chargeTierText;

        [Tooltip("倍率文字格式，{0} 會被替換成倍率。")]
        public string chargeTierFormat = "×{0}";

        [Tooltip("基數（還沒乘倍率的傷害）顯示文字。位置隨你放，在場景建立 TMP 物件後拖進來；釋放時基數會從這裡飛向倍率文字合併。")]
        public TextMeshProUGUI chargeBaseText;

        public TextMeshProUGUI monsterNameText;
        public TextMeshProUGUI monsterHpText;
        public TextMeshProUGUI playerHpText;
        public TextMeshProUGUI resultText;
        public TextMeshProUGUI fireChargeText;

        [Tooltip("當前回合剩餘球數的顯示文字。在場景自己建立後拖進來。")]
        public TextMeshProUGUI ballsLeftText;

        [Tooltip("剩餘球數的文字格式，{0} 會被替換成數字。")]
        public string ballsLeftFormat = "Balls: {0}";

        private Color normalChargeColor = Color.white;
        private readonly List<Image> overchargeLayers = new List<Image>();

        /// <summary>
        /// 確保蓄力分層的數量足夠。這些 Image 是複製 chargeFillImage 生成的（同一位置、同一個 sprite 與 Filled 設定），
        /// 所以肉鴿系統之後把倍率表加長時，這裡會自動長出對應的層數。
        /// </summary>
        public void SetOverchargeLayerCount(int count)
        {
            if (chargeFillImage == null || count <= overchargeLayers.Count)
            {
                return;
            }

            for (int i = overchargeLayers.Count; i < count; i++)
            {
                overchargeLayers.Add(CreateOverchargeLayer(i));
            }
        }

        private Image CreateOverchargeLayer(int index)
        {
            GameObject clone = Instantiate(chargeFillImage.gameObject, chargeFillImage.transform.parent);
            clone.name = "ChargeLayer_" + (index + 1);

            for (int i = clone.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(clone.transform.GetChild(i).gameObject);
            }

            clone.transform.SetSiblingIndex(chargeFillImage.transform.GetSiblingIndex() + 1 + index);

            Image image = clone.GetComponent<Image>();
            image.color = GetOverchargeColor(index);
            image.fillAmount = 0f;
            image.raycastTarget = false;
            return image;
        }

        private Color GetOverchargeColor(int index)
        {
            if (overchargeColors != null && overchargeColors.Length > 0)
            {
                return overchargeColors[index % overchargeColors.Length];
            }

            return Color.HSVToRGB((0.13f * index) % 1f, 0.85f, 1f);
        }

        public void ShowBallScore(BigNumber score)
        {
            if (scoreText != null)
            {
                scoreText.text = "Ball: " + score.ToString();
            }
        }

        /// <summary>
        /// 分層能量條。barLayers 是「這次要推進幾層」（由 BattleManager 依本次滾動的量算出，
        /// 所以不論總能量多大，每一次能量進來都會有看得見的推進）。
        /// 紅色底層填滿 = 點火，之後每一層在同一位置用不同顏色塗過去，超過圖層數量就循環。
        /// </summary>
        public void ShowEnergy(BigNumber displayed, BigNumber capacity, int tier, float tierMultiplier, BigNumber baseDamage, double barLayers)
        {
            int layerCount = overchargeLayers.Count;

            if (chargeFillImage != null)
            {
                chargeFillImage.fillAmount = Clamp01(barLayers);
            }

            if (layerCount > 0)
            {
                double local = barLayers;

                if (barLayers > 1.0)
                {
                    local = 1.0 + ((barLayers - 1.0) % layerCount);
                }

                for (int i = 0; i < layerCount; i++)
                {
                    Image layer = overchargeLayers[i];
                    if (layer == null) continue;
                    layer.fillAmount = Clamp01(local - (i + 1));
                }
            }

            if (chargeText != null)
            {
                // 只顯示充能量；超出上限時維持在 (容量/容量)。
                BigNumber shown = displayed;
                if (capacity > BigNumber.Zero && shown > capacity)
                {
                    shown = capacity;
                }

                chargeText.text = "(" + shown.ToString() + "/" + capacity.ToString() + ")";
                chargeText.color = normalChargeColor;
            }

            if (chargeTierText != null)
            {
                if (tier <= 0)
                {
                    chargeTierText.text = string.Empty;
                }
                else
                {
                    chargeTierText.text = string.Format(chargeTierFormat, tierMultiplier.ToString("0.##"));
                    chargeTierText.color = GetTierColor(tier, layerCount);
                }
            }

            if (chargeBaseText != null)
            {
                chargeBaseText.text = displayed <= BigNumber.Zero ? string.Empty : baseDamage.ToString();
            }
        }

        /// <summary>
        /// 釋放瞬間用：能量條填充歸零、清掉基數文字，但**保留倍率文字**——
        /// 倍率要等基數飛過來合併之後才消失。
        /// </summary>
        public void ResetChargeForRelease()
        {
            if (chargeFillImage != null)
            {
                chargeFillImage.fillAmount = 0f;
            }

            for (int i = 0; i < overchargeLayers.Count; i++)
            {
                if (overchargeLayers[i] != null)
                {
                    overchargeLayers[i].fillAmount = 0f;
                }
            }

            if (chargeBaseText != null)
            {
                chargeBaseText.text = string.Empty;
            }
        }

        /// <summary>基數合併完成：倍率文字此時才消失。</summary>
        public void ClearChargeTierText()
        {
            if (chargeTierText != null)
            {
                chargeTierText.text = string.Empty;
            }
        }

        public void ClearAllChargeTexts()
        {
            if (chargeBaseText != null)
            {
                chargeBaseText.text = string.Empty;
            }

            if (chargeTierText != null)
            {
                chargeTierText.text = string.Empty;
            }
        }

        public Vector2 GetChargeBaseScreenPosition()
        {
            if (chargeBaseText == null)
            {
                return GetChargeScreenPosition();
            }

            return GetScreenPosition(chargeBaseText.rectTransform);
        }

        public Vector2 GetChargeTierScreenPosition()
        {
            if (chargeTierText == null)
            {
                return GetChargeScreenPosition();
            }

            return GetScreenPosition(chargeTierText.rectTransform);
        }

        public void ShowBallsLeft(int ballsLeft)
        {
            if (ballsLeftText != null)
            {
                ballsLeftText.text = string.Format(ballsLeftFormat, ballsLeft);
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

        private static float Clamp01(double value)
        {
            if (value <= 0.0) return 0f;
            if (value >= 1.0) return 1f;
            return (float)value;
        }

        /// <summary>釋放結果文字的顏色＝目前正在塗抹的那一層，方便跟條子對照。</summary>
        public Color GetTierDisplayColor(int tier)
        {
            return GetTierColor(tier, overchargeLayers.Count);
        }

        /// <summary>倍率文字的顏色＝目前正在塗抹的那一層，方便跟條子對照。</summary>
        private Color GetTierColor(int tier, int layerCount)
        {
            int index = layerCount > 0 ? (tier - 1) % layerCount : tier - 1;
            return GetOverchargeColor(index);
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
