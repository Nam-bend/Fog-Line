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
    private string spawnFailure;

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
        var existingAgent = sceneEnemy != null ? sceneEnemy.GetComponent<NavMeshAgent>() : null;
        int agentType = existingAgent != null ? existingAgent.agentTypeID : 0;
        NavMeshSurface surface = null;
        foreach (var candidate in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
            if (candidate.agentTypeID == agentType)
            {
                surface = candidate;
                if (surface.navMeshData != null) break;
            }
        if (surface == null || surface.navMeshData == null)
        {
            if (surface == null)
            {
                surface = gameObject.AddComponent<NavMeshSurface>();
                surface.agentTypeID = agentType;
            }
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
        }
        modifier.ignoreFromBuild = previouslyIgnored;
        for (int i = 0; i < mothers.Length; i++)
        {
            motherModifiers[i].ignoreFromBuild = motherIgnored[i];
            mothers[i].SetTarget(player);
            var motherAgent = mothers[i].GetComponent<NavMeshAgent>();
            if (motherAgent != null && motherAgent.enabled
                && EnemySpawnPlacement.TryFloorNav(mothers[i].transform.position, player.transform,
                    mothers[i].transform,
                    new NavMeshQueryFilter { agentTypeID = motherAgent.agentTypeID, areaMask = motherAgent.areaMask },
                    out NavMeshHit floor, 4f))
                motherAgent.Warp(floor.position);
        }

        if (!TrySpawnPosition(out Vector3 position))
        {
            Debug.LogError("Enemy spawn failed: " + spawnFailure, this);
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
        if (!agent.enabled) agent.enabled = true;
        if (!agent.Warp(position))
        {
            Debug.LogError($"Enemy could not attach to NavMesh at {position} (agent {agent.agentTypeID}).", enemy);
            return;
        }
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
        var agent = sceneEnemy != null ? sceneEnemy.GetComponent<NavMeshAgent>() : null;
        var filter = new NavMeshQueryFilter { agentTypeID = agent != null ? agent.agentTypeID : 0,
            areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas };
        var controller = player.GetComponent<CharacterController>();
        Vector3 feet = controller != null ? controller.bounds.center - Vector3.up * controller.bounds.extents.y
            : player.transform.position;
        var camera = player.GetComponentInChildren<Camera>();
        Vector3 forward = camera != null ? camera.transform.forward : player.transform.forward;
        return EnemySpawnPlacement.TryFind(feet, forward, player.transform,
            sceneEnemy != null ? sceneEnemy.transform : null, spawnDistance, filter, out position, out spawnFailure);
    }

    private void OnDestroy()
    {
        if (enemyMaterial != null) Destroy(enemyMaterial);
    }
}
