# 물 차오름 + 뜨는 상자 설계 (서브프로젝트 D)

날짜: 2026-07-09
관련 로드맵: 선풍기/우산 퍼즐 시스템 D단계 (A: WindZone, B: 계절 낙하물 우산 차단에 이어지는 마지막 서브프로젝트)

## 배경

- 이 프로젝트는 3D 사이드뷰(2.5D) 기준으로 확정되었다 — 3D Mesh를 그대로 쓰되 카메라만 옆에서 본다. 이동 평면은 X(좌우)+Y(상하)이며 Z축은 고정이다.
- `RainDrop.cs`에는 현재 아래와 같은 주석이 있다: "이 프로젝트에는 아직 수위(WaterArea) 시스템이 없어서, 바닥 충돌 대신 BaseHazard의 Y 좌표 아래로 내려가면 자동 파괴 로직으로 정리된다." 이 서브프로젝트가 바로 그 수위 시스템을 만드는 작업이다.
- `PlayerController`는 `CharacterController` 기반으로 이동하며, 물리적으로 자동으로 발판에 실리지 않는다. A 서브프로젝트(WindZone)에서 이미 `SetWindZone`/`ClearWindZone` 훅으로 같은 문제(외부 힘을 어떻게 CharacterController에 반영할지)를 해결한 전례가 있다.

## 목표

1. 특정 구역(`WaterArea`)에 빗방울이 떨어져 **현재 물 표면**에 닿으면 물이 조금씩 차오른다.
2. 물은 최대 높이까지만 차오르며, 일정 시간 동안 빗방울이 닿지 않으면 서서히 원래 수위로 빠진다.
3. 물 위에 뜨는 상자(`FloatingBox`)가 있고, 플레이어가 그 위에 올라타면 물이 차오르는 만큼 함께 위로 실려 올라간다.

## 아키텍처

### 1. `WaterArea.cs` (신규, `Assets/Scripts/Puzzle/WaterArea.cs`)

물 수위를 관리하는 트리거 존이다. `WoodenBox.cs`, `WindZoneVolume.cs`와 같은 폴더에 둔다.

- `[SerializeField] float risePerDrop` — 빗방울 하나당 수위 상승량
- `[SerializeField] float maxRiseHeight` — 기준 수위 대비 최대로 오를 수 있는 높이
- `[SerializeField] float drainDelay` — 마지막으로 물이 찬 뒤 이 시간(초)이 지나면 빠지기 시작
- `[SerializeField] float drainSpeed` — 빠지는 속도 (유닛/초)
- `_baseY` (Awake에서 저장) — 물의 최소 높이(기준 수위)
- `_currentOffset` (ReadOnly, Odin) — 기준 수위로부터 현재 오른 높이
- `_lastFilledTime` — 마지막으로 `OnRainDropHit()`이 호출된 시각. `RainController`/`SnowController`의 `_lastBlownTime` 쿨다운 패턴과 동일하게 사용한다.

동작:
- `Update()`에서 `Time.time - _lastFilledTime > drainDelay`이면 `_currentOffset`을 `Mathf.MoveTowards`로 0을 향해 서서히 감소시킨다.
- 매 프레임 트리거 콜라이더(BoxCollider)의 윗면 위치를 `_baseY + _currentOffset`에 맞춰 갱신한다. 이렇게 하면 콜라이더의 "윗면"이 곧 현재 물 표면이 되고, 빗방울은 물리적으로 그 표면에 닿아야만 반응한다 (땅이 아니라 물 표면에 닿아야 차오른다는 요구사항을 그대로 구현).
- `public void OnRainDropHit()`: `_currentOffset = Mathf.Min(_currentOffset + risePerDrop, maxRiseHeight)`, `_lastFilledTime = Time.time`.
- `public float CurrentWaterY => _baseY + _currentOffset` — `FloatingBox`가 참조한다.
- 에디터 기즈모: A 서브프로젝트(WindZoneVolume)에서 만든 컨벤션을 따른다 — 전체 범위를 와이어박스로, 현재 수위선을 별도 색으로 표시한다.

### 2. `RainDrop.cs` 수정

