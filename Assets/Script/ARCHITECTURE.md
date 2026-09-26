# Script Architecture

這個資料夾依照「大範疇」分成多個模組。每個模組都有一個 Manager 當作對外窗口。

## 範疇與 Manager

| 範疇 | Manager | 職責 |
|---|---|---|
| Core | （無 Manager） | 共用工具：BigNumber、Singleton、初始化介面與註冊表 |
| Data | `DataManager` | 遊戲設定資料（GameConfigSO） |
| Health | `HealthManager` | 統一血量系統：玩家與怪物都用同一套扣血／回血 |
| Scoring | `ScoringManager` | 結算分數、充能條、開火次數 |
| Combat | `CombatManager` | 怪物、玩家傷害、怪物攻擊 |
| Board | `BoardManager` | 盤面、多顆球、釘子、袋口、球上分數、物理事件、發射器瞄準 |
| Input | `InputManager` | 力度條輸入、滑鼠指標位置與發射事件 |
| UI | `BattleUIManager` / `FloatingTextManager` | 分數、充能、HP、結算顯示；飄字生成與動畫（UI 層 Canvas，自動建立） |
| Flow | `BattleManager` / `SetupManager` | 串接各 Manager，控制初始化與一層戰鬥流程 |

## 存取規則

1. 其他範疇要使用某個範疇的功能，只能呼叫該範疇的 Manager。
2. 小功能類別（例如 `Health`、`ChargeMeter`、`PowerGauge`）是 Manager 的內部工具，不從其他範疇直接呼叫。
3. `BattleManager` 是唯一同時協調多個 Manager 的地方。
4. Manager 之間盡量單向依賴：
   - `CombatManager` → `HealthManager`
   - `SetupManager` → 所有 `ISetupInitializer` / `ISetupRunner` 實作者
   - `BattleManager` → `DataManager` / `ScoringManager` / `CombatManager` / `BoardManager` / `InputManager` / `BattleUIManager`
5. 事件用 C# `event` 或 Manager 的公開方法傳遞，不要讓小功能互相抓取。

## 初始化與啟動流程 (SetupManager)

`SetupManager` 分成兩個區，目的是避免「還沒初始化完成就被使用」造成空引用。

### 初始化區：`initializers`

- 所有需要初始化的程式都實作 `ISetupInitializer`。
- 它們在自己的 `Awake()` 呼叫：

```csharp
SetupRegistry.RegisterInitializer(this);
```

- `SetupManager.BeginSetup()` 會用 `ISetupInitializer[]` 取得所有初始化者，並逐一呼叫 `Setup(ISetupReporter reporter)`。
- 初始化程式完成後必須呼叫：

```csharp
reporter.ReportReady(this);
```

- `SetupManager` 收到所有 `ISetupInitializer` 的 `ReportReady` 後，才會輸出「初始化完成」。
- 完成判斷採用「所有 initializer 是否都在 readySet 中」，不依賴數量比較，重複註冊也不會卡住流程。
- 進度以完成數量計算，例如目前 2 個 initializer：完成 1 個為 0.5，完成全部為 1。

`SetupManager` 提供：

```csharp
public bool IsCompleted { get; }
public int CompletedCount { get; }
public int TotalCount { get; }
public float Progress01 { get; }
public event Action<float> OnSetupProgress;
public event Action OnSetupCompleted;
```

- 載入畫面可以直接讀 `Progress01` 做進度條，或訂閱 `OnSetupProgress`。

### 等待啟動區：`runners`

- 所有「必須等初始化完成才能開始運行」的程式都實作 `ISetupRunner`。
- 它們在自己的 `Awake()` 呼叫：

```csharp
SetupRegistry.RegisterRunner(this);
```

- `SetupManager` 只有在全部初始完成後，才會呼叫它們的：

```csharp
RunAfterSetup();
```

- 因此 `runners` 裡的程式可以安全使用各 Manager，不怕拿到尚未初始化的物件。
- `BattleManager` 就是 `ISetupRunner`，它會等到初始化完成後才呼叫 `StartBattle()`。

