using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MotherStagger))]
public sealed class MotherAI : MonoBehaviour
{
    private enum State { Dormant, Chase, Searching }

    [SerializeField] private PlayerHealth target;
    [SerializeField, Min(1f)] private float detectionRange = 30f;
    [SerializeField, Min(0f)] private float loseSightDelay = 0.75f;
    [SerializeField, Min(0.1f)] private float searchDuration = 5f;
    [SerializeField, Min(0.1f)] private float searchRadius = 5f;
    [SerializeField, Min(0.1f)] private float searchPointArrival = 0.6f;
    [SerializeField, Min(0.5f)] private float attackRange = 2.6f;
    [SerializeField, Min(0f)] private float attackDamage = 20f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;

    private NavMeshAgent agent;
    private MotherStagger stagger;
    private Animator animator;
    private CharacterController targetController;
    private State state;
    private Vector3 lastKnownPosition;
    private Vector3 searchCenter;
    private float lostSightTime;
    private float searchUntil;
    private bool hasSearchPoint;
    private bool attacking;
    private bool enteredAttack;
    private float attackRequestedAt;
    private float nextAttackTime;

    private bool HasAnimator => animator != null && animator.isActiveAndEnabled
        && animator.runtimeAnimatorController != null;

    public string CurrentState => state.ToString();

    public void SetTarget(PlayerHealth player)
    {
        target = player;
        targetController = player != null ? player.GetComponent<CharacterController>() : null;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stagger = GetComponent<MotherStagger>();
        animator = GetComponentInChildren<Animator>();
        // Animation events must be received on the Animator's own object.
        if (animator != null && animator.GetComponent<MonsterAttackEvents>() == null)
            animator.gameObject.AddComponent<MonsterAttackEvents>();
        agent.stoppingDistance = attackRange * .8f;
        SetTarget(target != null ? target : FindFirstObjectByType<PlayerHealth>());
        state = State.Dormant;
        StopAgent();
    }

    private void OnEnable() => AlertSystem.OnEnemyAlerted += HandleEnemyAlert;

    private void OnDisable()
    {
        AlertSystem.OnEnemyAlerted -= HandleEnemyAlert;
        CancelAttack();
        StopAgent();
    }

    private void Update()
    {
        if (target == null || !target.isActiveAndEnabled || target.IsDead)
        {
            CancelAttack();
            StopAgent();
            SetMovingAnimation(false);
            return;
        }
        if (stagger != null && stagger.IsStaggered)
        {
            CancelAttack();
            StopAgent();
            SetMovingAnimation(false);
            return;
        }

        if (UpdateAttack()) return;

        switch (state)
        {
            case State.Dormant:
                StopAgent();
                SetMovingAnimation(false);
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Searching:
                UpdateSearch();
                break;
        }
    }

    private void HandleEnemyAlert(Vector3 position)
    {
        lastKnownPosition = position;
        lostSightTime = 0f;
        searchCenter = position;
        searchUntil = 0f;
        hasSearchPoint = false;
        state = State.Chase;
    }

    private void UpdateChase()
    {
        if (target == null)
        {
            BeginSearch(lastKnownPosition);
            return;
        }

        Vector3 eye = transform.position + Vector3.up;
        Vector3 targetPoint = GetTargetPoint();
        bool visible = Vector3.Distance(eye, targetPoint) <= detectionRange
            && HasLineOfSight(eye, targetPoint);

        if (visible)
        {
            lostSightTime = 0f;
            lastKnownPosition = targetPoint;
            if (IsTargetInAttackRange(targetPoint))
            {
                StopAgent();
                SetMovingAnimation(false);
                FaceTarget();
                if (HasAnimator && Time.time >= nextAttackTime)
                {
                    attacking = true;
                    enteredAttack = false;
                    attackRequestedAt = Time.time;
                    animator.SetTrigger("Attack");
                }
            }
            else SetMovingAnimation(MoveTo(target.transform.position));
            return;
        }

        lostSightTime += Time.deltaTime;
        if (lostSightTime >= loseSightDelay) BeginSearch(lastKnownPosition);
        else SetMovingAnimation(MoveTo(lastKnownPosition));
    }

