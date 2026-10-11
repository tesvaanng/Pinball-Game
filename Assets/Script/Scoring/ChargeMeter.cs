using Pinball.Core;

namespace Pinball.Scoring
{
    public class ChargeMeter
    {
        public BigNumber charge;
        public BigNumber capacity;

        public ChargeMeter(BigNumber capacity)
        {
            this.capacity = capacity;
        }

        public void Add(BigNumber amount)
        {
            if (capacity <= BigNumber.Zero)
            {
                return;
            }

            charge += amount;
        }

        /// <summary>把累積的能量整條領出來並歸零。</summary>
        public BigNumber ConsumeAll()
        {
            BigNumber amount = charge;
            charge = BigNumber.Zero;
            return amount;
        }

        public void Clear()
        {
            charge = BigNumber.Zero;
        }
    }
}
