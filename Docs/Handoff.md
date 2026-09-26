# 交接文件（Handoff）

> ## ⚠️ 這不是規格
> **規格是 `Docs/Design.md`。決策理由在 `Docs/Decision-Log.md`。**
> 本文件是**某個時間點的狀態快照**，用途只有一個：
> 讓第一次接手的人或 AI 在五分鐘內知道「**現在在哪、剛做了什麼、下一步卡在哪**」。
> **如果本文件與 `Design.md` 衝突，一律以 `Design.md` 為準。**
>
> **快照時間**：2026-09-19　|　**HEAD**：`b19b945`
> 時間久了請直接看 `git log`，不要相信這裡的細節。

---

## 1. 三十秒摘要

Unity 2D 彈珠遊戲，目標是肉鴿大數字遊戲。核心循環、基礎規則、附錄 A 的實作規格都已定案。

**這次工作做了三件事：**

1. **修掉一個會讓怪物打不死的 bug**（浮點殘量導致 `IsDead()` 回傳 false）
2. **把盤面從「手擺場景物件」改成「資料驅動」**，並生出第一版盤面（**刻意**用均勻 Plinko 網格當基準線）
3. **把輸入從「只有力度」改成「瞄準 ＋ 力度」**，並廢除進場層（這是設計變更，見 §6）

**目前最大的缺口**：盤面生成器有了，但**沒有任何量測工具**，所以盤面好壞無法判斷。

---

## 2. 專案基本資訊

| 項目 | 值 |
|---|---|
| 路徑 | `W:\unity\unity project\Pinball Game` |
| Unity | **2022.3.45f1**（安裝在 `W:\unity\version\2022.3.45f1`） |
| 物理 | 2D（`Rigidbody2D` / `CircleCollider2D`） |
| 遠端 | `https://github.com/tesvaanng/pinball-game` |
| 命名空間 | `Pinball.*` |

### 權威文件（依重要性）

| 檔案 | 角色 |
|---|---|
| `Docs/Design.md` | **唯一權威規格。** 附錄 A 是可直接實作的無歧義規則；附錄 B 是實作思路 |
| `Docs/Decision-Log.md` | 每個決定的理由、**已推翻的方案（不要重提）**、討論中確立的原則 |
| `Assets/Script/ARCHITECTURE.md` | 程式碼的模組切分與存取規則（描述現況） |
| `Docs/_archive/` | 舊文件。**不是規格，不要引用** |
| 本文件 | 狀態快照。不是規格 |

> **閱讀順序建議**：`Design.md` 第 0 章 → 第 1 章 → 附錄 A → 附錄 B → 本文件 §5–§8。

---

## 3. 這次的工作（`b19b945`）

### 3.1 修掉浮點殘量的死亡判定（原 bug 記錄在 `Decision-Log` 2.15）

**症狀**：`ArmoredMonster`（`maxHp = 10`、`armor = 5`）被 100 發「每發 1 點」打完後，
UI 顯示 0 血，但 `HealthManager.Instance.IsDead()` 回傳 `false`，怪物不死。

**原因鏈**：

| # | 發生什麼 |
|---|---|
| 1 | 固定值減傷把 1 點打到負數 → 落在 **10% 下限** → 每發實際傷害是 **0.1** |
| 2 | `0.1` 在二進位浮點不是精確值；`BigNumber` 的減法每扣一次血就在尾數加一點誤差 |
| 3 | 100 發之後 HP 停在約 **`2.2e-16`**，不是 `0` |
| 4 | `IsDead()` 只比 `currentHp <= 0` → `false` |
| 5 | 而 `ToDisplayString()` 把 `2.2e-16` 印成 **`"0"`** → **畫面說謊**，所以看起來像「0 血卻沒死」 |

**修法**：

| 檔案 | 改動 |
|---|---|
| `Assets/Script/Health/Health.cs` | 死亡判定改用**相對容差** `currentHp <= maxHp × 1e-9`；成立時把 `currentHp` 夾成**剛好的 `0`** |
| `Assets/Script/Core/BigNumber.cs` | `ToDisplayString()`：`0 < value < 0.01` 改用科學記號，**不再印成 `"0"`** |

