using System;
using System.Collections.Generic;
using UnityEngine;
using Pinball.Core;

namespace Pinball.Board
{
    /// <summary>
    /// 盤面的**資料定義**（見 Docs/Design.md 6.1）。
    ///
    /// 為什麼盤面是資料而不是手擺的場景物件：
    /// 盤面會被改很多次（6.1 的流程是「生成 → 量測 → 改 → 再量」），
    /// 而且每次改完都要能量測。手擺的話，改一次盤面 = 拖 300 顆釘子。
    ///
    /// v1 是**刻意選的平均分佈（均勻 Plinko 網格）**。
    /// 它不是最終答案 —— Design.md 2.2.4 明確說過均勻網格會讓瞄準與力度同時失去意義。
    /// 選它是因為：
    ///   1. 它是「基準線」：先量出「完全沒有結構」長什麼樣，才知道結構要加多少
    ///   2. 它讓兩軸掃描的量測工具先能跑起來（沒有盤面就沒東西可量）
    /// **預期結果就是兩軸掃描會失敗**，那正是要證明的事。
    /// </summary>
    [CreateAssetMenu(menuName = "Pinball/Board Layout", fileName = "BoardLayout")]
    public class BoardLayout : ScriptableObject
    {
        [Serializable]
        public class PegSpec
        {
            public string pegId = "peg_normal";
            public BigNumber score = 1;
            public Vector2 position;
            public float radius = 0.2f;
        }

        [Serializable]
        public class PocketSpec
        {
            public BigNumber multiplier = BigNumber.One;
            public Vector2 position;
            public Vector2 size = new Vector2(3.45f, 1.2f);
        }

        [Header("── 盤面邊界（世界座標，指「內部區域」）──")]
        [Tooltip("盤面內部區域的中心。場景中的 Manchine Body 位於 (0, -3.97)。")]
        public Vector2 playAreaCenter = new Vector2(0f, -3.97f);

        [Tooltip("盤面內部區域的大小。四面牆圍出來的內部是 30 x 30。")]
        public Vector2 playAreaSize = new Vector2(30f, 30f);

        [Header("── 產生的內容 ──")]
        [Tooltip("v1 基準線：均勻 Plinko 網格。關掉的話就用下面的 pegs 清單手動指定。")]
        public bool generateUniformGrid = true;

        [Header("── 均勻網格參數（generateUniformGrid = true 時使用）──")]
        [Tooltip("釘子間距（世界單位）。建議 ≈ 球直徑的 2～2.5 倍。球的直徑是 0.6。")]
        public float pegSpacing = 1.5f;

        [Tooltip("釘子半徑（世界單位）。必須小到讓球過得去：間距 − 2×半徑 > 球直徑。")]
        public float pegRadius = 0.2f;

        [Tooltip("第一排釘子距離盤面頂端多遠。太小會讓球一出生就疊在釘子上。")]
        public float topMargin = 3f;

        [Tooltip("最後一排釘子距離袋口帶頂端多遠。")]
        public float bottomMargin = 1.5f;

        [Tooltip("左右各留多少邊。留一條淨空通道可以避免球卡在牆邊。")]
        public float sideMargin = 1.5f;

        [Tooltip("奇數排水平偏移半格（真正的 Plinko 排列）。關掉會變成方格，球容易直直穿過去。")]
        public bool staggerRows = true;

        [Header("── 袋口 ──")]
        [Tooltip("袋口帶的高度（從盤面底部往上算）。")]
        public float pocketBandHeight = 1.4f;

        [Tooltip("袋口寬度佔「盤面寬 ÷ 袋口數」的比例。留一點縫隙才看得出是 8 個袋子。")]
        public float pocketWidthRatio = 0.92f;

        [Tooltip("由左到右的倍率。數量決定袋口數量。預設是附錄 A.2 的 8 袋、中央低兩側高。")]
        public BigNumber[] pocketMultipliers = { 20, 8, 3, 1, 1, 3, 8, 20 };

        [Header("── 手動指定的釘子（generateUniformGrid = false 時使用）──")]
        public List<PegSpec> pegs = new List<PegSpec>();

        [Header("── 手動指定的袋口（留空則用上面的倍率自動排）──")]
        public List<PocketSpec> pockets = new List<PocketSpec>();
    }
}
