using UnityEngine;
using Pinball.Core;

namespace Pinball.Combat
{
    [CreateAssetMenu(menuName = "Pinball/Monsters/Armored Monster")]
    public class ArmoredMonsterData : MonsterData
    {
        public BigNumber armor = 5;

        public override Monster CreateMonster(string id)
        {
            return new ArmoredMonster(id, this);
        }
    }
}
