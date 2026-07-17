# 봄 구역 개화 퍼즐(FlowerBud) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 선풍기로 조준해 날린 꽃가루(`Pollen`)를 `FlowerBud`(신규)가 흡수해서 목표 개수를 채우면 개화 연출과 함께 `WoodenBox`를 만들어내고, 그 상자를 다시 선풍기로 밀어 기존 `PressButton` 2개를 동시에 눌러 `DoorController`로 문을 여는 체인 퍼즐을 완성한다.

**Architecture:** `FlowerBud`는 트리거 콜라이더로 `Pollen`을 감지하는 단방향 신규 컴포넌트 하나만 추가한다. `Pollen`/`PollenSpawner`/`WoodenBox`/`PressButton`/`DoorController`는 기존 코드를 전혀 건드리지 않고, 씬 배치(스폰 범위 밖에 `FlowerBud`를 두는 것)만으로 "선풍기로 조준해야만 채워진다"는 규칙을 구현한다. 개화 시 `WoodenBox` 프리팹을 `Instantiate`하고, 그 뒤로는 이미 완성되어 있는 `WoodenBox`(바람에 밀림) → `PressButton`(밟히면 눌림) → `DoorController`(모든 버튼이 눌리면 문 열림) 체인을 그대로 재사용한다.

**Tech Stack:** Unity 6 (6000.0.68f1), C#, DOTween Pro, Odin Inspector.

## Global Constraints

- 게임 시점은 3D 사이드뷰(2.5D)다 — 이동 평면은 X(좌우)+Y(상하), Z축은 고정한다. (CLAUDE.md 기준)
- 이 프로젝트에는 자동화 테스트 프레임워크가 구성되어 있지 않다. 각 태스크의 검증은 "구현 → Unity Editor Play Mode에서 수동 검증" 사이클로 진행한다.
- DOTween을 쓰는 곳은 트윈을 변수에 저장하고 오브젝트 파괴 시 `Kill()`한다.
- Odin `[SerializeField]`에는 한글 `[LabelText]`를 붙이고, 새 코드의 주요 필드/메서드에는 한국어 주석(왜 필요한지 위주)을 단다.
- `Pollen.cs`, `PollenSpawner.cs`, `WoodenBox.cs`, `PressButton.cs`, `DoorController.cs`는 전혀 수정하지 않는다. 새 컴포넌트 `FlowerBud`만 추가한다.
- 여러 `FlowerBud`가 서로 다른 아이템을 주는 분기형 구조, 구역 진입 시 `SeasonManager.SetSeason()`을 호출하는 존 트리거는 이번 범위에 포함하지 않는다.

---

## 파일 구조

- **Create:** `Assets/Scripts/Puzzle/FlowerBud.cs` — 꽃가루 흡수 카운트, 개화 연출, 아이템 생성

참고: 이 프로젝트에는 `FlowerBud` 전용 프리팹이 아직 없다. Task 2의 씬 조립은 현재 열려 있는 씬에 테스트용 GameObject를 직접 만들어 확인한다.

**(구현 후 추가 기록)** Task 2 진행 중 씬의 비활성 `WoodenBox` 오브젝트를 `Instantiate` 소스로 그대로 쓰면 복제본도 비활성 상태로 생성되는 버그를 발견해서, `Assets/Prefabs/Puzzle/WoodenBox.prefab`을 새로 만들어 `FlowerBud.bloomItemPrefab`이 이 프리팹을 참조하도록 했다. 이 프리팹은 이제 실제로 참조되는 필수 에셋이다.

---

### Task 1: FlowerBud 스크립트 생성 (꽃가루 흡수 + 개화 연출)

**Files:**
- Create: `Assets/Scripts/Puzzle/FlowerBud.cs`

**Interfaces:**
- Consumes: `Pollen`(`Assets/Scripts/Season/Pollen.cs`, 기존) — `GetComponent<Pollen>()`으로 감지만 하고 내부 필드는 건드리지 않는다.
- Produces (Task 2가 사용):
  - `[SerializeField] GameObject bloomItemPrefab` — Inspector에서 `WoodenBox` 프리팹을 연결하는 슬롯
  - `[Button("강제 개화 테스트")]` — Play Mode에서 즉시 개화를 트리거하는 Odin 버튼

- [ ] **Step 1: `Assets/Scripts/Puzzle/FlowerBud.cs`를 아래 내용으로 생성한다.**

