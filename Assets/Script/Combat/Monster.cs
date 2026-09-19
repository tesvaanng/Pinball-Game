using Pinball.Core;
using Pinball.Health;
using HealthState = Pinball.Health.Health;

namespace Pinball.Combat
{
    public abstract class Monster
    {
        public string id;
        public MonsterData data;
        public HealthState health;

        protected Monster(string id, MonsterData data)
        {
            this.id = id;
            this.data = data;
        }

        public string MonsterName
        {
            get { return data != null ? data.monsterName : "Monster"; }
        }

        public BigNumber Attack
        {
            get { return data != null ? data.attack : BigNumber.Zero; }
        }

        public bool IsDead()
        {
            return health == null || health.IsDead();
        }

        public virtual BigNumber ModifyIncomingDamage(BigNumber damage)
        {
            return damage;
        }

        public virtual BigNumber OnHitReceived(BigNumber damage, bool killed)
        {
            return BigNumber.Zero;
        }
    }
}
