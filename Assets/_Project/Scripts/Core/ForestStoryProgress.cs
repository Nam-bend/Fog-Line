using System;
using System.Collections.Generic;

// Pure progression rules shared by the runtime and standalone regression tests.
[Serializable]
public class ForestStoryProgress
{
    public int stage;
    public List<string> clues = new List<string>();
    public bool rangerTrail;
    public bool shadowSeen;
    public int kills;
    public bool Has(string id) => clues.Contains(id);
    public void Record(string id) { if (!Has(id)) clues.Add(id); }
    public void ExamineShell() { Record("shell"); stage = Math.Max(1,stage); }
    public bool CanObserve(string id) => !Has(id) &&
        ((id=="shell" && stage==0) || (id=="cloth" && stage==2) || (id=="blockage" && stage==3));
    public bool Observe(string id)
    {
        if(!CanObserve(id)) return false;
        if(id=="shell") ExamineShell(); else Record(id);
        return true;
    }
    public bool OpenServiceGate()
    {
        if(stage!=1 || !Has("shell")) return false;
        Record("power"); stage=2; return true;
    }
    public bool CollectDiagram()
    {
        if(stage!=2 || !Has("power")) return false;
        Record("diagram"); stage=3; return true;
    }
    public bool CanFinish => stage==3 && Has("diagram") && Has("blockage") && shadowSeen;
    public bool Complete()
    {
        if(!CanFinish) return false;
        stage=4; return true;
    }
}
