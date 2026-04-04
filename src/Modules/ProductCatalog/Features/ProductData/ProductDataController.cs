using Asp.Versioning;
using Wolverine;

namespace ProductCatalog.Features.ProductData;

[ApiVersion(1.0)]
public sealed partial class ProductDataController(IMessageBus bus) : ApiControllerBase { }
