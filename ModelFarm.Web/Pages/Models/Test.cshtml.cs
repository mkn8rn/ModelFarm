using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;

namespace ModelFarm.Web.Pages.Models;

public class TestModel : PageModel
{
    private readonly IModelTestingService _modelTestingService;
    private readonly IDatasetService _datasetService;

    public TestModel(IModelTestingService modelTestingService, IDatasetService datasetService)
    {
        _modelTestingService = modelTestingService;
        _datasetService = datasetService;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnGetModelsAsync()
    {
        var models = await _modelTestingService.GetAvailableModelsAsync();
        return new JsonResult(models.Select(m => new
        {
            m.JobId,
            m.JobName,
            m.ConfigurationName,
            ModelType = m.ModelType.ToString(),
            m.DatasetId,
            m.DatasetName,
            m.Symbol,
            m.SharpeRatio,
            m.MeetsRequirements
        }));
    }

    public async Task<IActionResult> OnGetDatasetsAsync()
    {
        var datasets = await _datasetService.GetAllDatasetsAsync();
        return new JsonResult(datasets.Select(d => new
        {
            d.Id,
            d.Name,
            d.Symbol,
            Interval = d.Interval.ToString(),
            d.RecordCount,
            Status = d.Status.ToString()
        }));
    }

    public async Task<IActionResult> OnGetTestsAsync()
    {
        var tests = await _modelTestingService.GetAllTestsAsync();
        return new JsonResult(tests);
    }

    public async Task<IActionResult> OnPostRunTestAsync(Guid modelId, Guid datasetId, string? testName)
    {
        try
        {
            var test = await _modelTestingService.CreateAndRunTestAsync(modelId, datasetId, testName);
            return new JsonResult(test);
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostDeleteTestAsync(Guid testId)
    {
        var success = await _modelTestingService.DeleteTestAsync(testId);
        return new JsonResult(new { success });
    }

    public async Task<IActionResult> OnPostDeleteAllTestsAsync()
    {
        var tests = await _modelTestingService.GetAllTestsAsync();
        foreach (var test in tests)
        {
            await _modelTestingService.DeleteTestAsync(test.Id);
        }
        return new JsonResult(new { success = true });
    }
}
