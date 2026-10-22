# Unity 系統 Demo 實作計畫

日期：2026-10-22。狀態：設計規格與本計畫均已批准；T1–T10 已完成，Demo 0.1.0 實際結果見 [驗證摘要](../../../verification/summary.md)。

依據：[已確認設計規格](../specs/2026-10-22-system-demo-design.md)。執行方式已由使用者選定：玩法＋程式＋QA 三個本次聊天協作角色，主 agent 整合；不再要求重新選角色或改用外部模型。

本機未找到 writing-plans 技能，已搜尋使用者 skills、agents skills、外掛快取與記憶技能目錄。本文件直接依已確認規格編寫，沒有聲稱已套用缺少的技能。任務不增加正式劇情、CG、其餘職業或完整五線內容。

## 1. 執行順序與責任

| 任務 | 負責人 | 前置依賴 | 完成證據 |
|---|---|---|---|
| T1 工程與共享介面 | 主 agent | 計畫批准 | Unity 匯入／編譯紀錄、共同介面清單 |
| T2 免費素材與繁中字型 | 主 agent | T1，可與 Core 並行 | 實際檔案、來源、授權及雜湊紀錄 |
| T3 核心規則 | 程式角色 | T1 介面固定 | Core 編譯、命令與狀態流程可運作 |
| T4 資料內容 | 玩法角色 | T1 資料契約固定 | 五份資料檔與引用驗證結果 |
| T5 磁碟存讀檔與畫廊 | 主 agent | T1，整合依賴 T3 | 回退／備份／重啟與隔離紀錄 |
| T6 可玩 UI 與場景 | 主 agent | T2；逐步整合 T3／T4／T5 | 一般操作流程、實際畫面 |
| T7 有風險規則驗證 | QA 角色 | T1，執行依賴 T3／T4／T5 | EditMode 測試 XML 與具體失敗修正 |
| T8 Windows 建置與啟動 | 主 agent | T1–T7 基本檢查通過 | BuildReport、exe／Data、Player 啟動紀錄 |
| T9 Player／UI 走查 | QA＋主 agent | T8 | 操作紀錄、實際截圖、未執行項說明 |
| T10 打包與交接 | 主 agent | T9 結果已收斂 | 可啟動產物、ZIP、README、驗證摘要 |

三角色沿用現有任務 game_designer、game_programmer、game_qa。每次派發都帶明確讀取文件、寫入範圍、依賴與驗收條件。共享介面由主 agent 單一擁有；各角色先提介面變更，由主 agent 統一修改，避免同時寫同一檔案。

## 2. T1：最小工程與共同介面

1. 檢查根目錄是否已出現 Assets、Packages、ProjectSettings；若有人新增，先讀內容再整合，不覆寫未知工程。
2. 在工作暫存目錄解開本機 2D URP 模板，只取必要設定與 URP 2D assets；將新產品檔案置於目前儲存庫根目錄。保留 docs、output 與既有未提交內容。
3. 配置 Unity 6000.3.7f1、URP 17.3.0、uGUI 2.0.0，以及相容的 Input System／Test Framework。先走官方 UPM 解析，再保留確切 manifest 與 lock；不直接引用 Editor 私有 DLL 作 Player runtime 依賴。
4. 配置一個 Demo.unity、URP 2D Renderer、Canvas、InputSystemUIInputModule；Windows x64、Mono backend、1280×720、可縮放視窗。
5. 建立 HGame.Core、HGame.Unity、HGame.Editor、HGame.Tests.EditMode assembly；Core 設 noEngineReferences，不引用 UnityEngine。
6. 第一次批次匯入只檢查套件與編譯；待成功後才執行 Editor 設定／建置方法，避免將未編譯的入口綁在首次解析。

共享模型由主 agent 建立於 Assets/_Project/Core：GameModels.cs、GameDefinitions.cs、GameCommands.cs、GameResults.cs、SaveModels.cs。數值以整數表示、容器用可序列化 List；資料 adapter 不要求 Core 依賴 Unity JSON 或多型序列化。

