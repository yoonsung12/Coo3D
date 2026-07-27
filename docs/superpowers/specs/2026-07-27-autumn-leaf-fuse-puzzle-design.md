# 가을 구역 퍼즐 설계 (공중 낙엽 점화 / 도화선 에스코트)

날짜: 2026-07-27
관련 로드맵: 큰 맵을 봄/여름/가을/겨울 4구역으로 나누는 레벨 디자인 중, 가을 구역의 퍼즐 2개. 이번 가을 구역 조합 퍼즐 캡(1~2개)을 채운다.

## 배경

- 가을 구역 고유 낙하물은 `LeafDrop`(→ 향후 `GinkgoFruit`로 리네임 예정, 아직 미반영)이며 "접촉 금지형" 하자드다.
- 기존 `TorchTool`은 `IIgnitable`을 구현한 대상을 전방 SphereCast로 점화하고(`igniteRange`), `FanTool`은 `IBlowable`을 구현한 대상에 `windRange` 내에서 바람/블라스트를 가한다. 두 도구 모두 이미 존재하며 이번 퍼즐에서 새로 만들지 않는다.
- `FlammableObject`는 `IIgnitable`을 구현해 점화 → 일정 시간 뒤 `BecomeAsh()`로 색이 변하며(오브젝트는 파괴되지 않음) 반경 내 다른 `IIgnitable`로 불을 전파(`SpreadFire`)하는 로직을 이미 갖고 있다. 다만 `Awake()`에서 콜라이더를 항상 트리거로 강제하고 있어, 현재 상태로는 "통행을 막는 벽"으로 쓸 수 없다.
- `DoorController`는 `List<PressButton>`에 강결합되어 있어, 버튼이 아닌 다른 트리거(이번 경우 "덩굴이 다 타면 열림")로 문을 여는 경로가 없다.
- 두 퍼즐 모두 07-26~07-27 세션 브레인스토밍에서 컨셉과 세부 스펙이 확정됐다(자세한 결정 과정은 메모리 `project_autumn_leafmound_puzzle_brainstorm` 참고).

## 목표

1. **공중 낙엽 점화**: 낭떠러지를 막는 마른 덩굴벽 앞에서, 낙엽더미를 성냥(`TorchTool`)으로 점화하고 다 타기 전에 선풍기(`FanTool`)로 밀어 덩굴벽까지 날려보내면 벽이 타며 길이 열린다.
2. **도화선 에스코트**: 바닥에 깔린 도화선을 성냥으로 점화하면 일정 속도로 타들어가고, 플레이어는 발판을 넘으며 함께 전진한다. 위에서 떨어지는 은행 열매가 도화선을 꺼뜨리려 하며, 우산(`UmbrellaTool`)+바람존(`WindZoneVolume`)의 기존 리스크(강풍에 끌림)를 그대로 재사용한다. 도화선 끝 문 앞 덩굴을 불이 태우면 문이 열린다.

## 공통 확장: `FlammableObject.cs` 수정 (기존 파일, 추가 방식)

두 퍼즐 모두 "다 타면 뭔가 열린다"는 결과가 필요한데, 현재 `FlammableObject`는 다 타도 그 자리에 재 상태로 남아있을 뿐 아무 것도 변하지 않는다. 기존 동작을 그대로 둔 채 **옵션 필드 2개 + 이벤트 1개**만 추가한다.

- `[SerializeField, LabelText("연소 전까지 통행 차단")] bool blocksPathUntilBurned = false`
  — 기본값 `false`(기존과 동일하게 항상 트리거). `true`면 `Awake()`에서 콜라이더를 `isTrigger = false`로 두어 실제 장애물(벽)로 동작하고, `BecomeAsh()` 완료 시 `isTrigger = true`로 전환해 길을 연다.
- `public event Action OnBurnedOut;` — `BecomeAsh()` 끝에서 호출. 문 열기 등 후속 연출을 외부에서 구독할 수 있게 한다. 기존에 이 이벤트를 구독하는 코드가 없으므로 기존 동작에 영향 없음.

**기존 기능 영향**: `blocksPathUntilBurned` 기본값이 `false`라 기존 씬에 배치된 `FlammableObject`(있다면)는 동작이 전혀 바뀌지 않는다. 순수 추가(additive) 변경.

## `DoorController.cs` 수정 (최소 변경)

