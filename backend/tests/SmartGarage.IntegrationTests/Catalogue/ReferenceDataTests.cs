using System.Net;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Reference;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Constants;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Catalogue;

[Collection(ApiTestGroup.Name)]
public sealed class ReferenceDataTests
{
    private readonly HttpClient _client;

    public ReferenceDataTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/vehicle-brands")]
    [InlineData("/api/vehicle-models")]
    [InlineData("/api/engine-types")]
    [InlineData("/api/service-categories")]
    [InlineData("/api/service-tags")]
    public async Task ReferenceLists_AreReadableByAnyUser_PagedAndCapped_ButWritableOnlyByAdministrators(string url)
    {
        var customer = await _client.LoginAsync("customer");

        var list = await _client.GetAsync($"{url}?pageSize=100000", customer.AccessToken);
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await list.ReadAsync<PagedResult<object>>();
        page.PageSize.Should().Be(PagingLimits.MaxPageSize);
        page.TotalCount.Should().BePositive();

        var create = await _client.SendJsonAsync(
            HttpMethod.Post, url, new { name = "Nedozvoljeno", code = "XX", slug = "xx" }, customer.AccessToken);
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await _client.DeleteAsync($"{url}/1", customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Brand_GoesThroughItsWholeLifecycle_AndCannotBeDeletedWhileItHasModels()
    {
        var admin = await _client.LoginAsync("admin");
        var brandName = $"Marka {Guid.NewGuid():N}"[..20];

        var created = await _client.SendJsonAsync(
            HttpMethod.Post, "/api/vehicle-brands", new VehicleBrandRequest { Name = brandName }, admin.AccessToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var brand = await created.ReadAsync<VehicleBrandResponse>();

        var duplicate = await _client.SendJsonAsync(
            HttpMethod.Post, "/api/vehicle-brands", new VehicleBrandRequest { Name = brandName }, admin.AccessToken);
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await duplicate.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("name");

        var model = await (await _client.SendJsonAsync(
                HttpMethod.Post,
                "/api/vehicle-models",
                new VehicleModelRequest { Name = "Prvi model", BrandId = brand.Id },
                admin.AccessToken))
            .ReadAsync<VehicleModelResponse>();

        model.BrandName.Should().Be(brandName);

        var byBrand = await (await _client.GetAsync(
                $"/api/vehicle-models?brandId={brand.Id}", admin.AccessToken))
            .ReadAsync<PagedResult<VehicleModelResponse>>();
        byBrand.Items.Should().ContainSingle().Which.Id.Should().Be(model.Id);

        // Refused, with a sentence that says what is in the way.
        var blocked = await _client.DeleteAsync($"/api/vehicle-brands/{brand.Id}", admin.AccessToken);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.ReadAsync<ApiErrorResponse>()).Detail.Should().Contain("1 modela");

        var renamed = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"/api/vehicle-brands/{brand.Id}",
            new VehicleBrandRequest { Name = brandName + "X", IsActive = false },
            admin.AccessToken);
        (await renamed.ReadAsync<VehicleBrandResponse>()).IsActive.Should().BeFalse();

        (await _client.DeleteAsync($"/api/vehicle-models/{model.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.DeleteAsync($"/api/vehicle-brands/{brand.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/vehicle-brands/{brand.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReferenceWrites_AreValidated_WithMessagesKeyedByField()
    {
        var admin = await _client.LoginAsync("admin");

        var emptyBrand = await _client.SendJsonAsync(
            HttpMethod.Post, "/api/vehicle-brands", new VehicleBrandRequest { Name = " " }, admin.AccessToken);
        emptyBrand.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await emptyBrand.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("name");

        var orphanModel = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/vehicle-models",
            new VehicleModelRequest { Name = "Bez marke", BrandId = 999_999 },
            admin.AccessToken);
        (await orphanModel.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("brandId");

        var badSlug = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/service-tags",
            new ServiceTagRequest { Name = "Nova oznaka", Slug = "Nije Slug!" },
            admin.AccessToken);
        (await badSlug.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("slug");

        var badCategory = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/service-categories",
            new ServiceCategoryRequest { Name = "K", DisplayOrder = -1 },
            admin.AccessToken);
        (await badCategory.ReadAsync<ApiErrorResponse>()).Errors!.Keys
            .Should().BeEquivalentTo("name", "displayOrder");
    }

    [Fact]
    public async Task ReferenceRow_InUse_CannotBeDeleted()
    {
        var admin = await _client.LoginAsync("admin");

        // Seeded categories, tags and engine types are all used by the seeded services.
        foreach (var url in new[] { "/api/service-categories/1", "/api/engine-types/1", "/api/service-tags/1" })
        {
            (await _client.DeleteAsync(url, admin.AccessToken))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, url);
        }
    }

    [Fact]
    public async Task Lookup_IsCached_ButShowsANewBrandImmediately()
    {
        var admin = await _client.LoginAsync("admin");
        var customer = await _client.LoginAsync("customer");

        var before = await (await _client.GetAsync("/api/lookup", customer.AccessToken)).ReadAsync<LookupResponse>();
        before.Brands.Should().NotBeEmpty();
        before.Brands.Should().OnlyContain(b => b.Models.Count > 0);
        before.EngineTypes.Should().NotBeEmpty();
        before.ServiceCategories.Should().NotBeEmpty();
        before.ServiceTags.Should().NotBeEmpty();

        var brandName = $"Lookup {Guid.NewGuid():N}"[..20];
        await _client.SendJsonAsync(
            HttpMethod.Post, "/api/vehicle-brands", new VehicleBrandRequest { Name = brandName }, admin.AccessToken);

        var after = await (await _client.GetAsync("/api/lookup", customer.AccessToken)).ReadAsync<LookupResponse>();
        after.Brands.Should().Contain(b => b.Name == brandName);
    }
}
