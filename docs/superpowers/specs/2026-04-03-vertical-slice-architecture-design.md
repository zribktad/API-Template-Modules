# Vertical Slice Architecture — Hybrid (Core + Host)

## Context

The current module structure splits each bounded context into 4 separate projects (Domain, Application, Infrastructure, Api). When working on a single feature/use-case, a developer must navigate across 4 projects to find all related files (entity, handler, DTO, validator, repository, controller). This creates unnecessary cognitive overhead.

The goal is to reorganize into a Vertical Slice Architecture where each use-case is self-contained in a small folder (2-4 files), while preserving compile-time layer isolation via a 2-project split: Core (business logic) + Host (infrastructure/API).

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Projects per module | 2 (Core + Host) | Minimal cognitive overhead + compile-time isolation |
| Feature organization | Sub-slicing per use-case | Each folder has 2-4 files, not 50 |
| Module uniformity | All modules use Core + Host | Never question "how many projects does this module have?" |
| Naming convention | `{Module}.Core` / `{Module}.Host` | Core = business logic, Host = framework integration |
| Controller splitting | Per use-case | Small focused controllers instead of one god-controller |

## Architecture

### Project Dependencies

```
{Module}.Core  -->  SharedKernel  (NO EF, NO ASP.NET references)
{Module}.Host  -->  {Module}.Core + SharedKernel  (EF, ASP.NET allowed)
APITemplate.Api --> all {Module}.Host projects
```

### Core Project Contains

- Entities, Value Objects
- Command/Query records + Wolverine handlers
- DTOs (request/response)
- FluentValidation validators
- Ardalis.Specification specifications
- Mapping extensions
- Repository interfaces
- Domain errors
- Domain events / cache tags
- DbMarker class

### Host Project Contains

- REST Controllers (split per use-case)
- Repository implementations
- EF Configurations (IEntityTypeConfiguration)
- DbContext
- Background services
- Stored procedures
- Soft delete cascade rules
- Module registration (Module.cs / RuntimeBridge.cs)
- GraphQL (cross-feature, stays in shared folder)
- Logging (structured log definitions)

## Feature Folder Structure

### Core — Use-case sub-slicing

```
{Module}.Core/
  Features/
    {Feature}/
      {UseCase}/                        # 2-4 files per use-case
        {UseCase}Command.cs             # record + Wolverine handler
        {UseCase}Request.cs             # DTO
        {UseCase}RequestValidator.cs    # FluentValidation
      {AnotherUseCase}/
        {AnotherUseCase}Query.cs
        {Filter}.cs
        {Filter}Validator.cs
        {Specification}.cs
      Shared/                           # shared within feature
        {Entity}.cs
        I{Entity}Repository.cs
        {Entity}Response.cs
        {Entity}Mappings.cs
        {Entity}SortFields.cs
        {Helper}.cs
        {SharedSpecification}.cs
  Common/                               # shared across features in module
    Errors/
    Events/
    Logging/
  {Module}DbMarker.cs
  GlobalUsings.cs
```

### Host — Mirrors Core feature structure

```
{Module}.Host/
  Features/
    {Feature}/
      {UseCase}/
        {Feature}Controller.cs          # 1-2 endpoints per controller
      Shared/
        {Entity}Repository.cs
        {Entity}Configuration.cs
  GraphQL/                              # cross-feature (ProductCatalog only)
    DataLoaders/
    Models/
    Mutations/
    Queries/
    Types/
  Persistence/
    {Module}DbContext.cs
    (MongoDbContext.cs if applicable)
  SoftDelete/
  StoredProcedures/
  Handlers/                             # infra-level handlers (cleanup jobs)
  Logging/
  {Module}Module.cs
  GlobalUsings.cs
```

## Concrete Example: ProductCatalog

### ProductCatalog.Core

