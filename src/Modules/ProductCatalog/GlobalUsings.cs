// ── Domain ────────────────────────────────────────────────────────────────────
// ── Application ───────────────────────────────────────────────────────────────

global using BuildingBlocks.Application.Batch;
global using BuildingBlocks.Application.Batch.Rules;
global using BuildingBlocks.Application.Context;
global using BuildingBlocks.Application.Contracts;
global using BuildingBlocks.Application.DTOs;
global using BuildingBlocks.Application.Events;
global using BuildingBlocks.Application.Extensions;
// ── Infrastructure ────────────────────────────────────────────────────────────
global using BuildingBlocks.Application.Options.Infrastructure;
global using BuildingBlocks.Application.Resilience;
global using BuildingBlocks.Application.Search;
global using BuildingBlocks.Application.Sorting;
global using BuildingBlocks.Application.Validation;
global using BuildingBlocks.Domain.Common;
global using BuildingBlocks.Domain.Entities;
global using BuildingBlocks.Domain.Entities.Contracts;
global using BuildingBlocks.Domain.Interfaces;
global using BuildingBlocks.Infrastructure.EFCore.Configurations;
global using BuildingBlocks.Infrastructure.EFCore.Persistence;
global using BuildingBlocks.Infrastructure.EFCore.Repositories;
global using BuildingBlocks.Infrastructure.EFCore.SoftDelete;
global using BuildingBlocks.Infrastructure.EFCore.StoredProcedures;
global using BuildingBlocks.Infrastructure.EFCore.UnitOfWork;
global using BuildingBlocks.Security;
global using BuildingBlocks.Web.Api;
global using ProductCatalog.Common.Errors;
global using ProductCatalog.Common.Events;
global using ProductCatalog.Entities;
global using ProductCatalog.Entities.ProductData;
global using ProductCatalog.Features.Category.Shared;
global using ProductCatalog.Features.Product.Shared;
global using ProductCatalog.Features.ProductData.Shared;
global using ProductCatalog.Features.Shared.Routing;
// ── API / Presentation ────────────────────────────────────────────────────────
global using ProductCatalog.GraphQL.Models;
global using ProductCatalog.Interfaces;
global using SharedKernel.Contracts.Events;
global using SharedKernel.Contracts.Queries.Reviews;
global using SharedKernel.GraphQL.Extensions;
global using Error = ErrorOr.Error;
