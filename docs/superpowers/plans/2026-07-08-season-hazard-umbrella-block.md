# 계절 낙하물 우산 차단 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 우산을 펼친 플레이어가 빗방울/눈송이/꽃가루/은행잎(착지 전)과 부딪히면 계절 게이지 증가 없이 축소되며 사라지게 한다.

**Architecture:** `BaseHazard`에 우산 상태를 확인해 낙하물을 무효화하는 공통 메서드 `TryBlockByUmbrella(PlayerController)`를 추가하고, `RainDrop`/`SnowFlake`/`Pollen`/`LeafDrop`의 `OnTriggerEnter`에서 기존 `AddGauge()` 호출 전에 이 메서드를 먼저 확인하도록 한 줄씩 추가한다. `LeafDrop`만 착지 전(`!_isLanded`)일 때만 차단이 적용되도록 예외를 둔다.

**Tech Stack:** Unity 6 (6000.0.68f1), C#, DOTween Pro, Odin Inspector.

## Global Constraints

- 신규 파일은 만들지 않는다. 기존 `BaseHazard.cs`, `RainDrop.cs`, `SnowFlake.cs`, `Pollen.cs`, `LeafDrop.cs` 5개 파일만 수정한다.
- `FanTool.cs`, `RainController.cs`, `SnowController.cs`, `UmbrellaTool.cs`는 수정하지 않는다. 선풍기로 개별 빗방울/눈송이를 날리는 기능은 이번 범위에 포함하지 않는다(구름을 미는 기존 기능으로 충분하다고 합의함).
- 이미 바닥에 착지한 은행잎(`LeafDrop._isLanded == true`)은 우산으로 차단하지 않는다. 착지 후에는 기존처럼 밟으면 게이지가 오르고 터진다.
- 이 프로젝트에는 자동화 테스트 프레임워크가 구성되어 있지 않다. 각 태스크의 검증은 기존 관례(Unity Editor Play Mode 수동 확인)를 따른다. "테스트 작성 → 실패 확인 → 구현 → 통과 확인" 사이클 대신 "구현 → Play Mode에서 수동 검증"으로 진행한다.
- DOTween을 쓰는 곳은 트윈을 변수에 저장하고 `OnDestroy()`에서 `Kill()`한다.
- Odin `[SerializeField]`에는 한글 `[LabelText]`를 붙이고, 새 코드의 주요 필드/메서드에는 한국어 주석(왜 필요한지 위주)을 단다.
- `Pollen.cs`/`LeafDrop.cs`의 기존 `private void OnDestroy()`는 `protected override void OnDestroy()`로 바꾸고 반드시 `base.OnDestroy()`를 호출한다(그래야 `BaseHazard`의 `_blockTween` 정리와 기존 `_blowTween` 정리가 둘 다 된다).

---

## 파일 구조

- **Modify:** `Assets/Scripts/Season/BaseHazard.cs` — 우산 차단 공통 로직(`TryBlockByUmbrella`), 차단 시 축소 트윈, `OnDestroy()` 가상 메서드 추가
- **Modify:** `Assets/Scripts/Season/RainDrop.cs` — `OnTriggerEnter`에서 우산 차단 확인 후 게이지 증가
- **Modify:** `Assets/Scripts/Season/SnowFlake.cs` — 위와 동일한 패턴
- **Modify:** `Assets/Scripts/Season/Pollen.cs` — 위와 동일한 패턴 + `OnDestroy()` override
- **Modify:** `Assets/Scripts/Season/LeafDrop.cs` — 착지 전 예외 처리 포함한 패턴 + `OnDestroy()` override

참고 프리팹(Play Mode 테스트용): `Assets/Prefabs/Season/RainDrop.prefab`, `SnowFlake.prefab`, `Pollen.prefab`, `LeafDrop.prefab`

---

### Task 1: BaseHazard 공통 차단 로직 추가 + RainDrop 연동

**Files:**
- Modify: `Assets/Scripts/Season/BaseHazard.cs`
- Modify: `Assets/Scripts/Season/RainDrop.cs:37-44`

**Interfaces:**
- Consumes: `UmbrellaTool.IsOpen`(기존 public), `PlayerController`(타입만 사용)
- Produces (이후 태스크가 사용):
  - `protected bool TryBlockByUmbrella(PlayerController player)` — 우산이 펼쳐져 있으면 축소 트윈 후 `Destroy`하고 `true` 반환, 아니면 `false` 반환
  - `protected virtual void OnDestroy()` — 하위 클래스가 오버라이드할 수 있는 가상 메서드

- [ ] **Step 1: `Assets/Scripts/Season/BaseHazard.cs` 전체를 아래 내용으로 교체한다.**

