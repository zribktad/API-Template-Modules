using BuildingBlocks.Application.Configuration;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Common;

[Trait("Category", "Unit")]
public sealed class ConfigurationExtensionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void SectionFor_StripsOptionsSuffix_WhenTypeNameEndsWithOptions()
    {
        IConfiguration config = BuildConfig(
            new Dictionary<string, string?> { ["Email:SmtpHost"] = "smtp.example.com" }
        );

        IConfigurationSection section = config.SectionFor<EmailSectionOptions>();

        section.Key.ShouldBe("EmailSection");
        section.GetValue<string>("SmtpHost").ShouldBeNull(); // different key, no match
    }

    [Fact]
    public void SectionFor_UsesTypeName_WhenNoOptionsSuffix()
    {
        IConfiguration config = BuildConfig(
            new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = "mongodb://localhost",
            }
        );

        IConfigurationSection section = config.SectionFor<MongoDbSettings>();

        section.Key.ShouldBe("MongoDbSettings");
        section.GetValue<string>("ConnectionString").ShouldBe("mongodb://localhost");
    }

    [Fact]
    public void SectionFor_ReturnsSectionMatchingStrippedName()
    {
        IConfiguration config = BuildConfig(
            new Dictionary<string, string?> { ["Email:SmtpHost"] = "smtp.example.com" }
        );

        IConfigurationSection section = config.SectionFor<EmailOptions>();

        section.Key.ShouldBe("Email");
        section.GetValue<string>("SmtpHost").ShouldBe("smtp.example.com");
    }

    [Fact]
    public void SectionFor_ReturnsEmptySection_WhenKeyAbsent()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>());

        IConfigurationSection section = config.SectionFor<EmailOptions>();

        section.Key.ShouldBe("Email");
        section.Exists().ShouldBeFalse();
    }

    // Stub types used only by these tests
    private sealed class EmailSectionOptions;

    private sealed class MongoDbSettings;

    private sealed class EmailOptions;
}
