namespace Notifications.Errors;

public static class ErrorCatalog
{
    public static class Templates
    {
        public const string ParseFailed = "NTF-0500-TEMPLATE-PARSE";
        public const string NotFound = "NTF-0500-TEMPLATE-NOT-FOUND";
    }

    public static class Smtp
    {
        public const string SendFailed = "NTF-0500-SMTP-SEND";
    }
}
