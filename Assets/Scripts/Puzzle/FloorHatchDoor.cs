using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 바닥에 뚫린 계단 입구를 덮고 있는 양쪽 여닫이 문이다.
// 평소에는 바닥과 같은 높이로 닫혀 있어 플레이어가 그 위를 걸어 지나갈 수 있고,
// Open()이 호출되면(예: TurbineGenerator의 "작동 시 이벤트") 덜컹거린 뒤 두 문짝이 바깥쪽 위로 젖혀진다.
// 완전히 열리면 문짝 콜라이더를 끄고 입구 포털(EntrancePortal)의 잠금을 푼다. 한 번 열리면 다시 닫히지 않는다.
// 경첩(Hinge)의 회전축은 구멍의 긴 변과 같은 방향(로컬 Z축)이어야 한다. 왼쪽은 +Z, 오른쪽은 -Z 방향으로 회전한다.
public class FloorHatchDoor : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("왼쪽 경첩")]
    private Transform leftHinge;
    // 왼쪽 문짝의 회전 피벗이다. 구멍의 왼쪽 가장자리에 놓고, 문짝은 이 오브젝트의 자식으로 둔다.

    [SerializeField, LabelText("오른쪽 경첩")]
    private Transform rightHinge;
    // 오른쪽 문짝의 회전 피벗이다. 구멍의 오른쪽 가장자리에 놓는다.

    [SerializeField, LabelText("문짝 콜라이더들")]
    private Collider[] leafColliders;
    // 닫혀 있는 동안 플레이어가 밟는 콜라이더다. 열리고 나면 젖혀진 문짝에 플레이어가 걸리지 않도록 꺼 준다.

    [SerializeField, LabelText("입구 포털")]
    private EntrancePortal portal;
    // 문이 완전히 열린 뒤 Unlock()을 호출할 포털이다. 비워두면 잠금 해제는 하지 않는다.

    [SerializeField, LabelText("먼지 파티클 (선택)")]
    private ParticleSystem dustParticle;
    // 열리는 순간 재생할 파티클이다. 비워도 동작한다.

    [Title("소리")]
    [SerializeField, LabelText("열리는 소리")]
    private AudioClip openSound;

    [SerializeField, LabelText("소리 크기"), Range(0f, 1f)]
    private float openSoundVolume = 1f;

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("시작 지연")]
    private float openDelay = 0f;
    // Open() 호출 후 연출이 시작되기까지 기다리는 시간(초)이다.

    [SerializeField, LabelText("덜컹 시간")]
    private float rattleDuration = 0.3f;
    // 열리기 전 문짝이 덜컹거리는 시간이다. 0이면 덜컹 연출을 건너뛴다.

    [SerializeField, LabelText("덜컹 세기(도)")]
    private float rattleStrength = 2f;
    // 값이 클수록 문짝이 더 크게 흔들린다.

    [SerializeField, LabelText("열리는 각도(도)")]
    private float openAngle = 105f;
    // 90보다 크게 주면 문짝이 바깥쪽으로 살짝 더 젖혀져 구멍 위로 넘어오지 않는다.

    [SerializeField, LabelText("열리는 시간")]
    private float openDuration = 0.8f;

    [SerializeField, LabelText("열리는 Ease")]
    private Ease openEase = Ease.OutBack;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("열림 여부")]
    private bool _isOpen;
    // 한 번 열리면 계속 true다. 리스폰해도 닫히지 않는다.

    private Sequence _openSequence;
    // 연출이 중복 실행되지 않도록 현재 Sequence를 저장한다.

    private void OnDestroy()
    {
        _openSequence?.Kill();
    }

    // 문을 연다. 이미 열렸거나 열리는 중이면 무시한다.
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        _openSequence?.Kill();
        _openSequence = DOTween.Sequence();
        _openSequence.AppendInterval(openDelay);

        if (rattleDuration > 0f)
        {
            // 두 문짝이 동시에 덜컹거린다. DOShakeRotation은 끝나면 원래 회전으로 돌아온다.
            _openSequence.Append(leftHinge.DOShakeRotation(rattleDuration, new Vector3(0f, 0f, rattleStrength), 20, 90f, false));
            _openSequence.Join(rightHinge.DOShakeRotation(rattleDuration, new Vector3(0f, 0f, rattleStrength), 20, 90f, false));
        }

        _openSequence.AppendCallback(PlayOpenEffects);

        // 왼쪽은 +Z, 오른쪽은 -Z 방향으로 돌아 두 문짝이 서로 반대편 바깥쪽 위로 젖혀진다.
        _openSequence.Append(leftHinge.DOLocalRotate(new Vector3(0f, 0f, openAngle), openDuration).SetEase(openEase));
        _openSequence.Join(rightHinge.DOLocalRotate(new Vector3(0f, 0f, -openAngle), openDuration).SetEase(openEase));

        _openSequence.OnComplete(OnOpened);
    }

    // 문짝이 젖혀지기 시작하는 순간의 소리와 먼지를 재생한다.
    private void PlayOpenEffects()
    {
        if (openSound != null)
            SfxPlayer.PlayAt(openSound, transform.position, openSoundVolume);

        if (dustParticle != null)
            dustParticle.Play();
    }

    // 완전히 열린 뒤 콜라이더를 끄고 포털을 연다.
    private void OnOpened()
    {
        foreach (Collider leaf in leafColliders)
        {
            if (leaf != null)
                leaf.enabled = false;
        }

        if (portal != null)
            portal.Unlock();
    }

    [Button("열기 테스트 (Play Mode)")]
    private void TestOpen()
    {
        if (!Application.isPlaying) return;
        Open();
    }
}
