using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Web.Pages;

public class JobsModel : PageModel
{
    private readonly ITrainingService _trainingService;

    public JobsModel(ITrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    // ==================== Training Job Form ====================
    [BindProperty]
    public Guid ConfigurationId { get; set; }

    [BindProperty]
    public string? JobName { get; set; }

    [BindProperty]
    public int MaxConcurrentJobs { get; set; }

    public void OnGet() { }

    // ==================== Concurrency Settings ====================
    public IActionResult OnGetConcurrencySettings()
    {
        return new JsonResult(new
        {
            maxConcurrent = _trainingService.GetMaxConcurrentJobs(),
            runningCount = _trainingService.GetRunningJobCount()
        });
    }

    public IActionResult OnPostSetConcurrency()
    {
        _trainingService.SetMaxConcurrentJobs(MaxConcurrentJobs);
        return new JsonResult(new
        {
            success = true,
            maxConcurrent = _trainingService.GetMaxConcurrentJobs(),
            runningCount = _trainingService.GetRunningJobCount()
        });
    }

    // ==================== Configuration Endpoints ====================
    public async Task<IActionResult> OnGetConfigsAsync()
    {
        var configs = await _trainingService.GetAllConfigurationsAsync();
        return new JsonResult(configs);
    }

    // ==================== Job Endpoints ====================
    public async Task<IActionResult> OnPostStartTrainingAsync()
    {
        try
        {
            var request = new TrainingJobRequest
            {
                ConfigurationId = ConfigurationId,
                JobName = string.IsNullOrWhiteSpace(JobName) ? null : JobName
            };

            var job = await _trainingService.StartTrainingAsync(request);
            return new JsonResult(new { success = true, job });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnGetJobsAsync()
    {
        var jobs = await _trainingService.GetAllTrainingJobsAsync();
        return new JsonResult(jobs);
    }


    public async Task<IActionResult> OnGetJobStatusAsync(Guid jobId)
    {
        var job = await _trainingService.GetTrainingJobAsync(jobId);
        return new JsonResult(job);
    }

    public async Task<IActionResult> OnPostCancelJobAsync(Guid jobId)
    {
        var cancelled = await _trainingService.CancelTrainingJobAsync(jobId);
        return new JsonResult(new { success = cancelled });
    }

    public async Task<IActionResult> OnPostPauseJobAsync(Guid jobId)
    {
        var paused = await _trainingService.PauseTrainingJobAsync(jobId);
        return new JsonResult(new { success = paused });
    }

    public async Task<IActionResult> OnPostResumePausedJobAsync(Guid jobId)
    {
        var resumed = await _trainingService.ResumePausedJobAsync(jobId);
        return new JsonResult(new { success = resumed });
    }

    public async Task<IActionResult> OnPostRetryJobAsync(Guid jobId)
    {
        var retried = await _trainingService.RetryTrainingJobAsync(jobId);
        return new JsonResult(new { success = retried });
    }

    public async Task<IActionResult> OnPostResumeFromCheckpointAsync(Guid jobId)
    {
        var resumed = await _trainingService.ResumeTrainingJobAsync(jobId);
        return new JsonResult(new { success = resumed });
    }
}
