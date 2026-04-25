using Identity.Persistence;

namespace Identity.Auth.Security.Sessions;

public interface IBffSessionDbContextFactory
{
    IBffSessionDbContextLease Create();
}

public interface IBffSessionDbContextLease : IAsyncDisposable
{
    IdentityDbContext DbContext { get; }
}
