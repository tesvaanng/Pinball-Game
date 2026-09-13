namespace Pinball.Input
{
    public class PowerGauge
    {
        public float power;
        public bool isCharging;
        public float chargeSpeed = 1f;

        public void StartCharge()
        {
            isCharging = true;
            power = 0f;
        }

        public void UpdateCharge(float deltaTime)
        {
            if (!isCharging) return;

            power += chargeSpeed * deltaTime;
            if (power > 1f)
            {
                power = 1f;
            }
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
