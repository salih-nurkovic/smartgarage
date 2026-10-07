using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartGarage.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProfileImagePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EngineTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IconPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleBrands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LogoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleBrands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HireDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAvailableForAssignment = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    RelatedEntityId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    JwtId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecommendedMileageIntervalKm = table.Column<int>(type: "int", nullable: true),
                    RecommendedMonthsInterval = table.Column<int>(type: "int", nullable: true),
                    MinVehicleAgeYears = table.Column<int>(type: "int", nullable: true),
                    MaxVehicleAgeYears = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.CheckConstraint("CK_Service_Duration", "[EstimatedDurationMinutes] >= 5 AND [EstimatedDurationMinutes] <= 1440");
                    table.CheckConstraint("CK_Service_Price", "[Price] > 0");
                    table.ForeignKey(
                        name: "FK_Services_ServiceCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ServiceCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VehicleModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleModels_VehicleBrands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "VehicleBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeProfileId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSchedules", x => x.Id);
                    table.CheckConstraint("CK_EmployeeSchedule_EndAfterStart", "[EndTime] > [StartTime]");
                    table.ForeignKey(
                        name: "FK_EmployeeSchedules_EmployeeProfiles_EmployeeProfileId",
                        column: x => x.EmployeeProfileId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceEngineTypeLinks",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    EngineTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceEngineTypeLinks", x => new { x.ServiceId, x.EngineTypeId });
                    table.ForeignKey(
                        name: "FK_ServiceEngineTypeLinks_EngineTypes_EngineTypeId",
                        column: x => x.EngineTypeId,
                        principalTable: "EngineTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceEngineTypeLinks_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTagLinks",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    ServiceTagId = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false, defaultValue: 1.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTagLinks", x => new { x.ServiceId, x.ServiceTagId });
                    table.CheckConstraint("CK_ServiceTagLink_Weight", "[Weight] > 0 AND [Weight] <= 1");
                    table.ForeignKey(
                        name: "FK_ServiceTagLinks_ServiceTags_ServiceTagId",
                        column: x => x.ServiceTagId,
                        principalTable: "ServiceTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceTagLinks_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    EngineTypeId = table.Column<int>(type: "int", nullable: false),
                    ProductionYear = table.Column<int>(type: "int", nullable: false),
                    RegistrationPlate = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Vin = table.Column<string>(type: "nchar(17)", fixedLength: true, maxLength: 17, nullable: false),
                    CurrentMileageKm = table.Column<int>(type: "int", nullable: false),
                    LastMileageUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.CheckConstraint("CK_Vehicle_Mileage", "[CurrentMileageKm] >= 0 AND [CurrentMileageKm] <= 3000000");
                    table.CheckConstraint("CK_Vehicle_ProductionYear", "[ProductionYear] >= 1950");
                    table.ForeignKey(
                        name: "FK_Vehicles_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_EngineTypes_EngineTypeId",
                        column: x => x.EngineTypeId,
                        principalTable: "EngineTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_VehicleBrands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "VehicleBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_VehicleModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "VehicleModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentNumber = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    AssignedEmployeeId = table.Column<int>(type: "int", nullable: true),
                    ScheduledStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MileageAtAppointmentKm = table.Column<int>(type: "int", nullable: true),
                    CustomerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledByUserId = table.Column<int>(type: "int", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointment_EndAfterStart", "[ScheduledEndUtc] > [ScheduledStartUtc]");
                    table.CheckConstraint("CK_Appointment_TotalPrice", "[TotalPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_Appointments_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointments_AspNetUsers_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appointments_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_EmployeeProfiles_AssignedEmployeeId",
                        column: x => x.AssignedEmployeeId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Appointments_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecommendationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    ExplanationText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExplanationFactorsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WasClicked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecommendationLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecommendationLogs_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecommendationLogs_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserServiceSignals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    SignalType = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserServiceSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserServiceSignals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserServiceSignals_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserServiceSignals_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppointmentItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DurationMinutesSnapshot = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentItems", x => x.Id);
                    table.CheckConstraint("CK_AppointmentItem_LineTotal", "[LineTotal] >= 0");
                    table.CheckConstraint("CK_AppointmentItem_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_AppointmentItem_UnitPrice", "[UnitPriceSnapshot] >= 0");
                    table.ForeignKey(
                        name: "FK_AppointmentItems_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentItems_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentStatusHistories_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentNumber = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    PayerId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    AmountChargedStripe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StripeCurrency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StripeChargeId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payment_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Payments_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_PayerId",
                        column: x => x.PayerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    AssignedEmployeeId = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Review_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                    table.ForeignKey(
                        name: "FK_Reviews_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reviews_EmployeeProfiles_AssignedEmployeeId",
                        column: x => x.AssignedEmployeeId,
                        principalTable: "EmployeeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reviews_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueMileageKm = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    GeneratedFromAppointmentId = table.Column<int>(type: "int", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceReminders", x => x.Id);
                    table.CheckConstraint("CK_ServiceReminder_HasDueCondition", "[DueDateUtc] IS NOT NULL OR [DueMileageKm] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_ServiceReminders_Appointments_GeneratedFromAppointmentId",
                        column: x => x.GeneratedFromAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServiceReminders_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceReminders_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StripeEventId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingResult = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RawPayloadHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEvents_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    StripeRefundId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.CheckConstraint("CK_Refund_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Refunds_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "EngineTypes",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "diesel", true, "Dizel" },
                    { 2, "petrol", true, "Benzin" },
                    { 3, "hybrid", true, "Hibrid" },
                    { 4, "electric", true, "Električni" },
                    { 5, "lpg", true, "Plin (LPG)" }
                });

            migrationBuilder.InsertData(
                table: "ServiceCategories",
                columns: new[] { "Id", "DeletedAtUtc", "DeletedByUserId", "Description", "DisplayOrder", "IconPath", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, null, null, "Planirani servisi koji produžavaju vijek trajanja vozila.", 1, null, true, "Redovno održavanje" },
                    { 2, null, null, "Pregled i zamjena kočionog sistema.", 2, null, true, "Kočnice" },
                    { 3, null, null, "Mehanički radovi na motoru i pogonskom sklopu.", 3, null, true, "Motor" },
                    { 4, null, null, "Montaža, balansiranje i zamjena guma.", 4, null, true, "Gume" },
                    { 5, null, null, "Kompjuterska dijagnostika i otklanjanje grešaka.", 5, null, true, "Dijagnostika" },
                    { 6, null, null, "Akumulator, alternator, osvjetljenje i instalacije.", 6, null, true, "Elektrika" },
                    { 7, null, null, "Punjenje, dezinfekcija i popravka klima uređaja.", 7, null, true, "Klima" },
                    { 8, null, null, "Limarski i lakirerski radovi.", 8, null, true, "Karoserija" }
                });

            migrationBuilder.InsertData(
                table: "ServiceTags",
                columns: new[] { "Id", "IsActive", "Name", "Slug" },
                values: new object[,]
                {
                    { 1, true, "Dizel", "diesel" },
                    { 2, true, "Benzin", "petrol" },
                    { 3, true, "Velika kilometraža", "high-mileage" },
                    { 4, true, "Starije vozilo", "older-vehicle" },
                    { 5, true, "Preventiva", "preventive" },
                    { 6, true, "Ulje", "oil" },
                    { 7, true, "Filter", "filter" },
                    { 8, true, "Sigurnost", "safety" },
                    { 9, true, "Godišnje", "annual" },
                    { 10, true, "Zima", "winter" },
                    { 11, true, "Ljeto", "summer" },
                    { 12, true, "DPF", "dpf" },
                    { 13, true, "Zupčasti kaiš", "timing-belt" },
                    { 14, true, "Akumulator", "battery" },
                    { 15, true, "Kočnice", "brakes" },
                    { 16, true, "Ovjes", "suspension" },
                    { 17, true, "Elektronika", "electronics" },
                    { 18, true, "Hlađenje", "cooling" },
                    { 19, true, "Mjenjač", "transmission" },
                    { 20, true, "Tehnički pregled", "inspection" }
                });

            migrationBuilder.InsertData(
                table: "VehicleBrands",
                columns: new[] { "Id", "IsActive", "LogoPath", "Name" },
                values: new object[,]
                {
                    { 1, true, null, "Volkswagen" },
                    { 2, true, null, "Audi" },
                    { 3, true, null, "BMW" },
                    { 4, true, null, "Mercedes-Benz" },
                    { 5, true, null, "Škoda" },
                    { 6, true, null, "Toyota" },
                    { 7, true, null, "Renault" },
                    { 8, true, null, "Opel" },
                    { 9, true, null, "Ford" },
                    { 10, true, null, "Peugeot" }
                });

            migrationBuilder.InsertData(
                table: "VehicleModels",
                columns: new[] { "Id", "BrandId", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, 1, true, "Golf" },
                    { 2, 1, true, "Passat" },
                    { 3, 1, true, "Polo" },
                    { 4, 1, true, "Tiguan" },
                    { 5, 2, true, "A3" },
                    { 6, 2, true, "A4" },
                    { 7, 2, true, "A6" },
                    { 8, 2, true, "Q5" },
                    { 9, 3, true, "Serija 3" },
                    { 10, 3, true, "Serija 5" },
                    { 11, 3, true, "X3" },
                    { 12, 3, true, "M3" },
                    { 13, 4, true, "A-klasa" },
                    { 14, 4, true, "C-klasa" },
                    { 15, 4, true, "E-klasa" },
                    { 16, 4, true, "GLC" },
                    { 17, 5, true, "Octavia" },
                    { 18, 5, true, "Fabia" },
                    { 19, 5, true, "Superb" },
                    { 20, 5, true, "Kodiaq" },
                    { 21, 6, true, "Corolla" },
                    { 22, 6, true, "Yaris" },
                    { 23, 6, true, "RAV4" },
                    { 24, 6, true, "Avensis" },
                    { 25, 7, true, "Clio" },
                    { 26, 7, true, "Megane" },
                    { 27, 7, true, "Captur" },
                    { 28, 7, true, "Scenic" },
                    { 29, 8, true, "Astra" },
                    { 30, 8, true, "Corsa" },
                    { 31, 8, true, "Insignia" },
                    { 32, 8, true, "Zafira" },
                    { 33, 9, true, "Focus" },
                    { 34, 9, true, "Fiesta" },
                    { 35, 9, true, "Mondeo" },
                    { 36, 9, true, "Kuga" },
                    { 37, 10, true, "208" },
                    { 38, 10, true, "308" },
                    { 39, 10, true, "3008" },
                    { 40, 10, true, "508" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentItems_AppointmentId_ServiceId",
                table: "AppointmentItems",
                columns: new[] { "AppointmentId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentItems_ServiceId",
                table: "AppointmentItems",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentNumber",
                table: "Appointments",
                column: "AppointmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AssignedEmployeeId_ScheduledStartUtc",
                table: "Appointments",
                columns: new[] { "AssignedEmployeeId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CancelledByUserId",
                table: "Appointments",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ConfirmedByUserId",
                table: "Appointments",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId_ScheduledStartUtc",
                table: "Appointments",
                columns: new[] { "CustomerId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status_ScheduledStartUtc",
                table: "Appointments",
                columns: new[] { "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_VehicleId_ScheduledStartUtc",
                table: "Appointments",
                columns: new[] { "VehicleId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistories_AppointmentId_ChangedAtUtc",
                table: "AppointmentStatusHistories",
                columns: new[] { "AppointmentId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentStatusHistories_ChangedByUserId",
                table: "AppointmentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsActive",
                table: "AspNetUsers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeProfiles_UserId",
                table: "EmployeeProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSchedules_EmployeeProfileId_DayOfWeek",
                table: "EmployeeSchedules",
                columns: new[] { "EmployeeProfileId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngineTypes_Code",
                table: "EngineTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngineTypes_Name",
                table: "EngineTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CorrelationId",
                table: "ErrorLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Level_OccurredAtUtc",
                table: "ErrorLogs",
                columns: new[] { "Level", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OccurredAtUtc",
                table: "ErrorLogs",
                column: "OccurredAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_UserId",
                table: "ErrorLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_PaymentId",
                table: "PaymentEvents",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_StripeEventId",
                table: "PaymentEvents",
                column: "StripeEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_AppointmentId_Status",
                table: "Payments",
                columns: new[] { "AppointmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PayerId_CreatedAtUtc",
                table: "Payments",
                columns: new[] { "PayerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentNumber",
                table: "Payments",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StripePaymentIntentId",
                table: "Payments",
                column: "StripePaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationLogs_ServiceId",
                table: "RecommendationLogs",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationLogs_UserId_VehicleId_GeneratedAtUtc",
                table: "RecommendationLogs",
                columns: new[] { "UserId", "VehicleId", "GeneratedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationLogs_VehicleId",
                table: "RecommendationLogs",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_JwtId",
                table: "RefreshTokens",
                column: "JwtId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAtUtc",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RequestedByUserId",
                table: "Refunds",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_StripeRefundId",
                table: "Refunds",
                column: "StripeRefundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AppointmentId",
                table: "Reviews",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AssignedEmployeeId",
                table: "Reviews",
                column: "AssignedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AuthorId",
                table: "Reviews",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ServiceId_IsVisible",
                table: "Reviews",
                columns: new[] { "ServiceId", "IsVisible" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_DisplayOrder",
                table: "ServiceCategories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_Name",
                table: "ServiceCategories",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceEngineTypeLinks_EngineTypeId",
                table: "ServiceEngineTypeLinks",
                column: "EngineTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReminders_GeneratedFromAppointmentId",
                table: "ServiceReminders",
                column: "GeneratedFromAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReminders_ServiceId",
                table: "ServiceReminders",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReminders_Status_DueDateUtc",
                table: "ServiceReminders",
                columns: new[] { "Status", "DueDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReminders_VehicleId_Status",
                table: "ServiceReminders",
                columns: new[] { "VehicleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Services_CategoryId_IsActive",
                table: "Services",
                columns: new[] { "CategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Services_Name",
                table: "Services",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Services_Price",
                table: "Services",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTagLinks_ServiceTagId",
                table: "ServiceTagLinks",
                column: "ServiceTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTags_Name",
                table: "ServiceTags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTags_Slug",
                table: "ServiceTags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserServiceSignals_ServiceId",
                table: "UserServiceSignals",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServiceSignals_UserId_OccurredAtUtc",
                table: "UserServiceSignals",
                columns: new[] { "UserId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserServiceSignals_UserId_ServiceId",
                table: "UserServiceSignals",
                columns: new[] { "UserId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserServiceSignals_VehicleId_SignalType",
                table: "UserServiceSignals",
                columns: new[] { "VehicleId", "SignalType" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleBrands_Name",
                table: "VehicleBrands",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleModels_BrandId_Name",
                table: "VehicleModels",
                columns: new[] { "BrandId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_BrandId",
                table: "Vehicles",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_EngineTypeId",
                table: "Vehicles",
                column: "EngineTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ModelId",
                table: "Vehicles",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OwnerId",
                table: "Vehicles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegistrationPlate",
                table: "Vehicles",
                column: "RegistrationPlate",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Vin",
                table: "Vehicles",
                column: "Vin",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentItems");

            migrationBuilder.DropTable(
                name: "AppointmentStatusHistories");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "EmployeeSchedules");

            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PaymentEvents");

            migrationBuilder.DropTable(
                name: "RecommendationLogs");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "ServiceEngineTypeLinks");

            migrationBuilder.DropTable(
                name: "ServiceReminders");

            migrationBuilder.DropTable(
                name: "ServiceTagLinks");

            migrationBuilder.DropTable(
                name: "UserServiceSignals");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "ServiceTags");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "ServiceCategories");

            migrationBuilder.DropTable(
                name: "EmployeeProfiles");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "EngineTypes");

            migrationBuilder.DropTable(
                name: "VehicleModels");

            migrationBuilder.DropTable(
                name: "VehicleBrands");
        }
    }
}