```
ProductCatalog.Core/
  Features/
    Product/
      CreateProducts/
        CreateProductsCommand.cs
        CreateProductRequest.cs
        CreateProductsRequest.cs
        CreateProductRequestValidator.cs
      UpdateProducts/
        UpdateProductsCommand.cs
        UpdateProductRequest.cs
        UpdateProductsRequest.cs
        UpdateProductItemValidator.cs
        UpdateProductRequestValidator.cs
      DeleteProducts/
        DeleteProductsCommand.cs
      GetProducts/
        GetProductsQuery.cs
        ProductFilter.cs
        ProductFilterValidator.cs
        ProductSpecification.cs
        ProductFilterCriteria.cs
      GetProductById/
        GetProductByIdQuery.cs
        ProductByIdSpecification.cs
        ProductByIdWithLinksSpecification.cs
      PatchProduct/
        PatchProductCommand.cs
        PatchableProductDto.cs
        PatchableProductDtoValidator.cs
      IdempotentCreate/
        IdempotentCreateCommand.cs
        IdempotentCreateRequest.cs
        IdempotentCreateResponse.cs
        IdempotentCreateRequestValidator.cs
      ValidateProductExists/
        ValidateProductExistsQueryHandler.cs
      Shared/
        Product.cs
        Price.cs
        ProductDataLink.cs
        IProductRepository.cs
        IProductDataLinkRepository.cs
        ProductResponse.cs
        ProductsResponse.cs
        ProductSearchFacetsResponse.cs
        ProductCategoryFacetValue.cs
        ProductPriceFacetBucketResponse.cs
        IProductRequest.cs
        ProductMappings.cs
        ProductSortFields.cs
        ProductValidationHelper.cs
        ProductsByIdsWithLinksSpecification.cs
        ProductCategoryFacetSpecification.cs
        ProductPriceFacetSpecification.cs
    Category/
      CreateCategories/
        CreateCategoriesCommand.cs
        CreateCategoriesRequest.cs
        CreateCategoryRequest.cs
        CreateCategoryRequestValidator.cs
      UpdateCategories/
        UpdateCategoriesCommand.cs
        UpdateCategoriesRequest.cs
        UpdateCategoryRequest.cs
        UpdateCategoryItemValidator.cs
      DeleteCategories/
        DeleteCategoriesCommand.cs
      GetCategories/
        GetCategoriesQuery.cs
        CategoryFilter.cs
        CategoryFilterValidator.cs
        CategorySpecification.cs
        CategoryFilterCriteria.cs
      GetCategoryById/
        GetCategoryByIdQuery.cs
        CategoryByIdSpecification.cs
      GetCategoryStats/
        GetCategoryStatsQuery.cs
        ProductCategoryStatsResponse.cs
      Shared/
        Category.cs
        ProductCategoryStats.cs
        ICategoryRepository.cs
        CategoryResponse.cs
        CategoryMappings.cs
        CategorySortFields.cs
        CategoriesByIdsSpecification.cs
    ProductData/
      CreateImageProductData/
        CreateImageProductDataCommand.cs
        CreateImageProductDataRequest.cs
        CreateImageProductDataRequestValidator.cs
      CreateVideoProductData/
        CreateVideoProductDataCommand.cs
        CreateVideoProductDataRequest.cs
        CreateVideoProductDataRequestValidator.cs
      DeleteProductData/
        DeleteProductDataCommand.cs
        ProductDataCascadeDeleteHandler.cs
      GetProductData/
        GetProductDataQuery.cs
      GetProductDataById/
        GetProductDataByIdQuery.cs
      Shared/
        ProductData.cs
        ImageProductData.cs
        VideoProductData.cs
        IProductDataRepository.cs
        ProductDataResponse.cs
        ProductDataMappings.cs
    Tenant/
      TenantCascadeDelete/
        TenantCascadeDeleteHandler.cs
        CategoriesForTenantSoftDeleteSpecification.cs
        ProductsForTenantSoftDeleteSpecification.cs
  Common/
    Errors/
      DomainErrors.cs
      ErrorCatalog.cs
      ProductCatalogDomainErrors.cs
    Events/
      CacheTags.cs
    Logging/
      ProductCatalogApplicationLogs.cs
  ProductCatalogDbMarker.cs
  GlobalUsings.cs
```

### ProductCatalog.Host

