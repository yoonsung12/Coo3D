# CLAUDE.md

이 파일은 Claude Code가 이 Unity 프로젝트에서 작업할 때 반드시 따라야 할 지침이다.

## Project Overview

이 프로젝트는 기존에 2D로 진행되던 프로젝트를 3D(쿼터뷰 / 탑다운 시점)로 전환하여 개발하는 Unity 게임 프로젝트다.

시점은 쿼터뷰/탑다운 3D이며, 벽점프 등 2D 전용 기믹은 더 이상 사용하지 않는다.

사용 환경:

* Unity 6 6000.0.68f1
* Universal Render Pipeline, URP 17.0.4 (3D 렌더링 기준)
* New Input System 1.18.0
* Visual Scripting 1.9.7
* Timeline 1.8.10
* uGUI 2.0.0
* Unity Test Framework 1.6.0
* MCP for Unity, CoplayDev GitHub
* DOTween Pro
* Odin Inspector

이 프로젝트는 이미 작성된 코드와 구조가 있으므로, 새로 갈아엎는 방식이 아니라 기존 구조를 유지하면서 필요한 부분만 3D 기준으로 개선해야 한다.

---

## 2D → 3D 전환 기준

이 프로젝트는 2D에서 3D(쿼터뷰/탑다운)로 전환 중이다. 전환 작업을 할 때는 아래 대응 관계를 기준으로 판단한다.

| 2D (기존) | 3D (전환 후) |
|---|---|
| Rigidbody2D | CharacterController 또는 Rigidbody(3D) — 상황별로 선택 |
| Collider2D (BoxCollider2D 등) | Collider(3D) (BoxCollider, CapsuleCollider 등) |
| Vector2 이동/방향 계산 | Vector3 이동/방향 계산 (필요 시 Y축 고정) |
| SpriteRenderer | MeshRenderer / Prefab(3D Mesh) |
| Tilemap | 3D Mesh 지형, ProBuilder, 또는 외부 제작 후 임포트 |
| 2D Light | 일반 3D Light (Directional, Point, Spot) |
| Sorting Layer | 일반적인 3D 렌더링 순서, 필요 시 카메라/Y축 높이로 제어 |
| 벽점프 (반대편으로 튕기는 점프) | 사용하지 않음 (제거 대상) |

캐릭터(플레이어, 적)의 실제 이동 방식은 아직 CharacterController와 Rigidbody(3D) 중 확정하지 않았으므로, 상황에 따라 유연하게 선택한다. 다만 다음 기준을 참고한다.

* CharacterController: 캐릭터 이동을 직접 제어(Move 기반)하고 싶을 때, 예측 가능한 움직임이 중요할 때 우선 고려한다.
* Rigidbody(3D): 물리적 충돌, 밀림, 외력 반응이 실제로 필요할 때만 사용한다.
* 적 AI의 추적/길찾기는 NavMeshAgent 사용 여부가 아직 정해지지 않았으므로, 단순 추적 로직과 NavMeshAgent 기반 로직 둘 다 가능성을 열어두고, 어떤 적/상황에 어떤 방식을 쓸지 작업 전에 먼저 묻거나 제안한다.
* 환경 오브젝트(발판, 던질 수 있는 물체 등)처럼 순수 물리 반응이 필요한 경우에만 부분적으로 Rigidbody(3D)를 사용한다.

Sprite/Tilemap 관련 코드나 에셋은 기본적으로 3D Mesh/Prefab 기준으로 대체하는 것을 우선하되, 파티클, 데칼, UI처럼 평면적 표현이 자연스러운 이펙트 요소는 예외로 유지할 수 있다.

---

## 가장 중요한 원칙

