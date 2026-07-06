# WindZone + 우산 이동 시스템 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 우산을 펼친 플레이어가 바람 구역(WindZone)에 들어가면 방향에 따라 밀리거나 위로 떠오르게 하고, 지속풍/간헐풍/바람소리/파티클/에디터 기즈모까지 갖춘 환경 기믹을 완성한다.

**Architecture:** 신규 `WindZone` MonoBehaviour(트리거 콜라이더)가 `PlayerController`에 이미 존재하는 `SetWindZone`/`ClearWindZone` 훅을 호출한다. `PlayerController`는 수직 바람을 반영하도록 한 줄만 수정한다. 힘 적용 여부는 "간헐풍 On/Off 타이머" × "우산이 펼쳐져 있는지" × "수직풍이면 항상, 수평풍이면 공중에서만"의 조합으로 매 프레임 판정한다.

**Tech Stack:** Unity 6 (6000.0.68f1), C#, DOTween Pro, Odin Inspector, CharacterController 기반 `PlayerController`.

## Global Constraints

- 신규 스크립트는 `Assets/Scripts/Puzzle/WindZone.cs` 하나만 만든다 (기존 `IceBlock`/`FlammableObject`와 같은 폴더 관례).
- `PlayerController.cs` 외의 기존 스크립트(`UmbrellaTool.cs`, `FanTool.cs`, `IBlowable.cs`)는 수정하지 않는다.
- 기존 `PlayerController.SetWindZone(Vector3)` / `ClearWindZone()` / `_windVelocity` 필드 이름을 그대로 재사용한다. 이름을 바꾸지 않는다.
- 이 프로젝트에는 자동화 테스트 프레임워크(Unity Test Runner용 테스트 스위트)가 구성되어 있지 않다. 각 태스크의 검증은 이 프로젝트의 기존 관례(Odin `[Button]` 테스트 메서드 + Unity Editor Play Mode 수동 확인)를 따른다. "테스트 작성 → 실패 확인 → 구현 → 통과 확인" 사이클 대신 "구현 → Play Mode에서 수동 검증"으로 진행한다.
- DOTween을 쓰는 곳은 트윈을 변수에 저장하고 `OnDestroy()`에서 `Kill()`한다.
- Odin `[SerializeField]`에는 한글 `[LabelText]`를 붙이고, 새 코드의 주요 필드/메서드에는 한국어 주석(왜 필요한지 위주)을 단다.

---

## 파일 구조

- **Create:** `Assets/Scripts/Puzzle/WindZone.cs` — 바람 구역 전체 로직(트리거 감지, 간헐풍 타이머, 힘 적용 판정, 사운드, 파티클, 기즈모)
- **Modify:** `Assets/Scripts/Controllers/Player/PlayerController.cs` — `ApplyFallSpeedLimit()` 호출 다음에 수직 바람 반영 한 줄 추가

---

### Task 1: WindZone 골격 — 필드, 트리거 감지, 기즈모

**Files:**
- Create: `Assets/Scripts/Puzzle/WindZone.cs`

**Interfaces:**
- Consumes: `PlayerController` (컴포넌트 존재 여부만 확인, `GetComponent<PlayerController>()`)
- Produces (이후 태스크가 이 파일 안에서 사용할 이름들):
  - `private Vector3 windDirection`, `private float windStrength`
  - `private enum WindMode { Constant, Intermittent }`, `private WindMode windMode`
  - `private PlayerController _playerInZone`
  - `private bool IsVerticalDominant` (프로퍼티)

- [ ] **Step 1: `Assets/Scripts/Puzzle/WindZone.cs` 파일을 아래 내용으로 생성한다.**

