using Pinball.Core;

namespace Pinball.Combat
{
    public interface IDamageOverflowHandler
    {
        void OnOverflowDamage(BigNumber overflowDamage, Monster monster);
    }
}