* 기존 게임 기능이 정상 작동하고 있다면 함부로 구조를 바꾸지 않는다.
* 기존 코드 스타일, 변수명, 메서드명, 클래스 구조를 최대한 유지한다.
* 요청받은 기능과 직접 관련 없는 코드는 수정하지 않는다.
* 불필요한 리팩토링, 추상화, 패턴 적용을 하지 않는다.
* DOTween Pro와 Odin Inspector는 기존 기능을 더 편하게 관리하고 연출을 개선하기 위한 목적으로 사용한다.
* DOTween이나 Odin을 쓰기 위해 이미 잘 작동하는 시스템을 무리하게 갈아엎지 않는다.
* 작업 전에는 반드시 수정 계획을 먼저 설명하고, 사용자의 승인을 받은 뒤 코드를 수정한다.

---

## 반드시 지켜야 할 점

* OOP 기반으로 설계한다.
* SOLID 원칙을 고려하되, 과하게 복잡한 구조는 만들지 않는다.
* 디자인 패턴은 필요한 경우에만 사용한다.
* 최적화를 고려한 코드를 작성한다.
* 초보 Unity 개발자가 읽어도 흐름을 이해할 수 있게 작성한다.
* 기존 코드 스타일과 구조를 최대한 유지한다.
* 코드를 수정할 때는 어떤 의도로 수정했는지 간단히 설명한다.
* 한 번에 큰 변경을 하지 말고, 기능 단위로 작게 수정한다.
* 기존 기능이 깨질 가능성이 있으면 반드시 먼저 말한다.

---

## 작업 전 계획 규칙

코드를 작성하거나 수정하기 전에 항상 아래 형식으로 먼저 설명한다.

수정 계획:

1. 어떤 파일을 수정할지
2. 어떤 기능을 추가하거나 변경할지
3. DOTween Pro를 어디에 사용할지
4. Odin Inspector를 어디에 사용할지
5. 기존 기능에 영향이 있는지
6. Unity Editor에서 어떻게 테스트해야 하는지

사용자가 승인하면 그때 실제 코드를 수정한다.

불확실한 부분이 있으면 추측해서 진행하지 말고, 무엇이 불확실한지 설명한다.

---

## 기존 코드 우선 원칙

이미 구현된 기능을 수정할 때는 아래 순서를 따른다.

1. 현재 코드가 어떤 방식으로 작동하는지 먼저 파악한다.
2. 기존 구조를 유지한 채 수정할 수 있는지 판단한다.
3. DOTween이나 Odin을 적용하면 실제로 좋아지는 부분인지 판단한다.
4. 필요한 부분만 작게 수정한다.
5. 수정 후 기존 기능이 유지되는지 확인 방법을 알려준다.

하지 말아야 할 것:

* 기존 시스템 전체를 새 구조로 갈아엎기
* 요청하지 않은 매니저 클래스 새로 만들기
* 단순 기능에 과한 디자인 패턴 적용하기
* 작동 중인 CharacterController/Rigidbody(3D) 이동을 DOTween 이동으로 강제로 바꾸기
* 기존 public 변수나 메서드를 함부로 private으로 바꾸기
* 다른 스크립트에서 참조 중일 수 있는 이름을 마음대로 변경하기

---

## DOTween Pro 사용 규칙

DOTween Pro는 주로 연출과 UI 애니메이션에 사용한다.

DOTween을 우선적으로 사용할 상황:

* UI 패널 열림/닫힘
* 버튼 클릭 애니메이션
* 체력바, 게이지바, 시즌 게이지 변화 연출
* 오브젝트 이동, 회전, 크기 변화 연출
* 문 열림/닫힘
* 퍼즐 장치 작동 연출
* 카메라 흔들림
* 피격, 사망, 등장, 사라짐 연출
* 페이드 인/아웃
* 보스 패턴 경고 표시
* 계절 기믹의 시각적 연출
* 아이템 획득 연출
* 경고 표시 깜빡임
* 스테이지 전환 연출

DOTween을 사용할 때 반드시 지킬 것:

