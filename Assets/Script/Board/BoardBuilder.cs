using System;
using UnityEngine;
using Pinball.Core;

namespace Pinball.Board
{
    /// <summary>
    /// 依 <see cref="BoardLayout"/> 生成盤面（見 Docs/Design.md 6.1）。
    ///
    /// 使用方式：
    ///   1. 在場景中開一個空物件（例如 BoardManager 底下）掛上這個元件
    ///   2. 指定 BoardLayout 資料資產
    ///   3. 把 pegParent 指到場景中的「All Peg」、pocketParent 指到「All Pocket」
    ///   4. 進 Play，或在 Inspector 右鍵選「生成盤面」預覽
    ///
    /// 生成的物件都帶 HideFlags.DontSave，所以**不會被寫進場景檔**。
    /// 盤面的唯一真相是 BoardLayout 資產 —— 改資料、進 Play，盤面就變了。
    /// </summary>
    [AddComponentMenu("Pinball/Board Builder")]
    [DisallowMultipleComponent]
    public class BoardBuilder : MonoBehaviour
    {
        [Header("資料")]
        public BoardLayout layout;

        [Tooltip("生成的釘子掛在這裡（場景中的 All Peg）。留空則掛在自己底下。")]
        public Transform pegParent;

        [Tooltip("生成的袋口掛在這裡（場景中的 All Pocket）。留空則掛在自己底下。")]
        public Transform pocketParent;

        [Tooltip("進入 Play 時自動生成。")]
        public bool buildOnAwake = true;

        [Header("灰盒外觀")]
        [Tooltip("執行時產生的圓形 sprite 解析度。")]
        public int circleSpriteSize = 32;

        public Color pegColor = new Color(0.85f, 0.85f, 0.90f, 1f);
        public Color pocketLowColor = new Color(0.35f, 0.38f, 0.45f, 1f);
        public Color pocketHighColor = new Color(0.95f, 0.75f, 0.20f, 1f);

        private Sprite circleSprite;
        private Sprite squareSprite;

        private void Awake()
        {
            if (buildOnAwake)
            {
                Build();
            }
        }

        /// <summary>
        /// 清除現有內容並依 layout 重新生成整個盤面。
        /// </summary>
        [ContextMenu("生成盤面")]
        public void Build()
        {
            if (layout == null)
            {
                // 沒指定資料時用記憶體裡的預設值，讓「加元件 → 進 Play」就能看到盤面。
                // 要調整參數時再建立一份 BoardLayout 資產並指定進來。
                layout = ScriptableObject.CreateInstance<BoardLayout>();
                Debug.LogWarning("BoardBuilder: 沒有指定 BoardLayout，這次用預設值（均勻網格）生成。" +
                                 "要調整參數請在 Assets 建立一份 Pinball → Board Layout 資產並指定進來。");
            }

            if (pegParent == null) pegParent = transform;
            if (pocketParent == null) pocketParent = transform;

            ClearChildren(pegParent);
            ClearChildren(pocketParent);

            // BoardLayout 的 playArea 是「內部區域」：四面牆圍出來的那塊
            Rect area = new Rect(
                layout.playAreaCenter.x - layout.playAreaSize.x * 0.5f,
                layout.playAreaCenter.y - layout.playAreaSize.y * 0.5f,
                layout.playAreaSize.x,
                layout.playAreaSize.y);

            float pocketBandTop = area.yMin + layout.pocketBandHeight;

            if (layout.generateUniformGrid)
            {
                BuildUniformGrid(area, pocketBandTop);
            }
            else
            {
                BuildManualPegs();
            }

            if (layout.pockets != null && layout.pockets.Count > 0)
            {
                BuildManualPockets();
            }
            else
            {
                BuildPockets(area);
            }
        }

        [ContextMenu("清除盤面")]
        public void Clear()
        {
            ClearChildren(pegParent != null ? pegParent : transform);
            ClearChildren(pocketParent != null ? pocketParent : transform);
        }

        // ────────────────────────────── 釘子 ──────────────────────────────

        private void BuildUniformGrid(Rect area, float pocketBandTop)
        {
            float spacing = Mathf.Max(0.05f, layout.pegSpacing);
            float radius = Mathf.Max(0.01f, layout.pegRadius);

            float xMin = area.xMin + layout.sideMargin;
            float xMax = area.xMax - layout.sideMargin;
            float yTop = area.yMax - layout.topMargin;
            float yBottom = pocketBandTop + layout.bottomMargin;

            if (xMax <= xMin || yTop <= yBottom)
            {
                Debug.LogWarning("BoardBuilder: 邊界／邊距設定讓盤面沒有空間，沒有生成任何釘子。");
                return;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt((xMax - xMin) / spacing) + 1);
            int rows = Mathf.Max(1, Mathf.FloorToInt((yTop - yBottom) / spacing) + 1);

            // 水平置中
            float xSpan = (columns - 1) * spacing;
            float xStart = (xMin + xMax) * 0.5f - xSpan * 0.5f;

            int count = 0;
            for (int r = 0; r < rows; r++)
            {
                float y = yTop - r * spacing;
                if (y < yBottom - 0.0001f)
                {
                    break;
                }

                bool staggered = layout.staggerRows && (r % 2) == 1;
                float rowOffset = staggered ? spacing * 0.5f : 0f;
                int rowColumns = staggered ? Mathf.Max(1, columns - 1) : columns;

                for (int c = 0; c < rowColumns; c++)
                {
                    float x = xStart + c * spacing + rowOffset;
                    if (x < xMin - 0.0001f || x > xMax + 0.0001f)
                    {
                        continue;
                    }

                    CreatePeg(new Vector2(x, y), radius, "peg_normal", 1);
                    count++;
                }
            }

            Debug.Log(string.Format(
                "BoardBuilder: 生成了 {0} 顆釘子（{1} 欄 × {2} 排，間距 {3}，半徑 {4}）。",
                count, columns, rows, spacing, radius));
        }

