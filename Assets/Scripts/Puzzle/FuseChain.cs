using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 도화선 에스코트 퍼즐에서 세그먼트들의 점화 순서/속도/재점화를 총괄하는 조율자다.
// 세그먼트(FuseSegment)는 자신의 연소 연출만 담당하고, "다음 세그먼트가 무엇인지 +
// 거리에 따라 얼마나 걸리는지"는 이 체인이 전담해서 계산한다 — 세그먼트끼리 서로를
// 몰라도 되게 역할을 나눴다. 마지막 세그먼트가 다 타면 문 앞 덩굴(FlammableObject)에
// 불을 옮기고, 그 덩굴이 다 타면 연결된 문이 열린다.
public class FuseChain : MonoBehaviour
{
    [Title("도화선 구성")]
    [SerializeField, LabelText("세그먼트 순서")]
    private List<FuseSegment> segments = new List<FuseSegment>();
    // Inspector에서 도화선 시작점부터 끝까지 순서대로 연결한다. 발판 배치가 정해지면 채운다.

    [Title("연소 설정")]
    [SerializeField, LabelText("타는 속도(m/s)")]
    private float burnSpeed = 2f;
    // 세그먼트 개수가 아니라 거리 기준이다. 세그먼트 배치 밀도가 달라져도 체감 속도가 유지된다.

    [SerializeField, LabelText("재점화 대기시간(초)")]
    private float reigniteDelay = 1f;
    // 은행 열매에 맞아 꺼진 뒤, 이 시간이 지나면 남은 구간부터 자동으로 다시 타들어간다.

    [Title("도착 지점")]
    [SerializeField, LabelText("문 앞 덩굴")]
    private FlammableObject doorVine;
    // 도화선 끝에서 태울 대상. 마지막 세그먼트가 다 타면 이 덩굴에 불이 옮는다.

    [SerializeField, LabelText("연결된 문")]
    private DoorController targetDoor;

    private void Awake()
    {
        foreach (var segment in segments)
        {
            if (segment != null)
                segment.Init(this);
        }

        if (doorVine != null && targetDoor != null)
        {
            // 도화선 끝 덩굴이 다 타면 문이 열리도록 미리 구독해둔다.
            doorVine.OnBurnedOut += () => targetDoor.OpenDoor();
        }
    }

    // 세그먼트가 성냥으로 점화됐을 때 호출된다.
    public void NotifyIgnited(FuseSegment segment)
    {
        segment.BeginBurn(CalcDuration(segment));
    }

    // 세그먼트가 다 타서 다음으로 넘어갈 때 호출된다.
    public void NotifyBurnedOut(FuseSegment segment)
    {
        int index = segments.IndexOf(segment);
        if (index < 0) return;

        if (index + 1 < segments.Count)
        {
            FuseSegment next = segments[index + 1];
            next.BeginBurn(CalcDuration(next));
        }
        else if (doorVine != null)
        {
            // 마지막 세그먼트까지 다 탔으면 문 앞 덩굴에 불을 옮긴다.
            doorVine.OnIgnited();
        }
    }

    // 은행 열매에 맞아 꺼졌을 때 호출된다. 대기시간 뒤 남은 구간부터 다시 타도록 재점화한다.
    public void NotifyExtinguished(FuseSegment segment)
    {
        StartCoroutine(ReigniteRoutine(segment));
    }

    private IEnumerator ReigniteRoutine(FuseSegment segment)
    {
        yield return new WaitForSeconds(reigniteDelay);
        segment.BeginBurn(segment.RemainingDuration);
    }

    // 이 세그먼트에서 다음 지점(다음 세그먼트, 없으면 문 앞 덩굴)까지의 거리를 속도로 나눠
    // 연소 시간을 계산한다. 다음 지점을 알 수 없으면 안전한 기본값(1초)을 쓴다.
    private float CalcDuration(FuseSegment segment)
    {
        int index = segments.IndexOf(segment);
        Vector3? nextPos = null;

        if (index >= 0 && index + 1 < segments.Count)
            nextPos = segments[index + 1].transform.position;
        else if (doorVine != null)
            nextPos = doorVine.transform.position;

        if (nextPos == null) return 1f;

        float distance = Vector3.Distance(segment.transform.position, nextPos.Value);
        return distance / Mathf.Max(burnSpeed, 0.01f);
    }

    [Button("도화선 처음부터 테스트 점화")]
    private void TestIgniteFirst()
    {
        if (segments.Count > 0)
            segments[0].OnIgnited();
    }
}
