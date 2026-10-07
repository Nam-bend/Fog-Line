// Two independent chambers. Reload interruption preserves already ejected/loaded shells.
public sealed class ShotgunAmmo
{
    public enum Chamber { Empty, Live, Spent }
    public const int Capacity = 2;
    private readonly Chamber[] chambers = { Chamber.Live, Chamber.Live };
    private int nextBarrel;
    public int Reserve { get; private set; }
    public int Loaded => (chambers[0] == Chamber.Live ? 1 : 0) + (chambers[1] == Chamber.Live ? 1 : 0);
    public bool CanReload => Loaded < Capacity && Reserve > 0;

    public ShotgunAmmo(int reserve)
    {
        Reserve = System.Math.Max(0, reserve);
    }

    public Chamber GetChamber(int index) => chambers[index];
    public void Restore(int left, int right, int reserve)
    {
        chambers[0] = (Chamber)System.Math.Max(0, System.Math.Min(2, left));
        chambers[1] = (Chamber)System.Math.Max(0, System.Math.Min(2, right));
        Reserve = System.Math.Max(0, reserve);
        nextBarrel = 0;
    }

    public int Fire()
    {
        for (int i = 0; i < Capacity; i++)
        {
            int barrel = (nextBarrel + i) % Capacity;
            if (chambers[barrel] != Chamber.Live) continue;
            chambers[barrel] = Chamber.Spent;
            nextBarrel = 1 - barrel;
            return barrel;
        }
        return -1;
    }

    public bool TryFire() => Fire() >= 0;

    public bool Eject(int index)
    {
        if (chambers[index] != Chamber.Spent) return false;
        chambers[index] = Chamber.Empty;
        return true;
    }
    public bool Insert(int index)
    {
        if (chambers[index] != Chamber.Empty || Reserve <= 0) return false;
        Reserve--;
        chambers[index] = Chamber.Live;
        return true;
    }
}
