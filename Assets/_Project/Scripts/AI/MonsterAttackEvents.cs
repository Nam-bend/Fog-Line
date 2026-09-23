using UnityEngine;

// Animation events are delivered to the GameObject containing the Animator.
public sealed class MonsterAttackEvents : MonoBehaviour
{
    public void OnMonsterStrike()
    {
        var mother = GetComponentInParent<MotherAI>();
        if (mother != null && mother.isActiveAndEnabled)
        {
            mother.ResolveAttackHit();
            return;
        }
        var enemy = GetComponentInParent<EnemyAI>();
        if (enemy != null && enemy.isActiveAndEnabled) enemy.ResolveAttackHit();
    }
}
