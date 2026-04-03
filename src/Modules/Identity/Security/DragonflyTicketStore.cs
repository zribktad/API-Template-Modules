using System.Security.Cryptography;
using Identity.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Identity.Security;

/// <summary>
/// A ticket store implementation that persists ASP.NET Core authentication tickets
/// into an <see cref="IDistributedCache"/> (typically DragonFly/Redis) under a unique key,
/// allowing the authentication cookie to contain only a small key while the full ticket
/// (claims + properties) is stored in a shared cache reachable by any application instance.
/// </summary>
public sealed class DragonflyTicketStore : ITicketStore
{
    private const string KeyPrefix = "bff:ticket:";

    private readonly IDistributedCache _cache;
    private readonly BffOptions _options;
    private readonly IDataProtector _protector;

    public DragonflyTicketStore(
        IDistributedCache cache,
        IOptions<BffOptions> options,
        IDataProtectionProvider dataProtection
    )
    {
        _cache = cache;
        _options = options.Value;
        _protector = dataProtection.CreateProtector("bff:ticket");
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        string key = KeyPrefix + Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        byte[] bytes = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        DistributedCacheEntryOptions entryOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.SessionTimeoutMinutes),
        };
        await _cache.SetAsync(key, bytes, entryOptions);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        byte[]? bytes = await _cache.GetAsync(key);
        if (bytes is null)
            return null;

        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(bytes));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public Task RemoveAsync(string key) => _cache.RemoveAsync(key);
}

