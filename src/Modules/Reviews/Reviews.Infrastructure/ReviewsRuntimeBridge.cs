using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Features.ProductReview.Validation;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Repositories;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Registration;

namespace Reviews;

public static class ReviewsRuntimeBridge
{
    public static IServiceCollection AddReviewsRuntimeBridge(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string connectionString = configuration.GetConnectionString(ConfigurationSections.DefaultConnection)!;

        services
            .AddModule<ReviewsDbContext>(configuration)
            .ConfigureDbContext((_, options) => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .AddRepository<IProductReviewRepository, ProductReviewRepository>();

        services.AddValidatorsFromAssemblyContaining<CreateProductReviewRequestValidator>(
            filter: registration => !registration.ValidatorType.IsGenericTypeDefinition
        );

        return services;
    }
}
