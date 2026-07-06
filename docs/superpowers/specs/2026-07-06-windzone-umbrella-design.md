# WindZone + 우산 이동 시스템 설계

- 작성일: 2026-07-06
- 서브프로젝트: A (전체 "선풍기/우산 퍼즐" 요청 중 첫 번째)
- 이후 예정: B(계절 낙하물 차단) → C(선풍기로 구름 이동) → D(비 → 물 차오름 + 뜨는 상자)

## 배경

플레이어 요청: 바람이 부는 구역이 있고, 우산을 공중에서 펼치면 바람 방향으로 밀려나며, 위로 부는 바람 구역에서는 우산으로 위로 이동할 수 있어야 한다. 바람은 지속풍/간헐풍 두 종류가 있고 바람소리가 나야 하며, 바람이 불 때 시각 효과(파티클)와 에디터에서 미리 확인할 수 있는 기즈모가 필요하다.

`PlayerController.cs`에 이미 `SetWindZone(Vector3)` / `ClearWindZone()` 메서드와 `_windVelocity` 필드가 존재하고, `IBlowable.cs` 주석에도 "WindZone에 반응하는 오브젝트"라는 언급이 있다. 즉 예전에 이 기능을 염두에 두고 만든 훅이 이미 있으나 실제로 이를 호출하는 `WindZone` 컴포넌트는 아직 없다. 이번 설계는 이 기존 훅을 재사용해서 완성하는 것을 목표로 한다.

## 범위

**포함**
- `WindZone` 환경 오브젝트 (지속풍/간헐풍, 방향, 세기)
- 우산이 펼쳐진 상태에서 WindZone에 들어갔을 때의 플레이어 이동(수평 밀림, 수직 상승)
- 바람소리, 바람 파티클 이펙트
- 에디터 기즈모 (존 범위 + 바람 방향 시각화)

**제외 (다음 서브프로젝트에서 다룸)**
- 나뭇잎/꽃가루/구름 등 `IBlowable` 오브젝트가 WindZone에 밀리는 것 (B, C)
- 비/눈/은행잎을 우산·선풍기로 막는 로직 (B)
- 물 차오름 구역, 뜨는 상자 (D) — 단, 기즈모 표현 방식은 이번 설계의 컨벤션을 그대로 따르기로 함

## 컴포넌트 구성

### 신규: `Assets/Scripts/Puzzle/WindZone.cs`

기존 `IceBlock`/`FlammableObject`와 같은 폴더에 배치한다. 이 폴더의 파일들은 전부 "도구(횃불/선풍기/우산)에 반응하는 환경 기믹 오브젝트"라는 같은 역할을 하기 때문이다.

```
public enum WindMode { Constant, Intermittent }

[SerializeField] private Vector3 windDirection;      // 바람이 부는 방향 (정규화해서 사용)
[SerializeField] private float windStrength;          // 바람 세기
[SerializeField] private WindMode windMode;           // 지속풍 / 간헐풍
[SerializeField] private float onDuration;            // 간헐풍: 바람 부는 시간
[SerializeField] private float offDuration;           // 간헐풍: 바람 멈추는 시간
[SerializeField] private float fadeDuration;          // On/Off 전환 시 세기·볼륨 페이드 시간
[SerializeField] private AudioClip windSound;
[SerializeField] private AudioSource audioSource;     // 3D 스페이셜, 루프
[SerializeField] private ParticleSystem windEffect;   // 비워두면 이펙트 없이 동작

[ReadOnly] 현재 활성 여부
[ReadOnly] 존 안에 플레이어가 있는지
[Button] 강제 On/Off 테스트
```

### 수정: `PlayerController.cs`

`SetWindZone`/`ClearWindZone`는 그대로 재사용한다. `ApplyFallSpeedLimit()` 호출 다음 줄에 아래를 추가한다.

```csharp
if (_windVelocity.y != 0f) _verticalVelocity = _windVelocity.y;
```

이유: 현재 `Update()`는 `horizontal = _moveVelocity + _recoilVelocity + _blastVelocity + _windVelocity` 를 계산한 뒤 `finalVelocity`에는 `horizontal.x`/`horizontal.z`만 사용하고 Y는 별도의 `_verticalVelocity`에서만 가져온다. 그 결과 `_windVelocity`에 위쪽 성분을 넣어도 무시되어 "위로 부는 바람"이 동작하지 않는다. 기존 필드를 그대로 재사용해 변경 범위를 최소화한다.

### 수정 없음: `UmbrellaTool.cs`, `FanTool.cs`, `IBlowable.cs`

`UmbrellaTool.IsOpen`이 이미 public이므로 `WindZone`이 직접 참조한다. 이번 서브프로젝트는 플레이어-우산 상호작용만 다룬다.

## 동작 규칙