```csharp
using Sirenix.OdinInspector;
using UnityEngine;

// 우산을 펼친 플레이어를 밀거나 띄우는 바람 구역이다.
// 수직 성분이 우세한 방향이면 접지 여부와 무관하게, 수평 성분이 우세하면
// 공중에서만 작동한다 (구체적인 힘 적용 판정은 다음 태스크에서 추가한다).
[RequireComponent(typeof(Collider))]
public class WindZone : MonoBehaviour
{
    public enum WindMode { Constant, Intermittent }

    [Title("바람 설정")]
    [SerializeField, LabelText("바람 방향")]
    private Vector3 windDirection = Vector3.right;
    // Inspector에서 바람이 부는 방향을 지정한다. 정규화해서 사용하므로 크기는 상관없다.
    // 예: (1,0,0)은 오른쪽, (0,1,0)은 위쪽 — 위쪽 방향이면 접지 중에도 작동하는 수직풍이 된다.

    [SerializeField, LabelText("바람 세기")]
    private float windStrength = 6f;
    // 값이 클수록 플레이어가 더 강하게 밀리거나 빠르게 떠오른다.

    [Title("간헐풍 설정")]
    [SerializeField, LabelText("바람 모드")]
    private WindMode windMode = WindMode.Constant;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("존 안의 플레이어")]
    private PlayerController _playerInZone;

    private Collider _collider;

    // 수직 성분이 수평 성분보다 크면 수직풍으로 취급한다.
    // 수직풍은 지상에서 우산을 펼치기만 해도 작동해야 "위로 슈웅" 이동이 가능하기 때문이다.
    private bool IsVerticalDominant => Mathf.Abs(windDirection.y) > Mathf.Abs(windDirection.x);

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        // 플레이어가 그냥 통과해야 하므로 트리거로 강제한다.
        _collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerInZone = player;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != _playerInZone) return;

        _playerInZone = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();

        // 지속풍은 파란색, 간헐풍은 하늘색으로 구분해 씬 뷰에서 바로 모드를 알 수 있게 한다.
        Gizmos.color = windMode == WindMode.Constant
            ? new Color(0.2f, 0.4f, 1f)
            : new Color(0.4f, 0.8f, 1f);

        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        // 존 중심에서 바람 방향으로 화살표(선 + 화살촉)를 그려 방향을 미리 확인할 수 있게 한다.
        Vector3 dir = windDirection.sqrMagnitude > 0.001f ? windDirection.normalized : Vector3.right;
        Vector3 start = transform.position;
        Vector3 end = start + dir * 2f;
        Gizmos.DrawLine(start, end);

        Vector3 back = -dir * 0.4f;
        Vector3 side = Vector3.Cross(dir, Vector3.forward).normalized * 0.3f;
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(dir, Vector3.up).normalized * 0.3f;

        Gizmos.DrawLine(end, end + back + side);
        Gizmos.DrawLine(end, end + back - side);
    }
#endif
}
```

- [ ] **Step 2: Unity Editor에서 확인한다.**

1. 빈 GameObject를 만들고 `BoxCollider`(Is Trigger 체크 안 해도 Awake가 강제로 켜줌)와 `WindZone` 컴포넌트를 붙인다.
2. 씬 뷰에서 파란색 와이어박스와 방향 화살표가 보이는지 확인한다. `windDirection`을 `(0,1,0)`으로 바꿔보고 화살표가 위를 향하는지 확인한다.
3. Play Mode로 들어가 플레이어를 그 박스 안으로 이동시키고, `WindZone` Inspector의 "존 안의 플레이어" 읽기 전용 필드에 플레이어 이름이 채워지는지 확인한다. 밖으로 나가면 다시 비는지 확인한다.
4. Console에 에러가 없는지 확인한다.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Puzzle/WindZone.cs
git commit -m "$(cat <<'EOF'
WindZone 골격 추가 - 필드/트리거 감지/기즈모

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: PlayerController 수직 바람 반영

**Files:**
- Modify: `Assets/Scripts/Controllers/Player/PlayerController.cs:260-265` (`ApplyFallSpeedLimit()` 메서드 바로 다음)

