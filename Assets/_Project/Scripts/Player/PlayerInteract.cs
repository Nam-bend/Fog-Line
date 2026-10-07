using UnityEngine;

public class PlayerInteract : MonoBehaviour
{

    private Camera cam;

    [SerializeField]
    private float distance = 2f;
    [SerializeField] 
    private LayerMask mask;
    private RaycastHit hitInfo;
    [SerializeField]
    private PlayerUI playerUI;

    [SerializeField, Min(0.02f)] private float scanInterval = 0.075f;
    private float nextScanTime;
    private PlayerHealth playerHealth;
    void Start()
    {
        cam = GetComponent<PlayerLook>().cam;
        playerHealth = GetComponent<PlayerHealth>();
    }

    // Called after movement/look. Button presses always rescan immediately.
    public void ProcessInteraction(bool pressed)
    {  
        if (playerHealth != null && playerHealth.IsDead) return;
        if (cam == null || (!pressed && Time.time < nextScanTime)) return;
        nextScanTime = Time.time + Mathf.Max(0.02f, scanInterval);
        //create ray at the center of the camera ,shooting outwards
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Interactable target = null;
        if(Physics.Raycast(ray, out hitInfo, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)){
            if ((mask.value & (1 << hitInfo.collider.gameObject.layer)) != 0)
                target = hitInfo.collider.GetComponentInParent<Interactable>();
        }
        if (playerUI != null) playerUI.UpdatePrompt(target != null ? target.promptMessage : string.Empty);
        if (pressed && target != null) target.BaseInteract();
    }
    private void OnDisable()
    {
        if (playerUI != null) playerUI.UpdatePrompt(string.Empty);
    }
}
  
