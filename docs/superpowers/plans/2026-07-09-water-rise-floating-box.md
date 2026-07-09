# 물 차오름 + 뜨는 상자 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 물 구역(`WaterArea`)에 빗방울이 현재 물 표면에 닿으면 수위가 서서히 차오르고, 일정 시간 비가 닿지 않으면 서서히 빠지며, 물 위에 뜨는 상자(`FloatingBox`)가 수위를 따라 오르내리며 플레이어를 태울 수 있게 한다.

**Architecture:** `WaterArea`는 트리거 콜라이더의 윗면 높이를 현재 수위에 맞춰 매 프레임 갱신해 "빗방울이 땅이 아니라 현재 물 표면에 닿아야 차오른다"는 규칙을 물리적으로 구현한다. `RainDrop`은 이 존에 닿으면 `OnRainDropHit()`을 호출한다. `FloatingBox`는 `WaterArea.CurrentWaterY`를 `Rigidbody.MovePosition`으로 따라가고, `PlayerController`에 새로 추가하는 `SetPlatformVelocity`/`ClearPlatformVelocity` 훅(A 서브프로젝트의 `SetWindZone`/`ClearWindZone`과 동일한 패턴)으로 CharacterController 기반 플레이어를 실어 나른다.

**Tech Stack:** Unity 6 (6000.0.68f1), C#, DOTween Pro, Odin Inspector, New Input System, CharacterController.

## Global Constraints

- 게임 시점은 3D 사이드뷰(2.5D)다 — 이동 평면은 X(좌우)+Y(상하), Z축은 고정한다. (CLAUDE.md 기준)
- 이 프로젝트에는 자동화 테스트 프레임워크가 구성되어 있지 않다. 각 태스크의 검증은 "구현 → Unity Editor Play Mode에서 수동 검증" 사이클로 진행한다.
- DOTween을 쓰는 곳은 트윈을 변수에 저장하고 오브젝트 파괴 시 `Kill()`한다.
- Odin `[SerializeField]`에는 한글 `[LabelText]`를 붙이고, 새 코드의 주요 필드/메서드에는 한국어 주석(왜 필요한지 위주)을 단다.
- `PlayerController`의 기존 이동/바람/반동 로직은 건드리지 않는다. 새 필드/메서드만 추가한다.
- 플레이어가 물에 빠지거나 수영하는 기능, 여러 `WaterArea`/`FloatingBox`가 서로 상호작용하는 케이스는 이번 범위에 포함하지 않는다.

---

## 파일 구조

- **Create:** `Assets/Scripts/Puzzle/WaterArea.cs` — 수위 상승/배수 로직, 콜라이더 윗면 높이 갱신, 에디터 기즈모
- **Modify:** `Assets/Scripts/Season/RainDrop.cs` — `OnTriggerEnter`에서 `WaterArea` 감지 분기 추가
- **Modify:** `Assets/Scripts/Controllers/Player/PlayerController.cs` — `SetPlatformVelocity`/`ClearPlatformVelocity`/`ApplyPlatformVertical` 추가
- **Create:** `Assets/Scripts/Puzzle/FloatingBox.cs` — 수위를 따라가는 발판, 플레이어 탑승 감지, DOTween 출렁임 연출

참고: 이 프로젝트에는 `WaterArea`/`FloatingBox` 전용 프리팹이 아직 없다. 각 태스크의 Unity Editor 검증은 현재 열려 있는 씬에 테스트용 GameObject를 직접 만들어 확인한다 (필요하면 마지막에 프리팹으로 저장해도 되지만 이번 플랜의 필수 범위는 아니다).

---

### Task 1: WaterArea 생성 (수위 상승/배수 + 콜라이더 갱신)

**Files:**
- Create: `Assets/Scripts/Puzzle/WaterArea.cs`

**Interfaces:**
- Consumes: 없음 (신규 독립 컴포넌트)
- Produces (이후 태스크가 사용):
  - `public float CurrentWaterY { get; }` — 현재 물 표면의 월드 Y 좌표
  - `public void OnRainDropHit()` — 빗방울이 물 표면에 닿았을 때 호출, 수위를 `risePerDrop`만큼 올림

