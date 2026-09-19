using Pinball.Core;

namespace Pinball.Scoring
{
    public class ChargeMeter
    {
        public BigNumber charge;
        public BigNumber capacity;
        public int maxFirePerCall = 100;

        public ChargeMeter(BigNumber capacity)
        {
            this.capacity = capacity;
        }

        public int Add(BigNumber amount)
        {
            if (capacity <= BigNumber.Zero)
            {
                return 0;
            }

            charge += amount;

            int fireCount = 0;
            while (charge >= capacity && fireCount < maxFirePerCall)
            {
                charge -= capacity;
                fireCount++;
            }

            return fireCount;
        }

        public void Clear()
        {
            charge = BigNumber.Zero;
        }
    }
}