* DOTween 사용 시 `using DG.Tweening;`을 추가한다.
* 반복되거나 중간에 끊길 수 있는 Tween은 변수에 저장한다.
* 오브젝트가 비활성화되거나 파괴될 때 Tween을 `Kill()` 처리한다.
* 여러 연출이 이어질 때는 `Sequence()`를 사용한다.
* Ease, Duration, Delay 값은 가능하면 Inspector에서 조절 가능하게 만든다.
* `Update()`에서 직접 색상, 위치, 크기를 계속 바꾸는 연출 코드는 가능하면 DOTween으로 대체한다.
* 동일한 Tween 코드가 반복되면 메서드로 분리한다.

주의할 점:

* 플레이어 이동, 점프, 충돌처럼 CharacterController/Rigidbody(3D) 물리 동작이 중요한 부분은 무조건 DOTween으로 바꾸지 않는다.
* 물리 이동은 기존 CharacterController/Rigidbody(3D) 흐름을 유지하고, DOTween은 연출 보조용으로만 사용한다.
* 충돌 판정이 중요한 오브젝트를 DOTween으로 이동시킬 때는 물리 충돌이 깨질 수 있는지 먼저 설명한다.
* DOTween을 적용해서 오히려 코드가 복잡해진다면 사용하지 않는다.

예시:

```csharp
using DG.Tweening;
using UnityEngine;

public class DoorTweenExample : MonoBehaviour
{
    [SerializeField] private Transform doorTransform;
    // Inspector에서 실제로 회전시킬 문 오브젝트를 연결한다.

    [SerializeField] private float openAngle = 90f;
    // 문이 열릴 때 회전할 각도다.

    [SerializeField] private float duration = 0.5f;
    // 문이 열리고 닫히는 데 걸리는 시간이다.

    private Tween doorTween;
    // 문 연출이 중복 실행되지 않도록 현재 실행 중인 Tween을 저장한다.

    private void OnDestroy()
    {
        doorTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해서 오류를 방지한다.
    }

    public void OpenDoor()
    {
        doorTween?.Kill();
        // 이전 문 연출이 남아 있으면 제거하고 새 연출을 시작한다.

        doorTween = doorTransform
            .DORotate(new Vector3(0f, openAngle, 0f), duration)
            .SetEase(Ease.OutQuad);
        // DOTween을 사용해 문이 자연스럽게 열리는 회전 연출을 만든다.
        // 3D에서는 보통 Y축(위에서 내려다보는 회전축)을 기준으로 회전시킨다.
    }
}
```

---

## Odin Inspector 사용 규칙

Odin Inspector는 Inspector를 보기 좋게 정리하고 테스트 편의성을 높이기 위해 사용한다.

Odin을 우선적으로 사용할 상황:

* Inspector 값이 많아서 보기 어려운 클래스
* 플레이어 설정값
* 적 AI 설정값
* 보스 패턴 설정값
* 퍼즐 기믹 설정값
* 계절 기믹 설정값
* Stage, Season, Debuff, Gauge 관련 설정값
* UI 연출 설정값
* DOTween 애니메이션 설정값
* Manager 클래스의 런타임 상태 확인
* 에디터에서 바로 테스트해야 하는 기능

자주 사용할 Odin Attribute:

* `[Title]` : 큰 구역 제목
* `[BoxGroup]` : 관련 변수 묶기
* `[FoldoutGroup]` : 접을 수 있는 설정 묶음
* `[TabGroup]` : 설정이 많을 때 탭으로 분리
* `[LabelText]` : Inspector에 한글 이름 표시
* `[InfoBox]` : 사용 방법이나 주의사항 설명
* `[ReadOnly]` : 런타임 상태 확인용
* `[ShowIf]`, `[HideIf]` : 조건에 따라 필요한 값만 표시
* `[Button]` : 에디터 테스트 버튼

Odin 사용 시 반드시 지킬 것:

