using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Resources;

namespace ModelFarm.Web.Pages;

public class QueuesModel : PageModel
{
    private readonly IResourceContainerService _containerService;
    private readonly IResourceQueueService _queueService;

    public QueuesModel(
        IResourceContainerService containerService,
        IResourceQueueService queueService)
    {
        _containerService = containerService;
        _queueService = queueService;
    }

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public Guid CpuContainerId { get; set; }

    [BindProperty]
    public Guid GpuContainerId { get; set; }

    [BindProperty]
    public Guid? RamContainerId { get; set; }

    [BindProperty]
    public int MaxConcurrentJobs { get; set; } = 1;

    [BindProperty]
    public int? MaxJobDurationMinutes { get; set; }

    [BindProperty]
    public int? MaxQueueWaitMinutes { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public bool IsDefault { get; set; }

    [BindProperty]
    public Guid QueueId { get; set; }

    public void OnGet() { }

    public IActionResult OnGetContainers()
    {
        var all = _containerService.GetAllContainerStatus();
        return new JsonResult(new
        {
            cpu = all.Where(c => c.Type == ResourceType.CPU).ToList(),
            gpu = all.Where(c => c.Type == ResourceType.GPU).ToList(),
            ram = all.Where(c => c.Type == ResourceType.RAM).ToList()
        });
    }

    public IActionResult OnGetQueues()
    {
        var queues = _queueService.GetAllQueueStatus();
        return new JsonResult(queues);
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var request = new CreateQueueRequest
            {
                Name = Name,
                CpuContainerId = CpuContainerId,
                GpuContainerId = GpuContainerId,
                RamContainerId = RamContainerId,
                MaxConcurrentJobs = MaxConcurrentJobs,
                MaxJobDuration = MaxJobDurationMinutes.HasValue ? TimeSpan.FromMinutes(MaxJobDurationMinutes.Value) : null,
                MaxQueueWaitTime = MaxQueueWaitMinutes.HasValue ? TimeSpan.FromMinutes(MaxQueueWaitMinutes.Value) : null,
                Description = Description,
                IsDefault = IsDefault
            };

            var queue = await _queueService.CreateQueueAsync(request);
            return new JsonResult(new { success = true, queue });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var deleted = await _queueService.DeleteQueueAsync(QueueId);
            return new JsonResult(new { success = deleted });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }
}
