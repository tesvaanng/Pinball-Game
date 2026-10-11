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
        /// 分層能量條：紅色底層填滿 = 點火；之後每一層都在同一位置用不同顏色「塗過去」，
        /// 塗滿一層就是升一階倍率。層數超過圖層數量時顏色循環，所以條子永遠在動、不會卡在滿值。
        /// </summary>
        public void ShowEnergy(BigNumber displayed, BigNumber capacity, int tier, float tierMultiplier, BigNumber previewDamage)
        {
            double pipes = GetPipes(displayed, capacity);
            int layerCount = overchargeLayers.Count;

            if (chargeFillImage != null)
            {
                chargeFillImage.fillAmount = Clamp01(pipes);
            }

            if (layerCount > 0)
            {
                double local = pipes;

                if (pipes > 1.0)
                {
                    local = 1.0 + ((pipes - 1.0) % layerCount);
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
                if (tier <= 0)
                {
                    chargeText.text = "(" + displayed.ToString() + "/" + capacity.ToString() + ")";
                    chargeText.color = normalChargeColor;
                }
                else
                {
                    chargeText.text = previewDamage.ToString();
                    chargeText.color = GetTierColor(tier, layerCount);
                }
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

        /// <summary>這段能量有幾「條」（charge / capacity）。</summary>
        private static double GetPipes(BigNumber charge, BigNumber capacity)
        {
            if (capacity <= BigNumber.Zero || charge <= BigNumber.Zero)
            {
                return 0.0;
            }

            double ratio = (charge / capacity).ToDouble();
            if (double.IsNaN(ratio) || ratio < 0.0)
            {
                return 0.0;
            }

            return ratio;
        }

        private static float Clamp01(double value)
        {
            if (value <= 0.0) return 0f;
            if (value >= 1.0) return 1f;
            return (float)value;
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
