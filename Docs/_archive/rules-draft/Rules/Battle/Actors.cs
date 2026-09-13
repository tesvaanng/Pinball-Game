using System;

namespace Pinball.Rules
{
    /// <summary>一層的怪物狀態。對應 Docs/Design.md 2.5.9、3.3.2。</summary>
    public sealed class MonsterState
    {
        public MonsterDef Def { get; private set; }

        /// <summary>目前 HP。<b>夾在 0</b>，不會是負數（附錄 A.9）。</summary>
        public BigNumber Hp { get; private set; }

        public BigNumber MaxHp { get; private set; }

        public MonsterState(MonsterDef def)
        {
            Def = def;
            MaxHp = def.MaxHp;
            Hp = def.MaxHp;
        }

        public bool IsDead
        {
            get { return Hp.IsZero; }
        }

        /// <summary>扣血，並回傳「實際扣掉多少」（溢傷會被夾掉）。</summary>
        public BigNumber ApplyDamage(BigNumber damage)
        {
            if (damage.IsZero || damage.IsNegative) return BigNumber.Zero;

            BigNumber before = Hp;
            Hp = BigNumber.Max(Hp - damage, BigNumber.Zero);
            return before - Hp;
        }

        /// <summary>這一輪的球用完後，怪物對玩家造成的傷害。</summary>
        public BigNumber AttackPower
        {
            get { return Def.AttackPower; }
        }
    }

    /// <summary>玩家狀態。對應 Docs/Design.md 2.5.9、2.5.10。</summary>
    public sealed class PlayerState
    {
        public BigNumber MaxHp { get; private set; }

        /// <summary>目前 HP。<b>跨層保留</b>，只有營火等節點會恢復。</summary>
        public BigNumber Hp { get; private set; }

        public PlayerState(BigNumber maxHp)
        {
            MaxHp = maxHp;
            Hp = maxHp;
        }

        public bool IsDead
        {
            get { return Hp.IsZero; }
        }

        /// <summary>扣血，並回傳「實際扣掉多少」（夾在 0）。</summary>
        public BigNumber ApplyDamage(BigNumber damage)
        {
            if (damage.IsZero || damage.IsNegative) return BigNumber.Zero;

            BigNumber before = Hp;
            Hp = BigNumber.Max(Hp - damage, BigNumber.Zero);
            return before - Hp;
        }

        public void Heal(BigNumber amount)
        {
            if (amount.IsZero || amount.IsNegative) return;
            Hp = BigNumber.Min(Hp + amount, MaxHp);
        }
    }
}
