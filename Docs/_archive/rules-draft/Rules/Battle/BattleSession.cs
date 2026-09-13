using System;
using System.Collections.Generic;

namespace Pinball.Rules
{
    /// <summary>
    /// 基礎玩法的完整狀態機。對應 Docs/Design.md 附錄 A（A.5 ~ A.8）。
    ///
    /// <para><b>Unity 層要做的三件事：</b></para>
    /// <list type="number">
    /// <item>產生「碰釘」事件 → <c>shot.RegisterPegHit(pegId)</c></item>
    /// <item>產生「落袋／超時」事件 → <c>SettleShot(shot, pocketIndex)</c>（超時傳 -1）</item>
    /// <item>播放回傳的事件（結算、充能、逐發開火）</item>
    /// </list>
    ///
    /// <para><b>典型呼叫順序：</b></para>
    /// <code>
    /// var battle = new BattleSession(GameContent.CreateM0Placeholder(), seed);
    /// battle.StartRun();
    /// battle.StartFloor(GameContent.CreateM0PlaceholderMonster(), isBoss: false);
    ///
    /// while (battle.CanThrowBall) {
    ///     var shot = battle.BeginShot();
    ///     // ... Unity 物理；每碰一顆釘就 shot.RegisterPegHit("peg_normal") ...
    ///     var settled = battle.SettleShot(shot, pocketIndex);   // 超時傳 -1
    ///     Play(settled);
    /// }
    ///
    /// if (battle.CanEndRound) Play(battle.EndRound());          // 怪物攻擊
    /// if (battle.IsFloorClear) battle.StartFloor(nextMonster, isBoss); // 下一層
    /// </code>
    /// </summary>
    public sealed class BattleSession
    {
        /// <summary>
        /// 單次結算最多開火幾次。這是<b>防呆</b>，不是設計限制（2.4.2 說開火沒有硬上限）。
        /// M0 用不到；如果它真的被觸發，那就是 `C` 需要成長曲線的信號（見 6.4）。
        /// 觸發時剩下的能量會留在條上，不會被丟棄。
        /// </summary>
        public const int MaxFiresPerSettlement = 100000;

        private readonly GameContent _content;
        private readonly List<FireEvent> _fireBuffer = new List<FireEvent>(16);

        private int _ballsInFlight;
        private int _shotIndexThisFloor;

        public GameContent Content { get { return _content; } }
        public RngBank Rng { get; private set; }
        public PlayerState Player { get; private set; }
        public MonsterState Monster { get; private set; }
        public ChargeMeter Charge { get; private set; }

        /// <summary>目前樓層（第一層為 1）。<see cref="StartRun"/> 後為 0。</summary>
        public int FloorNumber { get; private set; }

        public bool IsBossFloor { get; private set; }

        /// <summary>本局是否已經結束。</summary>
        public bool IsRunOver { get { return Outcome != RunOutcome.InProgress; } }

        public RunOutcome Outcome { get; private set; }

        /// <summary>這一輪還剩幾顆球沒投。</summary>
        public int BallsRemaining { get; private set; }

        /// <summary>目前第幾輪（從 1 開始）。</summary>
        public int RoundNumber { get; private set; }

        /// <summary>本層目前為止投了幾顆球（含正在飛的）。</summary>
        public int ShotsThrownThisFloor { get; private set; }

        /// <summary>還在飛的球數。M0 是 0 或 1。</summary>
        public int BallsInFlight { get { return _ballsInFlight; } }

        /// <summary>本層是否已經打完 —— 怪物死亡，而且已經沒有球在飛（附錄 A.7）。</summary>
        public bool IsFloorClear
        {
            get { return Monster != null && Monster.IsDead && _ballsInFlight == 0; }
        }

        /// <summary>現在可以投球嗎。</summary>
        public bool CanThrowBall
        {
            get
            {
                return !IsRunOver
                       && Monster != null
                       && !Monster.IsDead
                       && BallsRemaining > 0;
            }
        }

