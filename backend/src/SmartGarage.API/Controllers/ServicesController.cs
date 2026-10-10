using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.API.Extensions;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>The service catalogue, plus the image each service is shown with.</summary>
[Route("api/services")]
public sealed class ServicesController
    : AdminManagedController<ServiceResponse, ServiceSearchRequest, ServiceRequest>
{
    private readonly IServiceCatalogService _catalog;

    public ServicesController(IServiceCatalogService catalog)
        : base(catalog)
    {
        _catalog = catalog;
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetImage(int id, CancellationToken ct)
    {
        var image = await _catalog.GetImageAsync(id, ct);
        return File(image.Content, image.ContentType);
    }

    [HttpPost("{id:int}/image")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ServiceResponse>> SetImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        return Ok(await _catalog.SetImageAsync(id, file.ToUpload(content), ct));
    }
}
