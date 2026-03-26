# ALL IN ONE - m0n0t0ny's mod

[English](README.md) | [Italiano](README_IT.md) | [Français](README_FR.md) | [Deutsch](README_DE.md) | [中文简体](README_ZH_CN.md) | [中文繁體](README_ZH_TW.md) | 日本語 | [한국어](README_KO.md) | [Português](README_PT_BR.md) | [Русский](README_RU.md) | [Español](README_ES.md)

**Escape from Duckov** のオールインワン・クオリティオブライフMod。20の独立した機能を搭載し、すべてネイティブの**設定**メニューから設定可能です。

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Subscribe-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)
[![Latest Release](https://img.shields.io/github/v/release/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod)](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)

![Preview](AllInOneMod_m0n0t0ny/preview.png)

---

## 機能

すべての設定は保存され、ゲームの設定メニューの **ALL IN ONE** タブから設定できます——メインメニューとゲーム内ポーズメニューの両方からアクセス可能です。

![オプションメニュー](assets/f9-menu.png)

---

### 🎒 ルーティング

#### ホバー時にアイテム価値を表示
ショップだけでなく、いつでも任意のアイテムの売却価格を表示します。合計、単品、スタック、オフから選択できます。

![アイテム売却価値](assets/item-sell-value.png)

#### ホバー時のインベントリカウント
ホバーしたアイテムを何個持ち歩いているか、スタッシュに何個あるかを表示します。設定からトグル可能（デフォルトでオン）。

#### クイックアイテム転送
Alt+クリックまたはShift+クリックで、開いているコンテナとバックパック間でアイテムを即座に移動できます（逆方向も可）。

#### キル時に銃を自動アンロード
倒した敵をルーティングすると、その武器が自動的にアンロードされます——弾薬はルーティング可能なスタックとして直接スタッシュに入り、いつでも取れる状態になります。

#### 記録済みのキーとBlueprintsのバッジ
すでに記録したキーとブループリントに緑のチェックマークが表示され、何を残して何を売るべきかが一目でわかります。

![記録済みアイテムバッジ](assets/recorded-items-badge.png)

#### ルートボックスハイライト
ワールド内のルートコンテナにカラーアウトラインを表示し、見逃しを防ぎます。三つのモード：すべて / 未検索のみ / オフ。ボーダーカラーはアイテムのレアリティに従います（空のコンテナは白）。

![ルートボックスハイライト](assets/lootbox-highlight.png)

#### アイテムレアリティ表示
アイテムの売却価値に基づき、インベントリスロットにカラーボーダーを表示します。白（低価値）から赤（高価値）まで6段階。設定からトグル可能。

![アイテムレアリティ表示](assets/item-rarity-display.png)

#### アイテム名ラベル
インベントリスロットのアイテム名は中央揃えで、背景ラベルなしで表示されます。

---

### ⚔️ 戦闘

#### 敵の名前を表示
敵のHPバーの上に名前を表示します。

![敵の名前](assets/enemy-names.png)

#### キルフィード
レイド中に右上コーナーにキル情報を表示——キラー、被害者、ヘッドショット時の [HS] タグ。

#### ボスマップマーカー
全画面マップ上の各ボスのリアルタイムマーカー、カラーコード付き（赤=生存、灰色=死亡）。マップ開放時にボスリストオーバーレイが表示されます。設定からトグル可能（デフォルトでオン）。

#### 隠れた敵のHPバーを表示
デフォルトでHPバーが非表示の敵（例：???ボス）に強制的にHPバーを表示します。設定からトグル可能（デフォルトでオン）。

#### スクロール時に近接をスキップ
武器を切り替える際にスクロールホイールが近接スロットをスキップします。近接はV キーで装備できます。

---

### 🌙 サバイバル

#### ウェイクアッププリセット
スリープ画面のウェイクアッププリセットボタン：4つのカスタム設定可能な時間、雨、嵐 I、嵐 II、嵐の終わり。

![スリーププリセット](assets/sleep-presets.png)

#### コンテナ自動クローズ
WASD、Shift、スペース、またはダメージを受けたときに開いているコンテナを自動的に閉じます。各トリガーは独立してトグル可能。

---

### 🖥️ HUD

#### FPSカウンター
右上コーナーに現在のFPSを表示します（デフォルトでオフ）。

#### コントロールヒントを非表示
ネイティブのコントロール [O] ボタンとそのサブメニューを非表示にして、HUDをすっきりさせます。

![コントロールヒントを非表示](assets/hide-controls-hint.png)

#### ADS時にHUDを非表示
右クリックを保持している間HUDを非表示にし、よりクリーンで没入感のある照準体験を実現します。三つのモード：すべて非表示 / 弾薬のみ表示 / オフ。HPバーと照準は常に表示されます。

![ADS時にHUDを非表示](assets/hide-hud-on-ads.png)

#### カメラビュー
三モード設定：オフ / デフォルト / 俯瞰。選択したビューが即座に適用され、シーンロード時に自動的に復元されます。

---

### ⭐ クエスト

#### クエストお気に入り（N キー）
選択したクエストで N を押すとリストの先頭に固定されます。固定されたクエストはフィルターに関わらず常に表示されます。

![クエストお気に入り](assets/quest-favorites.png)

---

## インストール

### Steam（推奨）

1. [Steam Workshopページ](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781) でサブスクライブ
2. ゲームを起動 -> メインメニューの**Mods** -> Modを有効化

新しいバージョンが公開されるたびにModは自動的に更新されます。

### 手動

1. [リリースページ](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest) から最新のzipをダウンロード
2. `AllInOneMod_m0n0t0ny` フォルダをゲームインストールの `Mods` フォルダに解凍します（存在しない場合は作成）：

   | プラットフォーム     | パス                                                                                 |
   | -------------------- | ------------------------------------------------------------------------------------ |
   | Steam (Windows)      | `C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\` |
   | Epic Games (Windows) | `C:\Program Files\Epic Games\EscapeFromDuckov\Duckov_Data\Mods\`                     |
   | Steam (Linux)        | `~/.steam/steam/steamapps/common/Escape from Duckov/Duckov_Data/Mods/`               |

3. ゲームを起動 -> メインメニューの**Mods** -> Modを有効化

手動で更新するには、`AllInOneMod_m0n0t0ny` フォルダを新しいバージョンに置き換えてください。

---

## 変更履歴

完全なバージョン履歴は [リリース](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases) をご覧ください。