        private void BuildManualPegs()
        {
            if (layout.pegs == null)
            {
                return;
            }

            for (int i = 0; i < layout.pegs.Count; i++)
            {
                BoardLayout.PegSpec spec = layout.pegs[i];
                if (spec == null)
                {
                    continue;
                }

                CreatePeg(spec.position, spec.radius, spec.pegId, spec.score);
            }

            Debug.Log(string.Format("BoardBuilder: 生成了 {0} 顆手動指定的釘子。", layout.pegs.Count));
        }

        private void CreatePeg(Vector2 worldPos, float radius, string pegId, BigNumber score)
        {
            GameObject go = new GameObject("Peg");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(pegParent, true);
            go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            // 圓形 sprite 的直徑是 1 世界單位，所以 scale 直接等於直徑
            float diameter = radius * 2f;
            go.transform.localScale = new Vector3(diameter, diameter, 1f);

            SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetCircleSprite();
            spriteRenderer.color = pegColor;

            CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.5f;

            Peg peg = go.AddComponent<Peg>();
            peg.pegId = pegId;
            peg.score = score;
        }

        // ────────────────────────────── 袋口 ──────────────────────────────

        private void BuildPockets(Rect area)
        {
            BigNumber[] multipliers = layout.pocketMultipliers;
            if (multipliers == null || multipliers.Length == 0)
            {
                return;
            }

            int count = multipliers.Length;
            float cellWidth = area.width / count;
            float height = Mathf.Max(0.1f, layout.pocketBandHeight * 0.85f);
            float pocketWidth = cellWidth * Mathf.Clamp01(layout.pocketWidthRatio);
            float y = area.yMin + layout.pocketBandHeight * 0.5f;

            BigNumber highest = multipliers[0];
            for (int i = 1; i < count; i++)
            {
                if (multipliers[i] > highest)
                {
                    highest = multipliers[i];
                }
            }

            for (int i = 0; i < count; i++)
            {
                float x = area.xMin + cellWidth * (i + 0.5f);
                CreatePocket(new Vector2(x, y), new Vector2(pocketWidth, height), multipliers[i], highest);
            }

            Debug.Log(string.Format("BoardBuilder: 生成了 {0} 個袋口。", count));
        }

        private void BuildManualPockets()
        {
            BigNumber highest = BigNumber.One;
            for (int i = 0; i < layout.pockets.Count; i++)
            {
                if (layout.pockets[i] != null && layout.pockets[i].multiplier > highest)
                {
                    highest = layout.pockets[i].multiplier;
                }
            }

            for (int i = 0; i < layout.pockets.Count; i++)
            {
                BoardLayout.PocketSpec spec = layout.pockets[i];
                if (spec == null)
                {
                    continue;
                }

                CreatePocket(spec.position, spec.size, spec.multiplier, highest);
            }
        }

        private void CreatePocket(Vector2 worldPos, Vector2 size, BigNumber multiplier, BigNumber highest)
        {
            GameObject go = new GameObject("Pocket_" + multiplier.ToDisplayString());
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(pocketParent, true);
            go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetSquareSprite();
            spriteRenderer.color = Color.Lerp(pocketLowColor, pocketHighColor, TierRatio(multiplier, highest));
            spriteRenderer.sortingOrder = -1;

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.isTrigger = true;

            Pocket pocket = go.AddComponent<Pocket>();
            pocket.multiplier = multiplier;
        }

        /// <summary>把倍率映射到 0～1，純粹為了灰盒的顏色深淺。</summary>
        private static float TierRatio(BigNumber value, BigNumber highest)
        {
            double top = Math.Log10(Math.Max(1.0, highest.ToDouble()));
            if (top <= 0.0)
            {
                return 0f;
            }

            double current = Math.Log10(Math.Max(1.0, value.ToDouble()));
            return Mathf.Clamp01((float)(current / top));
        }

        // ────────────────────────────── 工具 ──────────────────────────────

        private void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            int size = Mathf.Max(8, circleSpriteSize);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            float radius = size * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            circleSprite.name = "BoardBuilderCircle";
            return circleSprite;
        }

        private Sprite GetSquareSprite()
        {
            if (squareSprite != null)
            {
                return squareSprite;
            }

            const int size = 4;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            squareSprite.name = "BoardBuilderSquare";
            return squareSprite;
        }
    }
}