    private void BeginSearch(Vector3 center)
    {
        state = State.Searching;
        searchCenter = center;
        searchUntil = Time.time + searchDuration;
        hasSearchPoint = false;
    }

    private void UpdateSearch()
    {
        if (Time.time >= searchUntil)
        {
            state = State.Dormant;
            StopAgent();
            SetMovingAnimation(false);
            return;
        }

        if (target != null)
        {
            Vector3 eye = transform.position + Vector3.up;
            Vector3 targetPoint = GetTargetPoint();
            if (Vector3.Distance(eye, targetPoint) <= detectionRange
                && HasLineOfSight(eye, targetPoint))
            {
                state = State.Chase;
                lostSightTime = 0f;
                return;
            }
        }

        if (!hasSearchPoint || ReachedSearchPoint())
            hasSearchPoint = TryPickSearchPoint(out Vector3 point) && MoveTo(point);
        else MoveTo(agent.destination);
        SetMovingAnimation(hasSearchPoint && agent.isOnNavMesh && !ReachedSearchPoint());
    }

    private bool TryPickSearchPoint(out Vector3 point)
    {
        Vector2 offset = Random.insideUnitCircle * searchRadius;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.SamplePosition(searchCenter + new Vector3(offset.x, 0f, offset.y),
            out NavMeshHit hit, searchRadius, filter))
        {
            point = hit.position;
            return true;
        }
        bool found = NavMesh.SamplePosition(searchCenter, out hit, searchRadius, filter);
        point = found ? hit.position : searchCenter;
        return found;
    }

    private bool ReachedSearchPoint() => agent.isOnNavMesh
        && !agent.pathPending
        && agent.remainingDistance <= Mathf.Max(searchPointArrival, agent.stoppingDistance + .05f);

    private bool MoveTo(Vector3 point)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
        agent.isStopped = false;
        return agent.SetDestination(point);
    }

    private void SetMovingAnimation(bool moving)
    {
        if (HasAnimator) animator.SetBool("IsMoving", moving);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    private Vector3 GetTargetPoint()
    {
        if (targetController == null) targetController = target.GetComponent<CharacterController>();
        return targetController != null ? targetController.bounds.center : target.transform.position + Vector3.up;
    }

    private bool IsPlayingAttack() => HasAnimator &&
        (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") ||
         (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack")));

    private bool UpdateAttack()
    {
        if (!attacking) return false;
        bool playing = IsPlayingAttack();
        enteredAttack |= playing;
        if (!HasAnimator || (!playing && (enteredAttack || Time.time - attackRequestedAt > 1f)))
        {
            CancelAttack();
            nextAttackTime = Time.time + attackCooldown;
            return false;
        }
        StopAgent();
        SetMovingAnimation(false);
        FaceTarget();
        return true;
    }

    private void CancelAttack()
    {
        attacking = false;
        enteredAttack = false;
        if (HasAnimator) animator.ResetTrigger("Attack");
    }

    // Each authored strike can land, including both hits in Attack_Combo.
    public void ResolveAttackHit()
    {
        if (!isActiveAndEnabled || !attacking || !IsPlayingAttack()
            || (stagger != null && stagger.IsStaggered)
            || target == null || !target.isActiveAndEnabled || target.IsDead) return;
        Vector3 eye = transform.position + Vector3.up;
        Vector3 aim = GetTargetPoint();
        if (IsTargetInAttackRange(aim) && HasLineOfSight(eye, aim))
            target.TakeDamage(attackDamage);
    }

    // Melee contact is judged on the ground plane and includes a small part of
    // the player's capsule. This keeps hits consistent on slopes and at corners.
    private bool IsTargetInAttackRange(Vector3 targetPoint)
    {
        Vector3 delta = targetPoint - transform.position;
        delta.y = 0f;
        float targetRadius = targetController != null ? targetController.radius : .5f;
        return delta.magnitude <= attackRange + targetRadius * .35f;
    }

    private void FaceTarget()
    {
        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 360f * Time.deltaTime);
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 destination)
    {
        Vector3 direction = destination - origin;
        if (direction.sqrMagnitude < 0.0001f) return true;
        return !Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
            direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || hit.collider.GetComponentInParent<PlayerHealth>() == target;
    }
}
