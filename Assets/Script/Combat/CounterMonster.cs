using Pinball.Core;

namespace Pinball.Combat
{
    public class CounterMonster : Monster
    {
        public int hitCount;

        public CounterMonster(string id, MonsterData data) : base(id, data)
        {
        }

        public override BigNumber OnHitReceived(BigNumber damage, bool killed)
        {
            CounterMonsterData counterData = data as CounterMonsterData;
            if (counterData == null || counterData.counterEveryHits <= 0)
            {
                return BigNumber.Zero;
            }

            hitCount++;

            if (killed)
            {
                return BigNumber.Zero;
            }

            if (hitCount % counterData.counterEveryHits == 0)
            {
                return counterData.counterDamage;
            }

            return BigNumber.Zero;
        }
    }
}
