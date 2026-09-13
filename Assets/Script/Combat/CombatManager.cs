using System;
using System.Collections.Generic;
using Pinball.Core;
using Pinball.Data;
using Pinball.Health;
using HealthState = Pinball.Health.Health;

namespace Pinball.Combat
{
    public class CombatManager : Singleton<CombatManager>
    {
        public string playerId = "player";
        public List<Monster> monsters = new List<Monster>();
        public Monster currentMonster;
        public IDamageOverflowHandler damageOverflowHandler;

        public event Action<BigNumber, BigNumber, bool> OnMonsterDamaged;
        public event Action<BigNumber, BigNumber, bool> OnPlayerDamaged;

        public void CreatePlayer(BigNumber maxHp)
        {
            HealthManager.Instance.Create(playerId, maxHp);
        }

        public void CreateMonsters(LevelConfigSO levelConfig, int levelIndex)
        {
            ClearMonsters();

            if (levelConfig == null || levelConfig.levels == null)
            {
                return;
            }

            if (levelIndex < 0 || levelIndex >= levelConfig.levels.Length)
            {
                return;
            }

            LevelConfigSO.LevelData level = levelConfig.levels[levelIndex];
            if (level == null || level.monsters == null)
            {
                return;
            }

            int monsterNumber = 0;
            for (int i = 0; i < level.monsters.Length; i++)
            {
                LevelConfigSO.MonsterSpawn spawn = level.monsters[i];
                if (spawn == null || spawn.monster == null || spawn.count <= 0)
                {
                    continue;
                }

                for (int j = 0; j < spawn.count; j++)
                {
                    string id = "monster_" + monsterNumber;
                    Monster monster = spawn.monster.CreateMonster(id);
                    monster.health = HealthManager.Instance.Create(id, spawn.monster.maxHp);
                    monsters.Add(monster);
                    monsterNumber++;
                }
            }

            currentMonster = GetFirstAliveMonster();
        }

        public bool ApplyFire(int shotCount, BigNumber damagePerShot)
        {
            if (shotCount <= 0)
            {
                return IsLevelCleared();
            }

            int shotsLeft = shotCount;

            while (shotsLeft > 0)
            {
                Monster target = GetFirstAliveMonster();
                if (target == null)
                {
                    break;
                }

                currentMonster = target;

                BigNumber finalDamage = target.ModifyIncomingDamage(damagePerShot);
                HealthState monsterHealth = HealthManager.Instance.Get(target.id);
                BigNumber hpBefore = monsterHealth.currentHp;

                BigNumber actualDamage = finalDamage;
                BigNumber overflowDamage = BigNumber.Zero;

                if (actualDamage > hpBefore)
                {
                    overflowDamage = actualDamage - hpBefore;
                    actualDamage = hpBefore;
                }

                if (overflowDamage > BigNumber.Zero && damageOverflowHandler != null)
                {
                    damageOverflowHandler.OnOverflowDamage(overflowDamage, target);
                }

                if (actualDamage > BigNumber.Zero)
                {
                    HealthManager.Instance.Damage(target.id, actualDamage);
                }

                BigNumber hpAfter = monsterHealth.currentHp;
                bool died = HealthManager.Instance.IsDead(target.id);

                if (OnMonsterDamaged != null)
                {
                    OnMonsterDamaged(hpBefore, hpAfter, died);
                }

                BigNumber counterDamage = target.OnHitReceived(actualDamage, died);
                if (counterDamage > BigNumber.Zero)
                {
                    bool playerDied = ApplyPlayerDamage(counterDamage);
                    if (playerDied)
                    {
                        break;
                    }
                }

                shotsLeft--;

                if (died)
                {
                    currentMonster = GetFirstAliveMonster();
                }
            }

            currentMonster = GetFirstAliveMonster();
            return IsLevelCleared();
        }

        public bool MonsterAttack()
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                Monster monster = monsters[i];
                if (monster == null || monster.IsDead())
                {
                    continue;
                }

                bool playerDied = ApplyPlayerDamage(monster.Attack);
                if (playerDied)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ApplyPlayerDamage(BigNumber damage)
        {
            HealthState playerHealth = HealthManager.Instance.Get(playerId);
            if (playerHealth == null)
            {
                return true;
            }

            BigNumber before = playerHealth.currentHp;

            HealthManager.Instance.Damage(playerId, damage);

            BigNumber after = playerHealth.currentHp;
            bool died = HealthManager.Instance.IsDead(playerId);

            if (OnPlayerDamaged != null)
            {
                OnPlayerDamaged(before, after, died);
            }

            return died;
        }

        public bool IsLevelCleared()
        {
            if (monsters.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] != null && !monsters[i].IsDead())
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsPlayerDead()
        {
            return HealthManager.Instance.IsDead(playerId);
        }

        public Monster GetFirstAliveMonster()
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] != null && !monsters[i].IsDead())
                {
                    return monsters[i];
                }
            }

            return null;
        }

        public BigNumber GetMonsterHp()
        {
            Monster monster = currentMonster;
            if (monster == null || monster.IsDead())
            {
                monster = GetFirstAliveMonster();
            }

            if (monster == null || monster.health == null)
            {
                return BigNumber.Zero;
            }

            return monster.health.currentHp;
        }

        public BigNumber GetPlayerHp()
        {
            HealthState playerHealth = HealthManager.Instance.Get(playerId);
            if (playerHealth == null)
            {
                return BigNumber.Zero;
            }

            return playerHealth.currentHp;
        }

        public string GetMonsterName()
        {
            Monster monster = currentMonster;
            if (monster == null || monster.IsDead())
            {
                monster = GetFirstAliveMonster();
            }

            if (monster == null)
            {
                return "";
            }

            return monster.MonsterName;
        }

        public int GetAliveMonsterCount()
        {
            int count = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] != null && !monsters[i].IsDead())
                {
                    count++;
                }
            }
            return count;
        }

        private void ClearMonsters()
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                if (monsters[i] != null)
                {
                    HealthManager.Instance.Remove(monsters[i].id);
                }
            }

            monsters.Clear();
            currentMonster = null;
        }
    }
}
