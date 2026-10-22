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

## 0.2.0 俯視示範地圖

`Maps` 下四份 JSON 各自直接使用 `WorldMap` 根結構，欄位依 `Runtime/MapModels.cs`，不是 `Items` 容器。它們只供此次 Demo 展示，不是正式地圖定案。座標由左上角開始，X 向右、Y 向下；Ground 每列對應一個 Y。`#` 牆與 `~` 水不可通行；`.` 草、`=` 路、`f` 石板、`c` 地毯為地板。互動物件本身不可站立，從四鄰格面向物件按 E／Enter。

| ID／檔名 | 尺寸 | 章節 | 出生座標 | 佈局 |
|---|---|---|---|---|
| town | 36×24 | 0（通用） | (18,13) | 十字主街、北側旅店與商店、南側工作與職業窗口、東南水池花園 |
| inn | 14×10 | 0（通用） | (6,6) | 中央地毯、床位、出口及室內記憶入口 |
| dungeon_c1 | 48×40 | 1 | (5,6) | 三列、每列四房的連續蛇形回廊；沿用十二個節點 |
| dungeon_c2 | 24×18 | 2 | (4,5) | 兩列、每列兩房的短蛇形；沿用四個驗證節點 |

城鎮功能 ID 為 `town_inn`、`town_shop`、`town_work`、`town_jobs`、`town_depart`、`town_memory`，Kind 依序為 Inn／Shop／Work／Jobs／Depart／Memory。工作窗口沿用兩類工作選項，不新增事件。旅店含 `inn_rest`／Rest、`inn_exit`／ExitInn，以及 `inn_memory`／Memory，確保深夜室內仍有記憶分歧入口；出口限制由正式日程處理。

迷宮的 Return 物件位於入口房，節點互動 ID 為 `map_` 加原 NodeId；全部使用 Kind=Node，存檔／補給／戰鬥均須明確互動。房間中心上方一格放節點物件，中心通道可走。每個房間往下一房的唯一一格通道有對應 Gate，必須該房 NodeId 已完成才能通過；日期、每趟推進量、零 HP 與節點條件仍由正式 Core 檢查。尾房 Gate 只通向末端小石板區，沒有新節點或獎勵。

地形採「房間－單格走廊－房間」連成一條鏈，未設平行通路，所以不能繞過尚未完成的節點。節點物件在 Gate 的入口側，關門時仍能走回入口 Return；已通過的 Gate 需保持開啟，返程不另鎖門。安全返城／恢復限制交給展示與核心入口處理，不把房間走動當成新節點或存檔資格。

配置上的可達性：城鎮六個窗口均面向連續道路，旅店功能面向室內開放地板；各迷宮房間的節點物件面向中心通道，Gate 不占互動鄰格。裝飾不占出生點、單格連接走廊或 Gate；關閉最前方 Gate 時，仍能由出生點接近第一節點及入口 Return。這是佈局說明，實際碰撞、Gate 及全流程驗證由主 agent 統一執行，本次資料工作未啟動 Unity 或執行測試。

實際引用 SpriteKey：`tree`、`bush`、`flower`、`rock`、`house`、`door`、`sign`、`npc`、`save`、`chest`、`enemy`、`boss`、`bed`、`table`、`torch`。四座房屋各使用一個 3×3 合成 house 圖，而非緊鄰重複屋圖；中心為 (7,6)、(28,6)、(6,17)、(13,17)，周圍 3×3 Ground 為阻擋 footprint，南側互動門在 footprint 外。SpriteKey 為資產 adapter 的語意索引，資料不綁下載封包的圖塊順序。圖塊來源、下載、匯入與授權由主 agent 處理。
