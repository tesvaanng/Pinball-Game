using System;

namespace Pinball.Rules
{
    /// <summary>
    /// 一顆球的飛行過程。對應 Docs/Design.md 附錄 A.4。
    ///
    /// 這一層<b>完全不管物理</b> —— Unity 層負責球怎麼飛，
    /// 飛的過程中每碰到一顆釘子就呼叫 <see cref="RegisterPegHit(string)"/>，
    /// 最後由 <see cref="BattleSession.SettleShot"/> 收尾。
    /// </summary>
    public sealed class ShotSession
    {
        private readonly GameContent _content;

        /// <summary>本層第幾顆球（從 0 開始）。</summary>
        public int ShotIndex { get; private set; }

        /// <summary>「球上分數」。尚未兌現，落袋時才乘上袋口倍率。</summary>
        public BigNumber PegScore { get; private set; }

        /// <summary>碰到幾顆釘子。飄字與統計用。</summary>
        public int PegHitCount { get; private set; }

        /// <summary>已經飛了幾秒。由 Unity 層呼叫 <see cref="Advance"/> 推進。</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>已經結算完畢，不能再加分。</summary>
        public bool IsSettled { get; private set; }

        /// <summary>是否已經達到超時門檻（附錄 A.4 的條件 b）。</summary>
        public bool HasTimedOut
        {
            get { return ElapsedSeconds >= _content.ShotTimeoutSeconds; }
        }

        internal ShotSession(GameContent content, int shotIndex)
        {
            _content = content;
            ShotIndex = shotIndex;
            PegScore = BigNumber.Zero;
            PegHitCount = 0;
            ElapsedSeconds = 0f;
        }

        /// <summary>推進計時器。Unity 層每個 frame 呼叫一次。</summary>
        public void Advance(float deltaSeconds)
        {
            if (IsSettled || deltaSeconds <= 0f) return;
            ElapsedSeconds += deltaSeconds;
        }

        /// <summary>碰到一顆釘子（用 id 查分數）。回傳這次加了多少分，方便飄字顯示。</summary>
        public BigNumber RegisterPegHit(string pegId)
        {
            return RegisterPegHit(_content.GetPegScore(pegId));
        }

        /// <summary>碰到一顆釘子（已有定義）。</summary>
        public BigNumber RegisterPegHit(PegDef peg)
        {
            return RegisterPegHit(peg.Score);
        }

        /// <summary>直接加一個分數。回傳實際加了多少（可能是 0）。</summary>
        public BigNumber RegisterPegHit(BigNumber score)
        {
            if (IsSettled) return BigNumber.Zero;

            // 純結構釘（peg_wall / peg_kicker）分數為 0，但仍然算一次碰撞。
            PegHitCount++;
            if (!score.IsZero)
            {
                PegScore += score;
            }
            return score;
        }

        internal void MarkSettled()
        {
            IsSettled = true;
        }
    }
}