**Interfaces:**
- Consumes: 기존 `_windVelocity` 필드, 기존 `_verticalVelocity` 필드 (둘 다 이미 존재)
- Produces: 없음 (내부 동작 변경만)

- [ ] **Step 1: `ApplyFallSpeedLimit()` 메서드 바로 아래에 새 메서드를 추가한다.**

수정 전 (`Assets/Scripts/Controllers/Player/PlayerController.cs:260-265`):

```csharp
    private void ApplyFallSpeedLimit()
    {
        // 우산이 열려 있을 때 수직 낙하 속도가 최대값을 초과하지 않도록 제한한다.
        if (_hasFallSpeedLimit && _verticalVelocity < _maxFallSpeed)
            _verticalVelocity = _maxFallSpeed;
    }
```

수정 후:

```csharp
    private void ApplyFallSpeedLimit()
    {
        // 우산이 열려 있을 때 수직 낙하 속도가 최대값을 초과하지 않도록 제한한다.
        if (_hasFallSpeedLimit && _verticalVelocity < _maxFallSpeed)
            _verticalVelocity = _maxFallSpeed;
    }

    private void ApplyWindVertical()
    {
        // WindZone의 바람 방향에 위쪽 성분이 있으면 수직 속도에 직접 반영한다.
        // _windVelocity는 horizontal 합산에도 쓰이지만(X/Z), Y 성분은 그쪽에서 버려지므로
        // 여기서 별도로 _verticalVelocity에 덮어써야 "위로 부는 바람"이 실제로 작동한다.
        if (_windVelocity.y != 0f)
            _verticalVelocity = _windVelocity.y;
    }
```

- [ ] **Step 2: `Update()`에서 `ApplyFallSpeedLimit();` 호출 바로 다음 줄에 새 메서드 호출을 추가한다.**

수정 전 (`Assets/Scripts/Controllers/Player/PlayerController.cs:104-126`의 일부):

```csharp
        HandleMove();
        HandleFacing();
        ApplyGravity();
        ApplyFallSpeedLimit();
        DecayRecoil();
```

수정 후:

```csharp
        HandleMove();
        HandleFacing();
        ApplyGravity();
        ApplyFallSpeedLimit();
        ApplyWindVertical();
        DecayRecoil();
```

- [ ] **Step 3: Unity Editor에서 확인한다.**

1. 컴파일 에러가 없는지 Console을 확인한다.
2. Play Mode에서 우산을 펼치고 공중에 뜬 상태로 낙하시켜, 기존 글라이드(천천히 낙하) 기능이 여전히 정상 작동하는지 확인한다 — 이번 수정이 `ApplyFallSpeedLimit()` 바로 다음 줄이라 이 회귀 확인이 중요하다.
3. `_windVelocity`는 아직 아무도 설정하지 않으므로(다음 태스크에서 WindZone이 호출) 이번 단계에서는 동작 변화가 없는 게 정상이다.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Controllers/Player/PlayerController.cs
git commit -m "$(cat <<'EOF'
PlayerController에 수직 바람 반영 로직 추가

WindZone이 위쪽으로 부는 바람을 설정했을 때 실제로 떠오르도록
_windVelocity.y를 _verticalVelocity에 반영한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: 간헐풍 타이머 + 세기 페이드

**Files:**
- Modify: `Assets/Scripts/Puzzle/WindZone.cs`

**Interfaces:**
- Consumes: `DG.Tweening.DOTween.To`
- Produces (이후 태스크가 사용):
  - `private bool _isActive`
  - `private float _strengthMultiplier`
  - `private void SetActive(bool active)`

- [ ] **Step 1: 파일 상단 `using`에 DOTween을 추가하고, 간헐풍 관련 필드를 추가한다.**

```csharp
using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
```

`[Title("간헐풍 설정")]` 블록을 아래로 교체한다 (기존 `windMode` 필드는 유지하고 아래 필드들을 추가):