```
ProductCatalog.Host/
  Features/
    Product/
      CreateProducts/
        CreateProductsController.cs
      GetProducts/
        GetProductsController.cs
      GetProductById/
        GetProductByIdController.cs
      UpdateProducts/
        UpdateProductsController.cs
      DeleteProducts/
        DeleteProductsController.cs
      PatchProduct/
        PatchProductController.cs
      IdempotentCreate/
        IdempotentCreateController.cs
      Shared/
        ProductRepository.cs
        ProductDataLinkRepository.cs
        ProductConfiguration.cs
        ProductDataLinkConfiguration.cs
    Category/
      CreateCategories/
        CreateCategoriesController.cs
      GetCategories/
        GetCategoriesController.cs
      GetCategoryById/
        GetCategoryByIdController.cs
      GetCategoryStats/
        GetCategoryStatsController.cs
      UpdateCategories/
        UpdateCategoriesController.cs
      DeleteCategories/
        DeleteCategoriesController.cs
      Shared/
        CategoryRepository.cs
        CategoryConfiguration.cs
        CategoryStatsConfiguration.cs
    ProductData/
      CreateImageProductData/
        CreateImageProductDataController.cs
      CreateVideoProductData/
        CreateVideoProductDataController.cs
      DeleteProductData/
        DeleteProductDataController.cs
      GetProductData/
        GetProductDataController.cs
      GetProductDataById/
        GetProductDataByIdController.cs
      Shared/
        ProductDataRepository.cs
  GraphQL/
    DataLoaders/
      ProductReviewsByProductDataLoader.cs
    Models/
      CategoryPageResult.cs
      CategoryQueryInput.cs
      ProductPageResult.cs
      ProductQueryInput.cs
      ProductReviewPageResult.cs
      ProductReviewQueryInput.cs
    Mutations/
      ProductMutations.cs
      ProductReviewMutations.cs
    Queries/
      CategoryQueries.cs
      ProductQueries.cs
      ProductReviewQueries.cs
    Types/
      ProductReviewType.cs
      ProductType.cs
      ProductTypeResolvers.cs
    ErrorOrGraphQLExtensions.cs
  Persistence/
    ProductCatalogDbContext.cs
    MongoDbContext.cs
    MongoDbHealthCheck.cs
    MongoDbSettings.cs
  SoftDelete/
    ProductSoftDeleteCascadeRule.cs
  StoredProcedures/
    GetProductCategoryStatsProcedure.cs
  Handlers/
    CleanupOrphanedProductDataHandler.cs
  Logging/
    ProductCatalogInfrastructureLogs.cs
  ProductCatalogModule.cs
  GlobalUsings.cs
```

## Concrete Example: Notifications (event-driven module)

### Notifications.Core

```
Notifications.Core/
  Features/
    Email/
      SendTenantInvitationEmail/
        TenantInvitationEmailHandler.cs
      SendUserRegisteredEmail/
        UserRegisteredEmailHandler.cs
      SendUserRoleChangedEmail/
        UserRoleChangedEmailHandler.cs
      Shared/
        FailedEmail.cs
        IFailedEmailRepository.cs
        EmailMessage.cs
        EmailOptions.cs
        EmailTemplateNames.cs
        IEmailQueue.cs
        IEmailRetryService.cs
        IEmailSender.cs
        IEmailTemplateRenderer.cs
        IFailedEmailStore.cs
  NotificationsDbMarker.cs
```

### Notifications.Host

```
Notifications.Host/
  Features/
    Email/
      Shared/
        FailedEmailRepository.cs
        FailedEmailConfiguration.cs
        ChannelEmailQueue.cs
        EmailRetryService.cs
        EmailSendingBackgroundService.cs
        FailedEmailErrorNormalizer.cs
        FailedEmailStore.cs
        FluidEmailTemplateRenderer.cs
        MailKitEmailSender.cs
        EmailRetryRecurringJob.cs
        EmailRetryRecurringJobRegistration.cs
  Persistence/
    NotificationsDbContext.cs
  StoredProcedures/
    ClaimExpiredFailedEmailsProcedure.cs
    ClaimRetryableFailedEmailsProcedure.cs
  Logging/
    NotificationsInfrastructureLogs.cs
  NotificationsRuntimeBridge.cs
  NotificationsModule.cs
```