`OnTriggerEnter`에 `WaterArea` 감지 분기를 추가한다.

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

    if (TryBlockByUmbrella(player)) return;
    AddGauge();
    Destroy(gameObject);
}
```

"아직 수위 시스템이 없어서..." 주석은 삭제하고, `WaterArea`와 연동한다는 내용으로 교체한다. `destroyBelowY`(BaseHazard의 자동 파괴 로직)는 물 구역 밖으로 벗어난 빗방울을 위해 그대로 유지한다.

### 3. `FloatingBox.cs` (신규, `Assets/Scripts/Puzzle/FloatingBox.cs`)

- `[SerializeField] WaterArea waterArea` — 어떤 물 구역을 따라갈지 Inspector에서 연결
- `Rigidbody`(Kinematic) + `BoxCollider`(Solid, 트리거 아님 — 플레이어가 물리적으로 밟고 설 수 있어야 하므로)
- `FixedUpdate()`에서 `rb.MovePosition`으로 Y 좌표를 `waterArea.CurrentWaterY`로 이동시킨다. 물이 빠질 때는 상자도 같이 내려간다 (수위를 그대로 따라가는 것이 기본 동작).
- `public float CurrentRiseSpeed { get; private set; }` — 이번 프레임에 상자가 움직인 Y 방향 속도(유닛/초). 매 `FixedUpdate`에서 `(newY - oldY) / Time.fixedDeltaTime`로 계산한다. `PlayerController`가 이 값을 읽어간다.
- DOTween: 상자가 물에 밀려 올라올 때 살짝 출렁이는 바운스 연출(`transform.DOPunchPosition` 등)을 옵션으로 추가한다. Duration/Ease는 Odin으로 노출한다.

### 4. `PlayerController.cs`에 플랫폼 탑승 훅 추가

CharacterController는 물리적으로 자동으로 발판에 실리지 않으므로, A 서브프로젝트와 동일한 패턴(트리거 진입/이탈 + Setter 메서드)을 재사용한다.

- 새 훅: `public void SetPlatformVelocity(Vector3 velocity)` / `public void ClearPlatformVelocity()` — 기존 `SetWindZone`/`ClearWindZone`과 동일한 구조.
- `FloatingBox`는 콜라이더 두 개를 갖는다: 플레이어가 물리적으로 밟고 설 수 있는 Solid `BoxCollider` 하나, 그리고 그 바로 위쪽에 얇게 겹치는 트리거 전용 `BoxCollider` 하나 (Unity는 한 GameObject에 여러 Collider 컴포넌트를 둘 수 있다). 플레이어가 이 트리거에 들어오면 `SetPlatformVelocity(new Vector3(0, CurrentRiseSpeed, 0))`을 매 프레임 갱신 호출하고, 나가면 `ClearPlatformVelocity()`를 호출한다.
- `PlayerController.Update()`에서 `_windVelocity`와 마찬가지로 `_platformVelocity.y`가 0이 아니면 `_verticalVelocity`에 직접 반영한다 (기존 `ApplyWindVertical()`과 같은 방식의 `ApplyPlatformVertical()` 추가).

## 기존 기능 영향

- `RainDrop.cs`: `OnTriggerEnter`에 분기 하나 추가. 기존 플레이어/우산 로직은 그대로 유지되어 영향 없음.
- `PlayerController.cs`: 새 필드/메서드만 추가 (`_platformVelocity`, `SetPlatformVelocity`, `ClearPlatformVelocity`, `ApplyPlatformVertical`). 기존 이동/바람/반동 로직은 건드리지 않음.
- 완전히 새로운 오브젝트(`WaterArea`, `FloatingBox`)라 씬에 배치하지 않으면 기존 동작에 영향 없음.

## 범위 밖 (Out of scope)

- 플레이어가 물에 빠지거나 수영하는 기능 — 이번 서브프로젝트는 "상자를 발판으로 태우는 것"까지만 다룬다.
- 여러 개의 `WaterArea`/`FloatingBox`가 서로 상호작용하는 케이스 — 1:1 연결만 가정한다.

## 테스트 방법 (Unity Editor)

- Play Mode에서 비가 내리는 동안 `WaterArea` 위로 빗방울이 떨어지는지, 수위가 서서히 오르는지 Odin ReadOnly 값(`_currentOffset`)으로 확인
- 최대 높이(`maxRiseHeight`)에서 멈추는지 확인
- 비가 그친 뒤 `drainDelay` 이후 수위가 서서히 빠지는지 확인
- `FloatingBox`가 수위를 따라 오르내리는지 확인
- 플레이어가 상자 위에 올라탔을 때 함께 위로 올라가는지, 내릴 때 정상적으로 걸어 내려올 수 있는지 확인
- Console에 Error 없는지 확인