```csharp
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 모든 계절 낙하물(꽃가루, 비, 은행, 눈)의 공통 기반 클래스다.
// 계절 타입, 게이지 칸 수, 스폰 범위를 공유한다.
// Spawner도 이 클래스를 상속해 스폰 범위 로직만 재사용한다.
public abstract class BaseHazard : MonoBehaviour
{
    [Title("시즌 게이지 설정")]
    [SerializeField, LabelText("계절 타입")]
    protected SeasonType seasonType;
    // Inspector에서 이 낙하물이 어느 계절에 속하는지 지정한다.

    [SerializeField, LabelText("게이지 칸 수")]
    protected int gaugeSlots = 1;
    // 낙하물이 플레이어에게 닿았을 때 증가시킬 게이지 칸 수다.

    [Title("스폰 범위 설정")]
    [SerializeField, LabelText("스폰 범위 절반 너비")]
    protected float spawnHalfWidth = 5f;
    // 스폰 범위의 절반 너비다. 이 오브젝트 위치를 중심으로 ±spawnHalfWidth 범위 안에서 생성된다.

    [SerializeField, LabelText("파괴 Y 좌표")]
    protected float destroyBelowY = -20f;
    // 이 Y 좌표 아래로 내려가면 자동으로 파괴된다.
    // 낭떠러지 아래로 떨어진 낙하물이 씬에 무한히 쌓이는 것을 방지한다.

    [Title("우산 차단 설정")]
    [SerializeField, LabelText("차단 시 축소 시간")]
    private float blockedShrinkDuration = 0.2f;
    // 우산에 막혔을 때 낙하물이 줄어들며 사라지는 데 걸리는 시간이다.

    private Tween _blockTween;

    protected virtual void Update()
    {
        if (transform.position.y < destroyBelowY)
            Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        _blockTween?.Kill();
    }

    // 이 오브젝트의 위치를 중심으로 ±spawnHalfWidth 범위 내 랜덤 위치를 반환한다.
    // Y와 Z는 자신의 위치를 그대로 사용한다 — 사이드뷰라 Z는 고정 평면, Y는 원하는 스폰 높이가 된다.
    protected Vector3 GetSpawnPosition()
    {
        float randomX = Random.Range(
            transform.position.x - spawnHalfWidth,
            transform.position.x + spawnHalfWidth);
        return new Vector3(randomX, transform.position.y, transform.position.z);
    }

    // SeasonGaugeManager에 gaugeSlots 칸만큼 게이지를 추가한다.
    // 낙하물이 플레이어에게 닿았을 때 하위 클래스에서 호출한다.
    protected void AddGauge()
    {
        SeasonGaugeManager.AddGauge(seasonType, gaugeSlots);
    }

    // 우산이 펼쳐진 플레이어와 부딪히면 게이지 증가 없이 축소되며 사라진다.
    // 반환값이 true이면 호출부에서 게이지 증가 로직을 건너뛰어야 한다.
    protected bool TryBlockByUmbrella(PlayerController player)
    {
        // WindZoneVolume이 우산을 찾는 방식과 동일하게, 자식에서 못 찾으면 씬 전체에서 하나 찾는다.
        UmbrellaTool umbrella = player.GetComponentInChildren<UmbrellaTool>();
        if (umbrella == null)
            umbrella = FindFirstObjectByType<UmbrellaTool>();

        if (umbrella == null || !umbrella.IsOpen)
            return false;

        _blockTween = transform.DOScale(Vector3.zero, blockedShrinkDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(gameObject));

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 씬 뷰에서 스폰 범위를 선으로 시각화한다.
        Gizmos.color = Color.cyan;
        Vector3 left  = transform.position + Vector3.left  * spawnHalfWidth;
        Vector3 right = transform.position + Vector3.right * spawnHalfWidth;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left,  0.15f);
        Gizmos.DrawWireSphere(right, 0.15f);
    }
#endif
}
```

- [ ] **Step 2: `Assets/Scripts/Season/RainDrop.cs`의 `OnTriggerEnter`를 아래와 같이 수정한다.**

수정 전 (`Assets/Scripts/Season/RainDrop.cs:37-44`):

```csharp
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
            Destroy(gameObject);
        }
    }
```

수정 후:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (TryBlockByUmbrella(player)) return; // 우산에 막힘: 게이지 증가 없이 사라짐

        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
        Destroy(gameObject);
    }
