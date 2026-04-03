// ── Domain ────────────────────────────────────────────────────────────────────
global using SharedKernel.Domain.Common;
global using SharedKernel.Domain.Entities;
global using SharedKernel.Domain.Entities.Contracts;
global using SharedKernel.Domain.Interfaces;

// ── Application ───────────────────────────────────────────────────────────────
global using ProductCatalog.Common.Errors;
global using ProductCatalog.Common.Events;
global using ProductCatalog.Features.Shared;
global using ProductCatalog.Entities;
global using ProductCatalog.Entities.ProductData;
global using ProductCatalog.Interfaces;
global using SharedKernel.Application.Batch;
global using SharedKernel.Application.Batch.Rules;
global using SharedKernel.Application.Context;
global using SharedKernel.Application.Contracts;
global using SharedKernel.Application.DTOs;
global using SharedKernel.Application.Events;
global using SharedKernel.Application.Extensions;
global using SharedKernel.Application.Resilience;
global using SharedKernel.Application.Search;
global using SharedKernel.Application.Sorting;
global using SharedKernel.Application.Validation;
global using SharedKernel.Contracts.Events;

// ── API / Presentation ────────────────────────────────────────────────────────
global using ProductCatalog.GraphQL.Models;
global using Reviews.Domain;
global using Reviews.Features;
global using SharedKernel.Contracts.Api;
global using SharedKernel.Contracts.Security;
global using Error = ErrorOr.Error;

// ── Infrastructure ────────────────────────────────────────────────────────────
global using SharedKernel.Application.Options.Infrastructure;
global using SharedKernel.Infrastructure.Configurations;
global using SharedKernel.Infrastructure.Persistence;
global using SharedKernel.Infrastructure.Repositories;
global using SharedKernel.Infrastructure.SoftDelete;
global using SharedKernel.Infrastructure.StoredProcedures;
global using SharedKernel.Infrastructure.UnitOfWork;
