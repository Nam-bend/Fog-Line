


using UnityEngine;

[DisallowMultipleComponent]
public sealed class MotherStagger : MonoBehaviour
{
    [SerializeField, Min(0f)] private float staggerDuration = 0.75f;
    [SerializeField] private Animator animator;
    [SerializeField] private string staggerTrigger = "Stagger";

    public bool IsStaggered { get; private set; }
    public float StaggerRemaining => Mathf.Max(0f, staggerUntil - Time.time);
    private float staggerUntil;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public void TriggerStagger()
    {
        IsStaggered = true;
        staggerUntil = Mathf.Max(staggerUntil, Time.time + staggerDuration);
        if (animator != null && !string.IsNullOrWhiteSpace(staggerTrigger))
            animator.SetTrigger(staggerTrigger);
    }

    public void Tick()
    {
        if (IsStaggered && Time.time >= staggerUntil) IsStaggered = false;
    }

    private void Update() => Tick();
}
