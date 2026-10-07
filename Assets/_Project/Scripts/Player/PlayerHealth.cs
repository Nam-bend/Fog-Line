using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0f;
    public void RestoreStoryHealth(float health)
    {
        CurrentHealth = Mathf.Clamp(health, 1f, maxHealth); RefreshUI(true);
    }

    private PlayerUI playerUI;

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        CurrentHealth = maxHealth;
        playerUI = GetComponent<PlayerUI>();
    }

    private void Start()
    {
        RefreshUI(true);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage))
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        RefreshUI();
        if (IsDead)
        {
            if (playerUI != null)
                playerUI.UpdatePrompt(string.Empty);
            onDeath.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        RefreshUI();
    }

    // Explicit reset also allows a respawn system to revive the player.
    public void RestoreFullHealth()
    {
        CurrentHealth = maxHealth;
        RefreshUI(true);
    }

    private void RefreshUI(bool immediate = false)
    {
        if (playerUI != null)
            playerUI.UpdateHealth(CurrentHealth, maxHealth, immediate);
    }

    [ContextMenu("Health/Take 10 Damage")]
    private void TestDamage()
    {
        if (Application.isPlaying) TakeDamage(10f);
    }

    [ContextMenu("Health/Heal 10")]
    private void TestHeal()
    {
        if (Application.isPlaying) Heal(10f);
    }

    [ContextMenu("Health/Restore Full Health")]
    private void TestRestore()
    {
        if (Application.isPlaying) RestoreFullHealth();
    }
}