```csharp
    [Title("간헐풍 설정")]
    [SerializeField, LabelText("바람 모드")]
    private WindMode windMode = WindMode.Constant;

    [SerializeField, LabelText("바람 부는 시간"), ShowIf("windMode", WindMode.Intermittent)]
    private float onDuration = 2f;
    // 간헐풍이 켜져 있는 지속 시간(초)이다.

    [SerializeField, LabelText("바람 멈추는 시간"), ShowIf("windMode", WindMode.Intermittent)]
    private float offDuration = 2f;
    // 간헐풍이 꺼져 있는 지속 시간(초)이다.

    [SerializeField, LabelText("전환 페이드 시간")]
    private float fadeDuration = 0.4f;
    // On/Off 전환 시 바람 세기가 부드럽게 바뀌는 데 걸리는 시간이다.
```

`[Title("런타임 상태 (읽기 전용)")]` 블록에 필드를 추가한다:

```csharp
    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("존 안의 플레이어")]
    private PlayerController _playerInZone;

    [ReadOnly, ShowInInspector, LabelText("현재 활성 여부")]
    private bool _isActive = true;
```

클래스 필드 영역(`private Collider _collider;` 다음)에 아래를 추가한다:

```csharp
    private Collider _collider;
    private float _strengthMultiplier = 1f;
    private Tween _fadeTween;
    private Coroutine _cycleRoutine;
```

- [ ] **Step 2: `OnEnable`/`OnDisable`/`OnDestroy`와 간헐풍 코루틴, `SetActive`를 추가한다.**

`Awake()` 메서드 다음에 아래 메서드들을 추가한다:

```csharp
    private void OnEnable()
    {
        if (windMode == WindMode.Intermittent)
            _cycleRoutine = StartCoroutine(IntermittentCycle());
        else
            SetActive(true);
    }

    private void OnDisable()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        _playerInZone = null;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }

    private IEnumerator IntermittentCycle()
    {
        while (true)
        {
            SetActive(true);
            yield return new WaitForSeconds(onDuration);

            SetActive(false);
            yield return new WaitForSeconds(offDuration);
        }
    }

    // 바람의 On/Off 상태를 바꾸고, 세기를 fadeDuration 동안 부드럽게 전환한다.
    private void SetActive(bool active)
    {
        _isActive = active;

        _fadeTween?.Kill();
        float targetMultiplier = active ? 1f : 0f;
        _fadeTween = DOTween.To(
            () => _strengthMultiplier,
            x => _strengthMultiplier = x,
            targetMultiplier,
            fadeDuration
        );
    }

    [Button("강제 On/Off 토글 테스트")]
    private void TestToggle() => SetActive(!_isActive);
```

- [ ] **Step 3: Unity Editor에서 확인한다.**

1. `windMode`를 `Constant`로 두면 Play Mode 진입 시 "현재 활성 여부"가 바로 `true`로 고정되는지 확인한다.
2. `windMode`를 `Intermittent`로, `onDuration=2`, `offDuration=2`로 설정하고 Play Mode에서 "현재 활성 여부"가 2초 주기로 자동으로 켜졌다 꺼지는지 확인한다.
3. Edit Mode(정지 상태)에서 Inspector의 "강제 On/Off 토글 테스트" 버튼을 눌러도 동작하는지 확인한다(Odin `[Button]`은 Play Mode가 아니어도 호출 가능하므로, 실제 확인은 Play Mode에서 하는 게 더 정확하다).
4. Play Mode를 껐다 켰다 하면서 Console에 Tween 관련 에러가 없는지 확인한다.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Puzzle/WindZone.cs
git commit -m "$(cat <<'EOF'
WindZone에 간헐풍 On/Off 타이머와 세기 페이드 추가

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: 힘 적용 로직 (우산 연동)

**Files:**
- Modify: `Assets/Scripts/Puzzle/WindZone.cs`

**Interfaces:**
- Consumes:
  - `PlayerController.SetWindZone(Vector3)`, `PlayerController.ClearWindZone()`, `PlayerController.IsGrounded` (public get) — 모두 기존 코드에 이미 존재
  - `UmbrellaTool.IsOpen` (public get) — 기존 코드에 이미 존재
