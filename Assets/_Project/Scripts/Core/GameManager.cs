using UnityEngine;

/// <summary>
/// Small persistent game state shared by scenes. The kill count is intentionally
/// recorded only; later scenes can decide how to use it.
/// </summary>
public sealed class GameManager : MonoBehaviour
{
    public static int SmallEnemyKillCount;
    public static GameManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null) return;
        var existing = FindFirstObjectByType<GameManager>();
        if (existing != null) return;
        var manager = new GameObject("GameManager");
        manager.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void IncrementKillCount()
    {
        SmallEnemyKillCount++;
        Debug.Log($"Small enemy kills: {SmallEnemyKillCount}");
    }
}
