using Microsoft.AspNetCore.Authentication;

namespace Identity.Auth.Security.Sessions.Lifecycle;

public interface IBffSessionRecordFactory
{
    BffSessionRecord CreateNew(AuthenticationTicket ticket, DateTimeOffset now);

    BffSessionRecord CreateUpdated(
        string sessionId,
        AuthenticationTicket ticket,
        BffSessionRecord currentSession,
        DateTimeOffset now
    );
}
