# 0.2.0 俯視探索與橫向戰鬥驗證摘要

日期：2026-10-22。Unity 6000.3.7f1，Windows x64、Mono、URP 2D。此目錄為新版獨立證據，0.1.0 紀錄仍保留於上層。

## 結果

| 項目 | 實際結果 | 證據 |
|---|---|---|
| Unity EditMode | **50/50 通過**，0 失敗／跳過 | core-tests.xml，UTC 10:53:29–30 |
| Windows 建置 | Succeeded，0 errors／warnings | build-report.json |
| 1280×720 Player 全流程 | passed | player-smoke.json、screenshots/ |
| 960×540 Player 全流程 | passed | small-window/player-smoke.json、screenshots/ |
| 關閉後另一程序 Continue | passed，同旅程、安全位置、回退不增、畫廊可播 | restart/player-smoke.json、screenshots/ |

EditMode 包含原 43 項規則／持久化回歸，以及 7 項地圖、位置、碰撞、安全存檔與不建立時間線的針對性測試。最後的像素占位、HUD 與走查入口調整以 Player 實跑確認，不將原 0.1.0 的成功當成 0.2.0 結果。

## Player 已走查

- 真正合成鍵盤輸入沿可通行路徑走到商店、入口與首個敵人，面向按 E；選單開啟時移動被鎖。
- 主選單、接敵確認、第一個普通攻擊、分歧確認、畫廊與零 HP 安全返城使用合成指標點擊。
- 普通／H／舞者多敵與 Boss、首章攻略、兩章特殊記憶、畫廊快照隔離、重大轉變候選／感知／鎖線、魔化群體技能勝利、讀回分歧前不自動鎖線。
- 後段規則序列使用正式 Presenter／GameSession 命令；沒有直接補資格旗標，沒有藉展示層重算獎勵。
- 動畫未結束時返回主選單並另開旅程，等待超過原動畫生命週期，確認新旅程狀態未被舊回呼改動。
- 敵人真實擊倒至 HP 0、選 H 續戰並獲勝，HP 仍為 0；點畫面安全返城按鈕後正常返回。
- 第一次 Player 完全退出後啟動第二程序，按繼續恢復同旅程、安全地圖位置與畫廊，回退計數不增。

操作細節見 [ui-walkthrough.md](ui-walkthrough.md)。自動化僅在測試模式忽略焦點停用與啟用驗證裝置；一般遊玩不改該設定。

## 交付與限制

可玩資料夾：Builds/HGameDemo-0.2.0。完整 ZIP：output/HGameDemo-0.2.0-win64.zip，包含操作說明、ThirdParty 原授權與本版摘要；排除 DoNotShip。工程仍位於 C:/Users/TINGOD/Desktop/CodeHome/HGame。只作本機 Git 提交，未推送或公開發布。

示範地圖、像素小人、敵人與簡易晃動／位移為占位，沒有完整四向角色幀或正式戰鬥動畫。正式地圖、角色造型、玩法細節與劇情仍由使用者後續補充；本版沒有新增 CG、完整五線結局或期限魔化救援。

已完成本機自動輸入與實際畫面核對；未宣稱人工完整鍵鼠走查、長時間平衡或其他硬體相容性測試。驗證資料在獨立 Verification 子目錄，不修改玩家正式存檔。