* Odin 사용 시 `using Sirenix.OdinInspector;`을 추가한다.
* 기존 public 변수를 무조건 private으로 바꾸지 않는다.
* 기존 코드에서 외부 접근 중일 수 있는 변수는 접근 범위를 함부로 변경하지 않는다.
* 새로 추가하는 Inspector 값은 가능하면 `[SerializeField] private`으로 작성한다.
* Inspector에서 어떤 값을 넣어야 하는지 한국어 주석으로 설명한다.
* 테스트용 메서드는 `[Button]`으로 만들어 Play Mode에서 바로 확인할 수 있게 한다.
* Odin Attribute를 너무 많이 붙여서 코드가 지저분해지지 않게 한다.

예시:

```csharp
using Sirenix.OdinInspector;
using UnityEngine;

public class SeasonGaugeExample : MonoBehaviour
{
    [Title("시즌 게이지 설정")]
    [SerializeField, LabelText("최대 게이지")]
    private float maxGauge = 100f;
    // Inspector에서 시즌 게이지의 최대값을 조절하기 위한 값이다.

    [SerializeField, LabelText("현재 게이지"), ReadOnly]
    private float currentGauge;
    // 런타임 중 현재 시즌 게이지 상태를 확인하기 위한 값이다.

    [Button("게이지 테스트 증가")]
    private void TestIncreaseGauge()
    {
        currentGauge = Mathf.Min(currentGauge + 10f, maxGauge);
        // 테스트 버튼을 눌렀을 때 게이지가 최대값을 넘지 않도록 제한한다.
    }
}
```

---

## DOTween Pro와 Odin Inspector를 함께 사용할 때

DOTween 연출에 필요한 값은 Odin으로 보기 좋게 정리한다.

예를 들어 UI 게이지 연출을 만든다면:

* 게이지 변화 시간
* Ease 타입
* 지연 시간
* 테스트 버튼
* 현재 게이지 값
* 목표 게이지 값

이런 값들을 Odin Inspector로 정리한다.

예시 구조:

```csharp
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class GaugeTweenExample : MonoBehaviour
{
    [Title("게이지 UI 연결")]
    [SerializeField, LabelText("게이지 이미지")]
    private Image gaugeImage;
    // Inspector에서 fillAmount를 변경할 UI Image를 연결한다.

    [Title("DOTween 연출 설정")]
    [SerializeField, LabelText("변화 시간")]
    private float tweenDuration = 0.3f;
    // 게이지가 목표값까지 부드럽게 변하는 데 걸리는 시간이다.

    [SerializeField, LabelText("Ease 타입")]
    private Ease easeType = Ease.OutQuad;
    // 게이지 변화 느낌을 조절하기 위한 DOTween Ease 타입이다.

    private Tween gaugeTween;
    // 게이지 연출이 중복 실행되지 않도록 현재 Tween을 저장한다.

    private void OnDestroy()
    {
        gaugeTween?.Kill();
        // 오브젝트가 파괴될 때 실행 중인 Tween을 정리한다.
    }

    public void SetGauge(float normalizedValue)
    {
        float targetValue = Mathf.Clamp01(normalizedValue);
        // UI Image의 fillAmount는 0~1 사이 값만 사용하므로 범위를 제한한다.

        gaugeTween?.Kill();
        // 이전 게이지 연출이 남아 있으면 제거하고 새 연출을 시작한다.

        gaugeTween = gaugeImage
            .DOFillAmount(targetValue, tweenDuration)
            .SetEase(easeType);
        // DOTween으로 게이지가 부드럽게 변하도록 만든다.
    }

    [Button("게이지 50% 테스트")]
    private void TestHalfGauge()
    {
        SetGauge(0.5f);
        // Unity Editor에서 버튼을 눌러 게이지 연출을 빠르게 확인한다.
    }
}
```

---

## 주석 규칙

주석은 한국어로 작성한다.

모든 줄에 억지로 주석을 달지 않는다.
대신 초보 Unity 개발자가 흐름을 이해할 수 있도록 중요한 부분에 주석을 단다.

