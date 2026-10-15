using System.Net;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Dtos.Reviews;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Reviews;

[Collection(ApiTestGroup.Name)]
public sealed class ReviewTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ReviewTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Review_CanBeWrittenOnce_ByTheCustomer_OnlyAfterTheWorkIsDone()
    {
        var (author, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();
        var admin = await _client.LoginAsync("admin");

        var vehicle = await _client.CreateVehicleAsync(author.AccessToken);
        var services = await _client.GetServicesForAsync(vehicle.EngineTypeId, author.AccessToken);
        var serviceId = services[0].Id;

        var pending = await _client.BookAsync(author.AccessToken, vehicle.Id, TestData.Slot(9, 13), serviceId);

        // Not completed yet: there is nothing to review.
        (await CreateAsync(author.AccessToken, pending.Id, serviceId, 5)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);

        var completed = await _client.BookAndCompleteAsync(
            _factory, author.AccessToken, vehicle, TestData.Slot(9, 9), serviceId);

        var reviewable = await (await _client.GetAsync("/api/reviews/reviewable", author.AccessToken))
            .ReadAsync<List<ReviewableAppointmentResponse>>();
        reviewable.Should().ContainSingle()
            .Which.Should().Match<ReviewableAppointmentResponse>(r =>
                r.AppointmentId == completed.Id && r.Services.Count == 1 && r.Services[0].Id == serviceId);

        // Somebody else's appointment, a service that was not part of it, a rating off the scale.
        (await CreateAsync(stranger.AccessToken, completed.Id, serviceId, 5)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await CreateAsync(author.AccessToken, completed.Id, services[1].Id, 5)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        var offScale = await CreateAsync(author.AccessToken, completed.Id, serviceId, 6);
        offScale.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await offScale.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("rating");

        var before = await SummaryAsync(serviceId, author.AccessToken);

        var created = await CreateAsync(author.AccessToken, completed.Id, serviceId, 5, "  Brzo i profesionalno.  ");
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var review = await created.ReadAsync<ReviewResponse>();
        review.Rating.Should().Be(5);
        review.Comment.Should().Be("Brzo i profesionalno.");
        review.AuthorDisplayName.Should().Be($"{author.User.FirstName} {author.User.LastName[0]}.");
        review.ServiceName.Should().Be(services[0].Name);
        review.IsMine.Should().BeTrue();

        // One review per appointment.
        (await CreateAsync(author.AccessToken, completed.Id, serviceId, 1)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        var after = await SummaryAsync(serviceId, author.AccessToken);
        after.TotalCount.Should().Be(before.TotalCount + 1);
        after.Distribution.Keys.Should().Equal(5, 4, 3, 2, 1);
        after.Distribution[5].Should().Be(before.Distribution[5] + 1);
        after.Distribution.Values.Sum().Should().Be(after.TotalCount);
        after.AverageRating.Should().BeInRange(1, 5);

        // The appointment no longer offers the review action, and nothing is left to review.
        (await (await _client.GetAsync($"/api/appointments/{completed.Id}", author.AccessToken))
            .ReadAsync<AppointmentDetailResponse>()).AllowedActions.Should().NotContain(AppointmentActions.LeaveReview);
        (await (await _client.GetAsync("/api/reviews/reviewable", author.AccessToken))
            .ReadAsync<List<ReviewableAppointmentResponse>>()).Should().BeEmpty();

        var seenByStranger = await ListAsync($"/api/reviews?serviceId={serviceId}", stranger.AccessToken);
        seenByStranger.Items.Should().ContainSingle(r => r.Id == review.Id).Which.IsMine.Should().BeFalse();

        // Moderation: a hidden review disappears for others and from the figures, not for its author.
        (await _client.SendJsonAsync(
                HttpMethod.Patch, $"/api/reviews/{review.Id}/visibility", new ReviewVisibilityRequest(), stranger.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendJsonAsync(
                HttpMethod.Patch, $"/api/reviews/{review.Id}/visibility", new ReviewVisibilityRequest(), admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await ListAsync($"/api/reviews?serviceId={serviceId}", stranger.AccessToken))
            .Items.Should().NotContain(r => r.Id == review.Id);
        (await ListAsync("/api/reviews?mine=true", author.AccessToken))
            .Items.Should().ContainSingle().Which.IsVisible.Should().BeFalse();
        (await SummaryAsync(serviceId, author.AccessToken)).TotalCount.Should().Be(before.TotalCount);

        // Only the author (or an administrator) can delete it.
        (await _client.DeleteAsync($"/api/reviews/{review.Id}", stranger.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/reviews/{review.Id}", author.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync("/api/reviews?mine=true", author.AccessToken)).Items.Should().BeEmpty();
    }

    private Task<HttpResponseMessage> CreateAsync(
        string accessToken,
        int appointmentId,
        int serviceId,
        int rating,
        string? comment = null) =>
        _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/reviews",
            new ReviewCreateRequest
            {
                AppointmentId = appointmentId,
                ServiceId = serviceId,
                Rating = rating,
                Comment = comment,
            },
            accessToken);

    private async Task<ReviewSummaryResponse> SummaryAsync(int serviceId, string accessToken) =>
        await (await _client.GetAsync($"/api/reviews/summary?serviceId={serviceId}", accessToken))
            .ReadAsync<ReviewSummaryResponse>();

    private async Task<PagedResult<ReviewResponse>> ListAsync(string url, string accessToken)
    {
        var response = await _client.GetAsync(url, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadAsync<PagedResult<ReviewResponse>>();
    }
}
