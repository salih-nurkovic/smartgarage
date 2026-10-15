using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Payments;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Controllers;

/// <summary>
/// Payments. A customer starts one and checks on it; the server decides the amount and whether
/// it succeeded. Refunds are an administrator's decision.
/// </summary>
[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    [HttpPost("intent")]
    [Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<PaymentIntentResponse>> CreateIntent(
        PaymentIntentRequest request,
        CancellationToken ct) =>
        Ok(await _payments.CreateIntentAsync(request, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentResponse>>> Get(
        [FromQuery] PaymentSearchRequest request,
        CancellationToken ct) =>
        Ok(await _payments.GetAsync(request, ct));

    [HttpGet("summary")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<PaymentSummaryResponse>> GetSummary(CancellationToken ct) =>
        Ok(await _payments.GetSummaryAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await _payments.GetByIdAsync(id, ct));

    [HttpPost("{id:int}/refund")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<PaymentResponse>> Refund(int id, RefundRequest request, CancellationToken ct) =>
        Ok(await _payments.RefundAsync(id, request, ct));
}
