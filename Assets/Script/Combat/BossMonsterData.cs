using UnityEngine;

namespace Pinball.Combat
{
    [CreateAssetMenu(menuName = "Pinball/Monsters/Boss Monster")]
    public class BossMonsterData : MonsterData
    {
        public override Monster CreateMonster(string id)
        {
            return new BossMonster(id, this);
        }
    }
}
