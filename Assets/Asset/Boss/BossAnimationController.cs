
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Boss))]
public class BossAnimationController : MonoBehaviour
{
    [Title("Animator 연결")]
    [SerializeField] private Animator animator;

    private Boss boss;
    private Rigidbody rb;
    private bool isDead;
    private bool actionPlaying;

    private static readonly int SpeedID =
        Animator.StringToHash("Speed");

    private void Awake()
    {
        boss = GetComponent<Boss>();
        rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (boss == null) return;

        boss.OnPatternTriggered += OnSeasonStarted;
        boss.OnPatternEnded += OnSeasonEnded;
    }

    private void OnDisable()
    {
        if (boss == null) return;

        boss.OnPatternTriggered -= OnSeasonStarted;
        boss.OnPatternEnded -= OnSeasonEnded;
    }

    private void Update()
    {
        if (animator == null || boss == null) return;

        if (boss.IsDead)
        {
            if (!isDead)
            {
                isDead = true;
                PlayInternal("Death");
            }
            return;
        }

        if (actionPlaying || boss.IsInvincible)
        {
            animator.SetFloat(SpeedID, 0f);
            return;
        }

        // 기존 Rigidbody 이동을 읽어서 Walk 재생
        Vector3 velocity = rb != null
            ? rb.linearVelocity : Vector3.zero;

        velocity.y = 0f;

        animator.SetFloat(
            SpeedID,
            velocity.magnitude > 0.1f ? 1f : 0f
        );
    }

    private void PlayInternal(string state)
    {
        if (animator == null) return;

        int hash = Animator.StringToHash(
            "Base Layer." + state
        );

        if (!animator.HasState(0, hash))
        {
            Debug.LogWarning(
                $"Boss Animator State 없음: {state}", this
            );
            return;
        }

        animator.CrossFadeInFixedTime(hash, 0.05f, 0, 0f);
    }

    public void PlayReady()
    {
        if (isDead) return;
        actionPlaying = true;
        PlayInternal("Attack_Ready");
    }

    public void PlayPeck() => PlayAttack("Attack_Peck");
    public void PlayWing() => PlayAttack("Attack_Wing");
    public void PlayShoot() => PlayAttack("Attack_Shoot");
    public void PlayStomp() => PlayAttack("Autumn_Stomp");

    private void PlayAttack(string state)
    {
        if (isDead) return;
        actionPlaying = true;
        PlayInternal(state);
    }

    public void PlayFly()
    {
        if (isDead) return;
        actionPlaying = true;
        PlayInternal("Fly");
    }

    public void PlaySpringDash()
    {
        PlayAttack("Spring_Dash");
    }

    public void PlaySummerWing()
    {
        PlayAttack("Summer_Wing");
    }

    public void PlayWinterWing()
    {
        PlayAttack("Winter_Wing");
    }

    public void EndAction()
    {
        if (isDead) return;

        actionPlaying = false;
        animator.SetFloat(SpeedID, 0f);
        PlayInternal("Idle");
    }

    private void OnSeasonStarted(Boss.SeasonPattern season)
    {
        switch (season)
        {
            case Boss.SeasonPattern.Spring:
            case Boss.SeasonPattern.Summer:
                PlayFly();
                break;

            case Boss.SeasonPattern.Autumn:
            case Boss.SeasonPattern.Winter:
                PlayReady();
                break;
        }
    }

    private void OnSeasonEnded()
    {
        EndAction();
    }
}