**關鍵細節（不要改回去）**：

專案裡原本已經有一個 `BigNumber.IsNearlyZero()`（判斷 `exponent < -9`），而 `Health` 也在用它。
**那是「絕對」門檻，對這款遊戲不夠用**：

| maxHp | 累積誤差量級 | `IsNearlyZero()` 抓得到嗎 |
|---|---|---|
| 10（M0） | ~1e-15 | ✅ |
| 1e20（後期） | ~1e7 | ❌ **抓不到 → bug 會回來** |

處理方式：`IsNearlyZero()` **保留**（它判斷「這個數值本身是不是雜訊」仍然是對的），
但在 `Health` 裡**降為保險**，主判準是相對門檻。
`IsNearlyZero()` 的註解已明寫「這是絕對門檻，不要拿它判斷相對比例」。

> **驗證方式**：`ArmoredMonster`（`maxHp = 10`、`armor = 5`）＋ `shotCount = 100`、`damagePerShot = 1`
> → 以前打不死，現在會正常死亡。

### 3.2 盤面改成資料驅動 ＋ 第一版（均勻網格）

**新增兩個檔案：**

| 檔案 | 角色 |
|---|---|
| `Assets/Script/Board/BoardLayout.cs` | **資料**（`ScriptableObject`）。盤面的唯一真相 |
| `Assets/Script/Board/BoardBuilder.cs` | **生成器**（`MonoBehaviour`）。照資料生成盤面 |

**為什麼盤面是資料**（`Decision-Log` 2.18）：`Design.md` 6.1 的流程是「生成 → 量測 → 改 → 再量」，
會跑很多次。手擺的話改一次盤面要拖 300 顆釘子，而且 git diff 是一堆座標 YAML。
生成物帶 `HideFlags.DontSave`，**不會寫進場景檔**。

**v1 是刻意選的平均分佈**（`Design.md` 6.1.1）：
`Design.md` 2.2.4 明確說過均勻網格會讓瞄準與力度同時失去意義。
v1 偏偏就是均勻網格，因為：

1. 需要**基準線**——先量出「完全沒有結構」長什麼樣，才知道結構要加多少
2. 量測工具需要**有東西可量**
3. **預期它會在兩軸掃描中失敗**，那正是要證明的事

> ⚠️ **不要讓它變成最終版本。** 它的價值在被量測、被推翻，不在能玩。

### 3.3 文件更新

| 檔案 | 更新 |
|---|---|
| `Docs/Design.md` | 新增 **6.1.1**（v1 基準線、實作位置、v1 參數表、量測前的三個干擾源）；`2.2.4`／`0.5` 交叉引用同步 |
| `Docs/Decision-Log.md` | 新增 **2.18**（為什麼用平均分佈、為什麼盤面是資料、干擾源清單）；**2.15** 補上實作結果與 `IsNearlyZero` 的修正 |
| `Assets/Script/ARCHITECTURE.md` | 修正第 174 行：原本聲稱「已把幾乎為 0 的 HP 視為 0」，但程式當時並沒有做。改為誠實記錄已知問題 |

---

## 4. 檔案清單

```
新增
  Assets/Script/Board/BoardLayout.cs          ← 盤面資料（ScriptableObject）
  Assets/Script/Board/BoardBuilder.cs         ← 盤面生成器（MonoBehaviour）

修改
  Assets/Script/Health/Health.cs              ← 相對容差的死亡判定
  Assets/Script/Core/BigNumber.cs             ← ToDisplayString 不再把非零印成 "0"
  Docs/Design.md
  Docs/Decision-Log.md
  Assets/Script/ARCHITECTURE.md

  Assets/ScriptableObject/BoardLayout.asset   ← 由使用者在 Unity 建立（內容＝全部預設值）
  Assets/Script/Board/*.cs.meta               ← Unity 自動產生（沒有它們，GUID 會變、參照會斷）

使用者自己的在建工作（**刻意沒有由我提交**）
  Assets/Scenes/SampleScene.unity
  Assets/Phyics Materal/*.physicsMaterial2D
```

### 場景現況（重要）