| 契約 | 必備語意 |
|---|---|
| RunState | 旅程／時間線節點、日程、資源、職業、地圖、當局敗北、勝利／成長、已領事件、純潔、感知、路線與難度 |
| MetaState | 同旅程時間線、有效回退、失敗憑證、已處理交易、特殊與被抹去記憶 |
| ProfileState | 音量／設定與跨旅程畫廊解鎖 |
| BattleState | 普通／H 階段、入場原因、敵方實例、暈眩／H／精力、擊倒選擇、終局及發獎狀態 |
| GameData | balance、enemy、skill、item、activity、event、node 的穩定 ID 與資料版本 |
| GameCommand／CommandResult | 動作、目標／選項／數量；成功／拒絕原因、狀態快照與領域事件 |
| GameSession | NewGame、GetAvailableActions、Execute、唯讀／深複製快照；UI 與正常驗收共用入口 |
| RewindRules | PlanLoad、ApplyLoad，區分 Continue／ManualLoad，支援一次性 transactionId，不呼叫路線鎖定 |
| SaveEnvelope／ISaveStore | 有版本的存檔封套、可替換檔案儲存介面及可注入的提交失敗點 |
| GallerySession | 持有獨立展示資料，不提供正式 Run／Meta 寫入入口 |

時段、職業、位置、戰鬥階段、深度入場原因、勝負、路線、載入意圖、命令種類使用共同 enum。內部診斷訊息與面向玩家的文字分開；不把玩家感知欄位名直接顯示在產品 UI。

完成後將固定契約與檔案範圍交給三角色；共享介面改動必須先同步，不能讓玩法與 QA 各自推測不同欄位。

## 3. T2：素材與字型

取得 Kenney UI Pack／Game Icons 的必要子集、靜態 NotoSansCJKtc-Regular.otf 與原始授權。來源已在設計規格列出；下載後再次核對實際封包附的授權及檔案。

Assets/_Project/Art 放 UI、圖示與自製幾何占位；Fonts 放字型及授權。女主面板使用專案原先接受的成年概念圖。使用者要求忽略的三張附件不使用。

以 TMP 動態字集或固定 Demo 文案字集呈現繁中，字型隨建置打包，不依賴玩家系統字型。docs/第三方素材授權紀錄.md 記錄來源、作者、授權、取得日期、修改及實際使用檔案；發行目錄附原授權文字。素材使用清單必須與實際產物一致。

## 4. T3：程式角色的 Core 範圍

只寫 Assets/_Project/Core 中的 GameSession.cs、BattleRules.cs、CalendarRules.cs、DungeonRules.cs、GrowthRules.cs、RouteRules.cs、RewindRules.cs、DefinitionRules.cs；共享 DTO／命令與 assembly 變更先交主 agent。

按順序實作：

1. 時間線節點、父子關係、失敗憑證與回退計畫。祖先／旁支回退只在捨棄真實新行動時加一次；相同節點、後代載入與 Continue 不加。
2. 日程、工作／購物／物品／深夜休息／章末休整，所有成本由資料讀取；多次 UI 刷新不重複扣費。
3. 普通戰鬥、舞者全體同時暈眩、Boss 可累積控制、主動／被動 H 入場與拒絕續戰。
4. HP 傷害只折算一份精力＋H 進度；H 槽溢出、保留 1 MP、等待／物品／放棄、終局後停止結算及一次發獎。
5. 迷宮開放日、十二節點／六存檔點、單趟新增一階段上限、敗北／撤退晚上返城、零 HP 恢復或返城限制。
6. 五線候選資料判定與一般／魔化的可玩分歧；魔化門檻、感知先於鎖線、拒絕分歧不完成感知、其他鎖線不可覆蓋。
7. 第一章結算、第二章記憶段落與完成試玩流程。

所有正常玩法經 GameSession 命令入口執行。不得在 Save／Load、畫廊、UI render 或讀取快照時隱式發獎或判定鎖線。

## 5. T4：玩法角色的資料範圍

只寫 Assets/_Project/Resources/Demo 下的 balance.json、enemies.json、skills.json、dungeon.json、events.json，以及資料來源說明。採 T1 固定欄位；不改 Core 或存檔程式。

