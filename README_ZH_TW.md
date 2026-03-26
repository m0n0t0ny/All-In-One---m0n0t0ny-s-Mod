# ALL IN ONE - m0n0t0ny's mod

[English](README.md) | [Italiano](README_IT.md) | [Français](README_FR.md) | [Deutsch](README_DE.md) | [中文简体](README_ZH_CN.md) | 中文繁體 | [日本語](README_JA.md) | [한국어](README_KO.md) | [Português](README_PT_BR.md) | [Русский](README_RU.md) | [Español](README_ES.md)

**Escape from Duckov** 的多功能生活品質模組。20個獨立功能，均可從原生**設定**選單中配置。

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Subscribe-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)
[![Latest Release](https://img.shields.io/github/v/release/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod)](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)

**目录:** [功能](#功能) · [拾取](#-拾取) · [戰鬥](#-戰鬥) · [生存](#-生存) · [HUD](#-hud) · [任務](#-任務) · [效能](#效能) · [安裝](#安裝) · [更新日誌](#更新日誌)

![Preview](AllInOneMod_m0n0t0ny/preview.png)

---

## 功能

所有設定均已儲存，可從遊戲設定選單中的 **ALL IN ONE** 分頁進行配置——主選單和遊戲內暫停選單均可存取。

![選項選單](assets/f9-menu.png)

---

### 🎒 拾取

#### 懸停時顯示物品價值
隨時顯示任何物品的出售價格，而不僅限於商店中。可選擇合計、單件、堆疊或關閉。

![物品出售價值](assets/item-sell-value.png)

#### 懸停時顯示庫存數量
顯示你正在攜帶的懸停物品數量以及藏匿處中的數量。可從設定中切換（預設開啟）。

![Inventory count on hover](assets/inventory-count-on-hover.png)

#### 快速物品轉移
Alt+點擊或Shift+點擊可在已開啟的容器和背包之間即時移動物品，反之亦然。

#### 擊殺時自動卸載槍枝
當你拾取被擊殺的敵人時，其武器會自動卸載——彈藥直接作為可拾取的堆疊進入藏匿處，隨時可以取用。

#### 已記錄鑰匙和藍圖上的徽章
已記錄的鑰匙和藍圖上顯示綠色勾選標記，讓你一眼就能知道該保留什麼、該出售什麼。

![已記錄物品徽章](assets/recorded-items-badge.png)

#### 戰利品箱高亮
世界中的戰利品容器顯示彩色輪廓，確保你不會錯過任何一個。三種模式：全部 / 僅未搜尋的 / 關閉。邊框顏色跟隨物品稀有度（空容器為白色）。

![戰利品箱高亮](assets/lootbox-highlight-coloured.png)

#### 物品稀有度顯示
根據物品出售價值，庫存欄位顯示彩色邊框。從白色（低價值）到紅色（高價值）共六個等級。可從設定中切換。

![物品稀有度顯示](assets/item-rarity-display.png)

#### 物品名稱標籤
庫存欄位中的物品名稱居中顯示，無背景標籤。

---

### ⚔️ 戰鬥

#### 顯示敵人名稱
在敵人血條上方顯示其名稱。

![敵人名稱](assets/enemy-names.png)

#### 擊殺訊息
在突襲過程中，右上角顯示擊殺訊息——擊殺者、受害者，以及爆頭時的 [HS] 標籤。

#### Boss地圖標記
全螢幕地圖上每個Boss的即時標記，顏色編碼（紅色=存活，灰色=死亡）。地圖開啟時會顯示Boss列表疊加層。可從設定中切換（預設開啟）。

![Boss map markers](assets/boss-map-markers.png)

#### 顯示隱藏的敵人血條
強制顯示預設隱藏血條的敵人血條（例如 ??? Boss）。可從設定中切換（預設開啟）。

#### 滾動時跳過近戰
滾動滑鼠滾輪切換武器時跳過近戰欄位。仍可透過 V 裝備近戰武器。

---

### 🌙 生存

#### 喚醒預設
睡眠介面上的喚醒預設按鈕：4個自訂可配置時間，加上雨天、風暴 I、風暴 II 和風暴結束。

![睡眠預設](assets/sleep-presets.png)

#### 自動關閉容器
按下WASD、Shift、空白鍵或受到傷害時自動關閉已開啟的容器。每個觸發器可獨立切換。

---

### 🖥️ HUD

#### FPS計數器
在右上角顯示目前FPS（預設關閉）。

#### 隱藏控制提示
隱藏原生控制 [O] 按鈕及其子選單，減少HUD雜亂。

![隱藏控制提示](assets/hide-controls-hint.png)

#### 瞄準時隱藏HUD
按住右鍵時隱藏HUD，獲得更清晰、更沉浸式的瞄準體驗。三種模式：隱藏全部 / 僅顯示彈藥 / 關閉。血條和準星始終保持可見。

![瞄準時隱藏HUD](assets/hide-hud-on-ads.png)

#### 攝影機視角
三模式設定：關閉 / 預設 / 俯視。所選視角立即套用，並在場景載入時自動還原。

---

### ⭐ 任務

#### 任務收藏（N鍵）
在選中的任務上按 N 將其置頂。置頂任務無論篩選器如何設定，始終可見。

![任務收藏](assets/quest-favorites.png)

## 效能

大多數功能由事件驅動，對效能沒有可測量的影響。少數功能會定期掃描場景，可能在低端硬體上造成短暫卡頓。如果遇到幀率下降，請按影響順序優先停用以下功能：

| 功能 | 峰值/場次 | 平均峰值 | 總開銷 | % |
|---|---|---|---|---|
| 顯示隱藏的敵人血條 | 32 | ~24ms | ~768ms | 34% |
| 物品稀有度顯示 | 43 | ~13ms | ~559ms | 25% |
| 戰利品箱高亮 | 32 | ~14ms | ~448ms | 20% |
| 顯示敵人名稱 | 32 | ~13ms | ~416ms | 18% |
| Boss地圖標記 | 5 | ~15ms | ~75ms | 3% |

---

## 安裝

### Steam（推薦）

1. 在 [Steam創意工坊頁面](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781) 訂閱
2. 啟動遊戲 -> 主選單中的**模組** -> 啟用該模組

每次發布新版本時，模組會自動更新。

### 手動安裝

1. 從 [發布頁面](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest) 下載最新壓縮檔
2. 將 `AllInOneMod_m0n0t0ny` 資料夾解壓縮到遊戲安裝目錄的 `Mods` 資料夾中（若不存在請建立）：

   | 平台                 | 路徑                                                                                 |
   | -------------------- | ------------------------------------------------------------------------------------ |
   | Steam (Windows)      | `C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\` |
   | Epic Games (Windows) | `C:\Program Files\Epic Games\EscapeFromDuckov\Duckov_Data\Mods\`                     |
   | Steam (Linux)        | `~/.steam/steam/steamapps/common/Escape from Duckov/Duckov_Data/Mods/`               |

3. 啟動遊戲 -> 主選單中的**模組** -> 啟用該模組

手動更新時，請用新版本替換 `AllInOneMod_m0n0t0ny` 資料夾。

---

## 更新日誌

完整版本歷史請查看 [發布頁面](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases)。
