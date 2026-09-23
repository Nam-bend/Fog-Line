using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Builds navigation and activates the scene enemy, or creates a fallback on Play.
public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private GameObject sceneEnemy;
    [SerializeField, Min(3f)] private float spawnDistance = 8f;
    private Material enemyMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSampleSceneSpawner()
    {
        // An already-open scene can still contain the old hierarchy after script import.
        // Keep this fallback scoped to the demo scene and never create a second spawner.
        if (SceneManager.GetActiveScene().name != "SampleScene"
            || FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include) != null)
            return;

        new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerHealth>();
        if (player == null)
        {
            Debug.LogError("EnemySpawner needs a PlayerHealth in the scene.", this);
            return;
        }

        // The moving player must not cut a permanent hole in the baked walking area.
        var modifier = player.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = player.gameObject.AddComponent<NavMeshModifier>();
        bool previouslyIgnored = modifier.ignoreFromBuild;
        modifier.ignoreFromBuild = true;
        var mothers = FindObjectsByType<MotherAI>(FindObjectsSortMode.None);
        var motherModifiers = new NavMeshModifier[mothers.Length];
        var motherIgnored = new bool[mothers.Length];
        for (int i = 0; i < mothers.Length; i++)
        {
            var motherModifier = mothers[i].GetComponent<NavMeshModifier>();
            if (motherModifier == null) motherModifier = mothers[i].gameObject.AddComponent<NavMeshModifier>();
            motherModifiers[i] = motherModifier;
            motherIgnored[i] = motherModifier.ignoreFromBuild;
            motherModifier.ignoreFromBuild = true;
        }
        var surface = gameObject.AddComponent<NavMeshSurface>();
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        Physics.SyncTransforms();
        surface.BuildNavMesh();
        modifier.ignoreFromBuild = previouslyIgnored;
        for (int i = 0; i < mothers.Length; i++)
        {
            motherModifiers[i].ignoreFromBuild = motherIgnored[i];
            mothers[i].SetTarget(player);
            var motherAgent = mothers[i].GetComponent<NavMeshAgent>();
            if (motherAgent != null && motherAgent.enabled
                && NavMesh.SamplePosition(mothers[i].transform.position, out NavMeshHit floor, 4f, motherAgent.areaMask))
                motherAgent.Warp(floor.position);
        }

        if (!TrySpawnPosition(out Vector3 position))
        {
            Debug.LogError("No reachable floor found for the enemy near Player.", this);
            return;
        }

        var enemy = sceneEnemy != null ? sceneEnemy : new GameObject("Enemy - Melee AI");
        enemy.transform.SetParent(transform);
        enemy.transform.position = position;
        if (sceneEnemy == null)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(enemy.transform, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                enemyMaterial = new Material(shader);
                enemyMaterial.color = new Color(0.85f, 0.08f, 0.06f);
                body.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
            }
        }
        EnsureEnemyCollider(enemy);

        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null) agent = enemy.AddComponent<NavMeshAgent>();
        agent.height = 2f;
        agent.radius = 0.4f;
        agent.speed = 3f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        var enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI == null) enemyAI = enemy.AddComponent<EnemyAI>();
        enemyAI.SetTarget(player);
        Debug.Log($"Enemy spawned at {position}. Look for EnemySpawner > Enemy - Melee AI in Hierarchy.", enemy);
    }

    private static void EnsureEnemyCollider(GameObject enemy)
    {
        if (enemy.GetComponentInChildren<Collider>() != null) return;

        // The scene demo enemy is a mesh-only object. Without a collider, the
        // weapon ray cannot hit it and EnemyHealth will never receive damage.
        Renderer body = enemy.GetComponentInChildren<Renderer>();
        GameObject target = body != null ? body.gameObject : enemy;
        var collider = target.AddComponent<CapsuleCollider>();
        collider.center = Vector3.zero;
        collider.radius = 0.5f;
        collider.height = 2f;
        collider.direction = 1;
    }

    private bool TrySpawnPosition(out Vector3 position)
    {
        position = default;
        if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit start, 4f, NavMesh.AllAreas))
            return false;
        var path = new NavMeshPath();
        if (sceneEnemy != null
            && NavMesh.SamplePosition(sceneEnemy.transform.position, out NavMeshHit placed, 3f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(placed.position, start.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete)
        {
            position = placed.position;
            return true;
        }
        var camera = player.GetComponentInChildren<Camera>();
        Vector3 forward = camera != null ? camera.transform.forward : player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = player.transform.forward;
        // Prefer an unobstructed spot in front of the camera, then try other directions.
        for (int attempt = 0; attempt < 2; attempt++)
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * forward;
            direction.y = 0f;
            Vector3 candidate = start.position + direction.normalized * spawnDistance;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)
                && Vector3.Distance(start.position, hit.position) >= 3f
                && NavMesh.CalculatePath(hit.position, start.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete)
            {
                Vector3 eye = camera != null ? camera.transform.position : start.position + Vector3.up;
                if (attempt == 0 && Physics.Linecast(eye, hit.position + Vector3.up,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                position = hit.position;
                return true;
            }
        }
        return false;
    }

    private void OnDestroy()
    {
        if (enemyMaterial != null) Destroy(enemyMaterial);
    }
}
