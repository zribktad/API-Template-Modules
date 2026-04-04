using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using Wolverine;

namespace ProductCatalog.Features.Product.IdempotentCreate;

[ApiVersion(1.0)]
public sealed partial class IdempotentController(IMessageBus bus) : ApiControllerBase { }