```csharp
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 봄 구역 첫 퍼즐 오브젝트다. 선풍기로 조준해 날린 꽃가루(Pollen)를 흡수해서 목표 개수를 채우면
// 개화 연출과 함께 아이템(기본: WoodenBox)을 하나 만들어낸다.
// PollenSpawner의 스폰 범위 밖에 배치해서, 자연 낙하로는 채워지지 않고 선풍기로 조준해야만 채워지게 한다.
[RequireComponent(typeof(Collider))]
public class FlowerBud : MonoBehaviour
{
    [Title("개화 조건")]
    [SerializeField, LabelText("필요한 꽃가루 개수")]
    private int requiredPollenCount = 5;
    // PollenSpawner의 한 번 우수수 개수(기본 8개)보다 적게 잡아, 우수수 한 번으로 채울 수 있지만
    // 다 못 맞히면 다음 우수수를 기다려야 하는 정도로 난이도를 맞춘다.

    [Title("개화 결과물")]
    [SerializeField, LabelText("개화 시 생성할 아이템 프리팹")]
    private GameObject bloomItemPrefab;
    // Inspector에서 WoodenBox 프리팹을 연결한다.

    [SerializeField, LabelText("아이템 생성 위치 오프셋")]
    private Vector3 itemSpawnOffset;
    // 자기 위치 기준 오프셋이다. 상자가 땅 위에 자연스럽게 놓이도록 조정한다.

    [Title("개화 연출 설정")]
    [SerializeField, LabelText("개화 연출 시간")]
    private float bloomDuration = 0.6f;

    [SerializeField, LabelText("개화 Ease")]
    private Ease bloomEase = Ease.OutBack;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 흡수한 꽃가루 수")]
    private int _currentPollenCount;

    [ReadOnly, ShowInInspector, LabelText("개화 완료 여부")]
    private bool _isBloomed;

    private Vector3 _originalScale;
    private Tween _bloomTween;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        // 꽃가루를 감지만 하면 되는 흡수용 트리거라 물리 충돌은 필요 없다.

        _originalScale = transform.localScale;
    }

    private void OnDestroy()
    {
        _bloomTween?.Kill();
    }

    // 선풍기로 날아온 꽃가루가 이 범위에 들어오면 흡수한다.
    private void OnTriggerEnter(Collider other)
    {
        if (_isBloomed) return;

        Pollen pollen = other.GetComponent<Pollen>();
        if (pollen == null) return;

        Destroy(other.gameObject);
        _currentPollenCount++;

        if (_currentPollenCount >= requiredPollenCount)
            Bloom();
    }

    private void Bloom()
    {
        _isBloomed = true;

        _bloomTween?.Kill();
        _bloomTween = transform
            .DOScale(_originalScale * 1.15f, bloomDuration)
            .SetEase(bloomEase)
            .OnComplete(SpawnBloomItem);
    }

    private void SpawnBloomItem()
    {
        if (bloomItemPrefab == null) return;
        Instantiate(bloomItemPrefab, transform.position + itemSpawnOffset, Quaternion.identity);
    }

    [Button("강제 개화 테스트")]
    private void TestBloom()
    {
        if (!_isBloomed) Bloom();
        // Play Mode에서 꽃가루를 직접 모으지 않고도 개화 연출과 아이템 생성만 빠르게 확인하기 위한 버튼이다.
    }
}
```

- [ ] **Step 2: Unity Editor에서 컴파일을 확인하고, 격리된 상태로 스크립트 자체를 테스트한다.**

1. Unity Editor로 돌아가 컴파일이 끝날 때까지 기다린다.
2. Console에 에러가 없는지 확인한다.
3. 씬에 빈 GameObject를 만들고 이름을 `FlowerBud_Test`로 바꾼 뒤, `SphereCollider`(반경 약 1)를 추가하고 `FlowerBud` 컴포넌트를 붙인다.
4. Inspector에 "개화 조건"/"개화 결과물"/"개화 연출 설정"/"런타임 상태" 그룹과 "강제 개화 테스트" 버튼이 정상적으로 표시되는지 확인한다.
5. `개화 시 생성할 아이템 프리팹`에 임시로 아무 프리팹(예: `Assets/Prefabs/Puzzle/WoodenBox.prefab` — 실제 경로는 프로젝트에서 확인)을 연결한다.
6. Play Mode에 들어가 "강제 개화 테스트" 버튼을 눌러, `FlowerBud_Test`가 살짝 커지는 연출과 함께 오브젝트 위치에 아이템이 생성되는지 확인한다.
7. Play Mode를 종료하고 Console에 Tween 관련 에러가 없는지 확인한다.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Puzzle/FlowerBud.cs
git commit -m "$(cat <<'EOF'
FlowerBud 생성 - 꽃가루를 모아 개화하면 아이템을 만들어내는 퍼즐 오브젝트

선풍기로 조준한 Pollen을 흡수해 목표 개수를 채우면 DOTween 개화 연출과
함께 WoodenBox 등 아이템을 생성한다. Pollen/PollenSpawner/WoodenBox/
PressButton/DoorController는 전혀 수정하지 않는다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: 씬 조립 — FlowerBud + PressButton x2 + DoorController 체인 연결

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (씬 배치만, 스크립트 수정 없음)

