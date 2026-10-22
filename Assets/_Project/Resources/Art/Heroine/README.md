# 女主 Demo 素材

依本專案既有成年女主造型，由內建 imagegen 生成。工作設定為 24 歲；未作完整版最終定稿。

- heroine-standing.png：887×1774，透明全身立繪。
- heroine-walk-sheet.png：1086×1448，四方向各三影格。圖集外側留白不平均，請使用下列量測 Rect，而非直接以整張圖寬度三等分。

影格寬 260、高 360；三欄起點 X 為 135、413、689。四列由上至下依序為朝下、朝左、朝右、朝上；各列距圖片頂部的 Y 起點為 0、357、706、1064。Unity 底部原點 Rect.y = 圖高 − 頂部起點 − 360。每列中間影格為站姿，左右影格為踏步。

原 PNG 未手動修圖，保留 alpha；Unity 在執行時切幀及縮放。完整來源、提示詞與 SHA-256 見 docs/09-AI素材使用紀錄.md、docs/heroine-assets-prompt-set.json 與 docs/AI_使用登記表.csv。
