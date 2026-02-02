using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Resources;

namespace ModelFarm.Web.Pages;

public class CpuContainersModel : PageModel
{
    private readonly IResourceContainerService _containerService;

    public CpuContainersModel(IResourceContainerService containerService)
    {
        _containerService = containerService;
    }

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public int MaxCapacity { get; set; } = 4;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public bool IsDefault { get; set; }

    [BindProperty]
    public Guid ContainerId { get; set; }

    public void OnGet() { }

    public IActionResult OnGetContainers()
    {
        var containers = _containerService.GetAllContainerStatus()
            .Where(c => c.Type == ResourceType.CPU)
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
                Type = ResourceType.CPU,
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
