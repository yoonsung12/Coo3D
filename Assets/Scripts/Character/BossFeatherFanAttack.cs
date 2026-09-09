using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 평범한 패턴 중 원거리 공격 "깃털 부채꼴 발사"를 담당한다. 기존 Boss.TryFireProjectile()과
// 동일하게 BossProjectile을 재사용하되, 한 번에 여러 각도로 부채꼴 발사한다는 점이 다르다.
public class BossFeatherFanAttack : MonoBehaviour, IBossBasicAttack
{
    [Title("발사 설정")]
    [SerializeField, LabelText("투사체 프리팹")]
    private BossProjectile projectilePrefab;
    // Inspector에서 Assets/Prefabs/Boss/BossProjectile.prefab을 연결한다.

    [SerializeField, LabelText("발사 위치")]
    private Transform spawnPoint;
    // Inspector에서 Boss 자식의 ProjectileSpawnPoint를 연결한다.

    [SerializeField, LabelText("깃털 개수")]
    private int featherCount = 5;

    [SerializeField, LabelText("전체 부채꼴 각도")]
    private float spreadAngle = 60f;
    // 값이 클수록 깃털 사이 틈이 넓어져 회피하기 쉬워진다.

    [SerializeField, LabelText("투사체 속도")]
    private float projectileSpeed = 10f;

    [SerializeField, LabelText("투사체 데미지")]
    private float projectileDamage = 20f;
    // Player 체력 하트 1칸(20)에 맞춘 값이다.

    [SerializeField, LabelText("발사 후 대기 시간")]
    private float recoveryDuration = 0.3f;
    // 발사 직후 다음 공격으로 넘어가기 전 잠깐 멈추는 회복 시간이다.

    public void Execute(float dir, Action onFinished)
    {
        StartCoroutine(FanRoutine(dir, onFinished));
    }

    private IEnumerator FanRoutine(float dir, Action onFinished)
    {
        FireFan(dir);
        yield return new WaitForSeconds(recoveryDuration);
        onFinished?.Invoke();
    }

    private void FireFan(float dir)
    {
        if (projectilePrefab == null || spawnPoint == null) return;

        // spawnPoint는 EnemyMovement.attackAnchor와 같은 이유로 보스 회전에 영향을 받는다.
        // 회전된 world position을 그대로 쓰지 않고 로컬 오프셋 크기 + 현재 방향(dir)으로 직접 계산한다.
        Vector3 baseOffset = spawnPoint.localPosition;
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Abs(baseOffset.x) * dir, baseOffset.y, baseOffset.z);

        Vector3 baseDir = new Vector3(dir, 0f, 0f);
        float half = (featherCount - 1) * 0.5f;
        float step = featherCount > 1 ? spreadAngle / (featherCount - 1) : 0f;

        for (int i = 0; i < featherCount; i++)
        {
            // 가운데(baseDir)를 기준으로 위아래로 각도를 벌려서 부채꼴을 만든다.
            float angle = (i - half) * step;
            Vector3 launchDir = Quaternion.Euler(0f, 0f, angle) * baseDir;

            BossProjectile projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            projectile.Launch(launchDir, projectileSpeed, projectileDamage);
        }
    }

    [Button("깃털 부채꼴 테스트 (오른쪽)")]
    private void TestFan() => FireFan(1f);
}