**Interfaces:**
- Consumes: Task 1의 `FlowerBud` 컴포넌트, 기존 `PollenSpawner`(스폰 범위 확인용), 기존 `WoodenBox`/`PressButton`/`DoorController` 프리팹·컴포넌트
- Produces: 없음 (이 서브프로젝트의 마지막 태스크)

- [ ] **Step 1: `FlowerBud`를 꽃가루 스폰 범위 밖에 배치한다.**

1. 씬에서 사용할(또는 새로 만들) `PollenSpawner`를 찾아 Inspector의 `spawnHalfWidth`(기본 5) 값을 확인한다.
2. `FlowerBud_Test` 오브젝트(또는 새로 만든 `FlowerBud`)를 `PollenSpawner` 위치의 X축 기준 `spawnHalfWidth`보다 바깥쪽(예: spawner.x + spawnHalfWidth + 1.5 이상)으로 옮긴다. 자연 낙하로는 꽃가루가 닿지 않고, 선풍기로 조준해야만 날아오게 하기 위함이다.
3. 오브젝트 이름을 `FlowerBud`로 정리한다.

- [ ] **Step 2: 개화 시 생성할 `WoodenBox` 프리팹을 정식으로 연결한다.**

1. `FlowerBud`의 `개화 시 생성할 아이템 프리팹`에 프로젝트의 실제 `WoodenBox` 프리팹을 연결한다.
2. `아이템 생성 위치 오프셋`을 조정해, 개화 시 상자가 땅 위에 자연스럽게 놓이도록 한다(예: `(0, 0.5, 0)` 근처 — 실제 지형 높이에 맞게 Scene 뷰에서 확인하며 조정).
3. 생성될 `WoodenBox` 인스턴스의 `복원력(restoreForce)`이 너무 강하면 상자를 버튼까지 밀고 가기 어려우므로, 프리팹 기본값이 이 방 크기에 비해 너무 강한지 Scene 뷰 배치를 보며 판단하고 필요하면 낮춘다.

- [ ] **Step 3: 기존 `PressButton` 2개와 `DoorController`를 배치한다.**

1. 기존에 사용 중인 `PressButton` 프리팹(또는 씬 내 구성)을 2개 배치해, `WoodenBox`가 도달할 수 있는 위치에 나란히 둔다.
2. `DoorController`가 붙은 문 오브젝트를 배치하고, Inspector의 `버튼 목록`에 방금 배치한 `PressButton` 2개를 모두 연결한다.
3. 기존 B/D 서브프로젝트에서 이미 검증된 조합이므로 `PressButton`/`DoorController` 자체는 수정하지 않는다.

- [ ] **Step 4: Play Mode에서 전체 체인을 확인한다.**

1. Console에 컴파일/배치 관련 에러가 없는지 확인한다.
2. Play Mode에 들어가 `PollenSpawner`가 꽃가루를 우수수 떨어뜨리는 동안, 선풍기로 조준해 `FlowerBud` 쪽으로 날려 보낸다.
3. `FlowerBud`의 `현재 흡수한 꽃가루 수`(Odin ReadOnly)가 꽃가루가 흡수될 때마다 올라가는지 확인한다.
4. 목표 개수(기본 5)에 도달하면 개화 연출과 함께 `WoodenBox`가 정상 위치에 생성되는지 확인한다.
5. 개화 후 추가로 꽃가루가 `FlowerBud`에 닿아도 더 이상 반응하지 않는지(`_isBloomed`) 확인한다.
6. 생성된 `WoodenBox`를 선풍기로 밀어 `PressButton` 2개를 동시에 누르고, `DoorController`로 문이 열리는지 확인한다.
7. Play Mode를 종료하고 Console에 에러가 없는지 확인한다.
8. 전체 회귀 체크리스트를 확인한다:
   - 기존 플레이어 이동/점프/선풍기 바람(WindZone)/우산 글라이드 기능이 모두 정상 작동
   - 기존 꽃가루(Pollen)가 플레이어에 닿았을 때 봄 게이지가 오르고 우산으로 차단되는 기존 동작이 그대로 유지
   - 기존 물 차오름(WaterArea)/뜨는 상자(FloatingBox) 기능이 모두 정상 작동
   - Console에 에러 없음

- [ ] **Step 5: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "$(cat <<'EOF'
봄 구역 개화 퍼즐(FlowerBud) 씬 조립 - 꽃가루 모으기부터 문 열림까지 체인 연결

FlowerBud를 PollenSpawner 범위 밖에 배치하고, 개화로 생성되는 WoodenBox를
기존 PressButton 2개 + DoorController와 연결해 전체 퍼즐 체인을 완성한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
