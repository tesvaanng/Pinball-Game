using TMPro;
using UnityEngine;
using Pinball.Core;

namespace Pinball.Board
{
    public class Pocket : MonoBehaviour
    {
        public BigNumber multiplier = BigNumber.One;

        [Tooltip("顯示倍率的 TextMeshPro（world space，掛在 Pocket 底下的子物件）。在場景／prefab 自己建立好再拖進來，不由程式生成。")]
        public TMP_Text multiplierText;

        private void Start()
        {
            RefreshLabel();
        }

        public void RefreshLabel()
        {
            if (multiplierText == null)
            {
                return;
            }

            multiplierText.text = multiplier.ToDisplayString();

            Vector3 lossy = transform.lossyScale;
            multiplierText.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, lossy.x),
                1f / Mathf.Max(0.0001f, lossy.y),
                1f);
        }
    }
}