버튼이 아닌 다른 트리거로도 문을 열 수 있도록, 기존 `OpenDoor()`(현재 버튼 이벤트 콜백에서만 호출되는 것으로 추정되는 내부 메서드)의 접근 범위를 `private` → `public`으로 넓힌다. 이미 문이 열렸으면 무시하는 `_isOpen` 체크가 있으므로 외부에서 호출해도 중복 실행 문제 없음. `List<PressButton> buttons` 필드나 버튼 판정 로직은 전혀 건드리지 않는다 — 기존 버튼 퍼즐 동작은 100% 동일하게 유지된다.

---

## 퍼즐 ① 공중 낙엽 점화

### `LeafMound.cs` (신규, `Assets/Scripts/Puzzle/LeafMound.cs`)

`IIgnitable`, `IBlowable`을 구현하는 낙엽더미. 레벨 디자인 시 자유롭게 배치하는 프리팹이며, 개수/위치는 스펙에 고정하지 않는다.

- `[Title("연소 설정")] [SerializeField, LabelText("연소 시간(초)")] float burnDuration = 3f` — 점화 후 다 타기까지 걸리는 시간. **덩굴벽까지 거리에 맞춰 씬마다 다르게 조정**하는 값이라 반드시 Inspector 노출.
- `[SerializeField] ParticleSystem burningParticle` — 점화 중 재생.
- `[SerializeField] ParticleSystem fizzleParticle` — 다 타서 실패했을 때 재생.
- `[ReadOnly, ShowInInspector] bool _isBurning`
- `Rigidbody`(필요 컴포넌트) — 점화 전에는 `isKinematic = true`로 제자리 고정, `OnBlown()` 호출 시 `false`로 전환 후 `AddForce`. `Pollen`/`LeafDrop`과 동일한 물리 기반 피격 방식을 따른다.
- `Collider` — `Pollen`/`LeafDrop`과 동일한 트리거 구성을 그대로 따른다(둘 다 이미 `FanTool`의 `BoxCastAll`에 정상적으로 걸리는 것이 검증된 구성이므로 그대로 재사용).

동작:
- `OnIgnited()`: 아직 안 타는 중일 때만 반응 → `_isBurning = true`, `burningParticle.Play()`, `BurnRoutine()` 코루틴 시작.
- `IBlowable.OnBlown(Vector3 direction, float force, bool impulse)`: `_isBurning`이 아니면 무시(불 안 붙은 낙엽은 안 날아감 — 기존 `FanTool` 사거리/판정 재사용). `_isBurning`이면 물리 활성화 후 `AddForce`.
- `OnTriggerEnter(Collider other)`: `_isBurning` 중이고 `other`가 덩굴벽(`FlammableObject` 컴포넌트 보유)이면 `other.GetComponent<IIgnitable>().OnIgnited()` 호출 → 성공. 축소 트윈 후 `Destroy`.
- `BurnRoutine()`: `burnDuration` 경과 시 아직 벽에 안 닿았으면 `Fizzle()`(파티클 재생 + DOTween 축소 후 `Destroy`) — 실패 처리. 이 타이머 값 자체가 "즉시 안 날리면 못 감"이라는 긴장감을 만드는 핵심 수치다.
- `OnDestroy()`: 진행 중인 Tween `Kill()`.
- Odin `[Button("강제 점화 테스트")]`, `[Button("강제 날리기 테스트")]`로 Play Mode에서 개별 단계 검증 가능하게 한다.

### 덩굴벽 (신규 클래스 없음 — `FlammableObject` 재사용)

- `blocksPathUntilBurned = true`로 설정한 `FlammableObject` 그대로 배치. 낭떠러지 앞을 막는 콜라이더가 처음엔 막혀 있다가, `LeafMound`가 닿아 점화되면 `BecomeAsh()` 이후 길이 열린다.
- 이번 퍼즐에서는 `OnBurnedOut` 이벤트를 굳이 구독할 필요 없음(길이 열리는 것 자체가 `isTrigger = true` 전환만으로 충분).

### 씬 배치 (코드 변경 없음)

- 낙엽더미-덩굴벽 거리는 "점화 후 즉시 선풍기로 안 날리면 `burnDuration` 안에 못 도착할 정도"로 배치해, 지체하면 실패하는 긴장감을 만든다. 정확한 미터 수는 실제 레벨에서 조정.
- 낙엽더미가 선풍기 사거리(`windRange` 기본 5m) 밖에 있으면 애초에 날릴 수 없으므로, 점화 지점은 항상 `windRange` 안쪽에 배치한다.

---

## 퍼즐 ② 도화선 에스코트

