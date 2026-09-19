using UnityEngine;
using Pinball.Core;

namespace Pinball.Combat
{
    public abstract class MonsterData : ScriptableObject
    {
        public string monsterName = "Monster";
        public BigNumber maxHp = 10;
        public BigNumber attack = 1;

        public abstract Monster CreateMonster(string id);
    }
}
