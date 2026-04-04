using Asp.Versioning;
using Wolverine;

namespace ProductCatalog.Features.Product.PatchProduct;

[ApiVersion(1.0)]
public sealed partial class PatchController(IMessageBus bus) : ApiControllerBase { }
