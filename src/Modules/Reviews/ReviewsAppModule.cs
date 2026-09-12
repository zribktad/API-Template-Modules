using System.Reflection;
using BuildingBlocks.Application.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Reviews;

public sealed class ReviewsAppModule : IAppModule
{
    public string Name => "Reviews";

    public IEnumerable<Assembly> Assemblies => [typeof(ReviewsAppModule).Assembly];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddReviewsModule(configuration);
    }
}
