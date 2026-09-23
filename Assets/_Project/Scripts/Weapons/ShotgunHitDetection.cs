using System.Collections.Generic;
using UnityEngine;

// Resolves one shot before feedback is played. Each enemy receives one damage call,
// even when several pellets hit it, so hit reactions are not restarted per pellet.
public sealed class ShotgunHitDetection
{
    private const float Range = 65f;
    private const float FullDamageRange = 10f;
    private const float MinimumDamageRange = 55f;
    private const float MinimumDamageMultiplier = 0.2f;
    private readonly Dictionary<EnemyHealth, float> damageByEnemy = new Dictionary<EnemyHealth, float>(8);
    private readonly HashSet<MotherStagger> hitMothers = new HashSet<MotherStagger>();
    private readonly RaycastHit[] visibleImpacts = new RaycastHit[3];

    public Vector3 Direction { get; private set; }
    public bool MuzzleBlocked { get; private set; }
    public int ImpactCount { get; private set; }
    public bool HitEnemy => damageByEnemy.Count > 0 || hitMothers.Count > 0;
    public bool KilledEnemy { get; private set; }

    public RaycastHit GetImpact(int index) => visibleImpacts[index];

    public void Fire(Transform camera, Vector3 origin, Transform owner,
        int pelletCount, float spreadDegrees, float damage)
    {
        damageByEnemy.Clear();
        hitMothers.Clear();
        ImpactCount = 0;
        KilledEnemy = false;

        Vector3 eye = camera.position;
        Vector3 target = eye + camera.forward * Range;
        if (FireProjectile.FindHit(eye, target, owner, 0.001f, out RaycastHit hit))
            target = hit.point;

        Vector3 direction = target - origin;
        Direction = direction.sqrMagnitude < 0.0001f ? camera.forward : direction.normalized;
        MuzzleBlocked = FireProjectile.FindHit(eye, origin, owner, 0.005f, out RaycastHit obstruction);
        Quaternion aim = Quaternion.LookRotation(Direction);
        int pellets = Mathf.Clamp(pelletCount, 4, 16);
        float spreadRadius = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);

        for (int i = 0; i < pellets; i++)
        {
            Vector2 spread = Random.insideUnitCircle * spreadRadius;
            Vector3 ray = aim * new Vector3(spread.x, spread.y, 1f).normalized;
            RaycastHit impact = obstruction;
            if (!MuzzleBlocked && !FireProjectile.FindHit(origin, origin + ray * Range, owner, 0.003f, out impact))
                continue;

            AccumulateDamage(impact, eye, damage / pellets);
            if (ImpactCount < visibleImpacts.Length)
                visibleImpacts[ImpactCount++] = impact;
        }

        foreach (var entry in damageByEnemy)
        {
            entry.Key.TakeDamage(entry.Value);
            KilledEnemy |= entry.Key.IsDead;
        }
    }

    private void AccumulateDamage(RaycastHit impact, Vector3 eye, float pelletDamage)
    {
        var mother = impact.collider.GetComponentInParent<MotherStagger>();
        if (mother != null)
        {
            if (hitMothers.Add(mother)) mother.TriggerStagger();
            return;
        }

        var enemy = impact.collider.GetComponentInParent<EnemyHealth>();
        if (enemy == null || enemy.IsDead) return;

        float distance = Vector3.Distance(eye, impact.point);
        float falloff = Mathf.Lerp(1f, MinimumDamageMultiplier,
            Mathf.InverseLerp(FullDamageRange, MinimumDamageRange, distance));
        damageByEnemy.TryGetValue(enemy, out float previous);
        damageByEnemy[enemy] = previous + pelletDamage * falloff;
    }
}