- Produces: 없음 (이 태스크가 이 서브프로젝트의 핵심 동작을 완성)

- [ ] **Step 1: `OnTriggerEnter`에서 `UmbrellaTool`도 함께 캐싱하도록 수정한다.**

수정 전:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerInZone = player;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != _playerInZone) return;

        _playerInZone = null;
    }
```

수정 후:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerInZone = player;
        // FanTool/UmbrellaTool의 기존 참조 탐색 방식과 동일하게, 자식에서 못 찾으면
        // 씬 전체에서 하나 찾는다 (도구가 플레이어와 분리된 오브젝트에 있을 수도 있어서).
        _umbrellaInZone = player.GetComponentInChildren<UmbrellaTool>();
        if (_umbrellaInZone == null)
            _umbrellaInZone = FindFirstObjectByType<UmbrellaTool>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != _playerInZone) return;

        _playerInZone.ClearWindZone();
        _playerInZone = null;
        _umbrellaInZone = null;
    }
```

`_umbrellaInZone` 필드를 `_playerInZone` 필드 선언 다음 줄에 추가한다:

```csharp
    private PlayerController _playerInZone;
    private UmbrellaTool _umbrellaInZone;
```

- [ ] **Step 2: 매 프레임 힘 적용 여부를 판정하는 `Update()`를 추가한다.**

클래스 안, `IntermittentCycle()` 메서드 다음에 추가한다:

```csharp
    private void Update()
    {
        if (_playerInZone == null) return;

        bool umbrellaOpen = _umbrellaInZone != null && _umbrellaInZone.IsOpen;
        // 수직풍은 접지 여부와 무관하게, 수평풍은 공중에서만 작동한다.
        bool canApply = _isActive && umbrellaOpen
            && (IsVerticalDominant || !_playerInZone.IsGrounded);

        if (canApply)
        {
            Vector3 velocity = windDirection.normalized * (windStrength * _strengthMultiplier);
            _playerInZone.SetWindZone(velocity);
        }
        else
        {
            _playerInZone.ClearWindZone();
        }
    }
```

- [ ] **Step 3: `OnDisable()`에서 플레이어가 존 안에 있을 때 바람을 방어적으로 해제하도록 수정한다.**

수정 전:

```csharp
    private void OnDisable()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        _playerInZone = null;
    }
```

수정 후:

```csharp
    private void OnDisable()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        // 오브젝트가 비활성화되는 동안 바람이 낀 채로 남지 않도록 방어적으로 해제한다.
        _playerInZone?.ClearWindZone();
        _playerInZone = null;
        _umbrellaInZone = null;
    }
```

- [ ] **Step 4: Unity Editor에서 확인한다.**

1. `windDirection = (1,0,0)`(수평풍) 존을 만들고, 공중에서 우산을 펼친 채 들어가면 방향으로 밀리는지, 지상에서는 우산을 펼쳐도 무반응인지 확인한다.
2. `windDirection = (0,1,0)`(수직풍) 존을 만들고, 지상에서 우산을 펼치자마자 떠오르는지 확인한다.
3. 존 안에서 우산을 접는 순간 바람 영향이 즉시 사라지는지 확인한다.
4. 존을 벗어나는 순간에도 바람 영향이 즉시 사라지는지 확인한다.
5. 기존 우산 글라이드, 선풍기 반동/블라스트가 여전히 정상 작동하는지 회귀 확인한다.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Puzzle/WindZone.cs
git commit -m "$(cat <<'EOF'
WindZone 힘 적용 로직 추가 - 우산 상태와 연동

수직풍은 접지 무관, 수평풍은 공중에서만 작동하도록 매 프레임 판정해
PlayerController.SetWindZone/ClearWindZone을 호출한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: 바람소리

**Files:**
- Modify: `Assets/Scripts/Puzzle/WindZone.cs`

