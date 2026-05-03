using ProductCatalog.Features.Category.Shared;

namespace ProductCatalog.GraphQL.Types;

/// <summary>
///     Hot Chocolate object type that maps <see cref="CategoryResponse" /> to the GraphQL schema,
///     including a <c>products</c> field resolved via <see cref="CategoryTypeResolvers" />.
/// </summary>
public sealed class CategoryType : ObjectType<CategoryResponse>
{
    protected override void Configure(IObjectTypeDescriptor<CategoryResponse> descriptor)
    {
        descriptor.Description("Represents a category in the product catalog.");

        descriptor
            .Field(c => c.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the category.");

        descriptor
            .Field(c => c.Name)
            .Type<NonNullType<StringType>>()
            .Description("The name of the category.");

        descriptor
            .Field(c => c.Description)
            .Description("The optional description of the category.");

        descriptor
            .Field(c => c.CreatedAtUtc)
            .Description("The UTC timestamp of when the category was created.");

        descriptor
            .Field("products")
            .ResolveWith<CategoryTypeResolvers>(r =>
                r.GetProducts(default!, default!, default!, default)
            )
            .Description("The products belonging to this category.");
    }
}