- balance：完整照已確認規格列初值、成本、EXP、H 次數與路線門檻。
- enemies：一般敵人兩種與 Boss、暈眩門檻、H 資源，以及魔化後配置。
- skills：普攻、斬擊、群舞、深度入場、基本／魔化 H、防禦與等待；目標、成本和依序效果明確。
- dungeon：第一章三階段十二節點／六存檔點、兩次補給負向選擇、第一／二章不同特殊記憶 ID。
- events：工作、補給選項、隱晦記憶提示、可重訪重大分歧、畫廊占位描述；不撰寫正式劇情或露骨演出。

主 agent 的資料驗證器拒絕重複 ID、斷鏈、負成本、非法階段、缺失引用、存檔點數量不符與無法抵達的必要節點。顯示文字不可用作狀態 ID。

## 6. T5：磁碟交易、記憶與畫廊

主 agent 寫 Runtime/SaveRepository.cs、SaveCodec.cs、GallerySession.cs 與相應儲存 adapter。Core 計算回退計畫與記憶接收，Runtime 處理磁碟，不各自重算不同規則。

- 在 Application.persistentDataPath/HGameDemo 下保存 Run 槽、同旅程 Meta 與 Profile。驗證用旅程／資料目錄與玩家正式資料隔離。
- 指定失敗先持久化一次性 receipt。載入交易有 transactionId，先準備再提交；崩潰後恢復同一交易不再加計數。
- 初次建檔與替換既有檔案分開，使用 tmp 與 bak；版本／損壞錯誤回報並保留原資料。
- 載入只還原 Run、接收合資格記憶並更新 Meta，感知與路線不在載入函式中重鎖。
- 畫廊建立展示 Session，讀取已解鎖內容；重播、跳過、退出都不呼叫正式事件效果、失敗、讀檔或路線命令。

完成至少一次「寫檔→退出→重啟→繼續」與「指定失敗→真回退→殘留記憶」驗證，不能只測記憶體內複製。

## 7. T6：主 agent 的 UI 整合

Runtime/DemoBootstrap.cs 取得 GameData 與儲存狀態，UI/DemoPresenter.cs 將按鈕轉成命令，按自然職責分出 MainMenuView、TownView、DungeonView、BattleView、SaveView、GalleryView、BranchView；避免單一巨型 UI 檔案。

依序完成：

1. 主選單與資源列、新遊戲／繼續／離開、版本與內容類型說明。
2. 城鎮時段、職業、三種活動、背包、工作報酬、購物結算、等待與深夜休息。
3. 地圖階段、節點、六存檔點、早出晚歸與剩餘新增進度。
4. 普通戰鬥、敵方暈眩顯示、主動／被動入場選項、H 資源、使用次數、零 HP 戰後面板。
5. 存讀檔、模糊記憶提示、可重訪的室內分歧、取消與確認後果。
6. 畫廊、第二章驗證段、章末與完成試玩頁。

在 1280×720 與較小視窗檢查換行、縮放、按鈕禁用原因。正式面板不顯示玩家感知或開發 route 診斷資訊。

## 8. T7：QA 的最少規則驗證

QA 只寫 Assets/_Project/Tests/EditMode 及 verification 驗收紀錄；不修改產品規則以讓測試通過。必要介面由 T1 提供，檔案／儲存測試透過 ISaveStore 注入失敗點。

以七組跨流程情境起步：

1. 戰鬥終局與資源：主動／被動入場、HP／MP 邊界、零 HP 續戰、終局與獎勵恰好一次。
2. 傷害轉換與多敵控制：0／25／50% 換算、多敵精力、死敵不復活、舞者暈眩重疊與 Boss、H 不套用普通暈眩勝利。
3. 時間線與特殊記憶：祖先／旁支／後代／相同節點、指定失敗位於捨棄路徑、憑證去重、跨章區分。
4. 回退與魔化：當局感知／路線回退、Meta 保留、缺一門檻不進線、取消與重複提交、感知事件先於鎖線、其他鎖線不覆蓋。
5. 畫廊與旅程隔離：Run／Meta 完整快照不變、Continue 不加次數、新旅程記憶隔離、Profile 解鎖保留。
6. 日程與正常可達性：開放日、六存檔點、三分之一、深夜封鎖、章末休整、期限失敗，以及從 NewGame 經真實命令達成魔化的固定走查序列。
7. 儲存交易與恢復：首次／替換／備份、提交中斷恢復與冪等、不相容／損壞不靜默清空。