**Interfaces:**
- Consumes: `AudioSource.DOFade` (DOTween Pro Audio 확장 메서드)
- Produces: `private void PlayEffects()`, `private void StopEffects()` (Task 6에서 파티클 재생/정지도 이 두 메서드에 합류)

- [ ] **Step 1: 사운드 관련 필드를 추가한다.**

`[Title("연출 설정")]` 블록을 새로 추가한다 (`[Title("간헐풍 설정")]` 블록 다음, `[Title("런타임 상태 (읽기 전용)")]` 블록 이전):

```csharp
    [Title("연출 설정")]
    [SerializeField, LabelText("바람 소리")]
    private AudioClip windSound;
    // Inspector에서 바람 소리 클립을 연결한다. 비워두면 소리 없이 동작한다.

    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;
    // Inspector에서 이 WindZone 오브젝트의 AudioSource를 연결한다. 3D 스페이셜로 설정해야
    // 존에 가까울수록 크게, 멀수록 작게 들린다.
```

- [ ] **Step 2: `PlayEffects()`/`StopEffects()`를 추가하고 `Update()`에서 호출한다.**

`Update()` 메서드를 아래로 교체한다:

```csharp
    private void Update()
    {
        if (_playerInZone == null) return;

        bool umbrellaOpen = _umbrellaInZone != null && _umbrellaInZone.IsOpen;
        bool canApply = _isActive && umbrellaOpen
            && (IsVerticalDominant || !_playerInZone.IsGrounded);

        if (canApply)
        {
            Vector3 velocity = windDirection.normalized * (windStrength * _strengthMultiplier);
            _playerInZone.SetWindZone(velocity);
            PlayEffects();
        }
        else
        {
            _playerInZone.ClearWindZone();
            StopEffects();
        }
    }

    private void PlayEffects()
    {
        if (audioSource == null || windSound == null) return;

        if (!audioSource.isPlaying)
        {
            audioSource.clip = windSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void StopEffects()
    {
        if (audioSource != null)
            audioSource.Stop();
    }
```

`OnTriggerExit`와 `OnDisable`에서도 이펙트를 멈추도록 수정한다. `OnTriggerExit` 수정 후:

```csharp
    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != _playerInZone) return;

        _playerInZone.ClearWindZone();
        _playerInZone = null;
        _umbrellaInZone = null;
        StopEffects();
    }
```

`OnDisable` 수정 후:

```csharp
    private void OnDisable()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        _playerInZone?.ClearWindZone();
        _playerInZone = null;
        _umbrellaInZone = null;
        StopEffects();
    }
```

- [ ] **Step 3: 간헐풍 On/Off 전환에 맞춰 볼륨을 페이드하도록 `SetActive`를 수정한다.**

수정 전:

```csharp
    private void SetActive(bool active)
    {
        _isActive = active;

        _fadeTween?.Kill();
        float targetMultiplier = active ? 1f : 0f;
        _fadeTween = DOTween.To(
            () => _strengthMultiplier,
            x => _strengthMultiplier = x,
            targetMultiplier,
            fadeDuration
        );
    }
```

수정 후:

```csharp
    private void SetActive(bool active)
    {
        _isActive = active;

        _fadeTween?.Kill();
        float targetMultiplier = active ? 1f : 0f;
        _fadeTween = DOTween.To(
            () => _strengthMultiplier,
            x => _strengthMultiplier = x,
            targetMultiplier,
            fadeDuration
        );

        if (audioSource != null)
            audioSource.DOFade(active ? 1f : 0f, fadeDuration);
    }
```

- [ ] **Step 4: Unity Editor에서 확인한다.**

