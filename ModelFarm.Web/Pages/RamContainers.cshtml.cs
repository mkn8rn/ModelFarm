using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Resources;

namespace ModelFarm.Web.Pages;

public class RamContainersModel : PageModel
{
    private readonly IResourceContainerService _containerService;

    public RamContainersModel(IResourceContainerService containerService)
    {
        _containerService = containerService;
    }

    public long TotalRam { get; private set; }
    public long AvailableRam { get; private set; }

    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public long MaxCapacity { get; set; } = 8L * 1024 * 1024 * 1024; // 8 GB default

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public bool IsDefault { get; set; }

    [BindProperty]
    public Guid ContainerId { get; set; }

    public void OnGet()
    {
        var hardware = _containerService.GetDetectedHardware();
        TotalRam = hardware.TotalRamBytes;
        AvailableRam = hardware.AvailableRamBytes;
    }

    public string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public IActionResult OnGetContainers()
    {
        var containers = _containerService.GetAllContainerStatus()
            .Where(c => c.Type == ResourceType.RAM)
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
                Type = ResourceType.RAM,
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
