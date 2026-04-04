using Asp.Versioning;
using Wolverine;

namespace ProductCatalog.Features.Product.IdempotentCreate;

[ApiVersion(1.0)]
public sealed partial class IdempotentController(IMessageBus bus) : ApiControllerBase { }
