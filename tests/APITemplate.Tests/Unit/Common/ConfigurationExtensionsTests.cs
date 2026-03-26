using APITemplate.Api.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Common;

public sealed class ConfigurationExtensionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void SectionFor_StripsOptionsSuffix_WhenTypeNameEndsWithOptions()
    {
        var config = BuildConfig(new() { ["Email:SmtpHost"] = "smtp.example.com" });

        var section = config.SectionFor<EmailSectionOptions>();

        section.Key.ShouldBe("EmailSection");
        section.GetValue<string>("SmtpHost").ShouldBeNull(); // different key, no match
    }

    [Fact]
    public void SectionFor_UsesTypeName_WhenNoOptionsSuffix()
    {
        var config = BuildConfig(
            new() { ["MongoDbSettings:ConnectionString"] = "mongodb://localhost" }
        );

        var section = config.SectionFor<MongoDbSettings>();

        section.Key.ShouldBe("MongoDbSettings");
        section.GetValue<string>("ConnectionString").ShouldBe("mongodb://localhost");
    }

    [Fact]
    public void SectionFor_ReturnsSectionMatchingStrippedName()
    {
        var config = BuildConfig(new() { ["Email:SmtpHost"] = "smtp.example.com" });

        var section = config.SectionFor<EmailOptions>();

        section.Key.ShouldBe("Email");
        section.GetValue<string>("SmtpHost").ShouldBe("smtp.example.com");
    }

    [Fact]
    public void SectionFor_ReturnsEmptySection_WhenKeyAbsent()
    {
        var config = BuildConfig(new());

        var section = config.SectionFor<EmailOptions>();

        section.Key.ShouldBe("Email");
        section.Exists().ShouldBeFalse();
    }

    // Stub types used only by these tests
    private sealed class EmailSectionOptions;

    private sealed class MongoDbSettings;

    private sealed class EmailOptions;
}
