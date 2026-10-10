using System.Net;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Catalogue;

[Collection(ApiTestGroup.Name)]
public sealed class ServiceCatalogueTests
{
    // Enough of a PNG for its type to be recognised: the eight signature bytes and an IHDR header.
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private readonly HttpClient _client;

    public ServiceCatalogueTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Catalogue_FiltersInTheDatabase_ByPriceCategoryDurationAndText()
    {
        var customer = await _client.LoginAsync("customer");

        var all = await GetPageAsync("/api/services?pageSize=100", customer.AccessToken);
        all.Items.Should().NotBeEmpty();
        all.Items.Should().OnlyContain(s => s.IsActive);

        var sample = all.Items[all.Items.Count / 2];

        var byPrice = await GetPageAsync(
            "/api/services?minPrice=30&maxPrice=80&pageSize=100&orderBy=price", customer.AccessToken);
        byPrice.Items.Should().OnlyContain(s => s.Price >= 30 && s.Price <= 80);
        byPrice.Items.Select(s => s.Price).Should().BeInAscendingOrder();
        byPrice.TotalCount.Should().BeLessThan(all.TotalCount);

        var byCategory = await GetPageAsync(
            $"/api/services?categoryId={sample.CategoryId}&pageSize=100", customer.AccessToken);
        byCategory.Items.Should().OnlyContain(s => s.CategoryId == sample.CategoryId);

        var byDuration = await GetPageAsync("/api/services?maxDurationMinutes=30&pageSize=100", customer.AccessToken);
        byDuration.Items.Should().OnlyContain(s => s.EstimatedDurationMinutes <= 30);

        var byText = await GetPageAsync(
            $"/api/services?search={Uri.EscapeDataString(sample.Name)}", customer.AccessToken);
        byText.Items.Should().Contain(s => s.Id == sample.Id);
    }

    [Fact]
    public async Task Service_CanBeCreatedEditedAndDeleted_ByAnAdministratorOnly()
    {
        var admin = await _client.LoginAsync("admin");
        var customer = await _client.LoginAsync("customer");
        var name = $"Usluga {Guid.NewGuid():N}"[..24];

        var request = new ServiceRequest
        {
            Name = name,
            Description = "Detaljan pregled i podešavanje geometrije točkova.",
            CategoryId = 1,
            Price = 45.50m,
            EstimatedDurationMinutes = 60,
            RecommendedMileageIntervalKm = 20_000,
            IsActive = false,
            TagIds = [1, 2],
            EngineTypeIds = [1],
        };

        (await _client.SendJsonAsync(HttpMethod.Post, "/api/services", request, customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var created = await _client.SendJsonAsync(HttpMethod.Post, "/api/services", request, admin.AccessToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var service = await created.ReadAsync<ServiceResponse>();
        service.Price.Should().Be(45.50m);
        service.Tags.Select(t => t.Id).Should().BeEquivalentTo([1, 2]);
        service.Tags.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Name));
        service.EngineTypes.Should().ContainSingle();
        service.CategoryName.Should().NotBeNullOrWhiteSpace();
        service.BookingCount.Should().Be(0);

        // Inactive: staff can see it, a customer cannot.
        (await _client.GetAsync($"/api/services/{service.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/services/{service.Id}", customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updated = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"/api/services/{service.Id}",
            new ServiceRequest
            {
                Name = name,
                Description = request.Description,
                CategoryId = 1,
                Price = 60m,
                EstimatedDurationMinutes = 90,
                IsActive = true,
                TagIds = [2, 3],
            },
            admin.AccessToken);

        var edited = await updated.ReadAsync<ServiceResponse>();
        edited.Price.Should().Be(60m);
        edited.Tags.Select(t => t.Id).Should().BeEquivalentTo([2, 3]);
        edited.EngineTypes.Should().BeEmpty();

        (await _client.GetAsync($"/api/services/{service.Id}", customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.DeleteAsync($"/api/services/{service.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/services/{service.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Service_WithBadValues_IsRejectedFieldByField()
    {
        var admin = await _client.LoginAsync("admin");

        var response = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/services",
            new ServiceRequest
            {
                Name = "X",
                Description = "abc",
                CategoryId = 999_999,
                Price = 0m,
                EstimatedDurationMinutes = 7,
                MinVehicleAgeYears = 10,
                MaxVehicleAgeYears = 5,
            },
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ApiErrorResponse>()).Errors!.Keys.Should().BeEquivalentTo(
            "name", "description", "price", "estimatedDurationMinutes", "maxVehicleAgeYears");

        var unknownLinks = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/services",
            new ServiceRequest
            {
                Name = $"Usluga {Guid.NewGuid():N}"[..24],
                Description = "Ispravan opis usluge.",
                CategoryId = 999_999,
                Price = 10m,
                EstimatedDurationMinutes = 30,
                TagIds = [999_999],
                EngineTypeIds = [999_999],
            },
            admin.AccessToken);

        (await unknownLinks.ReadAsync<ApiErrorResponse>()).Errors!.Keys.Should().BeEquivalentTo(
            "categoryId", "tagIds", "engineTypeIds");
    }

    [Fact]
    public async Task ServiceImage_IsCheckedByItsBytes_AndServedOnlyToSignedInUsers()
    {
        var admin = await _client.LoginAsync("admin");
        var customer = await _client.LoginAsync("customer");
        var serviceId = (await GetPageAsync("/api/services?pageSize=1", customer.AccessToken)).Items[0].Id;
        var url = $"/api/services/{serviceId}/image";

        (await _client.PostFileAsync(url, PngBytes, "slika.png", "image/png", customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A script with an image's name and an image's content type is still a script.
        var disguised = await _client.PostFileAsync(
            url, "<?php echo 'x'; ?> not an image"u8.ToArray(), "slika.png", "image/png", admin.AccessToken);
        disguised.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await disguised.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("file");

        // Real PNG bytes declared as something else are refused too.
        (await _client.PostFileAsync(url, PngBytes, "slika.jpg", "image/jpeg", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var uploaded = await _client.PostFileAsync(url, PngBytes, "slika.png", "image/png", admin.AccessToken);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await uploaded.ReadAsync<ServiceResponse>()).ImageUrl.Should().Be(url);

        var download = await _client.GetAsync(url, customer.AccessToken);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);

        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<PagedResult<ServiceResponse>> GetPageAsync(string url, string accessToken)
    {
        var response = await _client.GetAsync(url, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadAsync<PagedResult<ServiceResponse>>();
    }
}
