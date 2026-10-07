using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Profiling;

public sealed class FirstPersonWeapon : MonoBehaviour
{
    [SerializeField] private GameObject weaponModel;
    [Header("View animation")]
    [SerializeField] private float idleAmount = 0.004f;
    [SerializeField] private float idleSpeed = 1.8f;
    [SerializeField, Min(0.05f)] private float recoilDuration = 0.18f;
    [SerializeField, Min(0.4f)] private float reloadDuration = 1.8f;
    [Header("Shot")]
    [SerializeField, Min(0.05f)] private float fireInterval = 0.8f;
    [SerializeField, Min(1f)] private float damage = 50f;
    [SerializeField, Range(4, 16)] private int pelletCount = 8;
    [SerializeField, Range(0.1f, 10f)] private float spreadDegrees = 3f;
    [SerializeField] private Transform muzzle;
    [Header("Ammo")]
    [SerializeField, Min(0)] private int startingReserveAmmo = 24;
    [SerializeField] private bool acceptPlayerInput = true;

    private ShotgunAction action;
    private PlayerUI playerUI;
    private PlayerHealth playerHealth;
    private PlayerLook playerLook;
    private ShotgunAudio sounds;
    private Camera aimCamera;
    private Transform weaponView, barrelPivot;
    private Vector3 heldPosition;
    private Quaternion heldRotation, closedBarrelRotation;
    private float recoilTime = -1f;
    private float dryFireTime = -1f;
    private bool reloadRequested;
    private readonly Transform[] breeches = new Transform[ShotgunAmmo.Capacity];
    private readonly GameObject[] loadingShells = new GameObject[ShotgunAmmo.Capacity];
    private readonly ShotgunHitDetection hitDetection = new ShotgunHitDetection();
    private static readonly ProfilerMarker FireMarker = new ProfilerMarker("Shotgun.Fire");

    public bool IsEquipped => weaponModel != null && weaponModel.activeInHierarchy;
    public bool IsReloading => action != null && action.IsReloading;
    public int RoundsRemaining => action != null ? action.Ammo.Loaded : ShotgunAmmo.Capacity;
    public int ReserveAmmo => action != null ? action.Ammo.Reserve : startingReserveAmmo;
    public void SaveStoryAmmo(ForestStoryState state)
    {
        state.left = (int)action.Ammo.GetChamber(0); state.right = (int)action.Ammo.GetChamber(1);
        state.reserve = action.Ammo.Reserve;
    }
    public void RestoreStoryAmmo(ForestStoryState state)
    {
        action.CancelReload(); action.Ammo.Restore(state.left, state.right, state.reserve);
        ResetPose(); RefreshUI();
    }

    private void Awake()
    {
        action = new ShotgunAction(startingReserveAmmo);
        playerUI = GetComponent<PlayerUI>();
        playerHealth = GetComponent<PlayerHealth>();
        playerLook = GetComponent<PlayerLook>();
        aimCamera = playerLook != null && playerLook.cam != null ? playerLook.cam : GetComponentInChildren<Camera>();
        if (weaponModel == null || aimCamera == null || weaponModel.transform.parent == null)
        {
            Debug.LogWarning("Assign a weapon model under the player camera.", this);
            enabled = false;
            return;
        }
        InitializeView();
        if (muzzle == null) CreateMuzzle();
        InitializeReloadVisuals();
        sounds = GetComponent<ShotgunAudio>();
        if (sounds == null) sounds = gameObject.AddComponent<ShotgunAudio>();
        sounds.Initialize();
    }

