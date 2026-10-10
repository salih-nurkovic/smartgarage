using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Services;

namespace SmartGarage.API.Controllers;

/// <summary>
/// One call that returns every list a form needs for its dropdowns. Both clients load it once
/// instead of making four requests per screen.
/// </summary>
[ApiController]
[Route("api/lookup")]
public sealed class LookupController : ControllerBase
{
    private readonly ILookupService _lookup;

    public LookupController(ILookupService lookup)
    {
        _lookup = lookup;
    }

    [HttpGet]
    public async Task<ActionResult<LookupResponse>> Get(CancellationToken ct) =>
        Ok(await _lookup.GetAsync(ct));
}
