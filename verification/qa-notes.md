# QA 驗證紀錄

日期：2026-10-22。QA 角色只維護測試與本紀錄，產品修正由主 agent／程式角色處理。

## 已有證據

- 最終已直接核對完整 Unity EditMode XML：43 項通過，0 失敗／跳過；UTC 2026-10-22 05:48:40。證據 `verification/core-tests.xml`。分組為 BattleFlowTests 10、RewindAndRouteTests 17、CalendarAndReachabilityTests 8、PersistenceAndGalleryTests 8。先前 42 項結果已由此包含空交易回歸的最終結果取代。
- 已核對 `verification/build-report.json`：Windows Player 建置 Succeeded，0 errors、0 warnings。
- 已核對最終兩份真實 Player 自動走查報告：1280×720（`verification/player-smoke.json`，UTC 05:49:08–05:49:13）與 960×540（`verification/small-window/player-smoke.json`，UTC 05:49:17–05:49:22）均 passed、Error 空白。
- 已核對另一獨立 Player 程序的 `verification/restart/player-smoke.json`（UTC 05:49:26–05:49:27）：passed、Error 空白，沿用第一個 1280×720 程序的 SaveRoot；Continue 恢復同一旅程、不增加回退，重啟後回想解鎖仍可播放。
- 只讀檢查 `BattleRules.Finish`：已清理節點透過 `newlyCleared` 避免再次取得普通或 H EXP；實際勝場仍可計數。新增正常重訪測試同時驗證普通勝利後改用 H 勝利也不能重新取得節點 EXP。

## Runtime 與日程：16 項已通過

`Assets/_Project/Tests/RuntimeEditMode` 共 16 項測試，使用 HGame.Unity 與 HGame.Core 真正 API，未替換產品規則：

1. 寫入唯一暫存目錄、重建 repository、讀回自動槽；正常 Continue 不加回退。
2. 指定記憶點失敗後冷啟動手動讀檔：接收持久化記憶，當局敗北回退，連續載入不重複計數。
3. 載入交易在自動槽寫入前注入一次 I/O 中斷；重建 repository 恢復同一準備結果，計數／記憶／憑證不重複。
4. 真實磁碟初次寫入、替換及備份；主檔損壞可讀備份且保留原損壞檔。
5. 沒有備份的未知版本報錯並保留原資料。
6. 畫廊播放／跳過／退出不改完整正式快照或持久化 Meta。
7. 新旅程繼承 Profile 回想解鎖，隔離另一旅程記憶並保留舊旅程磁碟資料。
8. 重訪清理戰鬥不重領普通／H EXP 或新增探索進度。
9. 深夜封鎖工作／成人工作／購物／遠征，休息後正常恢復換日。
10. 四個階段日期案例：第 2／3 天的第二階段、第 5／6 天的第三階段。
11. 第 20 天未打 Boss 的期限失敗，不走一般保底或略過條件的魔化入口。
12. 一條從 NewGame 開始的正常命令序列：敗北兩次、三次有效回退、第一章記憶、H 勝利／養成、兩次補給、明示分歧選擇、鎖線回退、第六天舞者 Boss、章末休整無額外收益、第二章記憶與完成頁。
13. 完成真正讀檔後留下的空交易標記不被恢復成空交易；兩次重建 repository／Continue 維持主自動槽、旅程、完整 Run、遊玩 Meta 與記憶／回退計數。

資料與資源來自實際 Resources/Demo；正常可達性序列沒有直接補齊路線旗標。邊界日期／期限測試以建構子設定起始狀態。磁碟測試只建立並清理 `Path.GetTempPath()/HGame-QA-<唯一識別>`，不讀寫玩家正式資料。

上述 16 項已包含於最終 43 項 Unity Test Runner 通過結果，未跳過。僅因獨立 Player 啟動發現的具體空交易缺陷新增 1 項回歸，沒有為增加覆蓋率繼續擴寫測試。

## Player 自動走查與實際畫面

- 只讀核對 `PlayerSmokeRunner.cs`：由主選單建立新旅程，透過正式命令完成兩次敗北、指定失敗後讀檔、三次回退、H 養成與勝利、補給取捨、正式分歧、魔化群體技能、讀回鎖線前、舞者多敵／Boss、第二章記憶、零 HP H 勝與磁碟 Continue；沒有直接填入魔化資格旗標。
- NewGame、ModalAccept、Gallery、PlayGallery 使用 Input System 合成指標事件；其餘主要流程使用正式 Presenter 命令。此為真正 Windows Player 內的自動操作，不宣稱人工滑鼠／鍵盤完整逐項走查。
- 兩種尺寸皆產生實際 Player PNG。QA 已目視核對 960×540 的分歧警告與零 HP 畫面：分歧文字可讀、未明示玩家身分。
- 原先發現的零 HP 警示遮擋已由主 agent 修正，QA 重新查看刷新後的 `verification/small-window/screenshots/13-zero-hp-recovery.png`：粉紅說明完整置於按鈕上方，與「安全返城／使用恢復藥」不再重疊。此項已關閉。
- 已目視核對刷新後的 `verification/screenshots/02-town.png`：1280×720 城鎮的捲動條維持窄軌道，不蓋住內容；底部較長選項由 viewport 裁切並保留捲動區域。此為靜態畫面核對，未另行宣稱人工拖曳走查。

## 冷啟動空交易問題：已關閉

- 主 agent 在獨立程序 Continue 驗證發現：Unity Player 的 JsonUtility 會將已清空交易標記的 inline Target／ResultMeta 反序列化成空物件；只判 null 可能把空交易當成待恢復交易而覆寫自動槽。主 agent 已修復 transaction ID 與旅程一致性檢查。
- 對應回歸 `CompletedLoadMarkerCannotOverwriteAutoSlotOnRepeatedRepositoryRestart` 已在最終 43 項中通過：執行真正指定失敗與手動讀檔完成、取得空交易標記，再連續重建磁碟 repository 兩次並 Continue；直接檢查 primary auto 槽避免備份回退掩蓋空覆寫，完整比對 Run 與所有遊玩 Meta（僅排除 Continue 自身交易 ID 的合法增加）。
- 獨立 Windows Player 的 Continue／回想重播也已通過；證據為上列 restart 報告。本問題已關閉。

## 未完成或未由本 QA 獨立確認

- 沒有人工逐項操作完整鍵鼠流程；以合成 Input System 點擊與正式命令走查作已完成證據。
- 素材授權與最終打包由主 agent 核對，未由本 QA 另行驗證；不從程式測試結果推導其通過。