    private void InitializeView()
    {
        weaponView = weaponModel.transform.parent;
        heldPosition = weaponView.localPosition;
        heldRotation = weaponView.localRotation;
        foreach (Collider collider in weaponModel.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (Renderer renderer in weaponModel.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        foreach (Transform child in weaponModel.GetComponentsInChildren<Transform>(true))
            if (child.name == "BarrelPivot")
            {
                barrelPivot = child;
                closedBarrelRotation = child.localRotation;
                break;
            }
    }

    private void InitializeReloadVisuals()
    {
        ShotgunEffects.Prewarm();
        for (int i = 0; i < ShotgunAmmo.Capacity; i++)
        {
            breeches[i] = new GameObject(i == 0 ? "Left chamber" : "Right chamber").transform;
            breeches[i].SetParent(barrelPivot != null ? barrelPivot : weaponModel.transform, false);
            Vector3 hinge = barrelPivot != null ? barrelPivot.position : weaponModel.transform.position;
            breeches[i].position = hinge + weaponView.up * 0.072f + weaponView.forward * 0.01f
                + weaponView.right * (i == 0 ? -0.036f : 0.036f);
            breeches[i].rotation = Quaternion.LookRotation(weaponView.forward, weaponView.up);
            loadingShells[i] = ShotgunEffects.CreateShellVisual("Fresh shell");
            loadingShells[i].transform.SetParent(breeches[i], false);
            // Cylinder's Y axis is the cartridge's long axis.
            loadingShells[i].transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            loadingShells[i].SetActive(false);
        }
    }

    private void Start() => RefreshUI();
    private void RefreshUI()
    {
        if (playerUI != null)
        {
            playerUI.UpdateAmmo(RoundsRemaining, ReserveAmmo);
            playerUI.SetReloading(IsReloading);
        }
    }

    private void LateUpdate()
    {
        if (weaponView == null || !IsEquipped) return;
        if (playerHealth != null && playerHealth.IsDead) { ResetPose(); return; }
        if (Time.timeScale <= 0f) return;
        ReadInput();
        if (reloadRequested && recoilTime < 0f && !IsReloading) PlayReload();
        UpdateViewAnimation();
    }

    private void ReadInput()
    {
        if (ForestStoryDirector.BlocksInput) return;
        if (ForestStoryDirector.Instance != null && ForestStoryDirector.Instance.PowerRunning) return;
        if (!acceptPlayerInput) return;
        if (Mouse.current?.leftButton.wasPressedThisFrame == true) PlayFire();
        if (Keyboard.current?.rKey.wasPressedThisFrame == true) PlayReload();
    }

    private void UpdateViewAnimation()
    {
        Vector3 offset = new Vector3(Mathf.Sin(Time.time * idleSpeed * 0.5f) * idleAmount,
            Mathf.Sin(Time.time * idleSpeed) * idleAmount, 0f);
        Quaternion rotation = Quaternion.identity;
        AnimateRecoil(ref offset, ref rotation);
        AnimateDryFire(ref offset);
        AnimateReload(ref offset, ref rotation);
        weaponView.localPosition = heldPosition + offset;
        weaponView.localRotation = heldRotation * rotation;
    }

    private void AnimateRecoil(ref Vector3 offset, ref Quaternion rotation)
    {
        if (recoilTime >= 0f)
        {
            recoilTime += Time.deltaTime;
            float t = Mathf.Clamp01(recoilTime / Mathf.Max(0.05f, recoilDuration));
            float kick = t < 0.15f ? t / 0.15f : Mathf.Pow((1f - t) / 0.85f, 2f);
            offset += new Vector3(0f, -0.012f, -0.075f) * kick;
            rotation *= Quaternion.Euler(-8f * kick, 0f, 1.5f * kick);
            if (t >= 1f) recoilTime = -1f;
        }
    }

    private void AnimateDryFire(ref Vector3 offset)
    {
        if (dryFireTime >= 0f)
        {
            dryFireTime += Time.deltaTime;
            float t = Mathf.Clamp01(dryFireTime / 0.12f);
            offset.z -= Mathf.Sin(t * Mathf.PI) * 0.006f;
            if (t >= 1f) dryFireTime = -1f;
        }
    }

    private void AnimateReload(ref Vector3 offset, ref Quaternion rotation)
    {
        if (IsReloading)
        {
            float t = action.Progress(Time.time);
            float lower = Mathf.Sin(t * Mathf.PI);
            offset += new Vector3(-0.035f, -0.12f, -0.04f) * lower;
            rotation *= Quaternion.Euler(8f * lower, -4f * lower, -12f * lower);
            SetBarrelOpen(t < 0.24f ? Mathf.SmoothStep(0f, 1f, t / 0.24f)
                : t < 0.76f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 0.76f) / 0.24f));
            AnimateInsertion(0, t, 0.36f, ShotgunAction.LoadLeftProgress);
            AnimateInsertion(1, t, 0.56f, ShotgunAction.LoadRightProgress);
            HandleReloadEvents(action.Tick(Time.time));
        }
        else SetBarrelOpen(0f);
    }

