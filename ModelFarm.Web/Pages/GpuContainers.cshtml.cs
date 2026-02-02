using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Resources;
using TorchSharp;

namespace ModelFarm.Web.Pages;

public class GpuContainersModel : PageModel
{
    private readonly IResourceContainerService _containerService;

    public GpuContainersModel(IResourceContainerService containerService)
    {
        _containerService = containerService;
    }

    public bool CudaAvailable { get; private set; }
    public int GpuCount { get; private set; }

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public int MaxCapacity { get; set; } = 1;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public bool IsDefault { get; set; }

    [BindProperty]
    public Guid ContainerId { get; set; }

    public void OnGet()
    {
        CudaAvailable = torch.cuda.is_available();
        GpuCount = CudaAvailable ? (int)torch.cuda.device_count() : 0;
    }

    public IActionResult OnGetContainers()
    {
        var containers = _containerService.GetAllContainerStatus()
            .Where(c => c.Type == ResourceType.GPU)
            .ToList();
        return new JsonResult(containers);
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var request = new CreateContainerRequest
            {
                Name = Name,
                Type = ResourceType.GPU,
                MaxCapacity = MaxCapacity,
                Description = Description,
                IsDefault = IsDefault
            };

            var container = await _containerService.CreateContainerAsync(request);
            return new JsonResult(new { success = true, container });
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
            var deleted = await _containerService.DeleteContainerAsync(ContainerId);
            return new JsonResult(new { success = deleted });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }
}
