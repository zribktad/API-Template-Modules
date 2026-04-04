using Asp.Versioning;
using SharedKernel.Contracts.Api;
using Wolverine;

namespace ProductCatalog.Features.ProductData;

[ApiVersion(1.0)]
public sealed partial class ProductDataController(IMessageBus bus) : ApiControllerBase { }
