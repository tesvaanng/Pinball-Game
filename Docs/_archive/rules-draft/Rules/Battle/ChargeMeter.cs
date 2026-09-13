using System;

namespace Pinball.Rules
{
    /// <summary>
    /// 充能條 —— <b>一條連續累加器</b>。對應 Docs/Design.md 2.4.2 與附錄 A.5。
    ///
    /// 三個關鍵性質：
    /// <list type="bullet">
    /// <item>溢出無損：超出當前條的部分直接推進到下一條。</item>
    /// <item>跨球、跨層累積（2.5.8）。</item>
    /// <item>開火次數沒有上限（2.4.2）。</item>
    /// </list>
    /// </summary>
    public sealed class ChargeMeter
    {
        /// <summary>充能上限 `C`。每一條的容量。</summary>
        public BigNumber Capacity { get; private set; }

        /// <summary>每次開火傷害 `D`。<b>固定值，與 <see cref="Capacity"/> 無關。</b></summary>
        public BigNumber DamagePerFire { get; private set; }

        /// <summary>目前累積的能量 `E`。可以是小數（例：0.6 條）。</summary>
        public BigNumber Energy { get; private set; }

        public ChargeMeter(BigNumber capacity, BigNumber damagePerFire)
        {
            if (capacity.IsZero || capacity.IsNegative)
            {
                // 附錄 A.9：C <= 0 是未定義行為，所以在建構時就擋掉，不要讓它靜靜地壞掉。
                throw new ArgumentOutOfRangeException("capacity", "ChargeMeter: 充能上限 C 必須 > 0");
            }

            Capacity = capacity;
            DamagePerFire = damagePerFire;
            Energy = BigNumber.Zero;
        }

        /// <summary>至少有一條滿了，可以開火。</summary>
        public bool HasFullBar
        {
            get { return Energy.CompareTo(Capacity) >= 0; }
        }

        /// <summary>目前這條充了幾成（0.0 ~ 1.0+）。給 UI 用。</summary>
        public double FillRatio
        {
            get { return (Energy / Capacity).ApproximateValue; }
        }

        /// <summary>加入結算分數。<b>溢出無損</b> —— 不裁切、不歸零。</summary>
        public void Credit(BigNumber amount)
        {
            if (amount.IsZero) return;
            Energy += amount;
        }

        /// <summary>扣掉一條。<see cref="HasFullBar"/> 為 true 時才合法。</summary>
        public void SpendOneBar()
        {
            if (!HasFullBar)
            {
                throw new InvalidOperationException("ChargeMeter: 沒有滿的充能條可以扣");
            }
            Energy -= Capacity;
        }

        /// <summary>清空（例：進入 Boss 房之前 —— 附錄 A.7）。</summary>
        public void Reset()
        {
            Energy = BigNumber.Zero;
        }
    }
}
