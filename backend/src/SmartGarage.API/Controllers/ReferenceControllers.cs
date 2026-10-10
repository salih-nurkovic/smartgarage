using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Dtos.Reference;
using SmartGarage.Application.Services;

namespace SmartGarage.API.Controllers;

// The reference tables. The instructions require a CRUD form for every one of them, whether or
// not the registration document lists it. All behaviour is inherited; only the route differs.

[Route("api/vehicle-brands")]
public sealed class VehicleBrandsController(
    ICrudService<VehicleBrandResponse, ReferenceSearchRequest, VehicleBrandRequest> service)
    : AdminManagedController<VehicleBrandResponse, ReferenceSearchRequest, VehicleBrandRequest>(service);

[Route("api/vehicle-models")]
public sealed class VehicleModelsController(
    ICrudService<VehicleModelResponse, VehicleModelSearchRequest, VehicleModelRequest> service)
    : AdminManagedController<VehicleModelResponse, VehicleModelSearchRequest, VehicleModelRequest>(service);

[Route("api/engine-types")]
public sealed class EngineTypesController(
    ICrudService<EngineTypeResponse, ReferenceSearchRequest, EngineTypeRequest> service)
    : AdminManagedController<EngineTypeResponse, ReferenceSearchRequest, EngineTypeRequest>(service);

[Route("api/service-categories")]
public sealed class ServiceCategoriesController(
    ICrudService<ServiceCategoryResponse, ReferenceSearchRequest, ServiceCategoryRequest> service)
    : AdminManagedController<ServiceCategoryResponse, ReferenceSearchRequest, ServiceCategoryRequest>(service);

[Route("api/service-tags")]
public sealed class ServiceTagsController(
    ICrudService<ServiceTagResponse, ReferenceSearchRequest, ServiceTagRequest> service)
    : AdminManagedController<ServiceTagResponse, ReferenceSearchRequest, ServiceTagRequest>(service);