- [ ] **Step 1: `Assets/Scripts/Puzzle/WaterArea.cs`를 아래 내용으로 생성한다.**

```csharp
using Sirenix.OdinInspector;
using UnityEngine;

// 특정 구역에 비가 내려 빗방울이 "현재 물 표면"에 닿으면 수위가 서서히 차오르는 물 웅덩이다.
// 일정 시간 동안 빗방울이 닿지 않으면 서서히 원래 수위로 빠진다.
// 콜라이더는 트리거이며, 윗면 위치를 현재 수위에 맞춰 매 프레임 갱신한다 —
// 그래야 빗방울이 바닥이 아니라 실제 물 표면에 닿았을 때만 반응한다.
[RequireComponent(typeof(BoxCollider))]
public class WaterArea : MonoBehaviour
{
    [Title("수위 설정")]
    [SerializeField, LabelText("빗방울 1개당 상승량")]
    private float risePerDrop = 0.05f;
    // 값이 클수록 빗방울 하나에 물이 더 많이 차오른다.

    [SerializeField, LabelText("최대 상승 높이")]
    private float maxRiseHeight = 4f;
    // 기준 수위(콜라이더 원래 윗면)로부터 최대로 오를 수 있는 높이다.

    [Title("배수 설정")]
    [SerializeField, LabelText("배수 시작까지 대기 시간(초)")]
    private float drainDelay = 3f;
    // 마지막으로 물이 찬 뒤 이 시간이 지나면 서서히 빠지기 시작한다.

    [SerializeField, LabelText("배수 속도 (유닛/초)")]
    private float drainSpeed = 0.5f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 오른 높이")]
    private float _currentOffset;

    private BoxCollider _collider;
    private float _baseColliderHeight;
    private float _baseColliderCenterY;
    private float _baseSurfaceY;
    private float _lastFilledTime = -999f;

    // FloatingBox가 참조하는 현재 물 표면의 월드 Y 좌표다.
    public float CurrentWaterY => _baseSurfaceY + _currentOffset;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _collider.isTrigger = true;
        // 물은 물리 충돌이 필요 없는 감지 전용 트리거로 둔다 (RainController의 구름 콜라이더와 동일한 방식).

        _baseColliderHeight = _collider.size.y;
        _baseColliderCenterY = _collider.center.y;
        // 콜라이더 윗면의 원래 월드 Y 좌표를 기준 수위로 저장한다. 수위는 여기서부터 오른다.
        _baseSurfaceY = transform.position.y + _baseColliderCenterY + (_baseColliderHeight * 0.5f);
    }

    private void Update()
    {
        UpdateDrain();
        UpdateColliderHeight();
    }

    private void UpdateDrain()
    {
        bool canDrain = Time.time - _lastFilledTime > drainDelay;
        if (canDrain)
            _currentOffset = Mathf.MoveTowards(_currentOffset, 0f, drainSpeed * Time.deltaTime);
    }

    // 콜라이더 바닥면은 고정한 채 윗면만 _currentOffset만큼 올라가도록 크기와 중심을 함께 조정한다.
    private void UpdateColliderHeight()
    {
        float height = _baseColliderHeight + _currentOffset;
        Vector3 size = _collider.size;
        size.y = height;
        _collider.size = size;

        Vector3 center = _collider.center;
        center.y = _baseColliderCenterY + (_currentOffset * 0.5f);
        _collider.center = center;
    }

    // RainDrop이 이 물 표면에 닿았을 때 호출한다.
    public void OnRainDropHit()
    {
        _currentOffset = Mathf.Min(_currentOffset + risePerDrop, maxRiseHeight);
        _lastFilledTime = Time.time;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        // 최대로 차오를 수 있는 전체 범위를 옅은 파란색 와이어박스로 표시한다.
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        Vector3 maxCenter = transform.position + Vector3.up * (maxRiseHeight * 0.5f);
        Vector3 maxSize = new Vector3(col.size.x, maxRiseHeight, col.size.z);
        Gizmos.DrawWireCube(maxCenter, maxSize);

        // 현재 수위선은 진한 파란색 평면으로 표시한다.
        Gizmos.color = new Color(0f, 0.3f, 1f);
        float currentY = Application.isPlaying
            ? CurrentWaterY
            : transform.position.y + col.center.y + (col.size.y * 0.5f);
        Vector3 lineCenter = new Vector3(transform.position.x, currentY, transform.position.z);
        Vector3 lineSize = new Vector3(col.size.x, 0.05f, col.size.z);
        Gizmos.DrawCube(lineCenter, lineSize);
    }
#endif
}
```