주석을 반드시 달아야 하는 곳:

* 클래스
* 주요 변수
* 주요 메서드
* public 변수
* `[SerializeField]` 변수
* 복잡한 조건문
* 계산식
* 상태 변경
* 이벤트 처리
* DOTween 사용 부분
* Odin Inspector로 노출되는 값
* CharacterController, Rigidbody(3D), Collider(3D), Animator, Input System, Time.deltaTime, Coroutine, SceneManager 같은 Unity 기능을 사용하는 부분

주석 작성 방향:

* 단순히 코드를 한국어로 번역하지 않는다.
* “무엇을 하는 코드인지”보다 “왜 필요한 코드인지”를 설명한다.
* Inspector에서 어떤 값을 넣어야 하는지 설명한다.
* 값이 커지거나 작아지면 어떤 변화가 생기는지 설명한다.
* 너무 당연한 한 줄 코드에는 불필요한 주석을 달지 않는다.

좋은 주석 예시:

```csharp
[SerializeField] private float moveSpeed = 5f;
// Inspector에서 플레이어 이동 속도를 조절하기 위한 값이다.
// 값이 커질수록 플레이어가 더 빠르게 이동한다.

private CharacterController characterController;
// 플레이어의 이동과 충돌을 처리하기 위한 CharacterController 컴포넌트다.
// 직접 transform.position을 바꾸는 대신 Move()를 통해 충돌이 반영된 이동을 하기 위해 사용한다.

Vector3 moveDirection = new Vector3(inputDirection.x, 0f, inputDirection.y) * moveSpeed;
// 쿼터뷰/탑다운이므로 입력의 x, y를 각각 x, z축 이동으로 사용하고 y축(높이)은 그대로 유지한다.
// 이렇게 하면 캐릭터가 평면 위에서만 이동하고 임의로 떠오르거나 가라앉지 않는다.
```

피해야 할 주석 예시:

```csharp
count++;
// count를 1 증가시킨다.
```

이런 식의 너무 당연한 주석은 작성하지 않는다.

---

## New Input System 규칙

이 프로젝트는 New Input System을 사용한다.

* Legacy Input 방식인 `Input.GetKey`, `Input.GetKeyDown`, `Input.GetAxis`는 사용하지 않는다.
* 기존 `InputSystem_Actions.inputactions` 구조를 우선적으로 확인한다.
* 입력 기능을 추가할 때는 기존 Input Action Map과 연결 방식을 유지한다.
* 플레이어 입력 코드를 새로 만들기 전에 기존 입력 처리 스크립트가 있는지 먼저 확인한다.
* 입력 처리와 실제 이동, 공격, 상호작용 로직은 가능하면 분리한다.

---

## CharacterController / Rigidbody(3D) 물리 처리 규칙

플레이어 이동, 점프, 충돌, 넉백처럼 물리가 중요한 기능은 CharacterController 또는 Rigidbody(3D) 기반 흐름을 우선한다.

* 어떤 오브젝트에 CharacterController를 쓰고 어떤 오브젝트에 Rigidbody(3D)를 쓸지는 아직 확정되지 않았으므로, 새로운 캐릭터/오브젝트 이동 로직을 작성하기 전에는 둘 중 어느 것이 적합한지 먼저 판단하고 사용자에게 설명한다.
* CharacterController를 사용할 경우, 이동은 `CharacterController.Move()`를 사용하고 `transform.position`을 직접 바꾸지 않는다.
* Rigidbody(3D)를 사용할 경우, 이동은 `Rigidbody.linearVelocity` 또는 `Rigidbody.MovePosition` 등 적절한 물리 API를 사용한다.
* 충돌이 중요한 오브젝트를 단순히 `transform.position`으로 이동시키지 않는다.
* DOTween으로 CharacterController나 Rigidbody 오브젝트를 움직일 경우 충돌/물리 처리와 충돌할 수 있으므로 먼저 설명한다.
* FixedUpdate를 사용해야 하는 경우(Rigidbody 물리 연산)와 Update를 사용해야 하는 경우(CharacterController, 입력 처리)를 구분한다.
* Time.deltaTime을 사용할 때는 왜 필요한지 설명한다.
* 이동/방향 계산은 기본적으로 Vector3 기준으로 작성하며, 평면 이동만 필요한 경우에는 Y축을 고정하거나 별도로 처리하는 이유를 설명한다.
* 적 AI의 추적/이동에 NavMeshAgent를 사용할지, 단순 추적 로직(Transform 기반)을 사용할지는 상황에 따라 다르므로, 작업 전에 어떤 방식이 적합한지 먼저 판단하고 설명한다.

