namespace Pinball.Core
{
    public class DeterministicRng
    {
        public ulong state;

        public DeterministicRng(ulong seed)
        {
            state = seed == 0 ? 88172645463325252UL : seed;
        }

        public ulong NextULong()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 2685821657736338717UL;
        }

        public float NextFloat()
        {
            return (NextULong() >> 40) / 16777216.0f;
        }

        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextULong() % (ulong)(max - min));
        }
    }
}