```

- [ ] **Step 3: Unity Editor에서 확인한다.**

1. Console에 컴파일 에러가 없는지 확인한다.
2. `Assets/Prefabs/Season/RainDrop.prefab`을 씬의 플레이어 근처(살짝 위)에 드래그해서 배치한다.
3. Play Mode에 들어가 우산을 펼치지 않은 채 빗방울과 부딪혀서, 기존처럼 여름 게이지가 오르고 빗방울이 사라지는지 확인한다.
4. 다시 빗방울을 배치하고, 이번엔 우산을 펼친 채 부딪혀서 게이지가 오르지 않고 빗방울이 톡 줄어들며 사라지는지 확인한다.
5. Play Mode를 종료하고 Console에 Tween 관련 에러가 없는지 확인한다.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Season/BaseHazard.cs Assets/Scripts/Season/RainDrop.cs
git commit -m "$(cat <<'EOF'
BaseHazard에 우산 차단 공통 로직 추가 - RainDrop 연동

우산이 펼쳐진 플레이어와 부딪히면 게이지 증가 없이 축소되며 사라지는
TryBlockByUmbrella()를 BaseHazard에 추가하고 RainDrop에 먼저 적용한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: SnowFlake 연동

**Files:**
- Modify: `Assets/Scripts/Season/SnowFlake.cs:53-60`

**Interfaces:**
- Consumes: `BaseHazard.TryBlockByUmbrella(PlayerController)` (Task 1에서 생성)
- Produces: 없음

- [ ] **Step 1: `Assets/Scripts/Season/SnowFlake.cs`의 `OnTriggerEnter`를 아래와 같이 수정한다.**

수정 전 (`Assets/Scripts/Season/SnowFlake.cs:53-60`):

```csharp
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
            Destroy(gameObject);
        }
    }
```

수정 후:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (TryBlockByUmbrella(player)) return; // 우산에 막힘: 게이지 증가 없이 사라짐

        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
        Destroy(gameObject);
    }
```

- [ ] **Step 2: Unity Editor에서 확인한다.**

1. Console에 컴파일 에러가 없는지 확인한다.
2. `Assets/Prefabs/Season/SnowFlake.prefab`을 플레이어 근처에 배치한다.
3. Play Mode에서 우산 없이 부딪혀 겨울 게이지가 정상적으로 오르는지 확인한다.
4. 다시 배치 후 우산을 펼친 채 부딪혀 게이지가 오르지 않고 축소되며 사라지는지 확인한다.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Season/SnowFlake.cs
git commit -m "$(cat <<'EOF'
SnowFlake에 우산 차단 로직 연동

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Pollen 연동

**Files:**
- Modify: `Assets/Scripts/Season/Pollen.cs:82-85` (`OnDestroy`)
- Modify: `Assets/Scripts/Season/Pollen.cs:121-128` (`OnTriggerEnter`)

**Interfaces:**
- Consumes: `BaseHazard.TryBlockByUmbrella(PlayerController)`, `BaseHazard.OnDestroy()`(가상 메서드, Task 1에서 생성)
- Produces: 없음

- [ ] **Step 1: `Assets/Scripts/Season/Pollen.cs`의 `OnDestroy()`를 아래와 같이 수정한다.**

수정 전 (`Assets/Scripts/Season/Pollen.cs:82-85`):

```csharp
    private void OnDestroy()
    {
        _blowTween?.Kill();
    }
```

수정 후:

```csharp
    protected override void OnDestroy()
    {
        base.OnDestroy();
        _blowTween?.Kill();
    }
```

- [ ] **Step 2: `Assets/Scripts/Season/Pollen.cs`의 `OnTriggerEnter`를 아래와 같이 수정한다.**

수정 전 (`Assets/Scripts/Season/Pollen.cs:120-128`):

```csharp
    // 플레이어와 접촉 시 봄 게이지를 올리기 위해 트리거 콜백을 사용한다.
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
            Destroy(gameObject);
        }
    }
```

수정 후:

```csharp
    // 플레이어와 접촉 시 봄 게이지를 올리기 위해 트리거 콜백을 사용한다.
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (TryBlockByUmbrella(player)) return; // 우산에 막힘: 게이지 증가 없이 사라짐

        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
        Destroy(gameObject);
    }
```

- [ ] **Step 3: Unity Editor에서 확인한다.**

1. Console에 컴파일 에러가 없는지 확인한다.
2. `Assets/Prefabs/Season/Pollen.prefab`을 플레이어 근처에 배치한다.
3. Play Mode에서 우산 없이 부딪혀 봄 게이지가 정상적으로 오르는지 확인한다.
4. 다시 배치 후 우산을 펼친 채 부딪혀 게이지가 오르지 않고 축소되며 사라지는지 확인한다.
5. 기존 "선풍기로 꽃가루 날리기" 기능(`TestBlow` 버튼 또는 실제 `FanTool` 사용)이 여전히 정상 작동하는지 회귀 확인한다.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Season/Pollen.cs
git commit -m "$(cat <<'EOF'
Pollen에 우산 차단 로직 연동

