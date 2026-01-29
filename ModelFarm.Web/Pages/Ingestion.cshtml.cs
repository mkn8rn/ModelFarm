using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.MarketData;

namespace ModelFarm.Web.Pages;

public class IngestionModel : PageModel
{
    private readonly IIngestionService _ingestionService;
    private readonly IExchangeService _exchangeService;

    public IngestionModel(IIngestionService ingestionService, IExchangeService exchangeService)
    {
        _ingestionService = ingestionService;
        _exchangeService = exchangeService;
    }

    [BindProperty]
    public Exchange Exchange { get; set; } = Exchange.Binance;

    [BindProperty]
    public string Symbol { get; set; } = "BTCUSDT";

    [BindProperty]
    public KlineInterval Interval { get; set; } = KlineInterval.OneHour;

    [BindProperty]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-7).Date;

    [BindProperty]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

    public List<SelectListItem> AvailableExchanges =>
        _exchangeService.GetSupportedExchanges()
            .Select(e => new SelectListItem(e.ToDisplayString(), ((int)e).ToString()))
            .ToList();

    public List<SelectListItem> AvailableIntervals =>
        Enum.GetValues<KlineInterval>()
            .Select(i => new SelectListItem(i.ToDisplayString(), i.ToString()))
            .ToList();

    public void OnGet() { }

    public async Task<IActionResult> OnGetSymbolsAsync(int exchange, CancellationToken cancellationToken)
    {
        try
        {
            var symbols = await _exchangeService.GetSymbolsAsync((Exchange)exchange, cancellationToken);
            return new JsonResult(symbols);
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled (user navigated away) - return empty result
            return new JsonResult(Array.Empty<object>());
        }
    }

    public async Task<IActionResult> OnPostStartIngestionAsync()
    {
        try
        {
            var startTime = DateTime.SpecifyKind(StartDate, DateTimeKind.Utc);
            // Add 1 day to include the full end date, but cap at current time if it would be in the future
            var endTime = DateTime.SpecifyKind(EndDate.AddDays(1), DateTimeKind.Utc);
            var now = DateTime.UtcNow;
            if (endTime > now)
            {
                endTime = now;
            }

            var request = new IngestionRequest
            {
                Exchange = Exchange,
                Symbol = Symbol,
                Interval = Interval,
                StartTimeUtc = startTime,
                EndTimeUtc = endTime
            };

            var operationId = await _ingestionService.StartIngestionAsync(request);
            return new JsonResult(new { success = true, operationId });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public IActionResult OnGetProgress(Guid operationId)
    {
        var progress = _ingestionService.GetProgress(operationId);
        return new JsonResult(progress);
    }

    public IActionResult OnPostCancel(Guid operationId)
    {
        _ingestionService.CancelIngestion(operationId);
        return new JsonResult(new { success = true });
    }
}