        /// <summary>現在該結算一輪（怪物攻擊）了嗎。</summary>
        public bool CanEndRound
        {
            get
            {
                return !IsRunOver
                       && Monster != null
                       && !Monster.IsDead
                       && BallsRemaining <= 0
                       && _ballsInFlight == 0;
            }
        }

        public BattleSession(GameContent content, ulong runSeed)
        {
            if (content == null) throw new ArgumentNullException("content");

            _content = content;
            Rng = new RngBank(runSeed);
            Charge = new ChargeMeter(content.ChargeCapacity, content.DamagePerFire);
            Player = new PlayerState(content.PlayerMaxHp);
            Outcome = RunOutcome.InProgress;
        }

        // ------------------------------------------------------------ Run 層

        /// <summary>開始一整局。玩家 HP 補滿、充能清空。</summary>
        public void StartRun()
        {
            Player = new PlayerState(_content.PlayerMaxHp);
            Charge.Reset();
            Monster = null;
            FloorNumber = 0;
            IsBossFloor = false;
            BallsRemaining = 0;
            RoundNumber = 0;
            ShotsThrownThisFloor = 0;
            _ballsInFlight = 0;
            _shotIndexThisFloor = 0;
            Outcome = RunOutcome.InProgress;
        }

        // ------------------------------------------------------------ 樓層層

        /// <summary>
        /// 進入一層。若是 Boss 房，充能條<b>在這裡清空</b>（附錄 A.7 步驟 3）。
        /// 玩家 HP <b>不恢復</b>。
        /// </summary>
        public void StartFloor(MonsterDef monsterDef, bool isBossFloor)
        {
            if (IsRunOver)
            {
                throw new InvalidOperationException("BattleSession: 本局已經結束，不能再進新樓層");
            }
            if (_ballsInFlight > 0)
            {
                throw new InvalidOperationException("BattleSession: 還有球在飛，不能換樓層");
            }

            Monster = new MonsterState(monsterDef);
            FloorNumber++;
            IsBossFloor = isBossFloor;

            if (isBossFloor)
            {
                Charge.Reset();
            }

            BallsRemaining = _content.BallsPerRound;
            RoundNumber = 1;
            ShotsThrownThisFloor = 0;
            _shotIndexThisFloor = 0;
        }

        // ------------------------------------------------------------ 輪層

        public ShotSession BeginShot()
        {
            if (!CanThrowBall)
            {
                throw new InvalidOperationException("BattleSession: 現在不能投球");
            }

            BallsRemaining--;
            _ballsInFlight++;
            return new ShotSession(_content, _shotIndexThisFloor++);
        }

        /// <summary>
        /// 結算一顆球。對應附錄 A.4／A.5。
        /// </summary>
        /// <param name="pocketIndex">袋口索引；<b>傳 -1 表示超時</b>（以 1× 結算）。</param>
        public ShotSettlement SettleShot(ShotSession shot, int pocketIndex)
        {
            if (shot == null) throw new ArgumentNullException("shot");
            if (shot.IsSettled)
            {
                throw new InvalidOperationException("BattleSession: 這顆球已經結算過了");
            }
            if (_ballsInFlight <= 0)
            {
                throw new InvalidOperationException("BattleSession: 沒有球在飛，不能結算");
            }

            bool timedOut = pocketIndex < 0;
            BigNumber multiplier = timedOut
                ? BigNumber.One
                : _content.GetPocketMultiplier(pocketIndex);

            ShotSettlement result = new ShotSettlement();
            result.PegScore = shot.PegScore;
            result.PocketIndex = timedOut ? -1 : pocketIndex;
            result.PocketMultiplier = multiplier;
            result.SettledScore = shot.PegScore * multiplier;
            result.TimedOut = timedOut;
            result.EnergyBefore = Charge.Energy;

            // A.5：E += S（溢出無損），然後逐條開火
            Charge.Credit(result.SettledScore);
            _fireBuffer.Clear();
            ResolveFires(_fireBuffer);
            result.Fires = _fireBuffer.ToArray();
            result.EnergyAfter = Charge.Energy;
            result.MonsterDied = Monster != null && Monster.IsDead;

            shot.MarkSettled();
            _ballsInFlight--;
            ShotsThrownThisFloor++;

            return result;
        }

