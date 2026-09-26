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

        public bool TryConsumeOne()
        {
            if (capacity <= BigNumber.Zero || charge < capacity)
            {
                return false;
            }

            charge -= capacity;
            return true;
        }

        public void Clear()
        {
            charge = BigNumber.Zero;
        }
    }
}
