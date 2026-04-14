namespace Identity.Auth.Security.Sessions.Lifecycle;

public interface IBffSessionValidator
{
    BffSessionValidationResult Validate(BffSessionRecord session, DateTimeOffset now);
}
