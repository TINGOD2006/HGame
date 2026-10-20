# MVP 技術方案（Unity）

> 歷史技術與排程草案（2026-10-20）。舊二時段、信賴／慾望、隊友支援、合體技與人物數量不可直接照做。新版完整玩法及 MVP 待重訂狀態見 [專案總覽](00-專案總覽-AI交接.md) 與 [玩法紀錄](06-玩法決策紀錄-2026-10-21.md)。

> 決策日 2026-10-20。這份文件把「企劃草案」的 MVP 落成 Unity 實作方案，並記錄已拍板的四項決策。

## 0. 已拍板決策

| 項目 | 決定 | 備註 |
|---|---|---|
| 引擎 | **Unity** | 本機已安裝 6000.3.10f1 與 2021.3.38f1，建議用 6000.3.10f1 |
| 美術 | **AI 輔助先出全套草稿** | 需建立角色一致性流程與 AI 使用登記表 |
| 團隊 | **一人全包（企劃＋程式）** | 排程以 10 週估，美術全走 AI |
| 尺度 | **雙軸並存，MVP 含 1 個成人場景** | 驗證雙軸系統與獎勵層，資料結構須可切換全年齡/成人 |

**排程調整**：Unity 比 RPG Maker 多出約 2 週的基礎建設（場景流程、存檔、事件系統、UI），MVP 從 8 週改為 **10 週**。這 2 週花在架構上，是為了後面 3 位女主角不用重寫。

---

## 1. 引擎與專案設定

| 項目 | 設定 |
|---|---|
| Unity 版本 | 6000.3.10f1（Unity 6 LTS 線） |
| 範本 | 2D (URP) |
| 渲染 | 2D Renderer；MVP 不用 2D 光源，靠 Sprite 排序與 UI 達成氛圍 |
| 輸入 | Input System（新版） |
| UI | uGUI（不用 UI Toolkit，個人開發迭代較快） |
| 建置目標 | Windows Standalone x64 |
| 版本控制 | Git + `.gitignore`（Unity 標準）+ Git LFS（大圖） |
| 授權 | Unity Personal（年營收 < 20 萬美元；若未來團隊化再評估 Pro） |

**不做**：Addressables、ECS/DOTS、自訂 Shader、多人連線、Shader Graph 特效。

---

## 2. 專案結構

```
Assets/_Project/
├── Art/
│   ├── Characters/        # 立繪（表情差分）
│   ├── CG/                # 事件 CG（含差分）
│   ├── Backgrounds/
│   └── UI/
├── Audio/{BGM,SE}
├── Data/
│   ├── Json/              # events / heroines / enemies / dungeon / items
│   └── ScriptableObjects/ # 靜態設定（敵人、道具、技能）
├── Scripts/
│   ├── Core/              # GameManager, SceneLoader, SaveSystem, Flags
│   ├── Data/              # 資料模型、JSON 載入
│   ├── Battle/            # 回合制戰鬥
│   ├── Dungeon/           # 節點地圖
│   ├── Town/              # 據點日循環
│   ├── Relationship/      # 雙軸與事件觸發
│   ├── Dialogue/          # 對話執行器（Ink 或自製）
│   └── UI/
├── Prefabs/
├── Scenes/                # Boot, MainMenu, Town, Dungeon, Battle
└── Settings/
```

**Scene 規劃**：

| Scene | 職責 | 備註 |
|---|---|---|
| Boot | 初始化、載入設定、存檔檢查 | 只有一個空物件 + 初始化流程 |
| MainMenu | 新遊戲／繼續／回想／設定 | MVP 回想先做簡單列表 |
| Town | 據點：日夜行動、設施、關係事件 | 主要 Hub |
| Dungeon | 節點地圖、探索、撤退 | 進入戰鬥時切 Battle |
| Battle | 回合制戰鬥 | 結束返回 Dungeon |

**跨場景常駐**：`GameManager`（DontDestroyOnLoad）持有存檔資料、旗標、雙軸數值。

---

## 3. 核心系統設計

### 3.1 遊戲流程（State）

```
Boot → MainMenu → Town ⇄ Dungeon ⇄ Battle
                     ↓
                 Event（在 Town 內以 UI Overlay 播放）
```

- 一天 = 白天 1 AP ＋ 夜晚 1 AP；Town 的 `DayCycleController` 管理。
- 戰鬥結束回 Dungeon，Dungeon 撤退／完成回 Town。
- 事件在 Town 觸發時以 Overlay 播放，不切場景（避免相機與 UI 重載）。

### 3.2 戰鬥（MVP）

