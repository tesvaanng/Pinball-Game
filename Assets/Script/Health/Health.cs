using Pinball.Core;

namespace Pinball.Health
{
    public class Health
    {
        /// <summary>
        /// 死亡判定的**相對容差**（見 Docs/Decision-Log.md 2.15、Docs/Design.md 附錄 A.9）。
        ///
        /// 為什麼需要：BigNumber 是浮點數，連續扣血會累積誤差。例如 maxHp = 10 的
        /// ArmoredMonster 被 100 發「每發 1 點」打完（固定值減傷後每發實際 0.1），
        /// HP 最後會停在約 2.2e-16 而不是 0 —— 於是 IsDead() 回傳 false，怪物打不死，
        /// 而 UI 又把它印成 "0"，看起來像「0 血卻沒死」。
        ///
        /// 為什麼是「相對」而不是絕對：BigNumber 的範圍是 1e-300 ~ 1e300，
        /// 絕對門檻（例如 BigNumber.IsNearlyZero() 的 1e-9）只在 M0 的小數字下有效。
        /// 當 maxHp 長到 1e20，累積誤差會是 1e7 這種量級，絕對門檻就抓不到了。
        /// 相對門檻（maxHp 的十億分之一）在任何量級都成立：
        ///   - double 的機器精度約 2.2e-16
        ///   - 一千次連續運算最壞累積約 1e-13（相對）
        ///   - 1e-9 留了四個數量級餘裕，又遠小於任何一次開火的傷害
        /// </summary>
        private static readonly BigNumber DeathEpsilon = new BigNumber(1, -9);

        public BigNumber maxHp;
        public BigNumber currentHp;

        public Health(BigNumber maxHp)
        {
            this.maxHp = maxHp;
            currentHp = maxHp;
        }

        public void TakeDamage(BigNumber amount)
        {
            currentHp -= amount;

            // 夾成「剛好的 0」，讓讀取端（UI、溢傷處理、存檔）看到一致的狀態
            if (IsBelowDeathThreshold())
            {
                currentHp = BigNumber.Zero;
            }
        }

        public void Heal(BigNumber amount)
        {
            currentHp += amount;
            if (currentHp > maxHp)
            {
                currentHp = maxHp;
            }
        }

        public void Reset()
        {
            currentHp = maxHp;
        }

        public bool IsDead()
        {
            return IsBelowDeathThreshold();
        }

        private bool IsBelowDeathThreshold()
        {
            if (currentHp <= BigNumber.Zero)
            {
                return true;
            }

            if (maxHp <= BigNumber.Zero)
            {
                return true;
            }

            // 主判準：相對門檻。這是唯一對「大數字」正確的寫法。
            if (currentHp <= maxHp * DeathEpsilon)
            {
                return true;
            }

            // 保險：maxHp 本身極小時仍能收斂
            return currentHp.IsNearlyZero();
        }
    }
}