---

## URP / 3D 렌더링 규칙

이 프로젝트는 URP를 사용하며, 3D(쿼터뷰/탑다운) 렌더링을 기준으로 한다.

* Built-in Render Pipeline 전용 Shader를 사용하지 않는다.
* 새 Material을 만들 때는 URP 호환 Shader를 사용한다.
* 3D Light(Directional, Point, Spot), MeshRenderer, Material, Camera 설정을 변경할 때는 기존 렌더링 구조를 먼저 확인한다.
* 2D 전용 컴포넌트(SpriteRenderer, Tilemap, 2D Light, Sorting Layer)는 기본적으로 사용하지 않으며, 기존에 남아 있는 2D 전용 코드/에셋을 발견하면 3D로 대체가 필요한지 먼저 확인하고 알려준다.
* 단순한 연출은 Material을 새로 만들기보다 색상 변경, UI 색상, DOTween 페이드 등을 우선 고려한다.
* 파티클, 데칼, UI처럼 평면적 표현이 자연스러운 이펙트 요소는 예외적으로 2D적인 표현(Sprite, Billboard 등)을 사용할 수 있다.

---

## Scene / Asset 작업 규칙

* 스크립트는 기본적으로 `Assets/Scripts/` 아래에 둔다.
* 기존 폴더 구조가 있다면 그 구조를 따른다.
* Prefab, 3D Mesh, Material, Scene, ScriptableObject를 수정할 때는 기존 참조가 깨지지 않게 주의한다.
* 2D 전용 에셋(Sprite, Tilemap 등)을 3D 에셋(Mesh, Prefab)으로 교체할 때는 기존 참조가 끊어질 수 있으므로 먼저 영향 범위를 설명한다.
* `GameObject.Find`, `FindObjectOfType`는 특별한 이유가 없으면 사용하지 않는다.
* 오브젝트 참조는 가능하면 `[SerializeField]`로 Inspector에서 연결한다.
* Manager 클래스나 Singleton에 의존해야 할 경우 남발하지 않는다.

---

## MCP for Unity 사용 규칙

이 프로젝트는 MCP for Unity를 사용한다.

Claude Code가 Unity Editor 상태를 확인할 수 있다면 다음을 우선적으로 확인한다.

* 현재 열려 있는 Scene
* 선택된 GameObject
* 연결된 Component
* Prefab 상태
* Inspector에 연결된 참조
* Console Error
* Play Mode 여부

MCP로 확인 가능한 내용을 추측해서 말하지 않는다.
확인할 수 있으면 확인하고, 확인할 수 없으면 어떤 정보가 필요한지 말한다.

---

## 코드 작성 스타일

* 초보 Unity 개발자가 읽을 수 있게 작성한다.
* 변수명과 함수명은 역할이 명확하게 드러나게 작성한다.
* 너무 복잡한 코드는 만들지 말고, 기능별로 메서드를 분리한다.
* 기존 코드 구조를 함부로 크게 바꾸지 말고, 필요한 부분만 수정한다.
* Unity 생명주기 함수인 `Awake`, `Start`, `Update`, `FixedUpdate`, `OnEnable`, `OnDisable`, `OnDestroy`의 역할을 구분해서 사용한다.
* 새 기능은 재사용 가능한 컴포넌트 방식으로 작성한다.
* 하드코딩을 줄이고 Inspector에서 조절 가능한 구조로 만든다.
* 단, 요청하지 않은 과한 설정값은 만들지 않는다.
* 단순한 기능은 단순하게 작성한다.

