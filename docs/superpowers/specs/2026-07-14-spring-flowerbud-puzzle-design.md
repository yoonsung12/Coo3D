# 봄 구역 개화 퍼즐 설계 (FlowerBud)

날짜: 2026-07-14
관련 로드맵: 큰 맵을 봄/여름/가을/겨울 4구역으로 나누는 레벨 디자인 중, 봄 구역의 첫 퍼즐 체인

## 배경

- 게임을 하나의 큰 맵에 봄/여름/가을/겨울 4개 구역으로 나눌 예정이며, 각 구역은 **공간적으로 항상 그 계절**이다(시간에 따라 전역으로 바뀌는 게 아님).
- 확인 결과, `RainController`/`SnowController`/`PollenSpawner`/`LeafSpawner`는 애초에 `SeasonManager.CurrentSeason`을 참조하지 않고 씬에 배치되면 항상 작동한다. `SeasonManager.CurrentSeason`을 실제로 쓰는 곳은 `IceBlock`(겨울 재생성)과 게이지/디버프 시스템뿐이라, "구역별 고정 계절" 설계는 기존 구조를 거의 그대로 쓸 수 있다. (구역 진입 시 `SeasonManager.SetSeason()`을 호출하는 트리거는 이번 서브프로젝트 범위 밖이며, 필요해지면 별도로 다룬다.)
- 여름(비/물), 겨울(얼음)은 각각 `WaterArea`+`FloatingBox`, `IceBlock`처럼 그 계절만의 고유 퍼즐 오브젝트가 있지만, 봄은 `Pollen`(낙하물)과 `PollenSpawner`만 있고 고유 퍼즐 오브젝트가 없다.
- `Pollen`은 `IBlowable`을 구현하고 있어 `FanTool`로 날려 보낼 수 있다 — 지금까지는 "피해야 할 낙하물"로만 쓰였지만, 이 특성을 이용해 "선풍기로 조준해서 원하는 곳으로 모으는" 퍼즐을 만들 수 있다.

## 목표

1. 선풍기로 꽃가루를 조준해 날려서 `FlowerBud`(신규)에 모으면, 목표 개수만큼 쌓였을 때 개화하며 아이템(`WoodenBox`)을 만들어낸다.
2. 이렇게 얻은 `WoodenBox`를 다시 선풍기로 밀어 기존 `PressButton` 2개를 동시에 눌러 `DoorController`로 문을 연다 — "퍼즐을 풀어서 다음 퍼즐에 쓸 물건을 구한다"는 체인을 만든다.

## 아키텍처

### 1. `FlowerBud.cs` (신규, `Assets/Scripts/Puzzle/FlowerBud.cs`)

꽃가루를 흡수해 목표 개수가 차면 개화하는 1회성 오브젝트다. `WoodenBox.cs`, `IceBlock.cs`와 같은 폴더에 둔다.

- `[SerializeField] int requiredPollenCount = 5` — 개화에 필요한 꽃가루 개수. `PollenSpawner`의 기본 `burstCount`(8)보다 적게 잡아, 우수수 한 번으로 채울 수 있되 다 못 맞히면 다음 우수수를 기다려야 하는 정도로 난이도를 맞춘다.
- `[SerializeField] GameObject bloomItemPrefab` — 개화 시 생성할 아이템 프리팹(기본값: `WoodenBox` 프리팹).
- `[SerializeField] float bloomDuration = 0.6f`, `[SerializeField] Ease bloomEase = Ease.OutBack` — 개화 연출 값. Inspector에서 조절 가능하게 한다.
- `[SerializeField] Vector3 itemSpawnOffset` — 아이템이 생성될 위치(자기 위치 기준 오프셋). 상자가 땅 위에 자연스럽게 나오도록 조정할 수 있게 한다.
- `[ReadOnly, ShowInInspector] int _currentPollenCount` — 런타임 진행 상황 확인용.
- `[ReadOnly, ShowInInspector] bool _isBloomed` — 개화 후 더 이상 반응하지 않도록 하는 상태 플래그.

