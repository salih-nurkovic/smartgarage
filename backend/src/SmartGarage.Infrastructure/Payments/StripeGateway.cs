using Microsoft.Extensions.Configuration;
using Stripe;
using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Infrastructure.Payments;

/// <summary>
/// Talks to Stripe through the official SDK. Its HTTP client comes from
/// <see cref="IHttpClientFactory"/>, which the instructions require for every outbound call.
/// </summary>
public sealed class StripeGateway : IStripeGateway
{
    public const string HttpClientName = "stripe";

    private const string UnavailableMessage =
        "Platni servis trenutno nije dostupan. Pokušajte ponovo za nekoliko minuta.";

    private readonly StripeClient _client;
    private readonly string _webhookSecret;

    public StripeGateway(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        // Read once. A missing key is reported when Stripe refuses the first call, so the API
        // still starts - and everything that does not involve paying keeps working.
        PublishableKey = configuration["STRIPE_PUBLISHABLE_KEY"] ?? string.Empty;
        _webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"] ?? string.Empty;

        _client = new StripeClient(
            apiKey: configuration["STRIPE_SECRET_KEY"] ?? string.Empty,
            httpClient: new SystemNetHttpClient(httpClientFactory.CreateClient(HttpClientName)));
    }

    public string PublishableKey { get; }

    public async Task<StripeIntent> CreatePaymentIntentAsync(
        long amountMinor,
        string currency,
        IReadOnlyDictionary<string, string> metadata,
        string idempotencyKey,
        CancellationToken ct)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = amountMinor,
            Currency = currency,
            Metadata = metadata.ToDictionary(),

            // Card, and whatever else is switched on in the Stripe dashboard. The mobile payment
            // sheet shows exactly these.
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
        };

        var intent = await CallAsync(() => new PaymentIntentService(_client).CreateAsync(
            options, new RequestOptions { IdempotencyKey = idempotencyKey }, ct));

        return Map(intent);
    }

    public async Task<StripeIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct) =>
        Map(await CallAsync(() => new PaymentIntentService(_client).GetAsync(paymentIntentId, cancellationToken: ct)));

    public async Task<StripeRefundResult> CreateRefundAsync(
        string paymentIntentId,
        long amountMinor,
        string idempotencyKey,
        CancellationToken ct)
    {
        var refund = await CallAsync(() => new RefundService(_client).CreateAsync(
            new RefundCreateOptions { PaymentIntent = paymentIntentId, Amount = amountMinor },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            ct));

        return new StripeRefundResult(refund.Id, refund.Status);
    }

    public StripeWebhookEvent ParseWebhook(string payload, string signatureHeader)
    {
        // Throws StripeException when the signature, the secret or the timestamp is wrong.
        var stripeEvent = EventUtility.ConstructEvent(
            payload, signatureHeader, _webhookSecret, throwOnApiVersionMismatch: false);

        return stripeEvent.Data.Object switch
        {
            PaymentIntent intent => new StripeWebhookEvent(stripeEvent.Id, stripeEvent.Type, intent.Id, null, null),
            Charge charge => new StripeWebhookEvent(stripeEvent.Id, stripeEvent.Type, charge.PaymentIntentId, null, null),
            Refund refund => new StripeWebhookEvent(
                stripeEvent.Id, stripeEvent.Type, refund.PaymentIntentId, refund.Id, refund.Status),
            _ => new StripeWebhookEvent(stripeEvent.Id, stripeEvent.Type, null, null, null),
        };
    }

    private static StripeIntent Map(PaymentIntent intent) => new(
        intent.Id,
        intent.ClientSecret,
        intent.Status,
        intent.AmountReceived,
        intent.LatestChargeId,
        intent.LastPaymentError?.Message);

    // Stripe's own errors become one message the user can act on. The detail goes to the log
    // through the exception middleware, never to the client.
    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (StripeException exception)
        {
            throw new ExternalServiceException(UnavailableMessage, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalServiceException(UnavailableMessage, exception);
        }
    }
}
