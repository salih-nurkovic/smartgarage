using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Identity's own table name is kept so the schema reads the way every ASP.NET developer
        // expects. The instructions count Identity tables as reference tables, so they do not
        // contribute to the minimum of ten main tables.
        builder.ToTable("AspNetUsers");

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(ValidationRules.NameMaxLength);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(ValidationRules.NameMaxLength);

        // Phone is required on registration, so it is NOT NULL here too. The instructions
        // require mandatory fields to be NOT NULL and to match the mandatory fields on the UI.
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(ValidationRules.EmailMaxLength);
        builder.Property(x => x.UserName).IsRequired().HasMaxLength(60);

        builder.Property(x => x.ProfileImagePath).HasMaxLength(300);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasIndex(x => x.Email).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.IsActive);

        builder.Ignore(x => x.FullName);

        builder.HasOne(x => x.EmployeeProfile)
            .WithOne(x => x.User)
            .HasForeignKey<EmployeeProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
