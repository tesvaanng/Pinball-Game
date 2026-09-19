namespace Pinball.Input
{
    public class PowerGauge
    {
        public float power;
        public bool isCharging;
        public float chargeSpeed = 1f;
        public float maxCharge = 1f;

        public void StartCharge()
        {
            isCharging = true;
            power = 0f;
        }

        public float UpdateCharge(float deltaTime)
        {
            if (!isCharging) return 0;

            power += chargeSpeed * deltaTime;
            if (power > maxCharge)
            {
                power = maxCharge;
            }
            return power;
        }

        public float Release()
        {
            isCharging = false;
            float result = power;
            power = 0f;
            return result;
        }
    }
}
