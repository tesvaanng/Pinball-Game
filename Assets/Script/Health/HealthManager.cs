using System.Collections.Generic;
using Pinball.Core;

namespace Pinball.Health
{
    public class HealthManager : Singleton<HealthManager>
    {
        private Dictionary<string, Health> healths = new Dictionary<string, Health>();

        public Health Create(string id, BigNumber maxHp)
        {
            Health health = new Health(maxHp);
            healths[id] = health;
            return health;
        }

        public Health Get(string id)
        {
            Health health;
            healths.TryGetValue(id, out health);
            return health;
        }

        public void Damage(string id, BigNumber amount)
        {
            Health health = Get(id);
            if (health != null)
            {
                health.TakeDamage(amount);
            }
        }

        public void Heal(string id, BigNumber amount)
        {
            Health health = Get(id);
            if (health != null)
            {
                health.Heal(amount);
            }
        }

        public bool IsDead(string id)
        {
            Health health = Get(id);
            return health == null || health.IsDead();
        }

        public void Remove(string id)
        {
            healths.Remove(id);
        }

        public void Clear()
        {
            healths.Clear();
        }
    }
}
