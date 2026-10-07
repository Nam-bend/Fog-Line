using UnityEngine;
using UnityEngine.AI;

// Story-specific perception feeds the existing combat AI. Other scenes keep their AI.
[RequireComponent(typeof(EnemyAI), typeof(EnemyHealth), typeof(NavMeshAgent))]
[DefaultExecutionOrder(-40)]
public sealed class ForestCreature : MonoBehaviour
{
    public string storyId;
    public bool returnOnly;
    public bool powerWitness;
    public Vector3 feedingPosition;
    public Vector3 retreatPosition;
    public float viewRange = 11f;
    public float hearingRange = 4f;
    public float fieldOfView = 110f;
    public string Perception { get; private set; } = "Feeding";
    private EnemyAI combat;
    private EnemyHealth health;
    private NavMeshAgent agent;
    private PlayerHealth player;
    private Animator animator;
    private CharacterController playerController;
    private EnemyNavigation navigation;
    private Vector3 lastKnown;
    private float lostTime, searchUntil, staggerUntil, nextSense, awareness;
    private bool visible;
    private bool alerted, searching;

    private void Awake()
    {
        combat = GetComponent<EnemyAI>(); health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>(); animator = GetComponentInChildren<Animator>();
        player = FindFirstObjectByType<PlayerHealth>();
        playerController = player != null ? player.GetComponent<CharacterController>() : null;
        navigation=GetComponent<EnemyNavigation>();
        if(navigation==null) navigation=gameObject.AddComponent<EnemyNavigation>();
        nextSense=Time.time+Mathf.Abs(GetInstanceID()%7)*.02f;
        combat.enabled = false;
        feedingPosition = transform.position;
    }
    private void OnEnable() => ForestNoise.Heard += Hear;
    private void OnDisable() => ForestNoise.Heard -= Hear;
    public void Hit(float duration)
    {
        staggerUntil = Time.time + duration;
        if (player != null) { lastKnown = player.transform.position; searching = true; searchUntil=Time.time+8; awareness=1; }
    }
    private void Hear(Vector3 position, float radius)
    {
        if (health == null || health.IsDead || !isActiveAndEnabled) return;
        if (Vector3.Distance(transform.position, position) > radius) return;
        if (alerted && visible) return;
        lastKnown = position; searching = true; searchUntil = Time.time + 8;
    }
    private void Update()
    {
        if (health.IsDead || player == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (player.IsDead) { SetCombat(false); Stop("Feeding"); return; }
        var story = ForestStoryDirector.Instance;
        if (returnOnly && story != null && story.State.stage < 3) { SetCombat(false); Stop("Feeding"); return; }
        if (Time.time < staggerUntil) { SetCombat(false); Stop("Staggered"); return; }
        if (powerWitness && story != null && story.PowerRunning)
        {
            SetCombat(false);
            if (Vector3.Distance(transform.position, story.PowerPoint) < 9f) Move(retreatPosition, "Retreating from vibration");
            else Stop("Waiting on soft ground");
            return;
        }
        // Stagger checks across creatures, with range/FOV rejection before physics.
        if(Time.time>=nextSense)
        {
            const float interval=.15f;
            nextSense=Time.time+interval;
            Vector3 delta=player.transform.position-transform.position;
            Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up);
            visible=false;
            if(delta.sqrMagnitude<viewRange*viewRange && Vector3.Angle(transform.forward,flat)<fieldOfView*.5f)
            {
                Vector3 eye=transform.position+Vector3.up*1.15f;
                Vector3 aim=playerController!=null ? playerController.bounds.center : player.transform.position+Vector3.up;
                visible=!Physics.Linecast(eye,aim,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                    || hit.collider.GetComponentInParent<PlayerHealth>()==player;
            }
            float speed=playerController!=null ? Vector3.ProjectOnPlane(playerController.velocity,Vector3.up).magnitude : 0;
            float audibleRange=hearingRange*Mathf.Clamp01(speed/3f);
            if(speed>.7f && delta.sqrMagnitude<audibleRange*audibleRange && !visible)
                Hear(player.transform.position,audibleRange);
            awareness=Mathf.Clamp01(awareness+(visible ? interval/.45f : -interval/.8f));
            if(visible)
            {
                lastKnown=player.transform.position;
                if(awareness>=1f || alerted) { alerted=true; searching=false; lostTime=0; }
            }
        }
        if(visible && !alerted) { SetCombat(false); Stop("Suspicious"); return; }
        if (alerted)
        {
            if (visible) { SetCombat(true); Perception = "Chasing"; return; }
            lostTime += Time.deltaTime;
            SetCombat(false);
            if (lostTime < .6f) { Move(lastKnown, "Investigating"); return; }
            alerted = false; searching = true; searchUntil = Time.time + 8f;
        }
        SetCombat(false);
        if (searching && Time.time < searchUntil)
        {
            if (Vector3.Distance(transform.position,lastKnown) > 1.1f) Move(lastKnown,"Searching");
            else { Stop("Searching"); transform.Rotate(0, 45f * Time.deltaTime, 0); }
        }
        else
        {
            searching = false;
            if (Vector3.Distance(transform.position,feedingPosition) > 1.5f) Move(feedingPosition,"Returning");
            else Stop("Feeding");
        }
    }
    private void SetCombat(bool enabled)
    {
        if (combat.enabled != enabled) { combat.CancelPendingAttack(); combat.enabled = enabled; }
    }
    private void Move(Vector3 point, string state)
    {
        Perception = state;
        navigation.Move(point,.35f);
        if (animator != null) animator.SetBool("IsMoving", navigation.Moving);
    }
    private void Stop(string state)
    {
        Perception = state; navigation.Stop();
        if (animator != null) animator.SetBool("IsMoving",false);
    }
}
