using System;

namespace Pinball.Rules
{
    /// <summary>
    /// 決定性隨機：xorshift64*。整條流的狀態就是一個 ulong，所以可以存檔、可以分叉。
    ///
    /// 為什麼不用 UnityEngine.Random：它是全域狀態、無法分叉、無法存檔回復 —— 見 Docs/Design.md 4.2。
    /// </summary>
    public struct DeterministicRng
    {
        private const ulong FallbackSeed = 0x9E3779B97F4A7C15UL;

        private ulong _state;

        public DeterministicRng(ulong seed)
        {
            _state = seed == 0UL ? FallbackSeed : seed;
        }

        /// <summary>可存檔的狀態。搭配 <see cref="DeterministicRng(ulong)"/> 就能還原。</summary>
        public ulong State
        {
            get { return _state; }
        }

        public void RestoreState(ulong state)
        {
            _state = state == 0UL ? FallbackSeed : state;
        }

        public ulong NextUInt64()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>[0, 1)</summary>
        public double NextDouble()
        {
            // 取高 53 bits，與 double 的精度對齊
            return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>[min, max)</summary>
        public double Range(double min, double max)
        {
            return min + NextDouble() * (max - min);
        }

        /// <summary>[minInclusive, maxExclusive)</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % span);
        }

        /// <summary>回傳 true 的機率為 p（p 會被夾到 [0,1]）。</summary>
        public bool Chance(double p)
        {
            if (p <= 0.0) return false;
            if (p >= 1.0) return true;
            return NextDouble() < p;
        }

        /// <summary>
        /// 產生一條獨立的子流。同一個種子 + 同一個 tag 永遠得到同一條流。
        /// 用途：讓「玩家在商店多按一次」不會改變地圖（4.2 的分流設計）。
        /// </summary>
        public DeterministicRng Fork(ulong streamTag)
        {
            ulong mixed = _state ^ (streamTag * 0x9E3779B97F4A7C15UL);
            // 攪拌幾次，避免相近的 tag 產生相近的流
            mixed ^= mixed >> 30;
            mixed *= 0xBF58476D1CE4E5B9UL;
            mixed ^= mixed >> 27;
            mixed *= 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return new DeterministicRng(mixed);
        }
    }

    /// <summary>
    /// RNG 分流。每個用途一條獨立流，互不干擾 —— 見 Docs/Design.md 4.2。
    /// </summary>
    public enum RngStream
    {
        /// <summary>地圖生成。</summary>
        Map = 1,

        /// <summary>獎勵三選一。</summary>
        Reward = 2,

        /// <summary>每發球的判定（物理噪聲等）。</summary>
        Shot = 3,

        /// <summary>粒子、音效變體。<b>絕不能影響玩法判定。</b></summary>
        Cosmetic = 4,

        /// <summary>怪物行為變體。</summary>
        Enemy = 5
    }
}
