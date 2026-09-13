using System;
using System.Collections.Generic;

namespace Pinball.Rules
{
    /// <summary>一顆釘子。對應 Docs/Design.md 附錄 A.3。</summary>
    [Serializable]
    public struct PegDef
    {
        public string Id;

        /// <summary>碰到時加進「球上分數」的值。純結構釘（牆、彈開器）為 0。</summary>
        public BigNumber Score;

        public PegDef(string id, BigNumber score)
        {
            Id = id;
            Score = score;
        }
    }

    /// <summary>一個袋口。對應 Docs/Design.md 附錄 A.2。</summary>
    [Serializable]
    public struct PocketDef
    {
        public string Id;

        /// <summary>落入時乘上「球上分數」的倍率。不用 0×。</summary>
        public BigNumber Multiplier;

        /// <summary>袋口寬度佔盤面總寬的比例。全部加起來應該是 1。只給 Unity 排版用。</summary>
        public float WidthNormalized;

        public PocketDef(string id, BigNumber multiplier, float widthNormalized)
        {
            Id = id;
            Multiplier = multiplier;
            WidthNormalized = widthNormalized;
        }
    }

    /// <summary>一層的怪物。對應 Docs/Design.md 附錄 A.1／2.5.9。</summary>
    [Serializable]
    public struct MonsterDef
    {
        public string Id;
        public BigNumber MaxHp;

        /// <summary>每一輪的球用完後，對玩家造成的傷害。</summary>
        public BigNumber AttackPower;

        public MonsterDef(string id, BigNumber maxHp, BigNumber attackPower)
        {
            Id = id;
            MaxHp = maxHp;
            AttackPower = attackPower;
        }
    }

    /// <summary>
    /// 基礎玩法的全部設定。對應 Docs/Design.md 附錄 A.1／A.2／A.3。
    ///
    /// ⚠️ 這裡的數值全部是<b>起始佔位值</b>，不是平衡後的數字。
    /// M0 的目標是驗證「手感與循環」，不是平衡（見 6.3）。
    /// </summary>
    public sealed class GameContent
    {
        /// <summary>充能上限 `C`。滿一條 → 開火一次。</summary>
        public BigNumber ChargeCapacity = 100;

        /// <summary>每次開火傷害 `D`。<b>固定值，不隨 `C` 變動。</b></summary>
        public BigNumber DamagePerFire = 1;

        /// <summary>玩家最大 HP。</summary>
        public BigNumber PlayerMaxHp = 100;

        /// <summary>一輪幾顆球。球打完 → 怪物攻擊 → 補滿（2.5.9）。</summary>
        public int BallsPerRound = 8;

        /// <summary>一顆球飛超過這個秒數就強制以 1× 結算（2.5.2）。</summary>
        public float ShotTimeoutSeconds = 8.0f;

        /// <summary>防呆用。超過這個輪數視為失敗，避免模擬無限迴圈。</summary>
        public int MaxRounds = 100;

        /// <summary>逐發開火的演出間隔秒數。<b>只影響演出，不影響結算。</b></summary>
        public float FirePresentationInterval = 0.15f;

        /// <summary>袋口，由左到右。索引就是 <see cref="BattleSession.SettleShot"/> 的 pocketIndex。</summary>
        public PocketDef[] Pockets = new PocketDef[0];

        /// <summary>釘子定義表。</summary>
        public PegDef[] Pegs = new PegDef[0];

        private Dictionary<string, BigNumber> _pegScoreById;

        /// <summary>碰撞事件只需要傳 id 時用這個查分數。未知 id 會丟例外（我們要提早發現打錯字）。</summary>
        public BigNumber GetPegScore(string pegId)
        {
            if (_pegScoreById == null)
            {
                _pegScoreById = new Dictionary<string, BigNumber>(Pegs.Length);
                for (int i = 0; i < Pegs.Length; i++)
                {
                    _pegScoreById[Pegs[i].Id] = Pegs[i].Score;
                }
            }

            BigNumber score;
            if (!_pegScoreById.TryGetValue(pegId, out score))
            {
                throw new KeyNotFoundException("GameContent: 未知的釘子 id '" + pegId + "'");
            }
            return score;
        }

        public BigNumber GetPocketMultiplier(int pocketIndex)
        {
            if (pocketIndex < 0 || pocketIndex >= Pockets.Length)
            {
                throw new ArgumentOutOfRangeException(
                    "pocketIndex", pocketIndex, "GameContent: 袋口索引超出範圍（0.." + (Pockets.Length - 1) + "）");
            }
            return Pockets[pocketIndex].Multiplier;
        }

        /// <summary>
        /// M0 的佔位內容。數值刻意很小、很好算，方便用手驗算。
        /// <b>不要在平衡階段之前改這些數字。</b>
        /// </summary>
        public static GameContent CreateM0Placeholder()
        {
            var content = new GameContent();

            // ---- 常數（附錄 A.1）----
            content.ChargeCapacity = 100;
            content.DamagePerFire = 1;
            content.PlayerMaxHp = 100;
            content.BallsPerRound = 8;
            content.ShotTimeoutSeconds = 8.0f;
            content.MaxRounds = 100;
            content.FirePresentationInterval = 0.15f;

            // ---- 袋口（附錄 A.2）中央低、兩側高，最高 20×，不用 0× ----
            content.Pockets = new[]
            {
                new PocketDef("pocket_0", 20, 0.08f),
                new PocketDef("pocket_1", 8,  0.12f),
                new PocketDef("pocket_2", 3,  0.15f),
                new PocketDef("pocket_3", 1,  0.15f),
                new PocketDef("pocket_4", 1,  0.15f),
                new PocketDef("pocket_5", 3,  0.15f),
                new PocketDef("pocket_6", 8,  0.12f),
                new PocketDef("pocket_7", 20, 0.08f)
            };

            // ---- 釘子（附錄 A.3）----
            content.Pegs = new[]
            {
                new PegDef("peg_normal", 1),
                new PegDef("peg_dense",  1),
                new PegDef("peg_bonus",  3),
                new PegDef("peg_rich",   10),
                new PegDef("peg_wall",   0),
                new PegDef("peg_kicker", 0)
            };

            return content;
        }

        /// <summary>M0 的第一層怪物。故意設成「大概打得死」，讓核心循環跑得完。</summary>
        public static MonsterDef CreateM0PlaceholderMonster()
        {
            return new MonsterDef("monster_dummy", 200, 10);
        }
    }
}
