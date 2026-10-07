using UnityEngine;
using UnityEngine.AI;

// Resolve navigation from the floor, not the height of a falling player's pivot.
public static class EnemySpawnPlacement
{
    private const float FloorProbeDistance = 64f;
    private const float NavTolerance = 2f;

    public static bool TryFind(Vector3 playerFeet, Vector3 forward, Transform player,
        Transform placedEnemy, float preferredDistance, NavMeshQueryFilter filter,
        out Vector3 position, out string failure)
    {
        position = default;
        if (!TryFloorNav(playerFeet, player, placedEnemy, filter, out var start))
        {
            failure = $"No walkable floor below Player at {playerFeet} for agent {filter.agentTypeID}.";
            return false;
        }

        var path = new NavMeshPath();
        if (placedEnemy != null && TryFloorNav(placedEnemy.position, player, placedEnemy, filter, out var placed)
            && Reachable(placed.position, start.position, filter, path))
        {
            position = placed.position;
            failure = null;
            return true;
        }

        forward.y = 0;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        float distance = Mathf.Max(3f, preferredDistance);
        // A single 8 m ring can miss every valid point on hilly or narrow ground.
        var rings = new[] { distance, distance * .65f, distance * 1.35f };
        for (int pass = 0; pass < 2; pass++)
        foreach (float ring in rings)
        for (int i = 0; i < 16; i++)
        {
            Vector3 candidate = start.position + Quaternion.Euler(0, i * 22.5f, 0) * forward * ring;
            // Probe the candidate's own elevation rather than copying the player's Y.
            candidate.y += 12f;
            if (!TryFloorNav(candidate, player, placedEnemy, filter, out var hit)
                || !Reachable(hit.position, start.position, filter, path)) continue;
            Vector3 separation = hit.position - start.position;
            separation.y = 0;
            if (separation.sqrMagnitude < 9f) continue;
            if (pass == 0 && Physics.Linecast(start.position + Vector3.up * 1.5f,
                    hit.position + Vector3.up, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                continue;
            position = hit.position;
            failure = null;
            return true;
        }
        failure = $"Floor found at {start.position}, but no connected spawn at least 3 m away for agent {filter.agentTypeID}.";
        return false;
    }

    private static bool Reachable(Vector3 point, Vector3 start, NavMeshQueryFilter filter, NavMeshPath path)
    {
        Vector3 separation = point - start;
        separation.y = 0;
        return separation.sqrMagnitude >= 9f
            && NavMesh.CalculatePath(point, start, filter, path)
            && path.status == NavMeshPathStatus.PathComplete;
    }

    public static bool TryFloorNav(Vector3 point, Transform player, Transform enemy,
        NavMeshQueryFilter filter, out NavMeshHit nav, float navTolerance = NavTolerance)
    {
        // A downward ray from a buried spawn cannot hit Terrain's upper surface.
        foreach (var terrain in Terrain.activeTerrains)
        {
            var local = point - terrain.transform.position;
            var size = terrain.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x >= size.x || local.z >= size.z) continue;
            int resolution = terrain.terrainData.holesResolution;
            if (terrain.terrainData.IsHole(Mathf.FloorToInt(local.x / size.x * resolution),
                    Mathf.FloorToInt(local.z / size.z * resolution))) continue;
            point.y = Mathf.Max(point.y, terrain.SampleHeight(point) + terrain.transform.position.y);
        }
        // These queries run once at spawn, so use all hits to avoid buffer truncation.
        var hits = Physics.RaycastAll(point + Vector3.up * .5f, Vector3.down,
            FloorProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Vector3 floor = point;
        foreach (var hit in hits)
        {
            if ((player != null && hit.transform.IsChildOf(player))
                || (enemy != null && hit.transform.IsChildOf(enemy)) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            floor = hit.point;
        }
        // With no physical floor, do not select a distant island by broadening the radius.
        return NavMesh.SamplePosition(floor, out nav, navTolerance, filter);
    }
}
