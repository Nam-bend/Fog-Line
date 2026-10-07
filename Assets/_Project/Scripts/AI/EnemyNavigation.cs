using UnityEngine;
using UnityEngine.AI;

// Shared by combat and investigation so changing state never creates competing paths.
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyNavigation : MonoBehaviour
{
    private NavMeshAgent agent;
    private NavMeshPath path;
    private Vector3 requested, progressPosition;
    private float nextPlan, progressAt, retryAt;
    private bool requestedOnce;
    public bool Moving => agent != null && agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > .025f;
    public bool Unreachable { get; private set; }

    private void Awake()
    {
        agent=GetComponent<NavMeshAgent>();
        path=new NavMeshPath();
        agent.autoRepath=true; agent.angularSpeed=360;
        agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        progressPosition=transform.position; progressAt=Time.time;
    }
    public void Stop()
    {
        if(agent.enabled && agent.isOnNavMesh) { if(agent.hasPath) agent.ResetPath(); agent.isStopped=true; }
        requestedOnce=false; progressAt=Time.time; progressPosition=transform.position;
    }
    public bool Move(Vector3 destination,float stoppingDistance)
    {
        if(!agent.enabled || !agent.isOnNavMesh) return false;
        agent.stoppingDistance=stoppingDistance;
        bool changed=!requestedOnce || (destination-requested).sqrMagnitude>1f;
        if(Time.time<retryAt && !changed) return false;
        bool stalled=false;
        if((transform.position-progressPosition).sqrMagnitude>.09f)
        { progressPosition=transform.position; progressAt=Time.time; }
        else if(Time.time-progressAt>1.5f && agent.hasPath && agent.remainingDistance>stoppingDistance+.4f)
        { stalled=true; progressAt=Time.time; }
        if(Time.time<nextPlan && !stalled) return !Unreachable;
        if(!changed && !stalled && agent.hasPath && !agent.isPathStale) return !Unreachable;
        requested=destination; requestedOnce=true; nextPlan=Time.time+.35f;
        var filter=new NavMeshQueryFilter {agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        bool found=NavMesh.SamplePosition(destination,out var hit,2f,filter)
            && Mathf.Abs(hit.position.y-destination.y)<1.6f
            && agent.CalculatePath(hit.position,path) && path.status==NavMeshPathStatus.PathComplete;
        // If blocked physically, step to a reachable side point before trying the target again.
        if(stalled)
        {
            found=false;
            for(int i=0;i<8;i++)
            {
                Vector3 side=Quaternion.Euler(0,i*45,0)*transform.right*1.5f;
                if(!NavMesh.SamplePosition(transform.position+side,out var step,.6f,filter)) continue;
                if(Mathf.Abs(step.position.y-transform.position.y)>1f || (step.position-transform.position).sqrMagnitude<.5f) continue;
                if(agent.CalculatePath(step.position,path) && path.status==NavMeshPathStatus.PathComplete)
                { found=true; nextPlan=Time.time+1f; requestedOnce=false; break; }
            }
        }
        Unreachable=!found;
        if(!found) { agent.ResetPath(); agent.isStopped=true; retryAt=Time.time+1f; return false; }
        agent.isStopped=false; agent.SetPath(path); return true;
    }
}
