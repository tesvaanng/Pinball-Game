using TMPro;
using UnityEngine;
using Pinball.Core;

namespace Pinball.Board
{
    public class Pocket : MonoBehaviour
    {
        public BigNumber multiplier = BigNumber.One;

        public float fontSize = 10.0f;

        private const string LabelObjectName = "MultiplierLabel";
        private TextMeshPro label;

        private void Start()
        {
            RefreshLabel();
        }

        public void RefreshLabel()
        {
            if (label == null)
            {
                Transform existing = transform.Find(LabelObjectName);
                if (existing != null)
                {
                    label = existing.GetComponent<TextMeshPro>();
                }

                if (label == null)
                {
                    GameObject go = new GameObject(LabelObjectName);
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = Vector3.zero;

                    label = go.AddComponent<TextMeshPro>();
                    label.fontSize = fontSize;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = Color.white;
                    if (label.font == null && TMP_Settings.defaultFontAsset != null)
                    {
                        label.font = TMP_Settings.defaultFontAsset;
                    }

                    label.sortingOrder = 10;
                }
            }

            label.text = multiplier.ToDisplayString();

            Vector3 lossy = transform.lossyScale;
            label.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, lossy.x),
                1f / Mathf.Max(0.0001f, lossy.y),
                1f);
        }
    }
}
