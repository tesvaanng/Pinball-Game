using Pinball.Core;

namespace Pinball.Combat
{
    public class ArmoredMonster : Monster
    {
        public ArmoredMonster(string id, MonsterData data) : base(id, data)
        {
        }

        public override BigNumber ModifyIncomingDamage(BigNumber damage)
        {
            ArmoredMonsterData armorData = data as ArmoredMonsterData;
            if (armorData == null)
            {
                return damage;
            }

            BigNumber reduced = damage - armorData.armor;
            BigNumber minimum = damage * BigNumber.FromDouble(0.1);
            if (reduced < minimum)
            {
                return minimum;
            }

            return reduced;
        }
    }
}
