using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelFarm.Application.ML;
using ModelFarm.Contracts.Training;
using ModelFarm.Infrastructure.Persistence;

namespace ModelFarm.Application.Services;

/// <summary>
/// Handles recovery of training jobs that were interrupted by application shutdown.
/// Runs on application startup to mark interrupted jobs appropriately.
/// </summary>
public sealed class TrainingJobRecoveryService : IHostedService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<TrainingJobRecoveryService> _logger;
    private readonly CheckpointManager _checkpointManager;

    public TrainingJobRecoveryService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<TrainingJobRecoveryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _checkpointManager = new CheckpointManager();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Find all jobs that were in an active state when the app shut down
        var activeStatuses = new[]
        {
            TrainingJobStatus.Queued,
            TrainingJobStatus.WaitingForData,
            TrainingJobStatus.Preprocessing,
            TrainingJobStatus.Training,
            TrainingJobStatus.Backtesting
        };

        var interruptedJobs = await db.TrainingJobs
            .Where(j => activeStatuses.Contains(j.Status))
            .ToListAsync(cancellationToken);

        if (interruptedJobs.Count == 0)
        {
            _logger.LogInformation("No interrupted training jobs found on startup");
            return;
        }

        _logger.LogWarning("Found {Count} interrupted training job(s) from previous session", interruptedJobs.Count);

        foreach (var job in interruptedJobs)
        {
            var previousStatus = job.Status;
            var hasCheckpoint = _checkpointManager.CheckpointExists(job.Id);
            
            if (hasCheckpoint)
            {
                // Job has checkpoint - mark as interrupted but resumable
                job.Status = TrainingJobStatus.Failed;
                job.Message = $"Interrupted at {previousStatus} (checkpoint available at epoch {job.CurrentEpoch}). Use Resume to continue.";
                job.ErrorMessage = "Application shutdown interrupted training - checkpoint saved";
                job.HasCheckpoint = true;
                
                _logger.LogInformation(
                    "Job {JobId} ({JobName}) has checkpoint at epoch {Epoch} - can be resumed",
                    job.Id, job.Name, job.CurrentEpoch);
            }
            else
            {
                // No checkpoint - mark as failed, must restart from beginning
                job.Status = TrainingJobStatus.Failed;
                job.Message = $"Interrupted by application shutdown (was {previousStatus}). Use Retry to restart.";
                job.ErrorMessage = "Application shutdown interrupted training";
                job.HasCheckpoint = false;
                
                _logger.LogWarning(
                    "Marked job {JobId} ({JobName}) as failed - was in {PreviousStatus} state, no checkpoint",
                    job.Id, job.Name, previousStatus);
            }

            job.CompletedAtUtc = DateTime.UtcNow;
            job.IsPaused = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        
        var withCheckpoint = interruptedJobs.Count(j => j.HasCheckpoint);
        _logger.LogInformation(
            "Recovery complete: {Total} job(s) processed, {WithCheckpoint} with checkpoints (resumable)",
            interruptedJobs.Count, withCheckpoint);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