## 球與分數 (Multi-ball)

- 每一顆球自己持有分數：`BallController.score`。
- 球碰到釘子時，只加自己的 `score`，不會立刻加到能量條。
- 球加分後由 `BoardManager.OnBallScoreChanged` 通知外部；`BattleManager` 只更新分數 UI。
- `BoardManager` 追蹤目前場上所有球（`balls`），可同時存在多顆球。
- 球進袋或超時時，`BattleManager` 才會：
  1. 從該球讀取 `ballScore`
  2. 把球從場上移除
  3. 呼叫 `ScoringManager.SettleBall(ballScore, multiplier)`
  4. 由 `ChargeMeter` 把 `ballScore * multiplier` 加到能量條，並回傳可開火次數
- 只要 `ballsLeft > 0`，玩家可以繼續發射新球；場上可以同時有多顆球各自累積分數。
- 回合結束條件：`ballsLeft <= 0` 且 `BoardManager.ActiveBallCount <= 0`（所有球都已結算離場）。
- UI 只顯示代表球的分數，但每顆球自己的分數資料仍各自保存在 `BallController` 上。

## UI 更新規則

- UI 只在對應資料變動時更新，不做每次事件都刷新全部 UI。
- `BattleManager` 依事件種類只呼叫必要的 `BattleUIManager` 方法：
  - 發射新球、球碰到釘子 → 只更新分數
  - 球進袋／超時結算 → 更新充能、怪物資訊、玩家 HP（因為這些可能同時變動）
  - 怪物攻擊 → 只更新玩家 HP
  - 進入新關卡 → 只更新怪物名稱與 HP
  - 戰鬥開始 → 更新分數、充能、玩家 HP
- 未來新增 UI 欄位時，也應只更新該欄位，不要直接呼叫「更新全部 UI」。

## 發射器瞄準 (Launcher)

- 2D 板面在 XY 平面，`Launcher` 掛在發射器本體上，負責讓 `aimTransform` 的 local up 持續朝向滑鼠的世界座標。
- `InputManager.PointerScreenPosition` 每幀提供新 Input System 的滑鼠／觸控螢幕座標。
- `Launcher` 用 `Camera.ScreenToWorldPoint()` 把螢幕座標轉成世界座標，方向 = 滑鼠位置 - `aimTransform` 位置：
  - `maxAimAngle` 用 `Vector2.SignedAngle` 限制相對預設方向的最大角度，避免往後打。
  - `invertDirection` 在方向相反時可以打勾修正。
- `ApplyAimDirection()` 用 `Quaternion.Euler(0, 0, Atan2(-dir.x, dir.y))` 讓 `aimTransform.up` 對準目標方向。
- `BoardManager.ShootBall()` 發射方向是 `shootPoint.up`（回傳 `Vector2`）；只要把 `BoardManager.shootPoint` 指到 `Launcher.aimTransform`，發射方向就會跟著滑鼠。
- 發射器和板面配置：
  - 發射器放在板面頂部中央，預設方向 = `-pivot.up`（往下打）。
  - `pivot` 指向不旋轉的發射器本體。
  - `aimTransform` 指向會被旋轉的槍口。

## 2D 物理規則

這個專案是 Unity 2D（`com.unity.feature.2d`），只用 2D 物理，不要混用 3D 元件：

- 球：`Rigidbody2D` + `CircleCollider2D`，建議 Collision Detection 設 `Continuous`、Interpolate 設 `Interpolate`。
- 釘子：`Peg` 掛在有 `Collider2D` 的物件上，球用 `OnCollisionEnter2D` 加自己的分數。
- 袋口：`Pocket` 掛在 `Is Trigger` 的 `Collider2D` 上，球用 `OnTriggerEnter2D` 觸發結算。
- 發射：`Rigidbody2D.AddForce(direction * shootForce * power01, ForceMode2D.Impulse)`；`spawnOffSet` 改成沿垂直於發射方向偏移，避免多顆球重疊在同一點。
- 板面在 XY 平面、重力方向是 `-Y`（Physics2D 預設），相機建議 Orthographic 從 `-Z` 看向 `+Z`。
- 舊的 3D 資產（`Rigidbody`、`Collider`、`physicMaterial`、3D Prefab、URP 設定）都沒有帶過來。

