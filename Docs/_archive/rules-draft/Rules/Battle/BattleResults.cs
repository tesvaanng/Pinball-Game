using System;

namespace Pinball.Rules
{
    /// <summary>
    /// 一次開火。Unity 層拿這個清單去播「子彈從充能條射出、充滿→歸零→充滿」的演出。
    /// 演出間隔見 <see cref="GameContent.FirePresentationInterval"/>（只影響演出，不影響結算）。
    /// </summary>
    public struct FireEvent
    {
        /// <summary>這一波開火的第幾發（從 0 開始）。</summary>
        public int Sequence;

        /// <summary>造成的傷害 ＝ `D`。</summary>
        public BigNumber Damage;

        public BigNumber MonsterHpBefore;
        public BigNumber MonsterHpAfter;

        /// <summary>這一發打死怪物 → 之後不再開火（防鞭屍）。</summary>
        public bool KilledMonster;

        public override string ToString()
        {
            return "#" + Sequence + " dmg=" + Damage + " hp " + MonsterHpBefore + "→" + MonsterHpAfter
                   + (KilledMonster ? " KILL" : "");
        }
    }

    /// <summary>一顆球的完整結算結果。對應 Docs/Design.md 附錄 A.4／A.5。</summary>
    public sealed class ShotSettlement
    {
        /// <summary>球上分數。</summary>
        public BigNumber PegScore;

        /// <summary>落進哪個袋口。<b>-1 表示超時</b>。</summary>
        public int PocketIndex;

        /// <summary>袋口倍率。超時為 1×。</summary>
        public BigNumber PocketMultiplier;

        /// <summary>結算分數 ＝ 球上分數 × 袋口倍率。</summary>
        public BigNumber SettledScore;

        /// <summary>結算前的充能條能量。</summary>
        public BigNumber EnergyBefore;

        /// <summary>結算後的充能條能量（＝ 進入下一顆球時條上的殘量）。</summary>
        public BigNumber EnergyAfter;

        /// <summary>這一球觸發的每一次開火。可能是空的。</summary>
        public FireEvent[] Fires;

        /// <summary>是否超時（以 1× 結算）。</summary>
        public bool TimedOut;

        /// <summary>這一球是否把怪物打死了。</summary>
        public bool MonsterDied;

        public int FireCount
        {
            get { return Fires == null ? 0 : Fires.Length; }
        }
    }

    /// <summary>一輪（8–10 顆球）結束後，怪物攻擊的結果。對應 Docs/Design.md 附錄 A.6。</summary>
    public sealed class RoundResult
    {
        /// <summary>剛結束的是第幾輪（從 1 開始）。</summary>
        public int RoundNumber;

        /// <summary>怪物這一輪的攻擊力。</summary>
        public BigNumber MonsterAttack;

        public BigNumber PlayerHpBefore;
        public BigNumber PlayerHpAfter;

        /// <summary>玩家被打死 → 本局結束。</summary>
        public bool PlayerDied;

        /// <summary>這一輪是否因為「輪數超過防呆上限」而強制結束本局。</summary>
        public bool RoundLimitReached;
    }

    /// <summary>本局可能的結局。</summary>
    public enum RunOutcome
    {
        /// <summary>還在進行中。</summary>
        InProgress = 0,

        /// <summary>玩家 HP 歸零。</summary>
        PlayerDied = 1,

        /// <summary>輪數超過防呆上限（正常遊戲不該發生）。</summary>
        RoundLimitReached = 2
    }
}
