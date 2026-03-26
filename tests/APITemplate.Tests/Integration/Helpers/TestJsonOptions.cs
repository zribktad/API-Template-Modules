using System.Text.Json;

namespace APITemplate.Tests.Integration.Helpers;

internal static class TestJsonOptions
{
    internal static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
