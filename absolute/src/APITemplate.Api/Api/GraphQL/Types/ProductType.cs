namespace APITemplate.Api.GraphQL.Types;

/// <summary>
/// Hot Chocolate object type that maps <see cref="ProductResponse"/> to the GraphQL schema,
/// including a <c>reviews</c> field resolved via <see cref="ProductTypeResolvers"/> to batch-load
/// associated reviews using the data loader.
/// </summary>
public sealed class ProductType : ObjectType<ProductResponse>
{
    /// <summary>
    /// Configures field descriptions, scalar type mappings, and the batch-loaded <c>reviews</c>
    /// resolver for the Product GraphQL type.
    /// </summary>
    protected override void Configure(IObjectTypeDescriptor<ProductResponse> descriptor)
    {
        descriptor.Description("Represents a product in the catalog.");

        descriptor
            .Field(p => p.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the product.");

        descriptor
            .Field(p => p.Name)
            .Type<NonNullType<StringType>>()
            .Description("The name of the product.");

        descriptor
            .Field(p => p.Price)
            .Type<NonNullType<DecimalType>>()
            .Description("The price of the product.");

        descriptor
            .Field(p => p.Description)
            .Description("The optional description of the product.");

        descriptor
            .Field(p => p.ProductDataIds)
            .Type<NonNullType<ListType<NonNullType<UuidType>>>>()
            .Description("The ids of related ProductData documents.");

        descriptor
            .Field(p => p.CreatedAtUtc)
            .Description("The UTC timestamp of when the product was created.");

        descriptor
            .Field("reviews")
            .ResolveWith<ProductTypeResolvers>(r => r.GetReviews(default!, default!, default))
            .Description("The reviews associated with this product.");
    }
}