- [ ] **Step 2: Unity Editor에서 컴파일을 확인한다.**

1. Unity Editor로 돌아가 컴파일이 끝날 때까지 기다린다.
2. Console에 에러가 없는지 확인한다.
3. 씬에 빈 GameObject를 만들고 `BoxCollider`를 추가한 뒤 `WaterArea` 컴포넌트를 붙인다. Inspector에 "수위 설정"/"배수 설정"/"런타임 상태" 그룹이 정상적으로 표시되는지 확인한다.
4. Scene 뷰에서 옅은 파란 와이어박스(최대 범위)와 진한 파란 평면(현재 수위선)이 보이는지 확인한다.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Puzzle/WaterArea.cs
git commit -m "$(cat <<'EOF'
WaterArea 생성 - 빗방울로 차오르고 서서히 빠지는 물 웅덩이

콜라이더 윗면 높이를 현재 수위에 맞춰 매 프레임 갱신해, 빗방울이
바닥이 아니라 실제 물 표면에 닿았을 때만 반응하도록 한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: RainDrop에 WaterArea 연동

**Files:**
- Modify: `Assets/Scripts/Season/RainDrop.cs:1-47`

**Interfaces:**
- Consumes: `WaterArea.OnRainDropHit()` (Task 1에서 생성)
- Produces: 없음

- [ ] **Step 1: `Assets/Scripts/Season/RainDrop.cs` 상단 주석을 수정한다.**

수정 전:

```csharp
// 여름 계절 낙하물인 빗방울이다. 대각선으로 떨어지며 Player에 닿으면 여름 게이지를 올린다.
// 이 프로젝트에는 아직 수위(WaterArea) 시스템이 없어서, 바닥 충돌 대신 BaseHazard의
// "Y 좌표 아래로 내려가면 자동 파괴" 로직으로 정리된다.
```

수정 후:

```csharp
// 여름 계절 낙하물인 빗방울이다. 대각선으로 떨어지며 Player에 닿으면 여름 게이지를 올린다.
// WaterArea(물 웅덩이) 구역의 현재 수위에 닿으면 그 수위를 올리고 사라진다.
// WaterArea가 없는 곳에 떨어진 빗방울은 기존처럼 BaseHazard의
// "Y 좌표 아래로 내려가면 자동 파괴" 로직으로 정리된다.
```

- [ ] **Step 2: `OnTriggerEnter`를 아래와 같이 수정한다.**

수정 전 (`Assets/Scripts/Season/RainDrop.cs:37-46`):

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

수정 후:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        WaterArea waterArea = other.GetComponent<WaterArea>();
        if (waterArea != null)
        {
            waterArea.OnRainDropHit();
            Destroy(gameObject);
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (TryBlockByUmbrella(player)) return; // 우산에 막힘: 게이지 증가 없이 사라짐

        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
        Destroy(gameObject);
    }