### `FuseSegment.cs` (신규, `Assets/Scripts/Puzzle/FuseSegment.cs`)

도화선을 이루는 개별 세그먼트. `IIgnitable`을 구현하되, 체인 내부 전파는 `FuseChain`이 전담한다(세그먼트끼리 서로를 모르게 해서 역할을 분리).

- 상태: `enum FuseState { Unlit, Burning, Extinguished }`
- `[SerializeField] ParticleSystem burnSparkParticle` — 타는 지점에서 재생되는 스파클 이펙트(사용자 제공 레퍼런스 이미지 참고).
- `[SerializeField] Ease shrinkEase = Ease.Linear`
- `[ReadOnly, ShowInInspector] FuseState _state`
- `IIgnitable.OnIgnited()`: 성냥(`TorchTool`)이 **체인의 첫 세그먼트**를 직접 점화할 때만 쓰인다. 내부적으로 자신이 속한 `FuseChain`에 점화 사실을 알리기만 하고(`_chain.NotifyIgnited(this)`), 실제 연소 시작은 체인이 계산한 `duration`을 받아 `BeginBurn(float duration)`을 통해 이뤄진다.
- `BeginBurn(float duration)`: `Burning` 전환, 스파클 파티클 재생, **진행 방향 축을 `duration` 동안 0으로 줄이는 DOTween**(`transform.DOScaleX(0f, duration).SetEase(shrinkEase)` 또는 세그먼트 메시의 실제 긴 축에 맞춰 조정 — "타면서 점점 줄어드는" 레퍼런스 연출의 핵심). `duration` 종료 시 `_chain.NotifyBurnedOut(this)` 호출.
- `Extinguish()`: `Burning` 상태일 때만 반응. 진행 중이던 축소 Tween을 `Kill()`(중간 크기에서 멈춤), 스파클 파티클 정지, 상태를 `Extinguished`로. `_chain.NotifyExtinguished(this)` 호출.
- `OnTriggerEnter(Collider other)`: `other.GetComponent<LeafDrop>()`이 있고 `_state == Burning`이면 `Extinguish()`. (은행 열매가 이 세그먼트 위치에 떨어져 닿으면 불이 꺼지는 판정)
- `OnDestroy()`: Tween `Kill()`.

### `FuseChain.cs` (신규, `Assets/Scripts/Puzzle/FuseChain.cs`)

세그먼트 순서/속도/재점화를 총괄하는 조율자. `DoorController`처럼 `List<T>` 참조 방식을 그대로 따른다(기존 코드 컨벤션 재사용).

- `[Title("도화선 구성")] [SerializeField, LabelText("세그먼트 순서")] List<FuseSegment> segments` — 발판 배치가 정해지면 순서대로 연결. **발판 배치 자체는 이번 스펙 범위 밖**(레벨 디자인 시 결정, 메모리 기록됨).
- `[Title("연소 설정")] [SerializeField, LabelText("타는 속도(m/s)")] float burnSpeed = 2f` — 세그먼트 개수가 아니라 **거리 기준**으로 노출. 세그먼트 배치 밀도가 바뀌어도 체감 속도가 유지된다.
- `[SerializeField, LabelText("재점화 대기시간(초)")] float reigniteDelay = 1f` — 은행에 맞아 꺼진 뒤 자동으로 다시 붙기까지 걸리는 시간.
- `[SerializeField, LabelText("문 앞 덩굴")] FlammableObject doorVine` — 도화선 끝에서 태울 대상. `blocksPathUntilBurned`는 필요 없음(문 개방은 `OnBurnedOut` 이벤트로 처리하므로).
- `[SerializeField] DoorController targetDoor`
- `NotifyIgnited(FuseSegment segment)`: `segments`에서 다음 세그먼트까지의 거리를 계산(`Vector3.Distance`)해 `duration = distance / burnSpeed`로 `segment.BeginBurn(duration)` 호출.
- `NotifyBurnedOut(FuseSegment segment)`: 리스트 상 다음 세그먼트가 있으면 같은 방식으로 점화(`NotifyIgnited`와 동일 계산 재사용). 리스트의 마지막 세그먼트였다면 `doorVine.OnIgnited()` 호출 → `FlammableObject`가 알아서 타들어가고, `Awake`/`OnEnable` 시점에 `doorVine.OnBurnedOut += () => targetDoor.OpenDoor();`로 미리 구독해둔 콜백이 실행되어 문이 열린다.
- `NotifyExtinguished(FuseSegment segment)`: `reigniteDelay`초 뒤 코루틴으로 해당 세그먼트부터 다시 `BeginBurn()` 호출 — "은행에 맞으면 불 꺼졌다가 1초 뒤 주루룩 다시 생겨남" 요구사항을 그대로 구현한다. 재점화 시 진행 상황(현재까지 줄어든 크기)은 되돌리지 않고 **꺼진 지점부터 이어서** 타도록, 남은 시간만큼만 다시 `BeginBurn`(중간에 멈춘 스케일 기준으로 남은 거리/시간 재계산)한다.
- Odin `[Button("도화선 처음부터 테스트 점화")]` — `segments[0].OnIgnited()`를 바로 호출.