- **수직 성분이 우세한 존** (`Mathf.Abs(windDirection.y) > Mathf.Abs(windDirection.x)`): 접지 여부와 무관하게, 우산이 펼쳐진 순간부터 즉시 작동한다. 지상에서 우산을 펼치면 그 자리에서 떠오를 수 있다.
- **수평 성분이 우세한 존**: `player.IsGrounded`이면 무시하고, 공중에서 우산을 펼쳤을 때만 작동한다. (지상에서 우산을 펼쳤을 때도 끌려갈지에 대해 논의했으며, 공중에서만 반응하는 쪽을 기본값으로 채택했다. 이유: `PlayerController`의 지상 이동 로직을 그대로 유지할 수 있고 조작감이 예측 가능하다.)
- 존마다 방향이 고정되어 있고, 우산 이동은 "현재 있는 존의 방향"을 그대로 따른다. 하나의 존 안에서 방향이 실시간으로 바뀌는 기능(오실레이션)은 이번 범위에 포함하지 않는다.
- 우산을 접거나 존을 벗어나면 그 프레임에 바로 `ClearWindZone()`이 호출된다.

## 데이터 흐름

```
Player Enter Trigger
  → WindZone이 PlayerController / UmbrellaTool 참조 캐싱

매 프레임 (플레이어가 존 안에 있는 동안):
  isActive(간헐풍 On/Off 타이머로 결정)
  && umbrella.IsOpen
  && (수직 우세 존 || !player.IsGrounded)
    → true : player.SetWindZone(windDirection.normalized * windStrength)
    → false: player.ClearWindZone()

Player Exit Trigger
  → player.ClearWindZone(), 오디오 정지, 참조 해제
```

간헐풍 On/Off 전환 시 `windStrength`(0→1 배율)와 오디오 볼륨을 `fadeDuration` 동안 DOTween으로 함께 보간한다. `windEffect`는 활성화될 때 `Play()`, 비활성화될 때 `Stop()`.

## 예외 처리

- `OnDisable()`에서 `player.ClearWindZone()`을 방어적으로 호출해, 오브젝트가 비활성화되는 동안 바람이 낀 채로 남는 것을 방지한다.
- 겹치는 존이 여러 개일 경우 그 프레임에 마지막으로 갱신한 존이 우선 적용된다. 레벨 디자인 단계에서 존을 겹치지 않게 배치하는 것을 전제로 하며, 이번 범위에서 별도의 우선순위 시스템은 만들지 않는다.
- 간헐풍이 Off로 전환되는 순간에도 `ClearWindZone()`을 호출한다. 상승/이동 중 갑자기 멈추는 느낌이 있을 수 있으나, `fadeDuration` 페이드로 어느 정도 완화된다.
- 모든 DOTween 트윈(세기/볼륨 페이드)은 `OnDestroy()`에서 `Kill()` 처리한다.

## 에디터 기즈모

`BaseHazard.OnDrawGizmos()`가 스폰 범위를 씬 뷰에 그려주는 기존 관례(`#if UNITY_EDITOR` + `Gizmos.DrawLine`/`DrawWireSphere`)를 그대로 따른다.

- 트리거 콜라이더 범위를 와이어박스로 표시
- 존 중심에서 `windDirection` 방향으로 화살표(선 + 화살촉) 표시
- 지속풍은 파란색 계열, 간헐풍은 다른 색(예: 하늘색)으로 구분

**다음 서브프로젝트(D, 물 차오름 구역)도 이 컨벤션을 따른다**: 트리거 범위 + 최대 수위를 반투명 와이어박스/평면으로 미리 표시. 이번 스펙에서 D 시스템 자체를 구현하지는 않는다.

## Odin Inspector 적용

`[Title]`로 "바람 설정"/"간헐풍 설정"/"연출 설정" 구역을 나누고, 방향·세기·모드·On/Off 지속시간·페이드 시간을 `[LabelText]`로 한글 표시한다. 런타임 상태(활성 여부, 플레이어 존재 여부)는 `[ReadOnly, ShowInInspector]`로 노출하고, 강제 On/Off 토글용 `[Button]`을 추가한다.

## DOTween 적용

- 간헐풍 On/Off 전환 시 `windStrength`와 오디오 볼륨 페이드
- 반복/중간에 끊길 수 있는 트윈이므로 변수에 저장 후 `OnDestroy()`에서 `Kill()`

## Unity 테스트 방법

- 수평풍 존: 공중에서 우산을 펼쳤을 때만 밀리고 지상에서는 무반응인지 확인
- 수직풍 존: 지상에서 우산을 펼치자마자 떠오르는지 확인
- 우산을 접는 순간 바람 영향이 즉시 사라지는지 확인
- 간헐풍 On/Off 주기, 바람소리 페이드, 바람 파티클 On/Off가 서로 맞물려 동작하는지 확인
- 씬 뷰에서 기즈모(범위/방향 화살표)가 실제 트리거 범위·방향과 일치하는지 확인
- 기존 우산 글라이드(공중 낙하속도 제한), 선풍기 반동/블라스트가 여전히 정상 작동하는지 회귀 확인
- Console 에러 없는지, Play Mode 종료 시 Tween 관련 에러 없는지 확인
