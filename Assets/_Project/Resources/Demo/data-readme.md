# Demo 資料說明

本目錄依已確認的 `docs/superpowers/specs/2026-10-22-system-demo-design.md` 與 `Core/GameDefinitions.cs` 建立；所有數值僅用於此次 Demo，不是完整版永久企劃。資料採 UTF-8、PascalCase 欄位。Unity 以 `Resources/Demo/檔名` 載入，不包含副檔名。

| 檔案 | 根結構 | 用途 |
|---|---|---|
| balance.json | Balance、Items | 全域初值、門檻、經濟、藥品 |
| enemies.json | Items | 兩種一般敵人與首領 |
| skills.json | Items | 八個已授權技能／戰鬥行動 |
| dungeon.json | Items | 第一章十二節點、第二章四節點短段落 |
| events.json | Items | 抽象狀態文案與選項效果描述 |

## 共用 ID 與語意

- 藥品：`hp_potion`、`mp_potion`。品項價格／恢復量與 Balance 對應欄位保持相同。
- 敵人：`slime`、`guardian`、`boss`。這些是戰鬥占位 ID；不據此推定成人事件角色或允許範圍。
- 技能：`attack`、`slash`、`dance`、`enter_deep`、`h_touch`、`h_corrupt`、`defend`、`wait`。`defend` 只用於 Normal；Deep 等待使用 `wait`，不減少敵方 H 增量。
- 節點：`c1_n01`–`c1_n12`、`c2_n01`–`c2_n04`。`Kind` 為 Battle／Save／Supply；多敵以 EnemyIds、記憶位置以 IsMemorySite 與 MemoryId 表示。
- 每章特殊記憶分別為 `memory_c1`、`memory_c2`；同章重複失敗不增加章數。
- 補給選項為 `support`／`take`，重大分歧為 `keep`／`accept`。效果結算由 Core 命令入口單一負責，顯示 Choices 不表示 UI 可另加一次報酬。
- 成人工作只作明確成年角色的抽象報酬／成長結算；不改善惡與純潔資格，不含正式劇本或露骨演出。所有畫廊文字只讀展示。

## 一致性與占位參數

第一章每階段四節點、兩存檔點；開放日 1／3／6、單趟新增四節點。兩個不同補給節點各選一次 `take`，善惡可從 50 降至 30。已領節點不能重複發獎。

魔化後敵人加成暫設 HP 傷害 +2、H 增量 +5，僅驗證既有難度配置切換。魔化技能單目標 H 增量 140 且作用全體，相較基本單體 80 有明確提升；未增加新機制或新陣營。這兩項加成是資料端的 Demo 占位參數，可依實際驗證調整。

資料不含存檔狀態、失敗憑證、有效回退次數或感知旗標；這些由 RunState／MetaState 規則處理。事件數值不得在 Core 全域活動報酬之外重複套用。畫廊不得執行事件 Choices 效果。

資料自製，不包含外部圖片、音效、字型或第三方授權內容；所有美術沿用主專案的已接受成年參考與正式授權清單。使用者要求忽略的附件未採用。
