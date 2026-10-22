# Demo 0.1.0 驗證與交付摘要

日期：2026-10-22。Unity 6000.3.7f1，Windows x64、Mono、URP 2D。工程位於 C:/Users/TINGOD/Desktop/CodeHome/HGame。

## 已完成

| 項目 | 結果 | 證據 |
|---|---|---|
| Unity EditMode | **43/43 通過**，0 失敗／跳過 | core-tests.xml |
| Windows 建置 | Succeeded，0 errors／warnings | build-report.json |
| 1280×720 完整 Player 流程 | passed | player-smoke.json、screenshots/ |
| 960×540 完整 Player 流程 | passed | small-window/player-smoke.json、screenshots/ |
| 關閉後另一個 Player 程序 Continue | passed，同一旅程、回退不增、畫廊保留 | restart/player-smoke.json、screenshots/ |
| 實際畫面核對 | 主選單、城鎮、迷宮、戰鬥、分歧、畫廊、Boss、兩章記憶、完成頁與零 HP；QA 確認小視窗提示不再遮擋 | qa-notes.md 及兩尺寸 PNG |

測試分組：Battle 10、Rewind／Route 17、Calendar／Reachability 8、Persistence／Gallery 8。資料來自實際 Resources/Demo。

Player 走查從新旅程用正常命令達成三次回退、指定記憶、H 養成與勝利、善惡門檻，再經分歧確認鎖線；沒有直接補齊資格旗標。涵蓋普通／H／魔化技能／舞者多敵與 Boss、每日限制、章節、讀回鎖線前、畫廊隔離、實際敵方擊倒後 HP 0 勝利及安全返城。完整路徑見 [操作紀錄](ui-walkthrough.md)。

## 修正紀錄

- 更新與 Unity 6.3 相容的官方 Input System 版本；修正 UI 模組預設動作與 TMP 匯入。
- 修正小視窗零 HP 提示遮擋、短按鈕標籤、捲動條 handle 尺寸。
- 真實冷啟動發現 JsonUtility 將清空交易的 null inline 類別還原成空物件。恢復交易現須非空交易 ID，並驗證旅程一致性，避免空交易覆寫自動存檔。已新增針對性回歸，且另以獨立 Player 程序確認。

## 交付

- 可玩目錄：Builds/HGameDemo（完整資料夾）。
- ZIP：output/HGameDemo-0.1.0-win64.zip，附操作說明、ThirdParty 原授權及驗證結果；不含 DoNotShip 偵錯目錄。
- 原始碼：Assets、Packages、ProjectSettings；根目錄 README 說明開啟方式。
- 未推送 GitHub 或公開發布。

## 限制

本版為系統原型，沒有正式劇情、成人 CG、完整五線結局、其餘職業／風格或期限魔化救援；第二章只提供記憶驗證段。資產及數值暫定。

Player 自動化使用 Input System 合成指標事件及正式 Presenter 命令，屬真正 Windows 程式內操作；沒有宣稱人工完整鍵鼠走查、長時間平衡或多硬體相容性測試。自動驗證資料位於獨立 Verification 存檔子目錄，不影響正常遊玩資料。
