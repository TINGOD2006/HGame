# HGame｜迷宮與她的夜晚（暫名）

成年女性主角的日式回合制 RPG。每章 20 天安排迷宮探索與養成，以普通 RPG 或色色系統攻略迷宮，依善惡、探索與失敗紀錄走向五種結局。

**Unity 系統 Demo 0.1.0 已實作**，提供 Windows x64 試玩與工程原始碼。所有角色與外觀明確成年；成人題材以抽象狀態和資源結算呈現，本版沒有露骨腳本或 CG。

## 開始試玩

完整解壓縮 Windows 包，執行 `HGameDemo.exe`。需保留同層 Data、DLL、MonoBleedingEdge 等檔案。原始建置位於 `Builds/HGameDemo`，交付 ZIP 位於 `output/HGameDemo-0.1.0-win64.zip`；兩者是本機產物，不提交 Git。

使用滑鼠點選、滾輪捲動行動區。主選單可建立新旅程或繼續；詳見 [操作說明](docs/Demo操作說明.md)。

## Demo 內容與開發

- 第一章 20 天、每日四時段，城鎮準備、三階段迷宮、六個存檔點與 Boss。
- 劍士／舞者、普通與 H 戰鬥、零 HP 續戰、工作、購物與養成。
- 真實讀檔、特殊記憶、明示分歧、一個魔化技能、隔離主線的畫廊。
- 第二章只提供記憶驗證段與試玩完成頁；其餘職業、風格、正式劇情、CG、五線結局及期限魔化救援尚未納入。

使用 **Unity 6000.3.7f1** 開啟儲存庫根目錄。選單 `HGame/Prepare Demo` 建立／更新場景，開啟 `Assets/_Project/Scenes/Demo.unity` 後按 Play；`HGame/Build Windows Demo` 產生 Windows x64 建置。

規則位於 `Assets/_Project/Core`，不依賴 UnityEngine；Runtime／UI 負責儲存、展示及輸入。五份 JSON 位於 `Assets/_Project/Resources/Demo`。套件版本見 Packages 內 manifest 與 lock 檔。正式 Ink 劇本尚未整合。

驗證結果見 [摘要](verification/summary.md)、[操作路徑](verification/ui-walkthrough.md) 與 [QA 紀錄](verification/qa-notes.md)。素材來源與授權見 [紀錄](docs/第三方素材授權紀錄.md)。

## 從這裡開始

- [專案總覽與 AI 交接](docs/00-專案總覽-AI交接.md)：目前決策與待確認事項。
- [本輪玩法決策](docs/06-玩法決策紀錄-2026-10-21.md)：完整討論摘要與已淘汰方案。
- [詳細玩法流程圖](docs/07-玩法流程圖.md)：章節、日程、戰鬥、結局與失敗處理。
- [女主人設與暫定造型](docs/08-女主人設與美術參考.md)。
- [AI 素材使用紀錄](docs/09-AI素材使用紀錄.md)。

## 目前方向

- 每章 20 天，每日四時段；早上決定遠征，晚上返城，深夜不得外出。
- 單女主，四種普通職業、兩種色色風格；沒有固定隊友。
- 普通與 H 戰鬥可銜接，主動進入深度模式與被擊倒轉入分開處理。
- 一般、純潔、黑化、色魔、魔化五種結局；部分細部條件尚未定案。
- Unity 6000.3.10f1／URP 2D／Ink 是完整版原技術方向；本次 Demo 依批准規格使用本機 6000.3.7f1，完整 MVP 仍待重估。

## 暫時保留的造型

![成年女主暫定造型](docs/assets/heroine-reference-v01.png)

此圖為 AI 生成概念參考，使用者暫時接受，尚未最終定稿。

## 文件版本

2026-10-21 與 2026-10-22 新決策優先。Demo 規格與計畫均已批准並實作，不將原型設定改為完整版永久決策。原始入口另存於 [歷史總覽](docs/archive/2026-10-20-專案總覽-舊版.md)。未推送或公開發布。
