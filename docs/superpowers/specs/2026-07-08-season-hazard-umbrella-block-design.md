# 계절 낙하물 우산 차단 설계

- 작성일: 2026-07-08
- 서브프로젝트: B (전체 "선풍기/우산 퍼즐" 요청 중 두 번째)
- 이전: A(WindZone + 우산 이동, 완료) / 이후 예정: C(선풍기로 구름 이동) → D(비 → 물 차오름 + 뜨는 상자)

## 배경

기존 계절 낙하물(꽃가루/비/은행잎/눈)은 플레이어와 부딪히면 무조건 계절 게이지가 오르고 사라진다. `UmbrellaTool.cs`의 클래스 주석에는 "땅에서 사용하면 비를 막고"라고 적혀 있지만, 실제로 낙하물을 막는 코드는 없다(공중 낙하 속도 제한만 구현됨). 이번 서브프로젝트는 이 미구현 갭을 채워, 우산을 펼친 상태에서 낙하물과 부딪히면 게이지 증가 없이 무효화되게 한다.

선풍기(FanTool)로 낙하물을 날려보내는 기능은 `RainController`/`SnowController`(비/눈구름)와 `Pollen`/`LeafDrop`(착지 전)에 이미 `IBlowable`로 구현되어 있으며, 이번 작업에서는 변경하지 않는다.

## 범위

**포함**
- 우산을 펼친 상태에서 꽃가루/빗방울/은행잎(착지 전)/눈송이와 부딪혔을 때: 게이지 증가 없이 무효화 + 축소·소멸 시각 효과
- `BaseHazard`에 공통 차단 로직 추가, 4개 하위 클래스(`Pollen`, `RainDrop`, `SnowFlake`, `LeafDrop`)에 최소 반영

**제외**
- 선풍기로 개별 빗방울/눈송이를 직접 날려보내는 기능 (구름을 미는 기존 기능으로 충분하다고 판단해 이번 범위에서 제외)
- 이미 바닥에 착지한 은행잎을 우산으로 차단하는 것 (착지 후에는 밟으면 기존처럼 게이지가 오르고 터진다)
- `FanTool.cs`, `RainController.cs`, `SnowController.cs`, `UmbrellaTool.cs` 자체의 로직 변경 (전부 기존 그대로 사용)

## 컴포넌트 구성

### 수정: `Assets/Scripts/Season/BaseHazard.cs`

```csharp
[Title("우산 차단 설정")]
[SerializeField, LabelText("차단 시 축소 시간")]
private float blockedShrinkDuration = 0.2f;
// 우산에 막혔을 때 낙하물이 줄어들며 사라지는 데 걸리는 시간이다.

private Tween _blockTween;

protected virtual void OnDestroy()
{
    _blockTween?.Kill();
}

// 우산이 펼쳐진 플레이어와 부딪히면 게이지 증가 없이 축소되며 사라진다.
// 반환값이 true이면 호출부에서 게이지 증가 로직을 건너뛰어야 한다.
protected bool TryBlockByUmbrella(PlayerController player)
{
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
```

`Update()`가 이미 `protected virtual`로 선언되어 있는 기존 패턴과 동일하게 `OnDestroy()`도 가상 메서드로 추가한다. `Pollen`/`LeafDrop`은 이미 자체 `OnDestroy()`(`_blowTween?.Kill()`)를 갖고 있으므로 `protected override void OnDestroy()`로 바꾸고 `base.OnDestroy()`를 호출해 두 트윈이 모두 정리되게 한다.

### 수정: `Assets/Scripts/Season/RainDrop.cs`, `SnowFlake.cs`

```csharp
private void OnTriggerEnter(Collider other)
{
    PlayerController player = other.GetComponent<PlayerController>();
    if (player == null) return;

    if (TryBlockByUmbrella(player)) return; // 우산에 막힘: 게이지 증가 없이 사라짐

    AddGauge();
    Destroy(gameObject);
}
```

### 수정: `Assets/Scripts/Season/Pollen.cs`

`OnTriggerEnter`에 동일한 패턴을 적용한다. `OnDestroy()`를 `protected override`로 바꾸고 `base.OnDestroy()` 호출을 추가한다.

### 수정: `Assets/Scripts/Season/LeafDrop.cs`