## Controller Splitting Strategy

Each use-case gets its own controller with 1-2 endpoints:

```csharp
// Features/Product/CreateProducts/CreateProductsController.cs
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{v:apiVersion}/products")]
public class CreateProductsController : ApiControllerBase
{
    [HttpPost]
    [RequirePermission(Permission.ProductWrite)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductsRequest request,
        IMessageBus bus,
        CancellationToken ct)
    {
        ErrorOr<ProductsResponse> result = await bus.InvokeAsync<ErrorOr<ProductsResponse>>(
            new CreateProductsCommand(request), ct);
        return result.ToActionResult(this);
    }
}
```

Where the original controller handles multiple operations (GET list + GET by ID + POST + PUT + DELETE), it gets split into separate controllers per use-case. Each controller inherits from `ApiControllerBase` and uses the same route base.

## .csproj Configuration

### Core project — NO infrastructure dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\SharedKernel\SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

Note: SharedKernel contains FluentValidation, Ardalis.Specification, ErrorOr, and WolverineFx — all needed by Core. SharedKernel also contains EF references, but Core doesn't use them — this is acceptable since SharedKernel is a single shared project. If stricter isolation is needed in the future, SharedKernel could be split, but that's out of scope.

### Host project — infrastructure + API

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\{Module}.Core\{Module}.Core.csproj" />
    <ProjectReference Include="..\..\SharedKernel\SharedKernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <!-- Module-specific packages only -->
  </ItemGroup>
</Project>
```

## Migration Strategy

Migrate module by module, starting from smallest to validate the pattern:

1. **Chatting** (2 existing projects, minimal files) — prove the pattern works
2. **Webhooks** (empty Domain, small) — validate with slightly more complexity
3. **BackgroundJobs** — medium complexity
4. **FileStorage** — medium complexity
5. **Notifications** (event-driven) — validate pattern for non-REST modules
6. **Reviews** — standard CRUD module
7. **Identity** (complex, Keycloak integration) — high complexity
8. **ProductCatalog** (largest, dual DB, GraphQL) — most complex, last

Each module migration is a standalone commit. Tests must pass after each module.

## Per-Module Migration Steps

For each module, the migration follows this sequence:

1. Create `{Module}.Core/` and `{Module}.Host/` project directories with new `.csproj` files
2. `git mv` files from `{Module}.Domain/` and `{Module}.Application/` into `{Module}.Core/Features/` structure
3. `git mv` files from `{Module}.Infrastructure/` and `{Module}.Api/` into `{Module}.Host/Features/` structure
4. Update namespaces in all moved files (`{Module}.Domain.*` / `{Module}.Application.*` → `{Module}.Core.*`, `{Module}.Infrastructure.*` / `{Module}.Api.*` → `{Module}.Host.*`)
5. Update `using` statements across the codebase that reference old namespaces
6. Delete old `.csproj` files (`{Module}.Domain.csproj`, `{Module}.Application.csproj`, `{Module}.Infrastructure.csproj`, `{Module}.Api.csproj`)
7. Update `APITemplate.Api.csproj` to reference `{Module}.Host` instead of old projects
8. Update `APITemplate.slnx` to reflect new project paths
9. Update test project references and usings
10. Split existing controllers into per-use-case controllers
11. Build + test

## What This Design Does NOT Change

- **SharedKernel** — stays as-is (single project)
- **APITemplate.Api** — stays as entry point, just updates module references
- **Tests** — test reorganization is a separate task
- **GraphQL** — stays as cross-feature folder in ProductCatalog.Host
- **slnx** — updated to reflect new project names

## Verification

After each module migration:
1. `dotnet build APITemplate.slnx` — 0 errors
2. `dotnet test APITemplate.slnx` — all tests pass
3. Verify Core.csproj has NO `FrameworkReference Include="Microsoft.AspNetCore.App"` and NO EF packages
4. Verify feature folder structure: each use-case folder has 2-4 files max
