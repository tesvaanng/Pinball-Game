using Pinball.Core;
using UnityEngine;

namespace Pinball.Scoring
{
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

        public BigNumber ChargeCapacity
        {
            get
            {
                if (chargeMeter == null) return BigNumber.Zero;
                return chargeMeter.capacity;
            }
        }

        public void Init(BigNumber capacity)
        {
            chargeMeter = new ChargeMeter(capacity);
        }

        public void AddCharge(BigNumber amount)
        {
            if (chargeMeter == null)
            {
                Init(100);
            }

            chargeMeter.Add(amount);
        }

        public bool TryConsumeOneCharge()
        {
            if (chargeMeter == null)
            {
                return false;
            }

            return chargeMeter.TryConsumeOne();
        }

        public void ResetAll()
        {
            ClearCharge();
        }

        public void ClearCharge()
        {
            if (chargeMeter != null)
            {
                chargeMeter.Clear();
            }
        }
    }
}
