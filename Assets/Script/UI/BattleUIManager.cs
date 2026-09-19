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

        public void ShowBallScore(BigNumber score)
        {
            if (scoreText != null)
            {
                scoreText.text = "Ball: " + score.ToString();
            }
        }

        public void ShowCharge(BigNumber charge)
        {
            if (chargeText != null)
            {
                chargeText.text = "Charge: " + charge.ToString();
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
    }
}