---

## 설계 규칙

OOP, SOLID, 디자인 패턴을 고려하되, 과하게 적용하지 않는다.

좋은 방향:

* 역할별로 클래스를 나눈다.
* 한 클래스가 너무 많은 책임을 갖지 않게 한다.
* 중복 코드가 실제로 반복될 때만 공통화한다.
* Manager 클래스는 필요한 경우에만 사용한다.
* Singleton은 꼭 필요한 전역 접근에만 제한적으로 사용한다.
* ScriptableObject는 데이터 분리가 실제로 유리할 때만 사용한다.

피해야 할 방향:

* 단순 기능 하나에 인터페이스, 추상 클래스, 팩토리 패턴을 과하게 적용하기
* 아직 필요 없는 확장성을 미리 만들기
* 요청하지 않은 시스템을 새로 설계하기
* 기존 코드와 연결하기 어려운 독립 구조 만들기

---

## 기능별 추천 적용 방식

### UI

UI에는 DOTween을 적극적으로 사용한다.

추천 적용:

* 버튼 클릭 시 살짝 커졌다 돌아오기
* 패널 열림/닫힘 애니메이션
* 체력바와 게이지바 부드러운 변화
* 경고 텍스트 깜빡임
* 페이드 인/아웃
* 스테이지 시작/클리어 연출

Odin 적용:

* UI 연결 참조 정리
* Tween 시간, Ease 타입 정리
* 테스트 버튼 추가

---

### 플레이어

플레이어의 실제 이동과 점프는 CharacterController 또는 Rigidbody(3D) 기반(상황에 따라 선택) 흐름을 유지한다.

벽점프(벽에 닿으면 반대편으로 튕기는 점프)는 3D 전환과 함께 제거 대상이다. 관련 코드를 발견하면 삭제 여부를 먼저 확인한다.

DOTween 적용 가능:

* 피격 시 깜빡임
* 대시 잔상 연출
* 아이템 획득 연출
* 사망 연출
* 착지 연출
* UI 피드백

Odin 적용:

* 이동 속도, 점프력, 대시 값 정리
* 현재 상태 ReadOnly 표시
* 테스트 버튼 추가

---

### 적 / 보스

적과 보스의 실제 추적, 공격 판정은 기존 로직을 유지한다. 단, 추적 이동 방식이 NavMeshAgent 기반인지 단순 Transform 기반인지는 아직 확정되지 않았으므로, 새 적/보스를 작업할 때는 먼저 어떤 방식이 적합한지 확인한다.

DOTween 적용 가능:

* 공격 전 경고 표시
* 보스 패턴 시작 연출
* 피격 연출
* 등장 연출
* 사라짐 연출
* 카메라 흔들림
* 투사체 경고 범위 표시

Odin 적용:

* 패턴별 수치 정리
* 공격 쿨타임 정리
* 현재 상태 ReadOnly 표시
* 특정 패턴 테스트 버튼 추가

---

### 퍼즐 기믹

퍼즐 로직은 기존 작동 방식을 유지하고, 시각적 피드백에 DOTween을 사용한다.

DOTween 적용 가능:

* 문 열림/닫힘
* 발판 이동
* 버튼 눌림
* 기믹 활성화 표시
* 오브젝트 흔들림
* 장치 작동 연출

Odin 적용:

* 작동 시간
* 쿨타임
* 이동 거리
* 회전 각도
* 테스트 버튼
* 기믹 상태 ReadOnly 표시

---

### 계절 기믹

