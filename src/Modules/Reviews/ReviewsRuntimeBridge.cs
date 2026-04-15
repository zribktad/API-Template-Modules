using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Features;
using Reviews.Persistence;
using Reviews.Repositories;
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
        string connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        services
            .AddModule<ReviewsDbContext>(configuration)
            .ConfigureDbContext(options => options.UseNpgsql(connectionString))
            .AddDefaultInfrastructure()
            .ForwardUnitOfWork<ReviewsDbMarker>()
            .AddRepository<IProductReviewRepository, ProductReviewRepository>();

        services.AddValidatorsFromAssemblyContaining<ProductReviewFilterValidator>(filter: r =>
            !r.ValidatorType.IsGenericTypeDefinition
        );
        return services;
    }
}
