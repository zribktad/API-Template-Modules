using Asp.Versioning;
using Wolverine;

namespace ProductCatalog.Features.Product;

[ApiVersion(1.0)]
public sealed partial class ProductsController(IMessageBus bus) : ApiControllerBase { }