        /// <summary>
        /// A.5 的開火迴圈。防鞭屍：怪物一死就停止，剩下的能量留在條上。
        /// </summary>
        private void ResolveFires(List<FireEvent> into)
        {
            if (Monster == null) return;

            int sequence = 0;
            while (Charge.HasFullBar && !Monster.IsDead)
            {
                if (sequence >= MaxFiresPerSettlement)
                {
                    // 防呆：剩下的能量保留在條上，不丟棄。這代表 C 需要成長曲線（6.4）。
                    break;
                }

                Charge.SpendOneBar();

                FireEvent ev = new FireEvent();
                ev.Sequence = sequence;
                ev.Damage = Charge.DamagePerFire;
                ev.MonsterHpBefore = Monster.Hp;
                Monster.ApplyDamage(Charge.DamagePerFire);
                ev.MonsterHpAfter = Monster.Hp;
                ev.KilledMonster = Monster.IsDead;

                into.Add(ev);
                sequence++;
            }
        }

        /// <summary>
        /// 一輪的球全部用完、怪物還活著時呼叫。對應附錄 A.6。
        /// 怪物攻擊一次 → 沒死就補滿球數，進入下一輪。
        /// </summary>
        public RoundResult EndRound()
        {
            if (IsRunOver)
            {
                throw new InvalidOperationException("BattleSession: 本局已經結束");
            }
            if (Monster == null)
            {
                throw new InvalidOperationException("BattleSession: 還沒開始任何樓層");
            }
            if (Monster.IsDead)
            {
                throw new InvalidOperationException("BattleSession: 怪物已死，這一層應該結束了（IsFloorClear）");
            }
            if (_ballsInFlight > 0)
            {
                throw new InvalidOperationException("BattleSession: 還有球在飛，不能結束這一輪");
            }
            if (BallsRemaining > 0)
            {
                throw new InvalidOperationException("BattleSession: 這一輪還有 " + BallsRemaining + " 顆球沒投完");
            }

            RoundResult result = new RoundResult();
            result.RoundNumber = RoundNumber;
            result.MonsterAttack = Monster.AttackPower;
            result.PlayerHpBefore = Player.Hp;

            Player.ApplyDamage(Monster.AttackPower);
            result.PlayerHpAfter = Player.Hp;
            result.PlayerDied = Player.IsDead;

            if (result.PlayerDied)
            {
                Outcome = RunOutcome.PlayerDied;
                return result;
            }

            if (RoundNumber >= _content.MaxRounds)
            {
                result.RoundLimitReached = true;
                Outcome = RunOutcome.RoundLimitReached;
                return result;
            }

            RoundNumber++;
            BallsRemaining = _content.BallsPerRound;
            return result;
        }

        /// <summary>給 UI／除錯用的單行狀態摘要。</summary>
        public override string ToString()
        {
            return string.Format(
                "F{0}{1} R{2} balls={3} monster={4}/{5} player={6}/{7} charge={8}/{9} {10}",
                FloorNumber,
                IsBossFloor ? "(BOSS)" : "",
                RoundNumber,
                BallsRemaining,
                Monster == null ? "-" : Monster.Hp.ToString(),
                Monster == null ? "-" : Monster.MaxHp.ToString(),
                Player.Hp.ToString(),
                Player.MaxHp.ToString(),
                Charge.Energy.ToString(),
                Charge.Capacity.ToString(),
                Outcome);
        }
    }
}
