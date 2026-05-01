namespace SharedKernel.Application.Http;

/// <summary>
///     Provides constant values for rate limiting configuration and headers.
/// </summary>
public static class RateLimitConstants
{
    public const string GlobalPolicy = "global";
    public const string FallbackPartitionKey = "unknown";

    public static class Headers
    {
        public const string Policy = "RateLimit-Policy";
        public const string Limit = "RateLimit-Limit";
    }

    public static class Policies
    {
        public const string FixedTest1 = "fixed-test-1";
        public const string FixedTest2 = "fixed-test-2";
        public const string SlidingTest1 = "sliding-test-1";
    }
}
