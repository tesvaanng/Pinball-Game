using Pinball.Core;

namespace Pinball.Scoring
{
    public struct BallResult
    {
        public BigNumber ballScore;
        public BigNumber multiplier;
        public BigNumber finalScore;
        public int fireCount;
    }

    public class ScoringManager : Singleton<ScoringManager>
    {
        public ChargeMeter chargeMeter;

        public BigNumber CurrentCharge
        {
            get
            {
                if (chargeMeter == null) return BigNumber.Zero;
                return chargeMeter.charge;
            }
        }

        public void Init(BigNumber capacity)
        {
            chargeMeter = new ChargeMeter(capacity);
        }

        public BallResult SettleBall(BigNumber ballScore, BigNumber multiplier)
        {
            if (chargeMeter == null)
            {
                Init(100);
            }

            BallResult result = new BallResult();
            result.ballScore = ballScore;
            result.multiplier = multiplier;
            result.finalScore = ballScore * multiplier;
            result.fireCount = chargeMeter.Add(result.finalScore);
            return result;
        }

        public void ResetAll()
        {
            if (chargeMeter != null)
            {
                chargeMeter.Clear();
            }
        }
    }
}
