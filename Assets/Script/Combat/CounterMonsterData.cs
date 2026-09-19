using UnityEngine;
using Pinball.Core;

namespace Pinball.Combat
{
    [CreateAssetMenu(menuName = "Pinball/Monsters/Counter Monster")]
    public class CounterMonsterData : MonsterData
    {
        public int counterEveryHits = 5;
        public BigNumber counterDamage = 1;

        public override Monster CreateMonster(string id)
        {
            return new CounterMonster(id, this);
        }
    }
}
