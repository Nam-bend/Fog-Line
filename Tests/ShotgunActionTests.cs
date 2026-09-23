using System;

// Standalone regression tests for gameplay state, without Unity scene dependencies.
public static class ShotgunActionTests
{
    public static void Main()
    {
        FireRateAndDryFire();
        ReloadGuards();
        PartialReload();
        InterruptedReload();
        ResumeAfterEjection();
        SkippedFrames();
        Console.WriteLine("PASS: 6 shotgun ammo/fire-rate/reload scenarios.");
    }

    private static void FireRateAndDryFire()
    {
        var action = new ShotgunAction(10);
        Check(action.TryFire(0f, 0.8f) == 0, "First shot uses left chamber.");
        Check(action.TryFire(0.79f, 0.8f) == ShotgunAction.Blocked, "Cooldown blocks repeated input.");
        Check(action.Ammo.Loaded == 1, "Blocked shots do not consume ammo.");
        Check(action.TryFire(0.8f, 0.8f) == 1, "Second shot uses right chamber at cooldown boundary.");
        Check(action.TryFire(1.6f, 0.8f) == ShotgunAction.DryFire, "Empty gun dry fires.");
        Check(action.TryFire(1.7f, 0.8f) == ShotgunAction.Blocked, "Dry fire is rate limited.");
        Check(action.Ammo.Loaded == 0 && action.Ammo.Reserve == 10, "Shooting never consumes reserve.");
    }

    private static void ReloadGuards()
    {
        var action = new ShotgunAction(10);
        Check(!action.BeginReload(0f, 2f), "A full gun cannot reload.");
        action.TryFire(0f, 0.8f);
        Check(action.BeginReload(1f, 2f), "A used chamber can reload.");
        Check(!action.BeginReload(1.1f, 2f), "Repeated reload input does not restart reload.");
        Check(action.TryFire(2f, 0.8f) == ShotgunAction.Blocked, "Reload blocks firing even with a live chamber.");
        var emptyReserve = new ShotgunAction(-10);
        emptyReserve.TryFire(0f, 0.8f);
        Check(emptyReserve.Ammo.Reserve == 0 && !emptyReserve.BeginReload(1f, 2f), "No reserve means no reload.");
    }

    private static void PartialReload()
    {
        var action = new ShotgunAction(10);
        action.TryFire(0f, 0.8f);
        action.BeginReload(1f, 2f);
        var events = action.Tick(3f);
        Check(action.Ammo.Loaded == 2 && action.Ammo.Reserve == 9, "Reload replaces only the spent round.");
        Check((events & ShotgunAction.ReloadEvents.EjectRight) == 0, "Live right round is not ejected.");
        Check((events & ShotgunAction.ReloadEvents.LoadRight) == 0, "Live right chamber is not loaded twice.");
    }

    private static void InterruptedReload()
    {
        var action = EmptyGun(10);
        action.BeginReload(2f, 2f);
        action.Tick(3.1f);
        action.CancelReload();
        Check(action.Ammo.Loaded == 1 && action.Ammo.Reserve == 9, "Cancel retains the inserted round.");
        Check(action.Tick(10f) == ShotgunAction.ReloadEvents.None, "Cancelled reload cannot finish later.");
        Check(action.BeginReload(10f, 2f), "Interrupted reload can restart.");
        action.Tick(12f);
        Check(action.Ammo.Loaded == 2 && action.Ammo.Reserve == 8, "Restart loads only the missing round.");
    }

    private static void ResumeAfterEjection()
    {
        var action = EmptyGun(10);
        action.BeginReload(2f, 2f);
        action.Tick(2.6f);
        action.CancelReload();
        Check(action.Ammo.GetChamber(0) == ShotgunAmmo.Chamber.Empty
            && action.Ammo.GetChamber(1) == ShotgunAmmo.Chamber.Empty, "Ejected shells stay ejected on cancel.");
        Check(action.Ammo.Reserve == 10, "Ejection consumes no reserve.");
        action.BeginReload(3f, 2f);
        var events = action.Tick(5f);
        Check((events & (ShotgunAction.ReloadEvents.EjectLeft | ShotgunAction.ReloadEvents.EjectRight)) == 0,
            "Resuming after ejection does not eject duplicate shells.");
        Check(action.Ammo.Loaded == 2 && action.Ammo.Reserve == 8, "Empty chambers reload after interruption.");
    }

    private static void SkippedFrames()
    {
        var action = EmptyGun(1);
        action.BeginReload(2f, 2f);
        var events = action.Tick(20f);
        Check(!action.IsReloading && (events & ShotgunAction.ReloadEvents.Finished) != 0,
            "A long frame still completes reload.");
        Check(action.Ammo.Loaded == 1 && action.Ammo.Reserve == 0, "One reserve round cannot fill two chambers.");
        Check(action.Tick(21f) == ShotgunAction.ReloadEvents.None, "Reload events are emitted once.");
    }

    private static ShotgunAction EmptyGun(int reserve)
    {
        var action = new ShotgunAction(reserve);
        action.TryFire(0f, 0.8f);
        action.TryFire(1f, 0.8f);
        return action;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