場景 `Assets/Scenes/SampleScene.unity` 是**骨架**：四面牆、UI、9 個 Manager、發射器。
`All Peg` 和 `All Pocket` 是**空的容器**——盤面由 `BoardBuilder` 在執行時生成。

**場景中量到的實際數字**（這些是 `BoardLayout` 預設值的依據）：

| 項目 | 值 |
|---|---|
| 四面牆 | `bottomWall` y=-19.47、`topWall` y=11.53、`leftWall` x=-15.5、`rightWall` x=15.5，厚度 1 |
| **盤面內部** | **30 × 30**，x ∈ [-15, 15]，y ∈ [-18.97, 11.03] |
| 內部中心 | **`(0, -3.97)`** ＝ `Manchine Body` 的世界座標 |
| 發射器 | `Standed Shooter Point` 在世界 **(0, 11)**，預設朝下 |
| 球的實際大小 | prefab `scale 0.6` × `CircleCollider2D.radius 0.5` → **世界直徑 0.6** |

> ⚠️ `Design.md` 2.6.1 寫「球半徑 0.12」，但**實作是 0.3**。這個不一致還沒處理。

---

## 5. 怎麼用新的盤面程式（Unity 設定）

**① 掛生成器**：場景裡建一個空物件（或掛在 `BoardManager` 上）→ `Add Component` → **Board Builder**

**② 指定欄位**：

| 欄位 | 指到 | 留空的後果 |
|---|---|---|
| `Layout` | `Assets/ScriptableObject/BoardLayout.asset` | 用記憶體預設值（可跑，但參數不可調，Console 會提醒） |
| `Peg Prefab` | `Assets/Prefeb/Peg.prefab` | 不生成釘子，Console 會警告 |
| `Pocket Prefab` | `Assets/Prefeb/Pocket.prefab` | 不生成袋口，Console 會警告 |
| `Peg Parent` | 場景中的 **`All Peg`** | 全部塞在 BoardBuilder 自己底下 |
| `Pocket Parent` | 場景中的 **`All Pocket`** | 同上 |
| `Build On Awake` | 保持勾選 | 進 Play 不會生成 |

**③ 進 Play**。Console 應印出：

```
BoardBuilder: 生成了 315 顆釘子（19 欄 × 17 排，間距 1.5，半徑 0.2）。
BoardBuilder: 生成了 8 個袋口。
```

**編輯模式預覽**（不用 Play）：在元件名稱上**右鍵 → 生成盤面**。

### 四個必須知道的特性

| # | 特性 |
|---|---|
| 1 | **生成物不會被存進場景檔**（`HideFlags.DontSave`）。**無法手動拖動並保存**——要改盤面請改資料 |
| 2 | `Build()` 會清空 `Peg Parent` / `Pocket Parent` 的**所有**子物件。**不要把手擺的釘子放在 `All Peg` 底下**，會被砍掉 |
| 3 | 每次 Play 都重新生成，編輯模式的預覽是用完即丟的 |
| 4 | `Peg Parent` 的 **scale 必須是 1**，否則釘子大小會不對 |

### `BoardLayout` 參數對照

| 欄位 | 預設 | 說明 |
|---|---|---|
| `playAreaCenter` | `(0, -3.97)` | 盤面內部中心 |
| `playAreaSize` | `(30, 30)` | 盤面內部大小 |
| `generateUniformGrid` | ✅ | **關掉**才會改用 `pegs` 手動清單 |
| `pegSpacing` | `1.5` | 釘子間距。**建議 = 球直徑的 2～2.5 倍** |
| `pegRadius` | `0.2` | **限制：間距 − 2×半徑 > 球直徑**（1.5 − 0.4 = 1.1 > 0.6 ✅） |
| `topMargin` | `3` | 第一排離頂端多遠。**必須 > 球半徑 + 釘子半徑（= 0.5）**，否則球一出生就疊在釘子上 |
| `bottomMargin` | `1.5` | 最後一排離袋口帶多遠 |
| `sideMargin` | `1.5` | 左右淨空 |
| `staggerRows` | ✅ | 奇數排偏移半格。**這是真 Plinko**；關掉變方格，球容易直直穿過 |
| `pocketBandHeight` | `1.4` | 底部袋口帶高度 |
| `pocketWidthRatio` | `0.92` | 袋口寬 ÷ 格寬 |
| `pocketMultipliers` | `20,8,3,1,1,3,8,20` | **陣列長度決定袋口數量** |
| `pegs` / `pockets` | 空 | 手動清單（`pockets` 非空會優先於上面的倍率陣列） |