- **編制**：主角 1 人 ＋ 女主角支援技（不佔行動，每場 1 次）。
- **流程**：`BattleStart → 選擇行動 → 結算 → 敵方行動 → 檢查勝負 → 下一回合`。
- **屬性**：斬／火／冰（MVP 3 屬性，完整版擴到 5）。
- **共鳴值**：命中弱點 +15，受擊 +5；滿 100 可放「合體技」（女主角支援，傷害 ×2.5）。
- **數值表**（起始建議，之後調）：

| 對象 | HP | ATK | DEF | AGI |
|---|---|---|---|---|
| 主角 | 120 | 18 | 8 | 12 |
| 雜兵 A（史萊姆） | 40 | 8 | 4 | 6 |
| 雜兵 B（蝙蝠） | 55 | 10 | 5 | 14 |
| 精英（石像） | 160 | 14 | 15 | 5 |
| Boss（迷宮守衛） | 480 | 22 | 12 | 10 |

- **公式**：`傷害 = max(1, ATK × 技能倍率 − DEF × 0.5) × 弱點倍率(1.5) × 隨機(0.9–1.1)`。
- **節奏目標**：一般戰鬥 ≤ 30 秒、Boss 2–3 分鐘。

### 3.3 迷宮（節點式）

- **規則**：3 層，每層 8 個節點，分支後匯合；節點類型＝戰鬥／精英／事件／寶箱／休息／Boss。
- **探索度**：每層初始 0%，走訪節點 +12%；撤退時以探索度決定額外掉落。
- **撤退**：保留 70% 金幣與素材；戰敗損失 30% 並回 Town。
- **實作**：`DungeonNodeMap` 以資料驅動（節點權重表），UI 用按鈕／圖示顯示，不做自由行走。

### 3.4 關係雙軸

```csharp
// 每位女主角獨立數值，資料結構預留 4 位
public class HeroineState {
    public string id;        // "serena"
    public int trust;        // 0-100
    public int desire;       // 0-100
    public List<string> seenEvents;
}
```

- 事件條件：`flags`、`minTrust`、`minDesire`、`dayMin`、`chapterMin`。
- 事件效果：`trust +N`、`desire +N`、`flags`、`reward`。
- **四象限判定**放在章末（MVP 只在 EndingStub 顯示判定結果即可）。
- 事件播放完寫入 `seenEvents` → 回想模式讀取。

### 3.5 事件與對話

**建議：系統資料用 JSON / ScriptableObject，對話用 Ink。**

理由：MVP 有 20+ 個分支事件，手寫 JSON 條件判斷容易出錯；Ink 原生支援分支、變數、標籤與存檔狀態，且可把 `trust/desire/flag` 透過 external function 接到 C#。若不想多學一套語法，改用自製 JSON Runner（多花 3–4 天）。

資料格式（系統事件）：

```json
{
  "id": "s1_night",
  "type": "relationship",
  "conditions": { "flags": ["dungeon2_clear"], "minTrust": 30, "minDesire": 20, "dayMin": 3 },
  "skippable": true,
  "skipReward": { "gold": 150, "item": "herb_pack" },
  "presentation": { "bg": "inn_night", "cg": "serena_cg_01", "heroine": "serena", "expr": "shy" },
  "inkPath": "Assets/_Project/Data/Ink/s1_night.ink"
}
```

**成人場景 = 一種事件類型**：`type: "adult"`，同樣走 `skippable` + `skipReward`，播放時只替換 CG 與台詞，不影響主線。這樣「全年齡版」只要在資料層把 `adult` 事件的 `inkPath` 換成 `s1_night_safe.ink`，或直接停用該事件。

---

## 4. 存檔設計

```json
{
  "version": 1,
  "day": 3, "chapter": 1,
  "flags": 0,
  "heroines": [ { "id": "serena", "trust": 35, "desire": 22, "seenEvents": ["s1_drink"] } ],
  "player": { "hp": 120, "level": 3, "skills": ["slash","fire"] },
  "inventory": { "herb_pack": 2, "gold": 480 },
  "unlockedCg": ["serena_cg_01"]
}
```

- 路徑：`Application.persistentDataPath/save_01.json`。
- 版本升級：`SaveMigrator` 依 `version` 補欄位，避免加女主角時舊檔壞掉。
- **MVP 只做手動 1 槽 + 自動 1 槽**。

---

## 5. 10 週排程（一人開發）

