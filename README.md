# Pinball Game

一款 2D 彈珠機（Pachinko 式）遊戲，目標日後擴充為 **肉鴉（Roguelike）大數字** 遊戲。

- Unity **2022.3.45f1**、2D 物理（`Rigidbody2D` / `CircleCollider2D`）
- Input System 1.7.0、Cinemachine 2.10.7、TextMeshPro 3.0.6、UGUI

---

## 規格文件在哪裡

> ⚠️ **`Docs/` 是這個專案的權威規格，不要當成一般筆記刪掉。**
> `.gitignore` 若含 `[Dd]ocs` 會把整個資料夾排除，請勿加回那一行。

| 文件 | 內容 |
|---|---|
| [`Docs/Design.md`](Docs/Design.md) | **唯一權威規格。** 核心循環、基礎規則（附錄 A 為可直接實作的無歧義規格）、肉鴉層、工程分層、里程碑、未定項目 |
| [`Docs/Decision-Log.md`](Docs/Decision-Log.md) | **決策的理由與已推翻的方案。** 動手前先讀「第三部分：已推翻的決定」，避免重新提議已被否決的做法 |
| `Docs/_archive/` | 舊版規劃文件。**不是規格**，只保留歷史脈絡 |

閱讀順序建議：`Design.md` 第 0 章 → 第 1 章 → 附錄 A → 附錄 B。

---

## 目前狀態

遊戲設計已定案到「基礎玩法可實作」的程度；程式正在依 `Design.md` 附錄 B 的垂直切片順序建置中。

| 項目 | 狀態 |
|---|---|
| 核心循環與基礎規則 | ✅ 已定案（`Design.md` 附錄 A） |
| 盤面幾何（4 進場層、袋口位置） | 🔶 未定（`Design.md` 6.1） |
| 袋口具體倍率 | 🔶 未定（`Design.md` 6.2） |
| 充能上限 `C` 的成長曲線 | ⏸ 延後到平衡階段（`Design.md` 6.4） |
| 肉鴉層（遺物、當選、技能） | 尚未開始 |

---

## 目錄結構

```
Assets/
  Script/
    Core/      BigNumber、DeterministicRng、Singleton、初始化介面
    Data/      GameConfigSO、LevelConfigSO、DataManager
    Health/    Health、HealthManager
    Scoring/   ScoringManager、ChargeMeter
    Combat/    Monster 家族、CombatManager
    Board/     BoardManager、BallController、Peg、Pocket、Launcher
    Input/     InputManager、PowerGauge
    UI/        BattleUIManager
    Flow/      BattleManager、SetupManager
  ScriptableObject/   怪物與關卡設定資產
Docs/       ← 權威規格（見上）
```

`Assets/Script/ARCHITECTURE.md` 說明程式碼的模組切分與存取規則。

---

## 開啟專案

1. 用 Unity Hub 以 **Unity 2022.3.45f1** 開啟此資料夾
2. 開啟 `Assets/Scenes/SampleScene.unity`
3. 首次開啟會自動還原 `Packages/manifest.json` 中的套件

`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`*.csproj`、`*.sln` 都是 Unity 產生的，已在 `.gitignore` 中排除；**clone 後不需要、也不應該上傳這些**。