**⚠️ 這些數字綁著球的直徑。** 換球的大小、改牆的位置、改盤面尺寸 → 間距與半徑都要跟著改。

---

## 6. 這次也一起改掉的**設計**（不是程式）

`Design.md` 已升到 **v1.3**。如果你是從舊版文件接手的，這幾條要注意：

| 項目 | 舊 | 新（v1.3） |
|---|---|---|
| 輸入 | 只有力度條 | **瞄準（滑鼠即時轉向，±80°）＋ 力度（初速）並存** |
| 發射器 | 右側軌道，球沿軌道爬升 | **頂部中央**，預設朝下，直接射進盤面 |
| 「4 進場層」 | 核心概念 | ❌ **已廢除**（定義綁在軌道高度上） |
| 盤面驗收 | 5 檔力度 × 20 投 | **兩軸掃描**（見 §7） |
| 架構 | 規則層純 C# ＋ 第一天加 asmdef | ❌ **放棄**，追認現況：Manager 分層，不加 asmdef（`Decision-Log` 2.17） |

> **紅線（唯一沒妥協的一條）**：**永遠不做預測線。**
> 瞄準可以，預覽反彈不行——那會讓遊戲變成解謎（`Design.md` 2.1.7）。

---

## 7. 下一步：兩軸掃描（**這是什麼**）

### 它要回答的問題

> **你的兩個輸入（瞄準、力度）到底有沒有用？**

v1.3 把技術成分全押在「瞄準決定方向、力度決定初速」上，但**這目前只是假設**。
如果盤面設計得不好，可能出現「不管瞄哪裡結果都一樣」→ **瞄準是裝飾**。

### 為什麼要跑兩次

有兩個變數，**同時變兩個就分不出結果是誰造成的**。所以要控制變數：

| 掃描 | 固定 | 變動 | 次數 |
|---|---|---|---|
| **瞄準掃描** | 力度 **60%** | 瞄準角 5 檔（左40°、左20°、0°、右20°、右40°） | 每檔 **20 投** |
| **力度掃描** | 瞄準角 **正下方偏左 15°** | 力度 5 檔（20/40/60/80/100%） | 每檔 **20 投** |

每投一次記錄：**球進哪個袋口**、**球上分數**。

### 判讀

看兩件事：**集中**（同一檔的 20 投是否聚在一起）、**分開**（5 檔之間是否落在不同地方）。

**通過的長相**：

| 瞄準角 | 落點集中於 |
|---|---|
| 左 40° | 袋 0–1（20×、8×） |
| 左 20° | 袋 2（3×） |
| 0° | 袋 3–4（1×、1×） |
| 右 20° | 袋 5（3×） |
| 右 40° | 袋 6–7（8×、20×） |

**失敗的長相**：5 檔全部長得一樣（都是中央最高的鐘形分布）→ **那個輸入是死的。**

### 四種結果 → 四種行動 ⭐

| 瞄準 | 力度 | 意義 | 該做什麼 |
|---|---|---|---|
| ❌ | ❌ | 盤面沒有結構（**v1 預期結果**） | 加結構元素（擋板、漏斗、密度差），重測 |
| ✅ | ❌ | 方向有用、力度沒差 | 力度條是騙人的 → 改盤面讓初速有意義，或考慮拿掉力度條 |
| ❌ | ✅ | 初速有用、方向沒差 | 瞄準是多餘的 → 退回 v1.2 的單輸入設計 |
| ✅ | ✅ | 兩個輸入都成立 | 結構對了，繼續細調 |

> **「兩軸都必須過」不是為了嚴格**，是因為只過一軸就代表你多做了一個沒用的輸入。

### 但現在還不能跑

**① 三個干擾源必須先清掉**（見 §8）——否則同一檔的 20 投本來就會散開，量到的是雜訊。

