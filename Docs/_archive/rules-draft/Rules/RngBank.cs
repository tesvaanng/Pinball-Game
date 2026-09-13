using System;

namespace Pinball.Rules
{
    /// <summary>
    /// 一趟投球的五條 RNG 流。見 Docs/Design.md 4.2。
    /// 是 class 而非 struct，這樣 Unity 層可以直接寫 <c>battle.Rng.Shot.NextDouble()</c>。
    /// </summary>
    public sealed class RngBank
    {
        /// <summary>地圖生成。</summary>
        public DeterministicRng Map;

        /// <summary>獎勵三選一。</summary>
        public DeterministicRng Reward;

        /// <summary>每發球的判定（物理噪聲等）。</summary>
        public DeterministicRng Shot;

        /// <summary>粒子、音效變體。<b>絕不能影響玩法判定。</b></summary>
        public DeterministicRng Cosmetic;

        /// <summary>怪物行為變體。</summary>
        public DeterministicRng Enemy;

        public RngBank(ulong runSeed)
        {
            DeterministicRng root = new DeterministicRng(runSeed);
            Map = root.Fork((ulong)RngStream.Map);
            Reward = root.Fork((ulong)RngStream.Reward);
            Shot = root.Fork((ulong)RngStream.Shot);
            Cosmetic = root.Fork((ulong)RngStream.Cosmetic);
            Enemy = root.Fork((ulong)RngStream.Enemy);
        }

        /// <summary>存檔用：把五條流的狀態一次抓出來。</summary>
        public ulong[] CaptureStates()
        {
            return new[] { Map.State, Reward.State, Shot.State, Cosmetic.State, Enemy.State };
        }

        /// <summary>讀檔用：還原五條流。</summary>
        public void RestoreStates(ulong[] states)
        {
            if (states == null || states.Length != 5)
            {
                throw new ArgumentException("RngBank: 需要剛好 5 個流狀態", "states");
            }
            Map.RestoreState(states[0]);
            Reward.RestoreState(states[1]);
            Shot.RestoreState(states[2]);
            Cosmetic.RestoreState(states[3]);
            Enemy.RestoreState(states[4]);
        }
    }
}
