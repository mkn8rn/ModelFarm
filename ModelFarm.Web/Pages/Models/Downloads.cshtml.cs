using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.ML;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Web.Pages.Models;

public class DownloadsModel : PageModel
{
    private readonly ITrainingService _trainingService;
    private readonly CheckpointManager _checkpointManager;

    public DownloadsModel(ITrainingService trainingService)
    {
        _trainingService = trainingService;
        _checkpointManager = new CheckpointManager();
    }

    public void OnGet() { }

    public async Task<IActionResult> OnGetCompletedJobsAsync()
    {
        var jobs = await _trainingService.GetAllTrainingJobsAsync(TrainingJobStatus.Completed);
        
        var result = new List<object>();
        foreach (var job in jobs)
        {
            var config = await _trainingService.GetConfigurationAsync(job.ConfigurationId);
            var hasModel = _checkpointManager.CheckpointExists(job.Id);
            
            result.Add(new
            {
                id = job.Id,
                name = job.Name,
                configName = config?.Name,
                modelType = config?.ModelType.ToString(),
                completedAt = job.CompletedAtUtc,
                result = job.Result,
                hasModel
            });
        }
        
        return new JsonResult(result);
    }

    public async Task<IActionResult> OnGetDownloadModelAsync(Guid jobId)
    {
        var job = await _trainingService.GetTrainingJobAsync(jobId);
        if (job is null || job.Status != TrainingJobStatus.Completed)
        {
            return NotFound("Job not found or not completed");
        }

        var checkpointDir = _checkpointManager.GetCheckpointDirectory(jobId);
        if (!Directory.Exists(checkpointDir))
        {
            return NotFound("Model files not found");
        }

        // Create a zip file containing all checkpoint files
        var zipFileName = $"{SanitizeFileName(job.Name)}_model.zip";
        var memoryStream = new MemoryStream();
        
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var files = Directory.GetFiles(checkpointDir);
            foreach (var file in files)
            {
                var entryName = Path.GetFileName(file);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                
                using var entryStream = entry.Open();
                using var fileStream = System.IO.File.OpenRead(file);
                await fileStream.CopyToAsync(entryStream);
            }
        }
        
        memoryStream.Position = 0;
        return File(memoryStream, "application/zip", zipFileName);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}
