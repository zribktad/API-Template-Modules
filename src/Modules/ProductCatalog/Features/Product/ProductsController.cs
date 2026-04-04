using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using Wolverine;

namespace ProductCatalog.Features.Product;

[ApiVersion(1.0)]
public sealed partial class ProductsController(IMessageBus bus) : ApiControllerBase { }