착지 전(`!_isLanded`)일 때만 차단이 적용되도록 예외를 둔다. 착지 후에는 기존처럼 밟으면 게이지가 오르고 터진다.

```csharp
private void OnTriggerEnter(Collider other)
{
    if (_hasBurst) return;

    PlayerController player = other.GetComponent<PlayerController>();
    if (player == null) return;

    if (!_isLanded && TryBlockByUmbrella(player)) return; // 공중에서만 우산 차단 적용

    _hasBurst = true;
    AddGauge();
    Burst();
}
```

`OnDestroy()`도 `protected override`로 바꾸고 `base.OnDestroy()`를 호출한다.

## 동작 규칙

- 우산 차단 여부는 `UmbrellaTool.IsOpen` 하나로만 판단한다. `WindZoneVolume.OnTriggerEnter`가 우산을 찾는 방식(자식에서 탐색 → 없으면 씬 전체에서 탐색)을 그대로 재사용해 일관성을 유지한다.
- 차단되면 게이지는 전혀 오르지 않고, `blockedShrinkDuration` 동안 `Ease.InBack`으로 줄어들며 사라진다. 기존 "선풍기에 날려서 사라지는" 페이드(색상 투명화 + 서서히 축소)와는 다른, 더 짧고 톡 튕기는 듯한 연출로 구분한다.
- 착지 후 상태가 있는 낙하물은 `LeafDrop`뿐이며, 착지 후에는 우산 차단이 적용되지 않는다(바닥에 이미 쌓인 낙엽을 우산으로 없앨 수는 없다는 자연스러운 제약).

## 데이터 흐름

```
낙하물이 Player와 Trigger 충돌
  → PlayerController 참조 확인 (없으면 무시)
  → (LeafDrop만) 착지 전인지 확인
  → TryBlockByUmbrella(player)
       → player의 UmbrellaTool 탐색, IsOpen 확인
       → 우산 펼쳐짐: DOTween으로 축소 후 Destroy, true 반환
       → 우산 없음/접힘: false 반환
  → true 반환 시: 여기서 종료 (게이지 변화 없음)
  → false 반환 시: 기존처럼 AddGauge() 후 Destroy (또는 LeafDrop은 Burst())
```

## 예외 처리

- `TryBlockByUmbrella` 안에서 우산을 찾지 못하거나(플레이어에게 우산 도구가 없는 씬 등) 우산이 닫혀 있으면 항상 `false`를 반환해, 기존 게이지 증가 동작이 그대로 유지된다.
- `_blockTween`은 `OnDestroy()`에서 `Kill()` 처리해 오브젝트가 예기치 않게 먼저 파괴되어도(예: 씬 전환) 에러가 나지 않는다.
- `Pollen`/`LeafDrop`의 기존 `_blowTween`과 신규 `_blockTween`은 서로 다른 상황(선풍기로 날림 vs 우산 차단)에서만 각각 하나씩 실행되므로 동시에 실행될 일이 없다.

## Odin Inspector 적용

`BaseHazard`의 "우산 차단 설정" `[Title]` 아래 `blockedShrinkDuration`을 `[SerializeField, LabelText]`로 노출해 기획 단계에서 축소 속도를 조절할 수 있게 한다.

## DOTween 적용

- 우산 차단 시 `transform.DOScale`로 축소 후 `OnComplete`에서 `Destroy` — 반복되지 않는 1회성 트윈이지만 정리를 위해 `_blockTween` 변수에 저장하고 `OnDestroy()`에서 `Kill()` 처리한다.

## Unity 테스트 방법

- 우산을 펼친 채 빗방울/눈송이/공중의 은행잎/꽃가루와 부딪혔을 때 게이지가 오르지 않고 축소되며 사라지는지 확인
- 우산을 접은 상태에서는 기존처럼 게이지가 정상적으로 오르는지 확인
- 이미 바닥에 착지한 은행잎은 우산을 펼치고 밟아도 기존처럼 게이지가 오르는지 확인 (착지 후 예외 동작 확인)
- 선풍기로 비/눈구름을 미는 기존 기능, 꽃가루/은행잎(착지 전)을 날려보내는 기존 기능이 이번 변경 후에도 그대로 작동하는지 회귀 확인
- Console에 에러가 없는지, Play Mode 종료 시 Tween(`_blockTween`, `_blowTween`) 관련 에러가 없는지 확인