1. `WindZone` 오브젝트에 `AudioSource`(Spatial Blend = 1, Loop 체크 여부는 코드가 강제하므로 상관없음)를 붙이고 바람 소리 클립을 연결한다.
2. Play Mode에서 우산을 펼치고 존에 들어가면 소리가 재생되고, 나가거나 우산을 접으면 멈추는지 확인한다.
3. 간헐풍 모드에서 On/Off 전환 시 소리 볼륨이 뚝 끊기지 않고 `fadeDuration` 동안 부드럽게 줄었다 늘어나는지 확인한다.
4. Play Mode를 여러 번 껐다 켜도 Console에 오디오 관련 에러가 없는지 확인한다.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Puzzle/WindZone.cs
git commit -m "$(cat <<'EOF'
WindZone에 바람 소리 추가 - On/Off 페이드 연동

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: 바람 파티클 이펙트 + 최종 회귀 확인

**Files:**
- Modify: `Assets/Scripts/Puzzle/WindZone.cs`

**Interfaces:**
- Consumes: 없음 (순수 시각 효과)
- Produces: 없음 (이 서브프로젝트의 마지막 태스크)

- [ ] **Step 1: 파티클 필드를 추가한다.**

`[Title("연출 설정")]` 블록에 필드를 추가한다:

```csharp
    [Title("연출 설정")]
    [SerializeField, LabelText("바람 소리")]
    private AudioClip windSound;

    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;

    [SerializeField, LabelText("바람 파티클")]
    private ParticleSystem windEffect;
    // Inspector에서 바람 파티클을 연결한다. 비워두면 파티클 없이 동작한다.
```

- [ ] **Step 2: `Awake()`에서 파티클 방향을 바람 방향에 맞추고, `PlayEffects`/`StopEffects`에서 재생/정지하도록 수정한다.**

`Awake()` 수정 후:

```csharp
    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;

        if (windEffect != null)
        {
            // 파티클이 바람 방향을 바라보도록 회전시켜 흩날리는 모양이 방향과 일치하게 한다.
            Vector3 dir = windDirection.sqrMagnitude > 0.001f ? windDirection.normalized : Vector3.right;
            windEffect.transform.rotation = Quaternion.LookRotation(dir);
        }
    }
```

`PlayEffects`/`StopEffects` 수정 후:

```csharp
    private void PlayEffects()
    {
        if (windEffect != null && !windEffect.isPlaying)
            windEffect.Play();

        if (audioSource == null || windSound == null) return;

        if (!audioSource.isPlaying)
        {
            audioSource.clip = windSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void StopEffects()
    {
        if (windEffect != null)
            windEffect.Stop();

        if (audioSource != null)
            audioSource.Stop();
    }
```

- [ ] **Step 3: Unity Editor에서 최종 통합 확인을 한다.**

1. `WindZone`에 파티클 프리팹(또는 새 `ParticleSystem`)을 연결하고, `windDirection`을 바꿔가며 씬 뷰에서 파티클이 방향에 맞게 회전하는지 확인한다.
2. Play Mode에서 우산을 펼치고 존에 들어가면 파티클이 재생되고, 나가거나 우산을 접으면 멈추는지 확인한다.
3. 간헐풍 모드에서 On/Off 주기에 맞춰 파티클도 같이 켜졌다 꺼지는지 확인한다.
4. 아래 회귀 체크리스트를 모두 확인한다:
   - 수평풍 존: 공중에서만 밀리고 지상에서는 무반응
   - 수직풍 존: 지상에서도 우산 펼치면 떠오름
   - 우산 접는 즉시 바람 영향 소멸
   - 기존 우산 글라이드(공중 낙하속도 제한) 정상 작동
   - 기존 선풍기 반동/블라스트 정상 작동
   - 씬 뷰 기즈모(범위/방향 화살표)가 실제 동작과 일치
   - Console에 에러 없음, Play Mode 종료 시 Tween 에러 없음

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Puzzle/WindZone.cs
git commit -m "$(cat <<'EOF'
WindZone에 바람 파티클 이펙트 추가

바람 On/Off와 방향에 맞춰 파티클이 재생/회전하도록 연결해
WindZone + 우산 이동 시스템(서브프로젝트 A) 구현을 마무리한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
