using ProductCatalog.Entities;
using ProductCatalog.SoftDelete;
using ProductCatalog.ValueObjects;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class ProductSoftDeleteCascadeRuleTests
{
    private readonly ProductSoftDeleteCascadeRule _sut = new();

    [Fact]
    public void CanHandle_WhenProduct_ReturnsTrue()
    {
        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = "P",
            Description = "D",
            Price = Price.FromPersistence(1m),
            CategoryId = Guid.NewGuid(),
        };

        _sut.CanHandle(product).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_WhenCategory_ReturnsFalse()
    {
        Category category = new() { Id = Guid.NewGuid(), Name = "C" };

        _sut.CanHandle(category).ShouldBeFalse();
    }
}