계절 기믹은 이 프로젝트의 핵심 요소이므로 기존 시스템을 유지하면서 연출을 강화한다.

DOTween 적용 가능:

* 꽃가루 경고 연출
* 비 구름 이동 연출
* 은행잎 낙하 연출
* 눈보라 페이드 연출
* 시즌 게이지 변화
* 디버프 적용 시 UI 연출
* 보스가 계절 패턴을 사용할 때 경고 표시

Odin 적용:

* 계절별 설정값 정리
* 디버프 강도 정리
* 지속 시간 정리
* 현재 계절 상태 ReadOnly 표시
* 계절 전환 테스트 버튼 추가

---

## 테스트와 검증

코드를 수정한 뒤에는 반드시 Unity Editor에서 확인해야 할 내용을 알려준다.

예시:

* Play Mode에서 정상 작동하는지 확인
* Inspector 참조가 비어 있지 않은지 확인
* Console에 Error가 없는지 확인
* DOTween 연출이 중복 실행되지 않는지 확인
* 오브젝트 비활성화 또는 Scene 전환 시 Tween 오류가 없는지 확인
* 버튼이나 퍼즐 장치가 기존처럼 작동하는지 확인
* New Input System 입력이 정상적으로 연결되는지 확인
* CharacterController 또는 Rigidbody(3D) 충돌/이동이 의도대로 작동하는지 확인
* 3D 환경(지형, Mesh 충돌)에서 캐릭터가 끼이거나 통과하지 않는지 확인
* 카메라가 쿼터뷰/탑다운 시점을 정상적으로 따라가는지 확인

테스트가 필요한 경우 Unity Test Runner 사용을 고려한다.

Unity에서 테스트 실행:

* Window → General → Test Runner → Run All

빌드는 Unity Editor에서 실행한다.

* File → Build Settings → Build

---

## 답변 방식

Claude는 작업할 때 다음 순서를 따른다.

1. 현재 요청을 어떻게 이해했는지 간단히 설명한다.
2. 기존 구조를 유지할 수 있는지 판단한다.
3. 수정 계획을 먼저 제시한다.
4. DOTween Pro와 Odin Inspector 적용 위치를 명확히 말한다.
5. 사용자의 승인을 받은 뒤 코드 수정으로 넘어간다.
6. 수정 후 Unity에서 연결해야 할 Inspector 값과 테스트 방법을 알려준다.

답변은 한국어로 한다.
코드는 C#으로 작성한다.
주석은 한국어로 작성한다.

---

## 금지 사항

* 요청하지 않은 대규모 리팩토링 금지
* 기존 기능을 새 시스템으로 갈아엎기 금지
* Legacy Input API 사용 금지
* Built-in RP Shader 사용 금지
* 불필요한 Singleton 남발 금지
* `GameObject.Find` 남발 금지
* 작동 중인 CharacterController/Rigidbody(3D) 물리 이동을 DOTween으로 무리하게 변경 금지
* 기존 public API 이름 마음대로 변경 금지
* 관련 없는 코드 정리 금지
* 사용자가 승인하기 전에 코드 수정 금지
* 벽점프 등 제거 대상으로 확정된 2D 전용 기믹을 임의로 되살리거나 유지 금지
* CharacterController와 Rigidbody(3D) 중 어느 것을 쓸지 사용자 확인 없이 임의로 단정하고 진행 금지
* 2D 전용 컴포넌트(SpriteRenderer, Tilemap, 2D Light, Sorting Layer)를 새 기능에 임의로 사용 금지

---

## 한 줄 요약

이 프로젝트는 2D에서 3D(쿼터뷰/탑다운)로 전환 중인 Unity 게임이며, 기존 구조와 로직을 최대한 유지한 채 물리/렌더링/에셋만 3D 기준으로 옮기고, DOTween Pro는 연출 개선에, Odin Inspector는 Inspector 정리와 테스트 편의성 향상에 사용한다.