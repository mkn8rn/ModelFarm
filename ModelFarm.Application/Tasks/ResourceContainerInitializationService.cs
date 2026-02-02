using Microsoft.Extensions.Hosting;
using ModelFarm.Application.Services;

namespace ModelFarm.Application.Tasks;

/// <summary>
/// Hosted service that initializes resource containers and queues at startup.
/// </summary>
public sealed class ResourceContainerInitializationService : IHostedService
{
    private readonly IResourceContainerService _containerService;
    private readonly IResourceQueueService _queueService;

    public ResourceContainerInitializationService(
        IResourceContainerService containerService,
        IResourceQueueService queueService)
    {
        _containerService = containerService;
        _queueService = queueService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[ResourceContainerInitializationService] Initializing resource containers...");
        await _containerService.EnsureDefaultContainersExistAsync(cancellationToken);
        
        var hardware = _containerService.GetDetectedHardware();
        Console.WriteLine($"[ResourceContainerInitializationService] Detected hardware: {hardware.CpuCores} CPU cores, {hardware.GpuDevices} GPU devices, CUDA: {hardware.CudaAvailable}");
        
        var containerStatus = _containerService.GetAllContainerStatus();
        foreach (var container in containerStatus)
        {
            Console.WriteLine($"[ResourceContainerInitializationService] Container '{container.Name}' ({container.Type}): {container.MaxCapacity} capacity");
        }
        
        Console.WriteLine("[ResourceContainerInitializationService] Initializing resource queues...");
        await _queueService.EnsureDefaultQueueExistsAsync(cancellationToken);
        
        var queueStatus = _queueService.GetAllQueueStatus();
        foreach (var queue in queueStatus)
        {
            Console.WriteLine($"[ResourceContainerInitializationService] Queue '{queue.Name}': {queue.CpuContainerName} + {queue.GpuContainerName}, max {queue.MaxConcurrentJobs} jobs");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