동작:
- `RequireComponent(typeof(Collider))` — 트리거로 강제 설정(`Awake`에서 `isTrigger = true`). Inspector에는 SphereCollider(반경 약 1) 부착을 권장.
- `OnTriggerEnter(Collider other)`: `_isBloomed`면 무시. `other.GetComponent<Pollen>()`이 있으면 흡수 — `Destroy(other.gameObject)` 하고 `_currentPollenCount++`. `_currentPollenCount >= requiredPollenCount`면 `Bloom()` 호출.
- `Bloom()`: `_isBloomed = true`로 고정한 뒤, DOTween Sequence로 `transform.DOScale(원래크기 * 1.15f, bloomDuration).SetEase(bloomEase)` 연출 후 `OnComplete`에서 `Instantiate(bloomItemPrefab, transform.position + itemSpawnOffset, Quaternion.identity)`.
- `OnDestroy()`: 진행 중인 Tween `Kill()` 처리 (기존 `IceBlock`/`FlowerBud` 계열 컨벤션과 동일).
- Odin `[Button("강제 개화 테스트")]`로 `Bloom()`을 바로 호출할 수 있게 한다.

**`Pollen.cs`는 전혀 수정하지 않는다.** `FlowerBud`가 자신의 트리거로 `Pollen`을 감지하는 단방향 구조라, `Pollen` 쪽에서 `FlowerBud`의 존재를 알 필요가 없다.

### 2. 씬 배치 (코드 변경 없음)

- `FlowerBud`는 `PollenSpawner`의 스폰 범위(`spawnHalfWidth`)를 살짝 벗어난 위치(예: 옆으로 치우친 단상 위)에 둔다. 자연 낙하하는 꽃가루가 저절로 흘러들지 않고, 선풍기로 조준해서 날려야만 채워지도록 하기 위함이다. (`Pollen`의 `_isBlown` 상태를 코드로 직접 검사하는 대신 배치로 해결 — 기존 코드를 건드리지 않기 위한 선택.)
- `FlowerBud`가 생성하는 `WoodenBox`는 기존 프리팹을 재사용하되, 이 방 크기에 맞춰 `restoreForce`(복원력) 값만 Inspector에서 낮게 조정하는 걸 검토한다(상자를 목표 지점까지 밀고 가야 하므로 복원력이 너무 강하면 안 됨).
- 이후 `PressButton` 2개 + `DoorController`는 기존 시스템을 그대로 배치해 연결한다.

## 기존 기능 영향

- `Pollen.cs`, `PollenSpawner.cs`, `WoodenBox.cs`, `PressButton.cs`, `DoorController.cs` 전부 **수정 없음**. `FlowerBud`가 새로 추가되는 것뿐이라 기존 계절/퍼즐 동작에 영향 없음.

## 범위 밖 (Out of scope)

- 구역 진입 시 `SeasonManager.SetSeason()`을 호출하는 존 트리거 — 봄 구역 자체의 "항상 봄"이라는 특성은 이번 퍼즐 자체 동작에 필요하지 않으므로 별도 서브프로젝트로 다룬다.
- 여러 개의 `FlowerBud`가 서로 다른 아이템을 주는 분기형 구조 — 이번엔 1:1(꽃봉오리 하나 → 상자 하나)만 다룬다.
- 꽃가루를 "쳐내지 않고 모아야 하는" PressButton 변형(브레인스토밍 중 논의한 대안 ②) — 이번 범위에서는 제외.

## 테스트 방법 (Unity Editor)

- Play Mode에서 꽃가루가 우수수 떨어질 때 선풍기로 조준해 `FlowerBud` 쪽으로 날아가는지 확인
- `_currentPollenCount`(Odin ReadOnly)가 꽃가루 흡수마다 올라가는지 확인
- 목표 개수 도달 시 개화 연출과 함께 `WoodenBox`가 정상 위치에 생성되는지 확인
- 개화 후 추가로 꽃가루가 닿아도 더 이상 반응하지 않는지(`_isBloomed`) 확인
- 생성된 상자를 선풍기로 밀어 `PressButton` 2개를 동시에 눌러 문이 열리는지 확인 (기존 B/D 로드맵과 동일한 검증 방식)
- Console에 Error 없는지 확인
- 헤드리스 MCP 환경에서는 트리거 연쇄 반응(꽃가루 흡수 → 개화 → 상자 생성 → 버튼 → 문)이 안정적으로 재현되지 않을 수 있어, 리플렉션으로 각 단계 로직만 개별 검증하고 최종 확인은 사용자가 Play Mode에서 진행한다.
