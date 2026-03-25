# ALL IN ONE - m0n0t0ny's mod

**Escape from Duckov** 的多功能生活品质模组。20个独立功能，均可从原生**设置**菜单中配置。

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Subscribe-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)
[![Latest Release](https://img.shields.io/github/v/release/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod)](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)

![Preview](AllInOneMod_m0n0t0ny/preview.png)

---

## 功能

所有设置均已保存，可从游戏设置菜单中的 **ALL IN ONE** 选项卡进行配置——主菜单和游戏内暂停菜单均可访问。

![选项菜单](assets/f9-menu.png)

---

### 🎒 拾取

#### 悬停时显示物品价值
随时显示任何物品的出售价格，而不仅仅是在商店中。可选择合计、单件、堆叠或关闭。

![物品出售价值](assets/item-sell-value.png)

#### 悬停时显示库存数量
显示你正在携带的悬停物品数量以及藏匿处中的数量。可从设置中切换（默认开启）。

#### 快速物品转移
Alt+点击或Shift+点击可在打开的容器和背包之间即时移动物品，反之亦然。

#### 击杀时自动卸载枪支
当你拾取被击杀的敌人时，其武器会自动卸载——弹药直接作为可拾取的堆叠进入藏匿处，随时可以取用。

#### 已记录钥匙和蓝图上的徽章
已记录的钥匙和蓝图上显示绿色对勾，让你一眼就能知道该保留什么、该出售什么。

![已记录物品徽章](assets/recorded-items-badge.png)

#### 战利品箱高亮
世界中的战利品容器显示彩色轮廓，确保你不会错过任何一个。三种模式：全部 / 仅未搜索的 / 关闭。边框颜色跟随物品稀有度（空容器为白色）。

![战利品箱高亮](assets/lootbox-highlight.png)

#### 物品稀有度显示
根据物品出售价值，库存槽位显示彩色边框。从白色（低价值）到红色（高价值）共六个等级。可从设置中切换。

![物品稀有度显示](assets/item-rarity-display.png)

#### 物品名称标签
库存槽位中的物品名称居中显示，无背景标签。

---

### ⚔️ 战斗

#### 显示敌人名称
在敌人血条上方显示其名称。

![敌人名称](assets/enemy-names.png)

#### 击杀信息
在突袭过程中，右上角显示击杀信息——击杀者、受害者，以及爆头时的 [HS] 标签。

#### Boss地图标记
全屏地图上每个Boss的实时标记，颜色编码（红色=存活，灰色=死亡）。地图打开时会显示Boss列表叠加层。可从设置中切换（默认开启）。

#### 显示隐藏的敌人血条
强制显示默认隐藏血条的敌人血条（例如 ??? Boss）。可从设置中切换（默认开启）。

#### 滚动时跳过近战
滚动鼠标滚轮切换武器时跳过近战槽位。仍可通过 V 装备近战武器。

---

### 🌙 生存

#### 唤醒预设
睡眠界面上的唤醒预设按钮：4个自定义可配置时间，加上雨天、风暴 I、风暴 II 和风暴结束。

![睡眠预设](assets/sleep-presets.png)

#### 自动关闭容器
按下WASD、Shift、空格键或受到伤害时自动关闭已打开的容器。每个触发器可独立切换。

---

### 🖥️ HUD

#### FPS计数器
在右上角显示当前FPS（默认关闭）。

#### 隐藏控制提示
隐藏原生控制 [O] 按钮及其子菜单，减少HUD杂乱。

![隐藏控制提示](assets/hide-controls-hint.png)

#### 瞄准时隐藏HUD
按住右键时隐藏HUD，获得更清晰、更沉浸式的瞄准体验。三种模式：隐藏全部 / 仅显示弹药 / 关闭。血条和准星始终保持可见。

![瞄准时隐藏HUD](assets/hide-hud-on-ads.png)

#### 摄像机视角
三模式设置：关闭 / 默认 / 俯视。所选视角立即应用，并在场景加载时自动恢复。

---

### ⭐ 任务

#### 任务收藏（N键）
在选中的任务上按 N 将其置顶。置顶任务无论过滤器如何设置，始终可见。

![任务收藏](assets/quest-favorites.png)

---

## 安装

### Steam（推荐）

1. 在 [Steam创意工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781) 订阅
2. 启动游戏 -> 主菜单中的**模组** -> 启用该模组

每次发布新版本时，模组会自动更新。

### 手动安装

1. 从 [发布页面](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest) 下载最新压缩包
2. 将 `AllInOneMod_m0n0t0ny` 文件夹解压到游戏安装目录的 `Mods` 文件夹中（如不存在请创建）：

   | 平台                 | 路径                                                                                 |
   | -------------------- | ------------------------------------------------------------------------------------ |
   | Steam (Windows)      | `C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\` |
   | Epic Games (Windows) | `C:\Program Files\Epic Games\EscapeFromDuckov\Duckov_Data\Mods\`                     |
   | Steam (Linux)        | `~/.steam/steam/steamapps/common/Escape from Duckov/Duckov_Data/Mods/`               |

3. 启动游戏 -> 主菜单中的**模组** -> 启用该模组

手动更新时，请用新版本替换 `AllInOneMod_m0n0t0ny` 文件夹。

---

## 更新日志

完整版本历史请查看 [发布页面](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases)。
