using System.Text.Json;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.ML;

/// <summary>
/// Contains all data needed to resume training from a checkpoint.
/// </summary>
public sealed record TrainingCheckpoint
{
    /// <summary>
    /// Job ID this checkpoint belongs to.
    /// </summary>
    public required Guid JobId { get; init; }

    /// <summary>
    /// Configuration ID used for training.
    /// </summary>
    public required Guid ConfigurationId { get; init; }

    /// <summary>
    /// The epoch this checkpoint was saved at.
    /// </summary>
    public required int Epoch { get; init; }

    /// <summary>
    /// Best validation loss achieved so far.
    /// </summary>
    public required double BestValidationLoss { get; init; }

    /// <summary>
    /// Epochs since last improvement.
    /// </summary>
    public required int EpochsSinceImprovement { get; init; }

    /// <summary>
    /// Total training duration up to this checkpoint.
    /// </summary>
    public required TimeSpan TotalTrainingDuration { get; init; }

    /// <summary>
    /// Current retry attempt.
    /// </summary>
    public required int RetryAttempt { get; init; }

    /// <summary>
    /// Current learning rate (may have been adjusted on retry).
    /// </summary>
    public required double CurrentLearningRate { get; init; }

    /// <summary>
    /// Model type being trained.
    /// </summary>
    public required ModelType ModelType { get; init; }

    /// <summary>
    /// Number of input features.
    /// </summary>
    public required int FeatureCount { get; init; }

    /// <summary>
    /// Feature names for the model.
    /// </summary>
    public required string[] FeatureNames { get; init; }

    /// <summary>
    /// Hidden layer sizes (for MLP models).
    /// </summary>
    public required int[] HiddenLayerSizes { get; init; }

    /// <summary>
    /// Normalization statistics for the training data.
    /// </summary>
    public required NormalizationStats NormalizationStats { get; init; }

    /// <summary>
    /// When this checkpoint was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Relative path to the model weights file.
    /// </summary>
    public required string ModelWeightsPath { get; init; }

    /// <summary>
    /// Relative path to the best model weights file (may be same as ModelWeightsPath).
    /// </summary>
    public required string BestModelWeightsPath { get; init; }
}

/// <summary>
/// Service for managing training checkpoints.
/// </summary>
public sealed class CheckpointManager
{
    private const string CheckpointsFolder = "checkpoints";
    private const string CheckpointMetadataFile = "checkpoint.json";
    private const string CurrentModelFile = "model_current.pt";
    private const string BestModelFile = "model_best.pt";

    private readonly string _basePath;

    public CheckpointManager(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(AppContext.BaseDirectory, CheckpointsFolder);
    }

    /// <summary>
    /// Gets the checkpoint directory for a job.
    /// </summary>
    public string GetCheckpointDirectory(Guid jobId)
    {
        return Path.Combine(_basePath, jobId.ToString("N"));
    }

    /// <summary>
    /// Saves a training checkpoint.
    /// </summary>
    public async Task SaveCheckpointAsync(
        TrainingCheckpoint checkpoint,
        TorchSharp.torch.nn.Module<TorchSharp.torch.Tensor, TorchSharp.torch.Tensor> currentModel,
        TorchSharp.torch.nn.Module<TorchSharp.torch.Tensor, TorchSharp.torch.Tensor>? bestModel,
        CancellationToken cancellationToken = default)
    {
        var checkpointDir = GetCheckpointDirectory(checkpoint.JobId);
        Directory.CreateDirectory(checkpointDir);

        // Save current model weights
        var currentModelPath = Path.Combine(checkpointDir, CurrentModelFile);
        await Task.Run(() => currentModel.save(currentModelPath), cancellationToken);

        // Save best model weights (if different from current)
        var bestModelPath = Path.Combine(checkpointDir, BestModelFile);
        if (bestModel is not null)
        {
            await Task.Run(() => bestModel.save(bestModelPath), cancellationToken);
        }
        else
        {
            // Copy current as best if no separate best exists
            File.Copy(currentModelPath, bestModelPath, overwrite: true);
        }

        // Update checkpoint with paths and save metadata
        var checkpointWithPaths = checkpoint with
        {
            ModelWeightsPath = CurrentModelFile,
            BestModelWeightsPath = BestModelFile
        };

        var metadataPath = Path.Combine(checkpointDir, CheckpointMetadataFile);
        var json = JsonSerializer.Serialize(checkpointWithPaths, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
    }

    /// <summary>
    /// Loads a training checkpoint if one exists.
    /// </summary>
    public async Task<TrainingCheckpoint?> LoadCheckpointAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var checkpointDir = GetCheckpointDirectory(jobId);
        var metadataPath = Path.Combine(checkpointDir, CheckpointMetadataFile);

        if (!File.Exists(metadataPath))
            return null;

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        return JsonSerializer.Deserialize<TrainingCheckpoint>(json);
    }

    /// <summary>
    /// Checks if a checkpoint exists for a job.
    /// </summary>
    public bool CheckpointExists(Guid jobId)
    {
        var checkpointDir = GetCheckpointDirectory(jobId);
        var metadataPath = Path.Combine(checkpointDir, CheckpointMetadataFile);
        return File.Exists(metadataPath);
    }

    /// <summary>
    /// Gets the full path to the model weights file.
    /// </summary>
    public string GetModelWeightsPath(Guid jobId, string relativePath)
    {
        return Path.Combine(GetCheckpointDirectory(jobId), relativePath);
    }

    /// <summary>
    /// Deletes all checkpoints for a job.
    /// </summary>
    public void DeleteCheckpoint(Guid jobId)
    {
        var checkpointDir = GetCheckpointDirectory(jobId);
        if (Directory.Exists(checkpointDir))
        {
            Directory.Delete(checkpointDir, recursive: true);
        }
    }

    /// <summary>
    /// Cleans up old checkpoints for completed/failed jobs.
    /// </summary>
    public void CleanupOldCheckpoints(IEnumerable<Guid> activeJobIds)
    {
        if (!Directory.Exists(_basePath))
            return;

        var activeSet = activeJobIds.Select(id => id.ToString("N")).ToHashSet();
        
        foreach (var dir in Directory.GetDirectories(_basePath))
        {
            var dirName = Path.GetFileName(dir);
            if (!activeSet.Contains(dirName))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
