# 0.2.0 QA 驗證紀錄

日期：2026-10-22。此紀錄只針對俯視探索與橫向戰鬥 0.2.0；不覆寫或借用 0.1.0 驗證結果作為新版通過證據。

## EditMode：新版 50 項已通過

QA 已只讀核對 `verification/0.2.0/core-tests.xml`：UTC 2026-10-22 10:53:29–10:53:30，新版共 50 項通過、0 失敗、0 跳過。分組為 BattleFlowTests 10、RewindAndRouteTests 17、CalendarAndReachabilityTests 8、PersistenceAndGalleryTests 8、JrpgWorldTests 7。此為 0.2.0 實際重跑結果，沒有借用 0.1.0 報告。

QA 只新增 `Assets/_Project/Tests/RuntimeEditMode/JrpgWorldTests.cs`，共 7 個針對性 EditMode 測試：

1. 四張真實地圖的尺寸、互動／Gate 引用及循序節點可達性；沒有前置進度時無法走到最後節點。
2. 單軸選擇、方向往返、邊界／牆／水／阻擋裝飾／互動物件碰撞；互動必須正確面向。
3. `UpdatePresentation` 只改 World，完整 Run 其他部分、Meta、Profile 不變；傳入位置採深複製。
4. 相同時間線 head 但不同位置，透過真正 SaveRepository 手動讀檔還原存檔位置，不增加回退。
5. 有效位置保留；缺欄位、null、空 ID、舊地圖、牆內、失效錨點安全 fallback；深夜與第二章依當局狀態換正確地圖。
6. WorldSaveRules fallback 必須遵守 Core 日期與單趟新增上限，不保留位於尚不可達區域的座標。
7. 使用真實 `WorldSaveRules.CanSave` 作 SaveRepository gate；離開存檔錨點後，手動存檔拒絕且 Persist 不覆寫 auto／manual 槽。Meta／Profile 仍可持久化。

測試使用實際 Resources/Demo 與 Maps，儲存閘門測試用隔離記憶體 ISaveStore，不接觸玩家資料。Unity 由主 agent 統一執行；QA 本輪只讀核對其結果，未另開 Unity 或重跑測試。

## 已回報的具體風險：已修復並通過回歸

- 初版 `WorldSaveRules` 的 Resolve／CanSave 未傳 GameData 給 GridRules，可達性可能漏掉日期／單趟上限。主 agent 已修正；QA 只讀確認兩個入口均傳入 DefinitionLoader 載入的 Data，第 6 項日期／單趟限制回歸已在新版 50 項中通過。本項已關閉。
- 主 agent 回報實際 Presenter 儲存閘門另結合 WorldController.CanSave 的確切戰後返回格；其最終 Player 走查尚待下節證據，不能僅從 EditMode 結果推定全部輸入／動畫流程通過。

## 最終 Windows Player 與畫面證據：已核對

- 已只讀核對 `verification/0.2.0/build-report.json`：Succeeded，0 errors、0 warnings，輸出新版 Windows Player。
- 已核對 `player-smoke.json`：0.2.0、1280×720，UTC 11:03:20–11:03:33，passed、Error 空白。
- 已核對 `small-window/player-smoke.json`：0.2.0、960×540，UTC 11:03:38–11:03:50，passed、Error 空白。
- 已核對 `restart/player-smoke.json`：UTC 11:03:54–11:03:55，passed、Error 空白；與第一個 1280 程序具有相同 SaveRoot 與 CampaignId，獨立程序 Continue 恢復同旅程與安全位置、回退計數不增加，畫廊仍可播放。
- 報告包含 Input System 鍵盤逐格行走、面向商店／入口／接敵點、E 互動、Esc 選單及視窗開啟時移動鎖定；戰鬥攻擊、ModalAccept、SafeReturn 及畫廊入口使用合成指標事件。QA 只讀核對 Runner 的 Click／KeyPress／Step 確實透過 InputSystem.QueueStateEvent；後段規則走查使用正式命令，沒有冒稱全部操作皆由按鍵完成。
- 報告確認戰鬥動畫後解除控制鎖、普通／H／舞者勝利返回地圖、指定記憶與分歧確認、魔化群體技能、讀回分歧前不重鎖、第二章記憶，以及舊動畫不能改寫新旅程。
- QA 已查看最終 960×540 實際 PNG：`02-town` 的地圖人物與精簡 HUD 可辨；`07-battle`／`10-multiple` 的敵人居左、女主居右，目標、HP／MP、暈眩及可見指令可讀，指令區保留捲動條；`13b-branch-warning` 的說明、確認／返回選項完整，未揭露玩家身分；`15-zero-hp` 的粉紅提示與右上安全返城按鈕清楚，不互相遮擋。

上述為新版最終報告，取代較早的第一輪 1280 暫時結果；原 0.1.0 證據保持原狀。

## 驗證界線與未由 QA 獨立確認項目

- 沒有人工完整逐項操作鍵鼠介面；證據是 Windows Player 內的合成輸入事件、正式命令及實際截圖，不能稱人工全面驗收。
- 持續長按互動鍵跨多個視窗的每個組合未另作人工走查；沒有為增加數量擴寫測試。
- 素材授權與最終 ZIP 完整性／雜湊由主 agent 核對，未由本 QA 另行驗證，不從測試通過推導其通過。