## 目前簡化取捨

- `ChargeMeter` 的開火次數先用簡單 `while` 迴圈，並有 `maxFirePerCall` 上限；之後可改成批次數學結算。
- `SetupManager` 透過 `SetupRegistry` 在執行時收集 `ISetupInitializer` / `ISetupRunner`，不再使用大範圍的 `MonoBehaviour[]`。
- 尚未加入 asmdef；先用資料夾與命名空間 `Pinball.*` 分層。
- `BigNumber` 只支援非負數。

## 怪物與關卡設定

- `Monster` 是抽象父類，子類別：
  - `NormalMonster`：普通怪物
  - `CounterMonster`：反擊怪物，每受擊 N 次反擊
  - `ArmoredMonster`：高甲怪物，套用固定值減傷 + 10% 下限
  - `BossMonster`：Boss 房怪物，先和 `NormalMonster` 一樣是空模板，之後再加 Boss 技能
- `MonsterData` 是 ScriptableObject 定義基底，子類別：
  - `NormalMonsterData`
  - `CounterMonsterData`
  - `ArmoredMonsterData`
  - `BossMonsterData`
- `LevelConfigSO` 可以設定：
  - 關卡數量（`levels` 陣列長度）
  - 每關有哪些怪物資料資產（`MonsterData`）
  - 每種怪物數量（`count`）
  - 每關是否為 Boss 房（`isBoss`）；若關卡內有 `BossMonsterData` 也會自動視為 Boss 房
- `GameConfigSO.levelConfig` 指向一個 `LevelConfigSO`，不同難度可以建立不同的 `LevelConfigSO` 資產。
- `CombatManager` 負責建立與管理多隻怪物，`BattleManager` 負責一關一關推進。
- 進入 Boss 房時，`BattleManager` 會呼叫 `ScoringManager.ClearCharge()` 清空能量條。

## 攻擊結算規則

- `CombatManager.ApplyFire(shotCount, damagePerShot)` 會把 `shotCount` 當成「多次獨立攻擊」。
- 每一次攻擊都獨立經過：
  1. `Monster.ModifyIncomingDamage()`（例如高甲怪固定值減傷）
  2. 計算單次傷害是否大於怪物剩餘 HP
  3. 把溢出傷害交給 `IDamageOverflowHandler`（未來肉鴉接口）
  4. 最後才扣怪物 HP
  5. 呼叫 `Monster.OnHitReceived()`（例如反擊怪增加受擊計數）
- 如果怪物在中途死亡，剩餘攻擊次數會自動轉向下一隻存活怪物。
- ⚠️ **已知問題，尚未修**：`BigNumber` 是浮點數，累積多次小額傷害會留下殘值。
  例如 `ArmoredMonster`（`maxHp = 10`、`armor = 5`）被 100 發「每發 1 點」打完後，
  因為固定值減傷落在 10% 下限，每發實際傷害是 `0.1`，**HP 最後會停在 `2.2e-16` 而不是 `0`**。
- 目前 `Health.IsDead()` 只有 `currentHp <= BigNumber.Zero`，所以怪物**不會死**；
  而 `BigNumber.ToDisplayString()` 對 `< 1000` 的值用 `"0.##"`，會把 `2.2e-16` 印成 `"0"`，
  **讓 UI 看起來像「0 血卻沒死」**。
- 修法見 `Docs/Decision-Log.md` 2.15 與 `Docs/Design.md` 附錄 A.9：
  死亡判定改用相對容差（`HP ≤ maxHp × 1e-9`）並夾成剛好的 `0`，顯示層不得把非零值印成 `0`。