**② 量測工具還不存在。** 需要：`設定瞄準角 + 力度 → 發射 → 等球落袋 → 記錄袋口與球上分數 → 重複 20 次 → 輸出表格`。

---

## 8. 已知問題與阻塞（**動量測之前必須處理**）

`Design.md` 6.1.1 與 `Decision-Log` 2.18 都記錄了，但**程式還沒改**：

| # | 位置 | 問題 | 影響 |
|---|---|---|---|
| 1 | `BoardManager.ShootBall` 的 `spawnOffSet`（**預設 0.5**） | 每次發射給一個**隨機橫向偏移** | **同一瞄準角丟 20 次每次落點都不同** → 量到的是雜訊，不是分布。量測時必須設成 0 |
| 2 | 同上，用的是 `UnityEngine.Random` | 全域狀態、無法分叉、無法存檔回復 | 違反決定性；`Decision-Log` 已否決過 |
| 3 | `BallController.OnCollisionEnter2D` | **每碰一次就加分**，沒有「同一顆釘子 N 毫秒內只算一次」的冷卻（`Design.md` 附錄 B.4 坑 1） | 球貼著釘子滑動時分數會重複累加 → 球上分數失真 |

其他已知落差：

| 項目 | 說明 |
|---|---|
| `Design.md` 2.6.1 說球半徑 0.12，實作是 0.3 | 未處理 |
| `Assets/Phyics Materal/`（拼字錯誤，應為 `Physics`） | 目錄名打錯，進 git 了 |
| `Docs/Design.md` 附錄 B.7 的「開工第一天」清單 | **已經過期**（第一天早就過了），改寫成「建議的下一步」了 |

---

## 9. 環境備註（給 AI 用）

### 這個沙箱**不能 push 到 GitHub**

原因：沙箱不讓讀 Windows 憑證存放區，`git` 走 HTTPS 會失敗：

```
fatal: unable to access 'https://github.com/...': schannel: AcquireCredentialsHandle failed: SEC_E_NO_CREDENTIALS
```

**所以 commit 之後要請人類在自己的 cmd 執行 `git push`。**
`winget install` 也一樣會被沙箱擋下（要寫 `Program Files`）。

### 不開 Unity 也能驗證 C# 編譯 ⭐

Unity 的 managed 組件在 `W:\unity\version\2022.3.45f1\Editor\Data\Managed\UnityEngine\`。
可以建一個暫時的 `.csproj` 指向要檢查的 `.cs` 檔與那幾個 DLL，然後 `dotnet build`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\..\Assets\Script\Board\BoardLayout.cs" />
    <Compile Include="..\..\Assets\Script\Board\BoardBuilder.cs" />
    <!-- 其他要檢查的檔案 -->
  </ItemGroup>
  <ItemGroup>
    <Reference Include="UnityEngine">
      <HintPath>W:\unity\version\2022.3.45f1\Editor\Data\Managed\UnityEngine\UnityEngine.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>W:\unity\version\2022.3.45f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.Physics2DModule">
      <HintPath>W:\unity\version\2022.3.45f1\Editor\Data\Managed\UnityEngine\UnityEngine.Physics2DModule.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

放在 `obj/` 底下（`.gitignore` 已排除），驗證完刪掉。

> **這招真的有用。** `BoardBuilder.cs` 就是靠它抓到
> `CreateInstance<BoardLayout>()` 應該寫成 `ScriptableObject.CreateInstance<BoardLayout>()`。

### Commit 歷史

| Hash | 內容 |
|---|---|
| `b19b945` | 修浮點殘量的死亡判定；新增盤面生成器（v1 均勻網格） |
| `85433c2` | v1.3b：追認現況架構，放棄 asmdef 與純 C# 規則層 |
| `6254737` | v1.3：輸入改為瞄準＋力度、發射器移至頂部中央、廢除進場層 |
| `0485d9f` | Initial commit |

**未提交**：`Assets/Scenes/SampleScene.unity`、兩個 `.physicsMaterial2D`
（使用者自己的在建工作）。

> 本文件寫成時 `b19b945` 與 `Docs/Handoff.md` 都**還沒 push**，請確認 `git status` 再開始。