```

- [ ] **Step 3: Unity Editor에서 확인한다.**

1. Console에 컴파일 에러가 없는지 확인한다.
2. Task 1에서 만든 `WaterArea` 테스트 오브젝트를 씬에 두고, 그 바로 위에 `Assets/Prefabs/Season/RainDrop.prefab`을 배치한다.
3. Play Mode에 들어가 빗방울이 떨어져 `WaterArea`에 닿으면 Inspector의 "현재 오른 높이"(`_currentOffset`) 값이 올라가는지, 빗방울이 사라지는지 확인한다.
4. `WaterArea` 밖(예: 플레이어 쪽)에 빗방울을 배치해, 기존처럼 플레이어에 닿으면 여름 게이지가 오르는지, 우산을 펼치면 차단되는지 회귀 확인한다.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Season/RainDrop.cs
git commit -m "$(cat <<'EOF'
RainDrop에 WaterArea 연동

빗방울이 WaterArea의 현재 물 표면에 닿으면 수위를 올리고 사라지게 한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: PlayerController에 발판 탑승 훅 추가

**Files:**
- Modify: `Assets/Scripts/Controllers/Player/PlayerController.cs`

**Interfaces:**
- Consumes: 없음
- Produces (이후 태스크가 사용):
  - `public void SetPlatformVelocity(Vector3 velocity)` — 발판 위에 있는 동안 매 프레임 호출, `velocity.y`를 수직 속도에 반영
  - `public void ClearPlatformVelocity()` — 발판에서 벗어났을 때 호출, 발판 속도를 초기화

- [ ] **Step 1: `_windVelocity` 필드 바로 아래에 `_platformVelocity` 필드를 추가한다.**

수정 전 (`Assets/Scripts/Controllers/Player/PlayerController.cs:47-52`):

```csharp
    // 이동 속도 성분들 (선풍기 반동/바람과 분리해 각각 감속 처리한다)
    private Vector3 _moveVelocity;
    private Vector3 _recoilVelocity;
    private Vector3 _blastVelocity;
    private Vector3 _windVelocity;
    private float _verticalVelocity;
```

수정 후:

```csharp
    // 이동 속도 성분들 (선풍기 반동/바람과 분리해 각각 감속 처리한다)
    private Vector3 _moveVelocity;
    private Vector3 _recoilVelocity;
    private Vector3 _blastVelocity;
    private Vector3 _windVelocity;
    private Vector3 _platformVelocity;
    // FloatingBox 위에 올라탔을 때 그 발판의 상승/하강 속도를 담는다 (Y 성분만 사용).
    private float _verticalVelocity;
```

- [ ] **Step 2: `Update()`에서 `ApplyWindVertical()` 호출 다음 줄에 `ApplyPlatformVertical()` 호출을 추가한다.**

수정 전 (`Assets/Scripts/Controllers/Player/PlayerController.cs:113-118`):

```csharp
        HandleMove();
        HandleFacing();
        ApplyGravity();
        ApplyFallSpeedLimit();
        ApplyWindVertical();
        DecayRecoil();
```

수정 후:

```csharp
        HandleMove();
        HandleFacing();
        ApplyGravity();
        ApplyFallSpeedLimit();
        ApplyWindVertical();
        ApplyPlatformVertical();
        DecayRecoil();
```

- [ ] **Step 3: `ClearWindZone()` 메서드 바로 아래에 새 훅 메서드 두 개를 추가한다.**

수정 전 (`Assets/Scripts/Controllers/Player/PlayerController.cs:216-219`):

```csharp
    // WindZone 이탈 시 바람 속도를 초기화한다.
    public void ClearWindZone()
    {
        _windVelocity = Vector3.zero;
    }
```

수정 후:

```csharp
    // WindZone 이탈 시 바람 속도를 초기화한다.
    public void ClearWindZone()
    {
        _windVelocity = Vector3.zero;
    }

    // FloatingBox 위에 올라탔을 때 그 발판의 상승/하강 속도를 설정한다.
    public void SetPlatformVelocity(Vector3 velocity)
    {
        _platformVelocity = velocity;
    }

    // FloatingBox에서 벗어났을 때 발판 속도를 초기화한다.
    public void ClearPlatformVelocity()
    {
        _platformVelocity = Vector3.zero;
    }
