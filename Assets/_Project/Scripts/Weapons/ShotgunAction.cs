// Pure, clock-driven action state; animation length never controls firing cadence.
public sealed class ShotgunAction
{
    public const int Blocked = -2;
    public const int DryFire = -1;
    private const float DryFireInterval = 0.2f;
    public const float EjectProgress = 0.28f;
    public const float LoadLeftProgress = 0.50f;
    public const float LoadRightProgress = 0.70f;
    private const float CloseProgress = 0.88f;

    [System.Flags]
    public enum ReloadEvents
    {
        None = 0,
        EjectLeft = 1,
        EjectRight = 2,
        LoadLeft = 4,
        LoadRight = 8,
        Close = 16,
        Finished = 32
    }

    public readonly ShotgunAmmo Ammo;
    public bool IsReloading { get; private set; }
    public float NextFireTime { get; private set; }
    private float reloadStart;
    private float duration;
    private int phase;

    public ShotgunAction(int reserve) { Ammo = new ShotgunAmmo(reserve); }
    public int TryFire(float now, float interval)
    {
        if (IsReloading || now < NextFireTime) return Blocked;
        int barrel = Ammo.Fire();
        NextFireTime = now + (barrel < 0 ? DryFireInterval : System.Math.Max(0.05f, interval));
        return barrel < 0 ? DryFire : barrel;
    }
    public bool BeginReload(float now, float seconds)
    {
        if (IsReloading || !Ammo.CanReload) return false;
        reloadStart = now;
        duration = System.Math.Max(0.4f, seconds);
        phase = 0;
        IsReloading = true;
        return true;
    }
    public float Progress(float now)
    {
        return IsReloading ? System.Math.Max(0f, System.Math.Min(1f, (now - reloadStart) / duration)) : 0f;
    }
    public ReloadEvents Tick(float now)
    {
        if (!IsReloading) return ReloadEvents.None;
        float t = Progress(now);
        ReloadEvents result = ReloadEvents.None;
        if (phase == 0 && t >= EjectProgress)
        {
            if (Ammo.Eject(0)) result |= ReloadEvents.EjectLeft;
            if (Ammo.Eject(1)) result |= ReloadEvents.EjectRight;
            phase = 1;
        }
        if (phase == 1 && t >= LoadLeftProgress)
        {
            if (Ammo.Insert(0)) result |= ReloadEvents.LoadLeft;
            phase = 2;
        }
        if (phase == 2 && t >= LoadRightProgress)
        {
            if (Ammo.Insert(1)) result |= ReloadEvents.LoadRight;
            phase = 3;
        }
        if (phase == 3 && t >= CloseProgress)
        {
            result |= ReloadEvents.Close;
            phase = 4;
        }
        if (t >= 1f)
        {
            IsReloading = false;
            result |= ReloadEvents.Finished;
        }
        return result;
    }
    public void CancelReload() { IsReloading = false; }
}