| 週 | 目標 | 交付物 | 驗收 |
|---|---|---|---|
| W1 | 專案骨架 | Unity 專案、資料夾、Git、Boot→MainMenu→Town 空場景流轉、存檔讀寫 | 能建置出 exe 並切換場景 |
| W2 | 戰鬥原型 | 回合制流程＋3 屬性弱點＋共鳴值＋1 場可玩戰鬥 | 30 秒打完，數值從 JSON 載入 |
| W3 | 迷宮地圖 | 節點生成、節點事件、探索度、撤退 | 10 分鐘一輪，撤退有決策 |
| W4 | 戰鬥整合 | 從迷宮進戰鬥、掉落、回城結算 | 完整一圈：Town→Dungeon→Battle→Town |
| W5 | 據點循環 | 日夜 AP、鍛造（武器強化）、送禮、約會 | 一天 2 AP 的選擇有意義 |
| W6 | 雙軸與事件 | 事件觸發條件、效果、四象限判定、3 個事件 | 數值能推動事件解鎖 |
| W7 | 對話與回想 | Ink 整合、回想列表、跳過與替代獎勵 | 跳過不影響主線 |
| W8 | 成人場景 | 1 個成人事件（CG＋台詞＋跳過＋回想） | 播放完整、可跳過、可重播 |
| W9 | 內容填充 | 3 層完整迷宮、Boss、BGM、UI 打磨 | 45–60 分鐘可通關 |
| W10 | 測試與平衡 | 10 人試玩、Bug 修正、建置 | 完成率 >70%、無崩潰 |

**每週節奏**：週一拆任務、週三中期檢查、週五可玩版本（build）。沒有可玩版本就不算完成。

---

## 6. AI 美術流程（角色一致性是關鍵）

### 6.1 五步流程

1. **定稿**：用 AI 生成 10–20 版角色設計（正面／側面／背面、2 套服裝），挑 3 版 → 手動修成「角色設定三視圖」。
2. **鎖定**：以定稿圖做 reference（img2img / ControlNet / 角色 LoRA），固定 seed 與 prompt 模板。
3. **量產**：立繪（表情差分）→ CG → 背景；每張輸出都記錄 prompt、seed、修改紀錄。
4. **後處理**：去背、統一邊線與色調、裁切到 Unity 規格（Krita/GIMP/Photoshop）。
5. **匯入**：Sprite 匯入設定（PPU 100、Filter Mode 依風格）、Sprite Atlas 打包。

### 6.2 規格

| 資產 | 解析度 | 格式 | 備註 |
|---|---|---|---|
| 立繪 | 1024×2048 | PNG（透明） | 同一角色所有表情共用身體，只換臉層 |
| 事件 CG | 1920×1080 | PNG | 差分另存圖層，匯入時可切換 |
| 背景 | 1920×1080 | PNG/JPG | 迷宮背景可重複利用色調變化 |
| 敵人 | 512×512 | PNG | 3 雜兵 + 1 Boss |
| UI | 向量感元件 | PNG | 9-slice |

### 6.3 平台申報準備（先做，別事後補）

- 建立 `AI_使用登記表.csv`：檔名、工具、prompt、seed、人工修改說明、AI 比例。
- 決定申報定位：MVP 立繪與 CG 若為 AI 草稿＋人工修正，對 DLsite 申報「AI 使用 ON」，對 FANZA 申報「AI 一部利用」或「AI 補助」（依實際比例）。
- 這是**發行前**就要決定的，因為會影響商店頁文案與曝光。

---

## 7. MVP 資產清單

| 類別 | 數量 | 備註 |
|---|---|---|
| 女主角立繪 | 1 角色 × 3 表情 × 2 服裝（共用身體） | 6 張臉層 + 2 張身體 |
| 事件 CG | 2 張（1 成人向）＋差分 4 張 | |
| 背景 | 4 張（城鎮、迷宮、室內、旅館夜） | |
| 敵人 | 4 種 | |
| UI | 1 套 | 按鈕、面板、血條、對話框 |
| BGM | 5 首 | 城鎮、迷宮、戰鬥、Boss、事件 |
| SE | 20 個 | 打擊、選單、開門等 |
| 字型 | 1–2 套 | 中文需確認授權（思源黑體/源流明體） |

---

## 8. 風險與對策

| 風險 | 對策 |
|---|---|
| Unity 基礎建設拖長 | W1 只做「場景流轉＋存檔」，不做任何華麗效果 |
| AI 角色不一致 | 先花 3 天做「角色定稿＋reference 鎖定」，沒鎖定前不量產 |
| 一人開發生病／中斷 | 每週五留可建置版本，任何時候都能停 |
| 範圍膨脹 | 本文件第 3 節以外的系統一律不做，新想法寫進 backlog 等完整版 |
| 成人場景拖慢主線 | 場景先用「文字＋1 張 CG」驗證流程，差分與運鏡留到 W9 |

---

## 9. 待確認事項

1. Unity 版本：6000.3.10f1（建議）或 2021.3.38f1？
2. 對話系統：Ink（建議）或自製 JSON Runner？
3. 視覺風格：高解析動漫風（建議，配合 AI 產圖）或像素風？
4. 是否現在就由我建立專案骨架＋戰鬥原型（W1–2）？
