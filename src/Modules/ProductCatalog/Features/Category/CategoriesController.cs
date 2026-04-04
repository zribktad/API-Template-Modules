using Asp.Versioning;
using Wolverine;

namespace ProductCatalog.Features.Category;

[ApiVersion(1.0)]
public sealed partial class CategoriesController(IMessageBus bus) : ApiControllerBase { }