```

- [ ] **Step 4: `ApplyWindVertical()` 메서드 바로 아래에 `ApplyPlatformVertical()`을 추가한다.**

수정 전 (`Assets/Scripts/Controllers/Player/PlayerController.cs:268-276`):

```csharp
    private void ApplyWindVertical()
    {
        // WindZone의 바람 방향에 위쪽 성분이 있으면 수직 속도에 직접 반영한다.
        // _windVelocity는 horizontal 합산에도 쓰이지만(X/Z), Y 성분은 그쪽에서 버려지므로
        // 여기서 별도로 _verticalVelocity에 덮어써야 "위로 부는 바람"이 실제로 작동한다.
        if (_windVelocity.y != 0f)
            _verticalVelocity = _windVelocity.y;
    }
}
```

수정 후:

```csharp
    private void ApplyWindVertical()
    {
        // WindZone의 바람 방향에 위쪽 성분이 있으면 수직 속도에 직접 반영한다.
        // _windVelocity는 horizontal 합산에도 쓰이지만(X/Z), Y 성분은 그쪽에서 버려지므로
        // 여기서 별도로 _verticalVelocity에 덮어써야 "위로 부는 바람"이 실제로 작동한다.
        if (_windVelocity.y != 0f)
            _verticalVelocity = _windVelocity.y;
    }

    private void ApplyPlatformVertical()
    {
        // 뜨는 상자 위에 있는 동안은 상자의 상승/하강 속도를 그대로 수직 속도에 반영해
        // 플레이어가 상자와 함께 오르내리게 한다.
        if (_platformVelocity.y != 0f)
            _verticalVelocity = _platformVelocity.y;
    }
}
```

- [ ] **Step 5: Unity Editor에서 확인한다.**

1. Console에 컴파일 에러가 없는지 확인한다.
2. Play Mode에서 기존 이동/점프/바람(WindZone) 기능이 평소처럼 작동하는지 회귀 확인한다 (아직 `FloatingBox`가 없으므로 새 훅은 호출되는 곳이 없어 동작 변화는 없어야 한다).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Controllers/Player/PlayerController.cs
git commit -m "$(cat <<'EOF'
PlayerController에 발판 탑승 훅 추가

FloatingBox가 사용할 SetPlatformVelocity/ClearPlatformVelocity를
기존 SetWindZone/ClearWindZone과 동일한 패턴으로 추가한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: FloatingBox 생성 (수위 추종 + 플레이어 탑승 + 출렁임 연출)

**Files:**
- Create: `Assets/Scripts/Puzzle/FloatingBox.cs`

**Interfaces:**
- Consumes:
  - `WaterArea.CurrentWaterY` (Task 1에서 생성)
  - `PlayerController.SetPlatformVelocity(Vector3)` / `PlayerController.ClearPlatformVelocity()` (Task 3에서 생성)
- Produces: 없음 (이 서브프로젝트의 마지막 태스크)

- [ ] **Step 1: `Assets/Scripts/Puzzle/FloatingBox.cs`를 아래 내용으로 생성한다.**

```csharp
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 물 위에 떠서 수위를 따라 오르내리는 발판이다.
// 플레이어가 위에 올라타면 PlayerController의 SetPlatformVelocity 훅을 통해
// 상자와 함께 위아래로 실려 이동하게 한다.
[RequireComponent(typeof(Rigidbody))]
public class FloatingBox : MonoBehaviour
{
    [Title("연결 참조")]
    [SerializeField, LabelText("따라갈 물 웅덩이")]
    private WaterArea waterArea;
    // Inspector에서 이 상자가 수위를 따라갈 WaterArea를 연결한다.

    [SerializeField, LabelText("탑승 감지 콜라이더")]
    private BoxCollider rideTrigger;
    // 이 GameObject에 발판용 BoxCollider(Is Trigger 체크 안 함)와 별도로,
    // 발판 바로 위쪽에 얇게 겹치는 BoxCollider(Is Trigger 체크)를 하나 더 추가해 연결한다.
    // 플레이어가 이 트리거 안에 있는 동안 상자에 탑승한 것으로 간주한다.

    [Title("연출 설정")]
    [SerializeField, LabelText("차오를 때 출렁임 세기")]
    private float bouncePunch = 0.1f;

    [SerializeField, LabelText("출렁임 시간")]
    private float bounceDuration = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상승 속도")]
    private float _currentRiseSpeed;

    private Rigidbody _rb;
    private PlayerController _riderInTrigger;
    private Tween _bounceTween;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        // 물리 힘이 아니라 수위를 직접 따라가는 발판이라 Kinematic으로 설정하고 MovePosition으로 움직인다.