기존 OnDestroy()를 BaseHazard의 가상 메서드를 오버라이드하도록 바꿔
_blowTween과 _blockTween이 모두 정리되게 한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: LeafDrop 연동 (착지 전 예외 처리) + 최종 회귀 확인

**Files:**
- Modify: `Assets/Scripts/Season/LeafDrop.cs:99-102` (`OnDestroy`)
- Modify: `Assets/Scripts/Season/LeafDrop.cs:152-163` (`OnTriggerEnter`)

**Interfaces:**
- Consumes: `BaseHazard.TryBlockByUmbrella(PlayerController)`, `BaseHazard.OnDestroy()`(Task 1에서 생성), 기존 `LeafDrop._isLanded` 필드
- Produces: 없음 (이 서브프로젝트의 마지막 태스크)

- [ ] **Step 1: `Assets/Scripts/Season/LeafDrop.cs`의 `OnDestroy()`를 아래와 같이 수정한다.**

수정 전 (`Assets/Scripts/Season/LeafDrop.cs:99-102`):

```csharp
    private void OnDestroy()
    {
        _blowTween?.Kill();
    }
```

수정 후:

```csharp
    protected override void OnDestroy()
    {
        base.OnDestroy();
        _blowTween?.Kill();
    }
```

- [ ] **Step 2: `Assets/Scripts/Season/LeafDrop.cs`의 `OnTriggerEnter`를 아래와 같이 수정한다.**

수정 전 (`Assets/Scripts/Season/LeafDrop.cs:152-163`):

```csharp
    // 플레이어와 접촉하면 즉시 가을 게이지를 올리고, 그 자리에 냄새를 남긴 채 터진다.
    private void OnTriggerEnter(Collider other)
    {
        if (_hasBurst) return;

        if (other.GetComponent<PlayerController>() != null)
        {
            _hasBurst = true;
            AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다. 접촉 즉시 1회 오른다.
            Burst();
        }
    }
```

수정 후:

```csharp
    // 플레이어와 접촉하면 즉시 가을 게이지를 올리고, 그 자리에 냄새를 남긴 채 터진다.
    // 단, 착지 전(공중에서 떨어지는 중)에 우산을 펼친 플레이어와 부딪히면 우산에 막혀 사라진다.
    // 이미 바닥에 착지한 은행잎은 우산으로 막을 수 없다 — 밟으면 기존처럼 그대로 터진다.
    private void OnTriggerEnter(Collider other)
    {
        if (_hasBurst) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (!_isLanded && TryBlockByUmbrella(player)) return; // 공중에서만 우산 차단 적용

        _hasBurst = true;
        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다. 접촉 즉시 1회 오른다.
        Burst();
    }
```

- [ ] **Step 3: Unity Editor에서 최종 통합 확인을 한다.**

1. Console에 컴파일 에러가 없는지 확인한다.
2. `Assets/Prefabs/Season/LeafDrop.prefab`을 플레이어 근처 공중에 배치한다.
3. Play Mode에서 우산을 펼친 채 공중에서 떨어지는 은행잎과 부딪혀 게이지 증가 없이 축소되며 사라지는지 확인한다.
4. 은행잎을 다시 배치하고 착지(바닥에 멈춰 가만히 있는 상태)할 때까지 기다린 뒤, 우산을 펼친 채로 밟아서 기존처럼 게이지가 오르고 냄새(`LeafScent`)를 남기며 터지는지 확인한다 — 착지 후에는 우산 차단이 적용되면 안 된다.
5. 아래 전체 회귀 체크리스트를 확인한다:
   - 비/눈/꽃가루/은행잎(착지 전) 모두: 우산 펼침 시 게이지 증가 없이 축소되며 사라짐
   - 비/눈/꽃가루/은행잎 모두: 우산 접힘 시 기존처럼 게이지가 오르고 사라짐
   - 착지한 은행잎: 우산 여부와 무관하게 기존처럼 밟으면 터짐
   - 기존 "선풍기로 구름 밀기"(`RainController`/`SnowController`), "선풍기로 꽃가루/은행잎(착지 전) 날리기" 기능이 이번 변경 후에도 정상 작동
   - 기존 우산 글라이드(공중 낙하 속도 제한) 정상 작동
   - Console에 에러 없음, Play Mode 종료 시 `_blockTween`/`_blowTween` 관련 에러 없음

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Season/LeafDrop.cs
git commit -m "$(cat <<'EOF'
LeafDrop에 우산 차단 로직 연동 - 착지 전만 적용

착지 후(_isLanded)에는 우산 차단을 적용하지 않아 바닥에 쌓인 은행잎은
기존처럼 밟으면 게이지가 오르고 터지도록 유지한다. 계절 낙하물 우산
차단(서브프로젝트 B) 구현을 마무리한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
