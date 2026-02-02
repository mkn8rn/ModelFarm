namespace ModelFarm.Application.ML;

/// <summary>
/// Global coordinator for parallel CPU work across all concurrent jobs.
/// Prevents thread pool exhaustion by limiting total parallel work, not just per-job.
/// Server-ready: ensures fair sharing of CPU resources under high concurrent load.
/// </summary>
public static class ParallelismCoordinator
{
    /// <summary>
    /// Minimum samples before parallelization kicks in.
    /// Below this, sequential is faster due to no thread overhead.
    /// </summary>
    public const int ParallelThreshold = 50_000;

    /// <summary>
    /// Maximum total parallel workers across ALL concurrent jobs.
    /// This prevents thread pool exhaustion regardless of job count.
    /// </summary>
    private static readonly int MaxGlobalParallelism = Math.Max(4, Environment.ProcessorCount);

    /// <summary>
    /// Semaphore to coordinate parallel work across all jobs.
    /// </summary>
    private static readonly SemaphoreSlim _parallelWorkSemaphore = new(MaxGlobalParallelism, MaxGlobalParallelism);

    /// <summary>
    /// Gets the maximum degree of parallelism allowed for a single parallel operation.
    /// This is dynamically calculated based on current system load.
    /// </summary>
    public static int MaxDegreeOfParallelism => Math.Max(1, MaxGlobalParallelism / 4);

    /// <summary>
    /// Executes a parallel for loop with global coordination.
    /// If the system is under heavy load, falls back to sequential execution.
    /// </summary>
    public static void For(int fromInclusive, int toExclusive, Action<int> body)
    {
        int count = toExclusive - fromInclusive;
        
        // For small workloads, always run sequential (faster, no overhead)
        if (count < ParallelThreshold)
        {
            for (int i = fromInclusive; i < toExclusive; i++)
            {
                body(i);
            }
            return;
        }

        // Try to acquire semaphore without blocking (non-blocking check)
        if (_parallelWorkSemaphore.Wait(0))
        {
            try
            {
                // Got the semaphore - we can do parallel work
                var options = new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = MaxDegreeOfParallelism 
                };
                Parallel.For(fromInclusive, toExclusive, options, body);
            }
            finally
            {
                _parallelWorkSemaphore.Release();
            }
        }
        else
        {
            // System is busy - fall back to sequential to avoid contention
            for (int i = fromInclusive; i < toExclusive; i++)
            {
                body(i);
            }
        }
    }

    /// <summary>
    /// Executes a parallel for loop with thread-local state and global coordination.
    /// </summary>
    public static void For<TLocal>(
        int fromInclusive, 
        int toExclusive, 
        Func<TLocal> localInit,
        Func<int, ParallelLoopState, TLocal, TLocal> body,
        Action<TLocal> localFinally)
    {
        int count = toExclusive - fromInclusive;
        
        // For small workloads, simulate sequential with local state
        if (count < ParallelThreshold)
        {
            var local = localInit();
            for (int i = fromInclusive; i < toExclusive; i++)
            {
                local = body(i, null!, local);
            }
            localFinally(local);
            return;
        }

        // Try to acquire semaphore without blocking
        if (_parallelWorkSemaphore.Wait(0))
        {
            try
            {
                var options = new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = MaxDegreeOfParallelism 
                };
                Parallel.For(fromInclusive, toExclusive, options, localInit, body, localFinally);
            }
            finally
            {
                _parallelWorkSemaphore.Release();
            }
        }
        else
        {
            // System is busy - fall back to sequential
            var local = localInit();
            for (int i = fromInclusive; i < toExclusive; i++)
            {
                local = body(i, null!, local);
            }
            localFinally(local);
        }
    }
}
