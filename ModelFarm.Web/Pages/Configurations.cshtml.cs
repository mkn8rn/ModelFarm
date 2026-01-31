using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Web.Pages;

public class ConfigurationsModel : PageModel
{
    private readonly IDatasetService _datasetService;
    private readonly ITrainingService _trainingService;

    public ConfigurationsModel(
        IDatasetService datasetService,
        ITrainingService trainingService)
    {
        _datasetService = datasetService;
        _trainingService = trainingService;
    }

    // ==================== Training Config Form ====================
    [BindProperty]
    public ConfigurationType ConfigurationType { get; set; } = ConfigurationType.QuantStrategy;

    [BindProperty]
    public string ConfigName { get; set; } = "";

    [BindProperty]
    public Guid SelectedDatasetId { get; set; }

    [BindProperty]
    public ModelType SelectedModelType { get; set; } = ModelType.MLP;

    [BindProperty]
    public int MaxLags { get; set; } = 4;

    [BindProperty]
    public int ForecastHorizon { get; set; } = 1;

    [BindProperty]
    public double LearningRate { get; set; } = 0.001;

    [BindProperty]
    public int BatchSize { get; set; } = 32;

    [BindProperty]
    public int MaxEpochs { get; set; } = 10000;

    [BindProperty]
    public int EarlyStoppingPatience { get; set; } = 50;

    [BindProperty]
    public bool UseEarlyStopping { get; set; } = true;

    [BindProperty]
    public double ValidationSplit { get; set; } = 0.2;

    [BindProperty]
    public double TestSplit { get; set; } = 0.1;

    [BindProperty]
    public int RandomSeed { get; set; } = 42;

    [BindProperty]
    public double DropoutRate { get; set; } = 0.2;

    // Checkpoint Settings
    [BindProperty]
    public bool SaveCheckpoints { get; set; } = true;

    [BindProperty]
    public int CheckpointIntervalEpochs { get; set; } = 50;

    // Retry Settings
    [BindProperty]
    public bool RetryUntilSuccess { get; set; } = false;

    [BindProperty]
    public int MaxRetryAttempts { get; set; } = 10;

    [BindProperty]
    public bool ShuffleOnRetry { get; set; } = false;

    [BindProperty]
    public bool ScaleLearningRateOnRetry { get; set; } = false;

    [BindProperty]
    public double LearningRateRetryScale { get; set; } = 0.5;

    [BindProperty]
    public double? MinSharpeRatio { get; set; } = 1.0;

    [BindProperty]
    public double? MaxDrawdown { get; set; } = 0.25;

    [BindProperty]
    public decimal InitialCapital { get; set; } = 10000m;

    [BindProperty]
    public decimal MakerFee { get; set; } = 0.001m;

    [BindProperty]
    public decimal TakerFee { get; set; } = 0.001m;

    [BindProperty]
    public decimal Slippage { get; set; } = 0.0005m;

    // ==================== Select Lists ====================
    public List<SelectListItem> AvailableConfigurationTypes =>
        Enum.GetValues<ConfigurationType>()
            .Select(t => new SelectListItem(t switch
            {
                ConfigurationType.QuantStrategy => "Quant Strategy",
                _ => t.ToString()
            }, t.ToString()))
            .ToList();

    public List<SelectListItem> AvailableModelTypes =>
        Enum.GetValues<ModelType>()
            .Select(m => new SelectListItem(m.ToString(), m.ToString()))
            .ToList();

    public void OnGet() { }

    // ==================== Dataset Endpoints ====================
    public async Task<IActionResult> OnGetDatasetsAsync()
    {
        var datasets = await _datasetService.GetAllDatasetsAsync();
        return new JsonResult(datasets);
    }

    // ==================== Training Config Endpoints ====================
    public async Task<IActionResult> OnPostCreateConfigAsync()
    {
        try
        {
            var request = new CreateTrainingConfigurationRequest
            {
                Type = ConfigurationType,
                Name = ConfigName,
                DatasetId = SelectedDatasetId,
                ModelType = SelectedModelType,
                MaxLags = MaxLags,
                ForecastHorizon = ForecastHorizon,
                LearningRate = LearningRate,
                BatchSize = BatchSize,
                MaxEpochs = MaxEpochs,
                EarlyStoppingPatience = EarlyStoppingPatience,
                UseEarlyStopping = UseEarlyStopping,
                ValidationSplit = ValidationSplit,
                TestSplit = TestSplit,
                RandomSeed = RandomSeed,
                DropoutRate = DropoutRate,
                SaveCheckpoints = SaveCheckpoints,
                CheckpointIntervalEpochs = CheckpointIntervalEpochs,
                RetryUntilSuccess = RetryUntilSuccess,
                MaxRetryAttempts = MaxRetryAttempts,
                ShuffleOnRetry = ShuffleOnRetry,
                ScaleLearningRateOnRetry = ScaleLearningRateOnRetry,
                LearningRateRetryScale = LearningRateRetryScale,
                PerformanceRequirements = new PerformanceRequirements
                {
                    MinSharpeRatio = MinSharpeRatio,
                    MaxDrawdown = MaxDrawdown
                },
                TradingEnvironment = new TradingEnvironmentConfig
                {
                    InitialCapital = InitialCapital,
                    BaseCurrency = "USDT",
                    Fees = new TradingFees
                    {
                        MakerFeeRate = MakerFee,
                        TakerFeeRate = TakerFee
                    },
                    Slippage = new SlippageConfig
                    {
                        FixedSlippageRate = Slippage
                    }
                }
            };

            var config = await _trainingService.CreateConfigurationAsync(request);
            return new JsonResult(new { success = true, config });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnGetConfigsAsync()
    {
        var configs = await _trainingService.GetAllConfigurationsAsync();
        return new JsonResult(configs);
    }

    public async Task<IActionResult> OnPostDeleteConfigAsync(Guid configId)
    {
        var deleted = await _trainingService.DeleteConfigurationAsync(configId);
        return new JsonResult(new { success = deleted });
    }
}
