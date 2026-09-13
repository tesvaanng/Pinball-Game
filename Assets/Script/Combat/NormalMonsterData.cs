using UnityEngine;

namespace Pinball.Combat
{
    [CreateAssetMenu(menuName = "Pinball/Monsters/Normal Monster")]
    public class NormalMonsterData : MonsterData
    {
        public override Monster CreateMonster(string id)
        {
            return new NormalMonster(id, this);
        }
    }
}
