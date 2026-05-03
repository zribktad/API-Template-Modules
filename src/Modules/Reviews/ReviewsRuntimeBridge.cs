using BuildingBlocks.Application.Configuration;
using BuildingBlocks.Infrastructure.EFCore.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Features;
using Reviews.Persistence;
using Reviews.Repositories;

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

        return services;
    }
}
