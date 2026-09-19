namespace Pinball.Combat
{
    /// <summary>
    /// Boss 怪物。
    /// 目前先和 NormalMonster 一樣是空模板，之後再往上加 Boss 技能。
    /// </summary>
    public class BossMonster : Monster
    {
        public BossMonster(string id, MonsterData data) : base(id, data)
        {
        }
    }
}
