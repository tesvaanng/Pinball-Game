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
            public MonsterSpawn[] monsters;
        }

        public LevelData[] levels;

        public int LevelCount
        {
            get { return levels == null ? 0 : levels.Length; }
        }
    }
}
