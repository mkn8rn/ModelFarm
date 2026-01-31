using Microsoft.Extensions.DependencyInjection;
using ModelFarm.Application.ML;
using ModelFarm.Application.Services;
using ModelFarm.Application.Tasks;
using ModelFarm.Application.Tasks.Handlers;
using ModelFarm.Infrastructure;

namespace ModelFarm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddInfrastructure();

        // Register the centralized background task manager (singleton for shared state)
        services.AddSingleton<IBackgroundTaskManager, BackgroundTaskManager>();

        // Register task handlers (scoped to allow DI of scoped services)
        services.AddScoped<IBackgroundTaskHandler, DataIngestionTaskHandler>();

        // Register ML services
        services.AddSingleton<IModelTrainer>(sp => new TorchModelTrainer(seed: 42));
        services.AddSingleton<BacktestEngine>();

        // Register application services (scoped)
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IExchangeService, ExchangeService>();
        services.AddScoped<IDatasetService, DatasetService>();
        services.AddScoped<ITrainingService, TrainingService>();

        // Background services
        services.AddHostedService<TaskProcessorService>();
        services.AddHostedService<OperationRecoveryService>();
        services.AddHostedService<TrainingJobRecoveryService>();

        return services;
    }
}
