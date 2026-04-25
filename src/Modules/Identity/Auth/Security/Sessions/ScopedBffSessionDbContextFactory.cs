using Identity.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Auth.Security.Sessions;

public sealed class ScopedBffSessionDbContextFactory : IBffSessionDbContextFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedBffSessionDbContextFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public IBffSessionDbContextLease Create()
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        try
        {
            return new ScopedBffSessionDbContextLease(scope);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private sealed class ScopedBffSessionDbContextLease : IBffSessionDbContextLease
    {
        private readonly AsyncServiceScope _scope;

        public ScopedBffSessionDbContextLease(AsyncServiceScope scope)
        {
            _scope = scope;
            DbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        }

        public IdentityDbContext DbContext { get; }

        public ValueTask DisposeAsync()
        {
            return _scope.DisposeAsync();
        }
    }
}
