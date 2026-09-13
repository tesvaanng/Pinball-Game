using Pinball.Core;

namespace Pinball.Health
{
    public class Health
    {
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
            if (currentHp <= BigNumber.Zero)
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
            return currentHp <= BigNumber.Zero;
        }
    }
}
