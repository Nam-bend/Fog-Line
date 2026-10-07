using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float hitStaggerDuration = 0.45f;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;
    public void RestoreHealth(float health) => CurrentHealth = Mathf.Clamp(health, 0f, maxHealth);
    private Transform visual;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Coroutine reaction;
    private bool alertedByHit;

    private void Awake()
    {
        CurrentHealth = Mathf.Max(1f, maxHealth);
        Renderer body = GetComponentInChildren<Renderer>();
        if (body != null && body.transform != transform)
        {
            visual = body.transform;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        if (!alertedByHit)
        {
            alertedByHit = true;
            AlertSystem.RaiseEnemyAlerted(transform.position);
        }
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        var storyCreature = GetComponent<ForestCreature>();
        if (storyCreature != null) storyCreature.Hit(hitStaggerDuration);
        var ai = GetComponent<EnemyAI>();
        if (reaction != null) StopCoroutine(reaction);
        if (!IsDead)
        {
            if (ai != null) ai.ReactToHit(hitStaggerDuration);
            reaction = StartCoroutine(AnimateHit());
            return;
        }
        if (ai != null) ai.enabled = false;
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        foreach (Collider body in GetComponentsInChildren<Collider>()) body.enabled = false;
        GameManager.IncrementKillCount();
        if (ForestStoryDirector.Instance != null)
            ForestStoryDirector.Instance.EnemyKilled(storyCreature != null ? storyCreature.storyId : "", transform.position);
        AlertSystem.RaiseEnemyAlerted(transform.position);
        reaction = StartCoroutine(AnimateDeath());
    }

    private IEnumerator AnimateHit()
    {
        for (float time = 0f; time < 0.22f; time += Time.deltaTime)
        {
            if (visual != null)
            {
                float kick = Mathf.Sin(time / 0.22f * Mathf.PI);
                visual.localRotation = restRotation * Quaternion.Euler(-9f * kick, 0f, 3f * kick);
                visual.localPosition = restPosition + Vector3.back * (0.055f * kick);
            }
            yield return null;
        }
        RestoreVisual();
        reaction = null;
    }

    private IEnumerator AnimateDeath()
    {
        Transform body = visual != null ? visual : transform;
        Vector3 from = body.localPosition;
        Quaternion rotation = body.localRotation;
        Quaternion target = (visual != null ? restRotation : rotation) * Quaternion.Euler(0f, 0f, 85f);
        Vector3 landing = from + (visual != null ? Vector3.down * 0.6f : Vector3.zero);
        for (float time = 0f; time < 0.6f; time += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / 0.6f);
            body.localRotation = Quaternion.Slerp(rotation, target, t);
            body.localPosition = Vector3.Lerp(from, landing, t);
            yield return null;
        }
        body.localRotation = target;
        body.localPosition = landing;
        yield return new WaitForSeconds(1.4f);
        Destroy(gameObject);
    }

    private void RestoreVisual()
    {
        if (visual == null) return;
        visual.localPosition = restPosition;
        visual.localRotation = restRotation;
    }
    private void OnDisable()
    {
        if (reaction != null) StopCoroutine(reaction);
        reaction = null;
        if (!IsDead) RestoreVisual();
    }
}
