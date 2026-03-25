# ALL IN ONE - m0n0t0ny's mod

**Escape from Duckov**를 위한 올인원 삶의 질 향상 모드. 20개의 독립적인 기능이 모두 기본 **설정** 메뉴에서 구성 가능합니다.

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Subscribe-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)
[![Latest Release](https://img.shields.io/github/v/release/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod)](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)

![Preview](AllInOneMod_m0n0t0ny/preview.png)

---

## 기능

모든 설정은 저장되며, 게임의 설정 메뉴에 있는 **ALL IN ONE** 탭에서 구성할 수 있습니다——메인 메뉴와 인게임 일시정지 메뉴 모두에서 접근 가능합니다.

![옵션 메뉴](assets/f9-menu.png)

---

### 🎒 루팅

#### 마우스 오버 시 아이템 가치 표시
상점에서만이 아니라 언제든지 아이템의 판매 가격을 표시합니다. 합산, 단품, 스택 또는 끄기 중에서 선택할 수 있습니다.

![아이템 판매 가치](assets/item-sell-value.png)

#### 마우스 오버 시 인벤토리 개수
현재 마우스를 올린 아이템을 몇 개 소지하고 있는지, 보관함에 몇 개 있는지 표시합니다. 설정에서 전환 가능 (기본값: 켜짐).

#### 빠른 아이템 이동
Alt+클릭 또는 Shift+클릭으로 열린 컨테이너와 배낭 사이에서 아이템을 즉시 이동할 수 있으며, 반대도 마찬가지입니다.

#### 킬 시 총기 자동 언로드
처치한 적을 루팅하면 그들의 무기가 자동으로 언로드됩니다——탄약이 루팅 가능한 스택으로 직접 보관함으로 들어가 바로 가져갈 수 있습니다.

#### 기록된 열쇠와 Blueprints 뱃지
이미 기록한 열쇠와 블루프린트에 녹색 체크 표시가 나타나, 무엇을 보관하고 무엇을 팔아야 하는지 한눈에 알 수 있습니다.

![기록된 아이템 뱃지](assets/recorded-items-badge.png)

#### 루트박스 강조 표시
세계의 전리품 컨테이너에 색상 윤곽선을 표시하여 절대 놓치지 않도록 합니다. 세 가지 모드: 전체 / 미수색만 / 끄기. 테두리 색상은 아이템 희귀도를 따릅니다 (빈 컨테이너는 흰색).

![루트박스 강조 표시](assets/lootbox-highlight.png)

#### 아이템 희귀도 표시
아이템 판매 가치에 따라 인벤토리 슬롯에 색상 테두리를 표시합니다. 흰색 (낮은 가치)부터 빨간색 (높은 가치)까지 6단계. 설정에서 전환 가능.

![아이템 희귀도 표시](assets/item-rarity-display.png)

#### 아이템 이름 레이블
인벤토리 슬롯의 아이템 이름은 가운데 정렬되고 배경 레이블 없이 표시됩니다.

---

### ⚔️ 전투

#### 적 이름 표시
적의 체력 바 위에 이름을 표시합니다.

![적 이름](assets/enemy-names.png)

#### 킬 피드
레이드 중 오른쪽 상단에 킬 정보를 표시합니다——킬러, 피해자, 헤드샷 시 [HS] 태그.

#### 보스 지도 마커
각 보스에 대한 전체화면 지도의 실시간 마커, 색상 코드 (빨간색=생존, 회색=사망). 지도가 열리면 보스 목록 오버레이가 나타납니다. 설정에서 전환 가능 (기본값: 켜짐).

#### 숨겨진 적 체력 바 표시
기본적으로 체력 바가 숨겨진 적 (예: ??? 보스)에게 강제로 체력 바를 표시합니다. 설정에서 전환 가능 (기본값: 켜짐).

#### 스크롤 시 근접 무기 건너뛰기
마우스 휠로 무기를 순환할 때 근접 무기 슬롯을 건너뜁니다. 근접 무기는 여전히 V 키로 장착할 수 있습니다.

---

### 🌙 생존

#### 기상 프리셋
수면 화면의 기상 프리셋 버튼: 4개의 사용자 정의 설정 가능한 시간, 비, 폭풍 I, 폭풍 II, 폭풍 종료.

![수면 프리셋](assets/sleep-presets.png)

#### 컨테이너 자동 닫기
WASD, Shift, 스페이스 키를 누르거나 피해를 입을 때 열린 컨테이너를 자동으로 닫습니다. 각 트리거는 독립적으로 전환 가능합니다.

---

### 🖥️ HUD

#### FPS 카운터
오른쪽 상단에 현재 FPS를 표시합니다 (기본값: 끄기).

#### 조작 힌트 숨기기
HUD 혼잡을 줄이기 위해 기본 조작 [O] 버튼과 하위 메뉴를 숨깁니다.

![조작 힌트 숨기기](assets/hide-controls-hint.png)

#### ADS 시 HUD 숨기기
오른쪽 클릭을 유지하는 동안 HUD를 숨겨 더 깔끔하고 몰입감 있는 조준 경험을 제공합니다. 세 가지 모드: 모두 숨기기 / 탄약만 표시 / 끄기. 체력 바와 크로스헤어는 항상 표시됩니다.

![ADS 시 HUD 숨기기](assets/hide-hud-on-ads.png)

#### 카메라 뷰
세 가지 모드 설정: 끄기 / 기본 / 탑다운. 선택한 뷰는 즉시 적용되고 씬 로드 시 자동으로 복원됩니다.

---

### ⭐ 퀘스트

#### 퀘스트 즐겨찾기 (N 키)
선택한 퀘스트에서 N을 누르면 목록 상단에 고정됩니다. 고정된 퀘스트는 필터에 관계없이 항상 표시됩니다.

![퀘스트 즐겨찾기](assets/quest-favorites.png)

---

## 설치

### Steam (권장)

1. [Steam 워크샵 페이지](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)에서 구독
2. 게임 실행 -> 메인 메뉴의 **Mods** -> 모드 활성화

새 버전이 게시될 때마다 모드가 자동으로 업데이트됩니다.

### 수동 설치

1. [릴리스 페이지](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)에서 최신 zip 다운로드
2. `AllInOneMod_m0n0t0ny` 폴더를 게임 설치 경로의 `Mods` 폴더에 압축 해제 (없으면 생성):

   | 플랫폼               | 경로                                                                                 |
   | -------------------- | ------------------------------------------------------------------------------------ |
   | Steam (Windows)      | `C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\` |
   | Epic Games (Windows) | `C:\Program Files\Epic Games\EscapeFromDuckov\Duckov_Data\Mods\`                     |
   | Steam (Linux)        | `~/.steam/steam/steamapps/common/Escape from Duckov/Duckov_Data/Mods/`               |

3. 게임 실행 -> 메인 메뉴의 **Mods** -> 모드 활성화

수동으로 업데이트하려면 `AllInOneMod_m0n0t0ny` 폴더를 새 버전으로 교체하세요.

---

## 변경 로그

전체 버전 기록은 [릴리스](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases)를 참조하세요.
