using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Web.Pages;

public class DatasetsModel : PageModel
{
    private readonly IDatasetService _datasetService;
    private readonly IExchangeService _exchangeService;
    private readonly IIngestionService _ingestionService;

    public DatasetsModel(
        IDatasetService datasetService,
        IExchangeService exchangeService,
        IIngestionService ingestionService)
    {
        _datasetService = datasetService;
        _exchangeService = exchangeService;
        _ingestionService = ingestionService;
    }

    // ==================== Dataset Form ====================
    [BindProperty]
    public DatasetType DatasetType { get; set; } = DatasetType.ExchangeHistory;

    [BindProperty]
    public string DatasetName { get; set; } = "";

    [BindProperty]
    public Exchange Exchange { get; set; } = Exchange.Binance;

    [BindProperty]
    public string Symbol { get; set; } = "BTCUSDT";

    [BindProperty]
    public KlineInterval Interval { get; set; } = KlineInterval.OneHour;

    [BindProperty]
    public DateTime DatasetStartDate { get; set; } = DateTime.UtcNow.AddYears(-1).Date;

    [BindProperty]
    public DateTime DatasetEndDate { get; set; } = DateTime.UtcNow.Date;

    // ==================== Select Lists ====================
    public List<SelectListItem> AvailableDatasetTypes =>
        Enum.GetValues<DatasetType>()
            .Select(t => new SelectListItem(t switch
            {
                DatasetType.ExchangeHistory => "Exchange History",
                _ => t.ToString()
            }, t.ToString()))
            .ToList();

    public List<SelectListItem> AvailableExchanges =>
        _exchangeService.GetSupportedExchanges()
            .Select(e => new SelectListItem(e.ToDisplayString(), ((int)e).ToString()))
            .ToList();

    public List<SelectListItem> AvailableIntervals =>
        Enum.GetValues<KlineInterval>()
            .Select(i => new SelectListItem(i.ToDisplayString(), i.ToString()))
            .ToList();

    public void OnGet() { }

    // ==================== Dataset Endpoints ====================
    public async Task<IActionResult> OnPostCreateDatasetAsync()
    {
        try
        {
            var startTime = DateTime.SpecifyKind(DatasetStartDate, DateTimeKind.Utc);
            var endTime = DateTime.SpecifyKind(DatasetEndDate.AddDays(1), DateTimeKind.Utc);
            var now = DateTime.UtcNow;
            if (endTime > now)
            {
                endTime = now;
            }

            var request = new CreateDatasetRequest
            {
                Type = DatasetType,
                Name = DatasetName,
                Exchange = Exchange,
                Symbol = Symbol,
                Interval = Interval,
                StartTimeUtc = startTime,
                EndTimeUtc = endTime
            };

            var dataset = await _datasetService.CreateDatasetAsync(request);
            return new JsonResult(new { success = true, dataset });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnGetDatasetsAsync()
    {
        var datasets = await _datasetService.GetAllDatasetsAsync();
        
        var refreshedDatasets = new List<DatasetDefinition>();
        foreach (var dataset in datasets)
        {
            if (dataset.Status == DatasetStatus.Downloading && dataset.IngestionOperationId.HasValue)
            {
                var refreshed = await _datasetService.RefreshDatasetStatusAsync(dataset.Id);
                refreshedDatasets.Add(refreshed);
            }
            else
            {
                refreshedDatasets.Add(dataset);
            }
        }
        
        return new JsonResult(refreshedDatasets);
    }

    public async Task<IActionResult> OnPostDeleteDatasetAsync(Guid datasetId)
    {
        var deleted = await _datasetService.DeleteDatasetAsync(datasetId);
        return new JsonResult(new { success = deleted });
    }

    public IActionResult OnGetDatasetProgress(Guid operationId)
    {
        var progress = _ingestionService.GetProgress(operationId);
        return new JsonResult(progress);
    }

    public async Task<IActionResult> OnGetSymbolsAsync(int exchange, CancellationToken cancellationToken)
    {
        try
        {
            var symbols = await _exchangeService.GetSymbolsAsync((Exchange)exchange, cancellationToken);
            return new JsonResult(symbols);
        }
        catch (OperationCanceledException)
        {
            return new JsonResult(Array.Empty<object>());
        }
    }
}
