using System.Security.Cryptography;
using APITemplate.Application.Common.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace APITemplate.Infrastructure.Security;

/// <summary>
/// A ticket store implementation that persists ASP.NET Core authentication tickets
/// into an <see cref="IDistributedCache"/> (typically DragonFly/Redis) under a unique key.
/// </summary>
/// <remarks>
/// This allows the authentication cookie to contain only a small key, while the full
/// <see cref="AuthenticationTicket"/> (claims + properties) is stored in a shared cache
/// and can be retrieved by any application instance.
/// </remarks>
public sealed class DragonflyTicketStore : ITicketStore
{
    private const string KeyPrefix = "bff:ticket:";

    private readonly IDistributedCache _cache;
    private readonly BffOptions _options;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of <see cref="DragonflyTicketStore"/>.
    /// </summary>
    /// <param name="cache">A distributed cache instance used to persist ticket bytes.</param>
    /// <param name="options">Configuration options for session timeout etc.</param>
    /// <param name="dataProtection">Data protection provider for encrypting ticket bytes.</param>
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

    /// <summary>
    /// Stores the given authentication ticket in the distributed cache and returns the key.
    /// </summary>
    /// <param name="ticket">The authentication ticket to store.</param>
    /// <returns>The key under which the ticket is stored.</returns>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = KeyPrefix + Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    /// <summary>
    /// Updates the cached ticket under the specified key (usually to renew its expiration).
    /// </summary>
    /// <param name="key">The cache key previously returned by <see cref="StoreAsync"/>.</param>
    /// <param name="ticket">The ticket to store.</param>
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var bytes = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.SessionTimeoutMinutes),
        };
        await _cache.SetAsync(key, bytes, entryOptions);
    }

    /// <summary>
    /// Retrieves an authentication ticket by key from the distributed cache.
    /// </summary>
    /// <param name="key">The cache key previously returned by <see cref="StoreAsync"/>.</param>
    /// <returns>The ticket if found and successfully decrypted; otherwise null.</returns>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var bytes = await _cache.GetAsync(key);
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

    /// <summary>
    /// Removes the ticket stored under the given key from the cache.
    /// </summary>
    /// <param name="key">The key of the ticket to remove.</param>
    public Task RemoveAsync(string key) => _cache.RemoveAsync(key);
}
