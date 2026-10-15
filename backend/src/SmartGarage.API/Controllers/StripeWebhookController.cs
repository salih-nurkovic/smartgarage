using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Services;

namespace SmartGarage.API.Controllers;

/// <summary>
/// Where Stripe reports what happened to a payment.
/// <para>
/// The one write endpoint in the API without a JWT, because Stripe has no way to send one. It
/// is not open for that: every request must carry a <c>Stripe-Signature</c> computed with the
/// webhook secret, and one that does not verify is refused before anything in it is used.
/// </para>
/// </summary>
[ApiController]
[Route("api/webhooks/stripe")]
public sealed class StripeWebhookController : ControllerBase
{
    private const string SignatureHeader = "Stripe-Signature";

    private readonly IPaymentService _payments;

    public StripeWebhookController(IPaymentService payments)
    {
        _payments = payments;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // The signature covers the exact bytes Stripe sent, so the body is read raw rather
        // than bound to a model and re-serialised.
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        await _payments.HandleWebhookAsync(payload, Request.Headers[SignatureHeader].ToString(), ct);
        return Ok();
    }
}
