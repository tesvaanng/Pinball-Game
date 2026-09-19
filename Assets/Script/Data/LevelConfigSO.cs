using System;
using UnityEngine;
using Pinball.Combat;

namespace Pinball.Data
{
    [CreateAssetMenu(menuName = "Pinball/Level Config")]
    public class LevelConfigSO : ScriptableObject
    {
        [Serializable]
        public class MonsterSpawn
        {
            public MonsterData monster;
            public int count = 1;
        }

        [Serializable]
        public class LevelData
        {
            public string levelName = "Level";
            public bool isBoss;
            public MonsterSpawn[] monsters;
        }

        public LevelData[] levels;

        public int LevelCount
        {
            get { return levels == null ? 0 : levels.Length; }
        }

        public bool IsBossLevel(int index)
        {
            if (levels == null) return false;
            if (index < 0 || index >= levels.Length) return false;

            LevelData level = levels[index];
            if (level == null) return false;
            if (level.isBoss) return true;

            if (level.monsters == null) return false;

            for (int i = 0; i < level.monsters.Length; i++)
            {
                MonsterSpawn spawn = level.monsters[i];
                if (spawn == null || spawn.monster == null) continue;
                if (spawn.monster is BossMonsterData) return true;
            }

            return false;
        }
    }
}
