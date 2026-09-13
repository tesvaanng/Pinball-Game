using UnityEngine;
using Pinball.Core;

namespace Pinball.Data
{
    [CreateAssetMenu(menuName = "Pinball/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        public BigNumber chargeCapacity = 100;
        public BigNumber damagePerShot = 1;
        public BigNumber playerMaxHp = 100;
        public int ballsPerRound = 8;
        public float ballTimeoutSeconds = 8f;
        public int maxRounds = 100;

        [Header("Levels")]
        public LevelConfigSO levelConfig;
    }
}
