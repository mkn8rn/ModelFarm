using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Web.Pages.Models;

public class OverviewModel : PageModel
{
    private readonly ITrainingService _trainingService;
    private readonly IDatasetService _datasetService;

    public OverviewModel(ITrainingService trainingService, IDatasetService datasetService)
    {
        _trainingService = trainingService;
        _datasetService = datasetService;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnGetCompletedJobsAsync()
    {
        var jobs = await _trainingService.GetAllTrainingJobsAsync(TrainingJobStatus.Completed);
        return new JsonResult(jobs);
    }

    public async Task<IActionResult> OnGetModelDetailsAsync(Guid jobId)
    {
        var job = await _trainingService.GetTrainingJobAsync(jobId);
        if (job is null)
            return new JsonResult(new { });

        var config = await _trainingService.GetConfigurationAsync(job.ConfigurationId);
        if (config is null)
            return new JsonResult(new { });

        var dataset = await _datasetService.GetDatasetAsync(config.DatasetId);

        return new JsonResult(new
        {
            configName = config.Name,
            modelType = config.ModelType.ToString(),
            datasetName = dataset?.Name ?? "-",
            symbol = dataset?.Symbol ?? "-",
            interval = dataset?.Interval.ToString() ?? "-",
            initialCapital = config.TradingEnvironment.InitialCapital,
            maxLags = config.MaxLags,
            hiddenLayers = string.Join(", ", config.HiddenLayerSizes),
            learningRate = config.LearningRate,
            batchSize = config.BatchSize
        });
    }
}