        if (rideTrigger != null)
            rideTrigger.isTrigger = true;
    }

    private void OnDestroy()
    {
        _bounceTween?.Kill();
    }

    private void FixedUpdate()
    {
        if (waterArea == null) return;

        float targetY = waterArea.CurrentWaterY;
        Vector3 pos = _rb.position;
        float previousY = pos.y;

        pos.y = targetY;
        _rb.MovePosition(pos);

        // 이번 FixedUpdate에서 실제로 움직인 거리를 속도(유닛/초)로 환산해 저장한다.
        // 플레이어가 탑승 중이면 이 값을 그대로 수직 속도로 넘겨받는다.
        _currentRiseSpeed = (targetY - previousY) / Time.fixedDeltaTime;

        if (targetY > previousY + 0.001f)
            PlayRiseBounce();

        if (_riderInTrigger != null)
            _riderInTrigger.SetPlatformVelocity(new Vector3(0f, _currentRiseSpeed, 0f));
    }

    // 물이 차올라 상자가 올라갈 때 살짝 출렁이는 느낌을 주는 연출이다.
    private void PlayRiseBounce()
    {
        if (_bounceTween != null && _bounceTween.IsActive()) return;
        _bounceTween = transform.DOPunchPosition(Vector3.up * bouncePunch, bounceDuration, 4, 0.5f);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _riderInTrigger = player;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null || player != _riderInTrigger) return;

        _riderInTrigger.ClearPlatformVelocity();
        _riderInTrigger = null;
    }
}
```

- [ ] **Step 2: Unity Editor에서 씬에 테스트용 FloatingBox를 만든다.**

1. 3D Cube 오브젝트를 하나 만들고 이름을 `FloatingBox`로 바꾼다.
2. 기존 BoxCollider(Is Trigger 체크 안 함 — 발판으로 밟고 설 수 있어야 한다)를 그대로 두고, `Add Component`로 `BoxCollider`를 하나 더 추가한다. 이 두 번째 BoxCollider는 발판 윗면보다 살짝 위쪽에 얇게 겹치도록 `Center`/`Size`를 조정하고 `Is Trigger`를 체크한다.
3. `FloatingBox` 컴포넌트를 붙이고, Inspector에서 `따라갈 물 웅덩이`에 Task 1에서 만든 `WaterArea` 오브젝트를, `탑승 감지 콜라이더`에 방금 추가한 두 번째 BoxCollider를 연결한다.

- [ ] **Step 3: Unity Editor에서 확인한다.**

1. Console에 컴파일 에러가 없는지 확인한다.
2. Play Mode에 들어가 `RainDrop`을 `WaterArea` 위로 여러 번 떨어뜨려 수위가 오르는 동안 `FloatingBox`가 함께 위로 올라가는지, 살짝 출렁이는 연출이 보이는지 확인한다.
3. 플레이어를 `FloatingBox` 위로 이동시킨 뒤 수위가 계속 오르면, 플레이어가 상자와 함께 위로 실려 올라가는지 확인한다.
4. 플레이어가 상자에서 내려왔을 때 정상적으로 걸어 다닐 수 있는지(발판 속도가 남아있지 않은지) 확인한다.
5. 비가 그치고 `drainDelay` 시간이 지나 수위가 빠질 때, 상자도 함께 서서히 내려가는지 확인한다.
6. Play Mode를 종료하고 Console에 Tween 관련 에러가 없는지 확인한다.
7. 전체 회귀 체크리스트를 확인한다:
   - 기존 플레이어 이동/점프/선풍기 바람(WindZone)/우산 글라이드 기능이 모두 정상 작동
   - 기존 "선풍기로 구름 밀기"(RainController/SnowController), 계절 낙하물 우산 차단(B) 기능이 모두 정상 작동
   - Console에 에러 없음

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Puzzle/FloatingBox.cs
git commit -m "$(cat <<'EOF'
FloatingBox 생성 - 수위를 따라 오르내리며 플레이어를 태우는 발판

WaterArea의 수위를 MovePosition으로 따라가고, 플레이어가 위에 있으면
PlayerController의 SetPlatformVelocity 훅으로 함께 실어 올린다.
물 차오름 + 뜨는 상자(서브프로젝트 D) 구현을 마무리한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
