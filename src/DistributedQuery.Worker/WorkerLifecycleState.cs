namespace DistributedQuery.Worker;

public sealed class WorkerLifecycleState
{
    public bool IsReady { get; private set; }

    public bool IsDraining { get; private set; }

    public void MarkReady()
    {
        IsDraining = false;
        IsReady = true;
    }

    public void MarkDraining()
    {
        IsDraining = true;
        IsReady = false;
    }
}
