using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private PlayerHealth target;
    [SerializeField, Min(1f)] private float detectionRange = 20f;
    [SerializeField, Min(0.5f)] private float attackRange = 1.8f;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1.2f;
    [SerializeField, Min(0f)] private float attackWindup = 0.4f;

    private NavMeshAgent agent;
    private Animator animator;
    private CharacterController targetController;
    private float nextAttackTime;
    private float strikeTime;
    private float nextPathTime;
    private bool windingUp;
    private float staggerUntil;
    private bool HasAnimator => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;

    public void ReactToHit(float duration)
    {
        windingUp = false;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        SetMovingAnimation(false);
        if (HasAnimator) { animator.ResetTrigger("Attack"); animator.Play("Idle", 0, 0f); }
        staggerUntil = Time.time + duration;
        nextAttackTime = Mathf.Max(nextAttackTime, staggerUntil);
    }

    public void SetTarget(PlayerHealth player)
    {
        target = player;
        targetController = player != null ? player.GetComponent<CharacterController>() : null;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        agent.stoppingDistance = attackRange * 0.8f;
        SetTarget(target != null ? target : FindFirstObjectByType<PlayerHealth>());
    }

    private void Update()
    {
        if (!agent.isOnNavMesh) { SetMovingAnimation(false); return; }
        if (Time.time < staggerUntil) { agent.isStopped = true; SetMovingAnimation(false); return; }
        if (target == null || !target.isActiveAndEnabled || target.IsDead)
        {
            agent.isStopped = true;
            SetMovingAnimation(false);
            windingUp = false;
            return;
        }

        Vector3 aimPoint = targetController != null
            ? targetController.bounds.center : target.transform.position + Vector3.up;
        Vector3 eye = transform.position + Vector3.up;
        float distance = Vector3.Distance(eye, aimPoint);
        bool canStrike = distance <= attackRange && HasLineOfSight(eye, aimPoint);

        if (windingUp)
        {
            FaceTarget();
            if (!HasAnimator && Time.time >= strikeTime)
            {
                ResolveAttackHit();
            }
            return;
        }

        if (HasAnimator && (IsAttackState(animator.GetCurrentAnimatorStateInfo(0)) ||
            (animator.IsInTransition(0) && IsAttackState(animator.GetNextAnimatorStateInfo(0)))))
        {
            agent.isStopped = true;
            SetMovingAnimation(false);
            FaceTarget();
            return;
        }

        agent.isStopped = distance > detectionRange || canStrike;
        SetMovingAnimation(!agent.isStopped);
        if (canStrike)
        {
            FaceTarget();
            if (Time.time >= nextAttackTime)
            {
                windingUp = true;
                strikeTime = Time.time + attackWindup;
                if (HasAnimator) animator.SetTrigger("Attack");
            }
        }
        else if (!agent.isStopped && Time.time >= nextPathTime)
        {
            nextPathTime = Time.time + 0.2f;
            agent.SetDestination(target.transform.position);
        }
    }

    private void SetMovingAnimation(bool moving)
    {
        if (HasAnimator) animator.SetBool("IsMoving", moving);
    }

    private static bool IsAttackState(AnimatorStateInfo state) => state.IsName("Attack") || state.IsTag("Attack");

    public void ResolveAttackHit()
    {
        if (!windingUp || Time.time < staggerUntil) return;
        windingUp = false;
        nextAttackTime = Time.time + attackInterval;
        if (target == null || !target.isActiveAndEnabled || target.IsDead) return;
        Vector3 aim = targetController != null ? targetController.bounds.center : target.transform.position + Vector3.up;
        Vector3 eye = transform.position + Vector3.up;
        if (Vector3.Distance(eye, aim) <= attackRange && HasLineOfSight(eye, aim)) target.TakeDamage(damage);
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 destination)
    {
        Vector3 direction = destination - origin;
        return !Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                   direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
               || hit.collider.GetComponentInParent<PlayerHealth>() == target;
    }

    private void FaceTarget()
    {
        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 360f * Time.deltaTime);
    }
}
