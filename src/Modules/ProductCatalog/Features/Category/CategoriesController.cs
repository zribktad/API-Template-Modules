using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using Wolverine;

namespace ProductCatalog.Features.Category;

[ApiVersion(1.0)]
public sealed partial class CategoriesController(IMessageBus bus) : ApiControllerBase { }
