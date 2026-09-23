using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
public class PlayerUI : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI promptText;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private Text legacyAmmoText;
    private int loadedAmmo = 2;
    private int reserveAmmo = 24;
    [SerializeField] private Image frontHealthBar;
    [SerializeField] private Image backHealthBar;
    [SerializeField, Min(0f)] private float damageTrailDelay = 0.35f;
    [SerializeField, Min(0.01f)] private float damageTrailSpeed = 0.5f;

    private float targetHealth = 1f;
    private float trailDelayRemaining;
    private Coroutine healthTrail;
    private CanvasGroup hitMarker;
    private Image[] markerLines;
    private TMP_Text reloadLabel;
    private Coroutine markerFade;
    private bool reloading;
  
    void Start()
    {
         UpdatePrompt(string.Empty);
         UpdateAmmo(loadedAmmo, reserveAmmo);
         EnsureWeaponFeedback();
         SetReloading(reloading);
    }
    private IEnumerator AnimateHealthTrail()
    {
        while (backHealthBar != null && !Mathf.Approximately(backHealthBar.fillAmount, targetHealth))
        {
            yield return null;
            if (trailDelayRemaining > 0f)
                trailDelayRemaining -= Time.deltaTime;
            else if (backHealthBar != null)
                backHealthBar.fillAmount = Mathf.MoveTowards(backHealthBar.fillAmount,
                    targetHealth, Mathf.Max(0.01f, damageTrailSpeed) * Time.deltaTime);
        }
        healthTrail = null;
        if (markerFade != null) StopCoroutine(markerFade);
        markerFade = null;
        if (hitMarker != null) hitMarker.alpha = 0f;
    }

    private void OnDisable()
    {
        if (healthTrail != null) StopCoroutine(healthTrail);
        healthTrail = null;
    }

    private void OnEnable() => ResumeHealthTrail();

    private void ResumeHealthTrail()
    {
        if (Application.isPlaying && isActiveAndEnabled && healthTrail == null && backHealthBar != null
            && !Mathf.Approximately(backHealthBar.fillAmount, targetHealth))
            healthTrail = StartCoroutine(AnimateHealthTrail());
    }

    public void UpdateHealth(float currentHealth, float maxHealth, bool immediate = false)
    {
        float previousHealth = targetHealth;
        targetHealth = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        if (frontHealthBar != null)
            frontHealthBar.fillAmount = targetHealth;

        if (immediate || targetHealth > previousHealth)
        {
            if (backHealthBar != null) backHealthBar.fillAmount = targetHealth;
            trailDelayRemaining = 0f;
        }
        else if (targetHealth < previousHealth)
        {
            trailDelayRemaining = damageTrailDelay;
        }
        ResumeHealthTrail();
    }

    public void UpdateAmmo(int loaded, int reserve)
    {
        loadedAmmo = Mathf.Max(0, loaded);
        reserveAmmo = Mathf.Max(0, reserve);
        if (ammoText == null && legacyAmmoText == null) BindAmmoText();
        string value = $"{loadedAmmo} / {reserveAmmo}";
        if (ammoText != null) ammoText.text = value;
        if (legacyAmmoText != null) legacyAmmoText.text = value;
    }

    public void SetReloading(bool value)
    {
        reloading = value;
        if (reloadLabel != null) reloadLabel.gameObject.SetActive(value);
    }

    public void ShowHitMarker(bool killed)
    {
        if (!isActiveAndEnabled) return;
        EnsureWeaponFeedback();
        if (hitMarker == null) return;
        Color color = killed ? new Color(1f, 0.3f, 0.15f) : Color.white;
        foreach (Image line in markerLines) line.color = color;
        if (markerFade != null) StopCoroutine(markerFade);
        markerFade = StartCoroutine(FadeHitMarker());
    }

    private IEnumerator FadeHitMarker()
    {
        float time = 0f;
        while (time < 0.18f)
        {
            hitMarker.alpha = 1f - time / 0.18f;
            yield return null;
            time += Time.deltaTime;
        }
        hitMarker.alpha = 0f;
        markerFade = null;
    }

    private void EnsureWeaponFeedback()
    {
        if (hitMarker != null) return;
        Canvas canvas = promptText != null ? promptText.GetComponentInParent<Canvas>() : null;
        if (canvas == null && ammoText != null) canvas = ammoText.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var root = new GameObject("Shotgun Hit Marker", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(40f, 40f);
        hitMarker = root.GetComponent<CanvasGroup>();
        hitMarker.alpha = 0f;
        hitMarker.blocksRaycasts = false;
        markerLines = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var line = new GameObject("Hit line", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(root.transform, false);
            var r = (RectTransform)line.transform;
            float angle = 45f + i * 90f;
            r.anchoredPosition = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 10f;
            r.sizeDelta = new Vector2(7f, 2f);
            r.localRotation = Quaternion.Euler(0f, 0f, angle);
            markerLines[i] = line.GetComponent<Image>();
            markerLines[i].raycastTarget = false;
        }
        var label = new GameObject("Reload Indicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(canvas.transform, false);
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
        // The health bar is centered at y=70 and is about 63 px tall.
        // Keep the reload status above it instead of drawing over the bar.
        labelRect.anchoredPosition = new Vector2(0f, 130f);
        labelRect.sizeDelta = new Vector2(240f, 30f);
        reloadLabel = label.GetComponent<TextMeshProUGUI>();
        if (ammoText != null) reloadLabel.font = ammoText.font;
        else if (promptText != null) reloadLabel.font = promptText.font;
        reloadLabel.text = "RELOADING";
        reloadLabel.fontSize = 18f;
        reloadLabel.alignment = TextAlignmentOptions.Center;
        reloadLabel.raycastTarget = false;
        label.SetActive(reloading);
    }

    private void OnDestroy()
    {
        if (hitMarker != null) Destroy(hitMarker.gameObject);
        if (reloadLabel != null) Destroy(reloadLabel.gameObject);
    }

    private void BindAmmoText()
    {
        // Scope discovery to this player's existing HUD; never create a second UI.
        Canvas canvas = promptText != null ? promptText.GetComponentInParent<Canvas>() : null;
        if (canvas == null && frontHealthBar != null)
            canvas = frontHealthBar.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            if (IsAmmoName(text.name)) { ammoText = text; return; }
        foreach (Text text in canvas.GetComponentsInChildren<Text>(true))
            if (IsAmmoName(text.name)) { legacyAmmoText = text; return; }
    }

    private static bool IsAmmoName(string name)
    {
        string compact = name.Replace(" ", "").Replace("_", "").Replace("-", "");
        return compact.StartsWith("Ammo", System.StringComparison.OrdinalIgnoreCase);
    }
    public void UpdatePrompt(string text){

        if (promptText != null && promptText.text != text) promptText.text = text;
        
    }

    
}