    private void AnimateInsertion(int index, float t, float start, float end)
    {
        bool visible = t >= start && t < end && action.Ammo.GetChamber(index) == ShotgunAmmo.Chamber.Empty
            && action.Ammo.Reserve > 0;
        loadingShells[index].SetActive(visible);
        if (visible)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, t));
            loadingShells[index].transform.localPosition = Vector3.Lerp(new Vector3(0f, -0.07f, -0.16f),
                Vector3.zero, progress);
        }
    }

    private void HandleReloadEvents(ShotgunAction.ReloadEvents events)
    {
        for (int i = 0; i < ShotgunAmmo.Capacity; i++)
        {
            var eject = i == 0 ? ShotgunAction.ReloadEvents.EjectLeft : ShotgunAction.ReloadEvents.EjectRight;
            var load = i == 0 ? ShotgunAction.ReloadEvents.LoadLeft : ShotgunAction.ReloadEvents.LoadRight;
            if ((events & eject) != 0)
            {
                ShotgunEffects.EjectShell(breeches[i].position, -breeches[i].forward * 1.3f + weaponView.up * 1.2f);
                ShotgunEffects.Smoke(breeches[i].position, weaponView.up, 3);
                sounds.Play(ShotgunAudio.Cue.Eject);
            }
            if ((events & load) != 0)
            {
                loadingShells[i].SetActive(false);
                sounds.Play(ShotgunAudio.Cue.Insert);
            }
        }
        if ((events & ShotgunAction.ReloadEvents.Close) != 0) sounds.Play(ShotgunAudio.Cue.Close);
        if (events != ShotgunAction.ReloadEvents.None) RefreshUI();
    }

    private void SetBarrelOpen(float amount)
    {
        if (barrelPivot == null) return;
        Vector3 axis = barrelPivot.parent.InverseTransformDirection(weaponView.right);
        barrelPivot.localRotation = Quaternion.AngleAxis(38f * amount, axis) * closedBarrelRotation;
    }

    public void PlayFire()
    {
        if (!CanOperate() || Time.timeScale <= 0f) return;
        int barrel = action.TryFire(Time.time, fireInterval);
        if (barrel == ShotgunAction.Blocked) return;
        if (barrel == ShotgunAction.DryFire)
        {
            sounds.Play(ShotgunAudio.Cue.Dry);
            dryFireTime = 0f;
            return;
        }
        using (FireMarker.Auto()) FirePellets(barrel);
        sounds.Play(ShotgunAudio.Cue.Fire);
        playerLook?.AddRecoil(1.6f, barrel == 0 ? -0.2f : 0.2f);
        recoilTime = 0f;
        RefreshUI();
    }

    private void FirePellets(int barrel)
    {
        Vector3 origin = muzzle.position + weaponView.right * (barrel == 0 ? -0.036f : 0.036f);
        hitDetection.Fire(aimCamera.transform, origin, transform, pelletCount, spreadDegrees, damage);
        ForestNoise.Emit(transform.position, 45f);
        PlayHitFeedback(origin);
    }

    private void PlayHitFeedback(Vector3 origin)
    {
        if (!hitDetection.MuzzleBlocked) ShotgunEffects.Fire(origin, hitDetection.Direction);
        for (int i = 0; i < hitDetection.ImpactCount; i++)
        {
            RaycastHit impact = hitDetection.GetImpact(i);
            bool flesh = impact.collider.GetComponentInParent<EnemyHealth>() != null
                || impact.collider.GetComponentInParent<MotherStagger>() != null;
            ShotgunEffects.Impact(impact.point + impact.normal * 0.01f, impact.normal, flesh);
        }
        if (hitDetection.HitEnemy) playerUI?.ShowHitMarker(hitDetection.KilledEnemy);
    }

    public void PlayReload()
    {
        if (!CanOperate() || IsReloading || Time.timeScale <= 0f) return;
        if (!action.Ammo.CanReload) { reloadRequested = false; return; }
        if (recoilTime >= 0f) { reloadRequested = true; return; }
        reloadRequested = false;
        if (action.BeginReload(Time.time, reloadDuration))
        {
            sounds.Play(ShotgunAudio.Cue.Open);
            RefreshUI();
        }
    }

    private bool CanOperate() => isActiveAndEnabled && IsEquipped && aimCamera != null && muzzle != null
        && action != null && (playerHealth == null || !playerHealth.IsDead);

    private void CreateMuzzle()
    {
        Vector3 tip = weaponModel.transform.position;
        float furthest = float.NegativeInfinity;
        foreach (MeshFilter mesh in weaponModel.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null) continue;
            Bounds b = mesh.sharedMesh.bounds;
            Vector3 forward = mesh.transform.InverseTransformDirection(weaponView.forward);
            Vector3 face = b.center;
            if (Mathf.Abs(forward.x) > Mathf.Max(Mathf.Abs(forward.y), Mathf.Abs(forward.z)))
                face.x += Mathf.Sign(forward.x) * b.extents.x;
            else if (Mathf.Abs(forward.y) > Mathf.Abs(forward.z)) face.y += Mathf.Sign(forward.y) * b.extents.y;
            else face.z += Mathf.Sign(forward.z) * b.extents.z;
            Vector3 world = mesh.transform.TransformPoint(face);
            float depth = Vector3.Dot(world - aimCamera.transform.position, weaponView.forward);
            if (depth > furthest) { furthest = depth; tip = world; }
        }
        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(barrelPivot != null ? barrelPivot : weaponModel.transform, false);
        muzzle.position = tip;
    }

    public void Equip() => SetEquipped(true);
    public void Unequip() => SetEquipped(false);
    public void SetEquipped(bool equipped)
    {
        if (!equipped) ResetPose();
        if (weaponModel != null) weaponModel.SetActive(equipped);
        RefreshUI();
    }
    private void OnDisable() => ResetPose();
    private void ResetPose()
    {
        reloadRequested = false;
        action?.CancelReload();
        recoilTime = dryFireTime = -1f;
        foreach (GameObject shell in loadingShells) if (shell != null) shell.SetActive(false);
        if (weaponView != null)
        {
            weaponView.localPosition = heldPosition;
            weaponView.localRotation = heldRotation;
            SetBarrelOpen(0f);
        }
        playerLook?.ResetRecoil();
        RefreshUI();
    }
    private void OnDestroy()
    {
        foreach (GameObject shell in loadingShells) if (shell != null) Destroy(shell);
        foreach (Transform chamber in breeches) if (chamber != null) Destroy(chamber.gameObject);
    }
}