**플레이어 동행 판정에 대한 참고**: 이번 스펙에서는 "불이 얼마나 왔는지"(`FuseChain`이 알고 있음)와 "플레이어가 얼마나 왔는지"를 직접 비교해 실패시키는 로직까지는 포함하지 않는다. 발판 배치가 아직 미정이라 정확한 판정 지점을 정의할 수 없기 때문 — 발판 배치가 정해지면 각 발판에 "불이 이 지점을 이미 지났는데 플레이어가 아직 안 왔으면 낙사/실패" 판정을 추가하는 후속 작업이 필요할 수 있다(범위 밖, 아래 참고).

---

## 기존 기능 영향

- `FlammableObject.cs`: 필드 2개 + 이벤트 1개 추가, 기존 필드/메서드는 시그니처 변경 없음. 기본값 유지 시 동작 100% 동일.
- `DoorController.cs`: `OpenDoor()` 접근 범위만 `public`으로 변경. 버튼 판정 로직 무변경.
- `TorchTool`, `FanTool`, `LeafDrop`, `ToolManager`, `WindZoneVolume`, `UmbrellaTool`, `PressButton`: **수정 없음.**

## 범위 밖 (Out of scope)

- 도화선 발판 배치(개수/간격) — 레벨 디자인 시 결정.
- "불이 지나간 지점에 플레이어가 늦으면 실패"하는 명시적 동행 판정 로직 — 발판 배치 확정 후 후속 작업.
- `LeafDrop`/`LeafScent` → `GinkgoFruit`/`GinkgoScent` 리네임 — 별도 작업.
- 가을비(`RainDrop`) 연동 — 이전 세션에 제외 확정됨.

## 테스트 방법 (Unity Editor)

**공통**
- Console에 컴파일 에러 없는지 확인.

**공중 낙엽 점화**
- Play Mode에서 성냥으로 `LeafMound`를 점화하면 `burningParticle`이 재생되는지 확인.
- 점화 후 선풍기로 밀면 `_isBurning`(Odin ReadOnly)이 `true`인 동안만 날아가는지, 불 안 붙은 상태에서는 안 날아가는지 확인.
- `burnDuration` 안에 덩굴벽에 닿으면 벽이 `BecomeAsh()`로 전환되며 `isTrigger = true`가 되어 실제로 지나갈 수 있는지 확인.
- `burnDuration`을 넘기면 `Fizzle()`로 낙엽더미가 사라지고 벽은 계속 막혀 있는지(실패 케이스) 확인.
- Odin `[Button]` 테스트 버튼으로 각 단계를 개별 재현해 헤드리스 환경에서도 로직 자체는 검증한다.

**도화선 에스코트**
- 첫 세그먼트를 성냥으로 점화하면 `FuseChain.burnSpeed`에 맞춰 순서대로 다음 세그먼트가 이어 점화되는지 확인.
- 각 세그먼트가 타는 동안 진행 방향으로 스케일이 줄어드는 연출과 스파클 파티클이 레퍼런스 이미지와 유사하게 보이는지 확인(수치/이징은 Play Mode에서 튜닝 필요).
- 은행 열매(`LeafDrop`)가 타는 중인 세그먼트에 떨어지면 `Extinguish()`되고, `reigniteDelay` 뒤 자동으로 다시 타는지 확인.
- 도화선이 끝까지 도달하면 `doorVine`이 점화되어 다 타고, 연결된 `DoorController`의 문이 열리는지 확인.
- 헤드리스 Unity MCP 환경은 실시간 타이밍 검증에 한계가 있어(이전 퍼즐들보다 실시간성이 높음), 로직 단위 검증 후 최종 타이밍/난이도 확인은 사용자가 Play Mode에서 직접 진행한다.
