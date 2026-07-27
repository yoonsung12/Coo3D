# 가을 구역 퍼즐(낙엽 점화 / 도화선 에스코트) 구현 계획

> superpowers 플러그인 없이 진행한다(2026-07-27, 사용자 결정). Claude가 직접 파일 단위로 작게 나눠 수정하고, 각 파일 작업 전 CLAUDE.md 형식의 수정 계획을 제시해 승인받은 뒤 코드를 수정한다.

**참고 스펙 문서:** `docs/superpowers/specs/2026-07-27-autumn-leaf-fuse-puzzle-design.md`

**Goal:** 공중 낙엽 점화(낙엽더미를 점화해 선풍기로 날려 덩굴벽을 태워 여는 퍼즐)와 도화선 에스코트(성냥으로 붙인 도화선을 따라 발판을 넘으며 문 앞 덩굴까지 불을 이어가는 퍼즐) 두 개를 완성한다.

## 파일 구조

| 파일 | 종류 | 내용 |
|---|---|---|
| `Assets/Scripts/Puzzle/FlammableObject.cs` | 수정(추가형) | `blocksPathUntilBurned` 필드 + `OnBurnedOut` 이벤트 추가. 기본값은 기존 동작 유지 |
| `Assets/Scripts/Puzzle/DoorController.cs` | 수정(최소) | `OpenDoor()` 접근 범위 `private` → `public` |
| `Assets/Scripts/Puzzle/LeafMound.cs` | 신규 | 퍼즐① 낙엽더미 (`IIgnitable` + `IBlowable`) |
| `Assets/Scripts/Puzzle/FuseSegment.cs` | 신규 | 퍼즐② 도화선 세그먼트 (`IIgnitable`) |
| `Assets/Scripts/Puzzle/FuseChain.cs` | 신규 | 퍼즐② 도화선 순서/속도/재점화 조율 |
| 씬 배치 (프리팹 + `SampleScene.unity`) | 이후 태스크 | 두 퍼즐 각각의 실제 배치. 도화선 발판 배치가 아직 미정이라 퍼즐②의 씬 조립은 발판 위치가 정해진 뒤 별도로 진행 |

## Task 순서

1. **`FlammableObject.cs` 수정** — 다른 신규 스크립트들이 이 확장에 의존하므로 가장 먼저 진행.
2. **`DoorController.cs` 수정** — 사소한 접근 범위 변경, 독립적으로 먼저 처리 가능.
3. **`LeafMound.cs` 신규 생성** — 퍼즐①의 유일한 신규 스크립트.
4. **`LeafMound` + 덩굴벽 조합 씬 테스트 배치** — 실제 프리팹/파티클 연결 전, 임시 오브젝트로 Play Mode 로직 검증.
5. **`FuseSegment.cs` + `FuseChain.cs` 신규 생성** — 퍼즐②의 신규 스크립트 2개.
6. **`FuseSegment`/`FuseChain` 임시 씬 테스트 배치** — 세그먼트 2~3개짜리 짧은 체인으로 점화→전파→소화→재점화→문 열림 흐름 검증.
7. (발판 배치가 정해진 뒤) 최종 씬 조립 — 범위 밖, 발판 스펙 확정 시 별도 태스크로 진행.

각 Task는 CLAUDE.md 규칙에 따라 파일/기능/DOTween 사용처/Odin 사용처/기존 기능 영향/테스트 방법을 먼저 제시하고 승인받은 뒤 코드를 작성한다. 태스크 사이사이 Console 컴파일 에러 확인을 거친다.

## Global Constraints

- 이동 평면은 X(좌우)+Y(상하), Z축 고정 (CLAUDE.md 기준, 3D 사이드뷰).
- DOTween 사용 시 트윈을 변수에 저장하고 `OnDestroy()`에서 `Kill()`.
- Odin `[SerializeField]`에는 한글 `[LabelText]`, 주요 필드/메서드에는 "왜 필요한지" 위주 한국어 주석.
- `TorchTool`, `FanTool`, `LeafDrop`, `ToolManager`, `WindZoneVolume`, `UmbrellaTool`, `PressButton`은 수정하지 않는다.
- 자동화 테스트 프레임워크 없음 — "구현 → Unity Editor Play Mode 수동 검증" 사이클.
- 도화선 발판 배치, 동행 판정(불보다 플레이어가 늦으면 실패) 로직은 이번 범위 밖.