測試驗證結果與不變條件，不逐一複製內部方法。規則已足夠驗證後，僅因新修改或具體剩餘風險增加測試。

## 9. T8–T9：建置與 Player 驗證

主 agent 建立 Editor/DemoBuild.cs，Windows 輸出集中為 Builds/HGameDemo；建置方法檢查 BuildReport，失敗返回非零退出碼。建置與測試的 Unity 呼叫序列化，避免同一專案同時開兩個 Editor。

以下命令在計畫批准、對應檔案已建立後才執行：

~~~powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\TINGOD\Desktop\CodeHome\HGame' -logFile 'C:\Users\TINGOD\Desktop\CodeHome\HGame\work\unity-import.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe' -batchmode -projectPath 'C:\Users\TINGOD\Desktop\CodeHome\HGame' -runTests -testPlatform EditMode -testResults 'C:\Users\TINGOD\Desktop\CodeHome\HGame\verification\core-tests.xml' -logFile 'C:\Users\TINGOD\Desktop\CodeHome\HGame\work\unity-tests.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\TINGOD\Desktop\CodeHome\HGame' -executeMethod HGame.Editor.DemoBuild.BuildWindows -logFile 'C:\Users\TINGOD\Desktop\CodeHome\HGame\verification\editor-build.log'
~~~

Test Runner 命令不加提前退出的 quit；以 XML 與錯誤內容判斷結果，不只看程序退出碼。命令依據：[Unity 6.3 Editor CLI](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html)、[Test Framework CLI](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html)。解析後的實際 Test Framework 版本另記於產物，這個文件連結不宣稱已安裝某版本。

真實 Player 的 smoke 使用獨立驗證資料目錄，至少啟動場景／UI、新遊戲、寫檔讀回與退出，留下 JSON／log。自動 smoke 不稱作實際滑鼠點擊驗收；正常遊玩驗證經相同正式命令與 UI 事件，不直接塞齊魔化旗標。

UI 走查包含一般循環、舞者多敵與 Boss、H 及零 HP、第一／二章記憶、魔化分歧、畫廊及重啟繼續。截圖取自真正 Unity Player 畫面。若工具無法完成某項實際輸入操作，明列未執行及替代證據，不把自動命令測試當成人工操作。

固定證據：verification/core-tests.xml、editor-build.log、player-smoke.json、player-smoke.log、ui-walkthrough.md、screenshots/、summary.md。摘要分開列 Core、Editor／Build、Player smoke 與 UI 的實際通過、失敗和未執行。

## 10. T10：打包、版本與交付

建置通過後，驗證 exe、Data 及必要 runtime 檔案完整，將完整目錄與授權、操作說明打包，不只交單一 exe。產物置於 Builds/HGameDemo；另以不覆寫既有內容的新版本目錄／ZIP 提供可下載包。

README 寫明 Unity 版本、啟動方法、系統 Demo 範圍、暫定數值、一般／舞者／H／記憶與分歧的正常驗證路徑、資料位置與已知限制。最後更新總覽實際狀態，不把尚未執行的驗證列成成功。

產品提交按可建置里程碑分批；只加入本次明確檔案，不使用全目錄暫存來混入既有 output/ 或未知修改。本地提交不等於推送或發布，遠端操作不在此次計畫內。

## 11. 失敗處理與範圍控制

- 官方 UPM 解析失敗先查具體版本／網路／授權日誌，不靜默改成不同引擎、網頁版或移除已確認玩法。
- Unity 需要帳號登入或授權時，由使用者在本機完成；不讀取或輸出憑證。
- 素材不可用時，使用已確認的其他免費來源或專案自製幾何占位，記錄實際採用來源，不用來源不明圖片填補。
- 三角色的錯誤先回報具體情境，主 agent 決定同範圍修正；增加完整職業／路線／CG 或變更已確認規格則另行處理。
- 本計畫已批准；按 T1–T10 執行，驗證結果依實際紀錄更新，沿用已選協作方式，不重複索取已給的 Demo 製作授權。
