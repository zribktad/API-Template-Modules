using APITemplate.Application.Common.Events;
using APITemplate.Application.Features.Category;
using APITemplate.Application.Features.Category.Mappings;
using APITemplate.Application.Features.Category.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class CategoryRequestHandlersTests
{
    private readonly Mock<ICategoryRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMessageBus> _busMock;
    private readonly Mock<IValidator<CreateCategoryRequest>> _createValidatorMock;
    private readonly Mock<IValidator<UpdateCategoryItem>> _updateValidatorMock;

    public CategoryRequestHandlersTests()
    {
        _repositoryMock = new Mock<ICategoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _busMock = new Mock<IMessageBus>();
        _createValidatorMock = new Mock<IValidator<CreateCategoryRequest>>();
        _updateValidatorMock = new Mock<IValidator<UpdateCategoryItem>>();
        _unitOfWorkMock.SetupImmediateTransactionExecution();

        _createValidatorMock
            .Setup(v =>
                v.ValidateAsync(It.IsAny<CreateCategoryRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ValidationResult());

        _updateValidatorMock
            .Setup(v =>
                v.ValidateAsync(It.IsAny<UpdateCategoryItem>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ValidationResult());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPagedCategories()
    {
        var ct = TestContext.Current.CancellationToken;
        var items = new List<CategoryResponse>
        {
            new(Guid.NewGuid(), "Electronics", null, DateTime.UtcNow),
            new(Guid.NewGuid(), "Books", "All books", DateTime.UtcNow),
        };

        var paged = new PagedResponse<CategoryResponse>(items, 2, 1, 10);
        _repositoryMock
            .Setup(r =>
                r.GetPagedAsync(
                    It.IsAny<CategorySpecification>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(paged);

        var result = await GetCategoriesQueryHandler.HandleAsync(
            new GetCategoriesQuery(new CategoryFilter()),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Items.Count().ShouldBe(2);
        result.Value.Items.First().Name.ShouldBe("Electronics");
        result.Value.Items.Last().Name.ShouldBe("Books");
        result.Value.Items.Last().Description.ShouldBe("All books");
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var paged = new PagedResponse<CategoryResponse>(new List<CategoryResponse>(), 0, 1, 10);
        _repositoryMock
            .Setup(r =>
                r.GetPagedAsync(
                    It.IsAny<CategorySpecification>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(paged);

        var result = await GetCategoriesQueryHandler.HandleAsync(
            new GetCategoriesQuery(new CategoryFilter()),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenCategoryExists_ReturnsResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoryId = Guid.NewGuid();
        var response = new CategoryResponse(
            categoryId,
            "Electronics",
            "Electronic devices",
            DateTime.UtcNow
        );

        _repositoryMock
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<CategoryByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);

        var result = await GetCategoryByIdQueryHandler.HandleAsync(
            new GetCategoryByIdQuery(categoryId),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(categoryId);
        result.Value.Name.ShouldBe("Electronics");
        result.Value.Description.ShouldBe("Electronic devices");
    }

    [Fact]
    public async Task GetByIdAsync_WhenCategoryDoesNotExist_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<CategoryByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((CategoryResponse?)null);

        var result = await GetCategoryByIdQueryHandler.HandleAsync(
            new GetCategoryByIdQuery(Guid.NewGuid()),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task BatchCreateAsync_CreatesAndReturnsBatchResponse()
    {
        var request = new CreateCategoryRequest("Electronics", "Electronic devices");
        var batchRequest = new CreateCategoriesRequest([request]);

        _repositoryMock
            .Setup(r =>
                r.AddRangeAsync(It.IsAny<IEnumerable<Category>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (IEnumerable<Category> entities, CancellationToken _) => entities.ToList()
            );

        var result = await CreateCategoriesCommandHandler.HandleAsync(
            new CreateCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.FailureCount.ShouldBe(0);
        result.Value.Failures.ShouldBeEmpty();

        _repositoryMock.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<Category>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task BatchCreateAsync_WithNullDescription_CreatesCategory()
    {
        var request = new CreateCategoryRequest("Books", null);
        var batchRequest = new CreateCategoriesRequest([request]);

        _repositoryMock
            .Setup(r =>
                r.AddRangeAsync(It.IsAny<IEnumerable<Category>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (IEnumerable<Category> entities, CancellationToken _) => entities.ToList()
            );

        var result = await CreateCategoriesCommandHandler.HandleAsync(
            new CreateCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.FailureCount.ShouldBe(0);
        result.Value.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchCreateAsync_WithValidationFailure_ReturnsFailureResponse()
    {
        var request = new CreateCategoryRequest("", null);
        var batchRequest = new CreateCategoriesRequest([request]);

        _createValidatorMock
            .Setup(v =>
                v.ValidateAsync(It.IsAny<CreateCategoryRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new ValidationResult([new ValidationFailure("Name", "Category name is required.")])
            );

        var result = await CreateCategoriesCommandHandler.HandleAsync(
            new CreateCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.FailureCount.ShouldBe(1);
        result.Value.Failures[0].Errors.ShouldContain("Category name is required.");

        _repositoryMock.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<Category>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task BatchCreateAsync_GeneratesIdsServerSide()
    {
        var batchRequest = new CreateCategoriesRequest([
            new CreateCategoryRequest("Generated 1", null),
            new CreateCategoryRequest("Generated 2", null),
        ]);

        IEnumerable<Category>? captured = null;
        _repositoryMock
            .Setup(r =>
                r.AddRangeAsync(It.IsAny<IEnumerable<Category>>(), It.IsAny<CancellationToken>())
            )
            .Callback<IEnumerable<Category>, CancellationToken>(
                (entities, _) => captured = entities.ToList()
            )
            .ReturnsAsync(
                (IEnumerable<Category> entities, CancellationToken _) => entities.ToList()
            );

        var result = await CreateCategoriesCommandHandler.HandleAsync(
            new CreateCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(2);
        result.Value.FailureCount.ShouldBe(0);
        captured.ShouldNotBeNull();
        captured!.All(x => x.Id != Guid.Empty).ShouldBeTrue();
        captured.Select(x => x.Id).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task BatchUpdateAsync_WhenCategoryExists_UpdatesAndCommits()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old Description",
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
        };

        var updateItem = new UpdateCategoryItem(category.Id, "New Name", "New Description");
        var batchRequest = new UpdateCategoriesRequest([updateItem]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([category]);

        var result = await UpdateCategoriesCommandHandler.HandleAsync(
            new UpdateCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.FailureCount.ShouldBe(0);
        result.Value.Failures.ShouldBeEmpty();

        _repositoryMock.Verify(
            r =>
                r.UpdateAsync(
                    It.Is<Category>(c =>
                        c.Name == "New Name" && c.Description == "New Description"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task BatchUpdateAsync_WhenCategoryDoesNotExist_ReturnsFailure()
    {
        var nonExistentId = Guid.NewGuid();
        var updateItem = new UpdateCategoryItem(nonExistentId, "Name", null);
        var batchRequest = new UpdateCategoriesRequest([updateItem]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<Category>());

        var result = await UpdateCategoriesCommandHandler.HandleAsync(
            new UpdateCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.FailureCount.ShouldBe(1);
        result.Value.Failures[0].Errors.ShouldContain(e => e.Contains("not found"));

        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task BatchUpdateAsync_WithValidationFailure_ReturnsValidationFailureWithoutStartingTransaction()
    {
        var updateItem = new UpdateCategoryItem(Guid.NewGuid(), "", null);
        var batchRequest = new UpdateCategoriesRequest([updateItem]);

        _updateValidatorMock
            .Setup(v =>
                v.ValidateAsync(It.IsAny<UpdateCategoryItem>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new ValidationResult([new ValidationFailure("Name", "Category name is required.")])
            );
        _repositoryMock
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        var result = await UpdateCategoriesCommandHandler.HandleAsync(
            new UpdateCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.FailureCount.ShouldBe(1);
        result.Value.Failures[0].Errors.ShouldContain("Category name is required.");

        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task BatchDeleteAsync_WhenCategoryExists_DeletesAndCommits()
    {
        var id = Guid.NewGuid();
        var category = new Category { Id = id, Name = "To Delete" };
        var batchRequest = new BatchDeleteRequest([id]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([category]);

        var result = await DeleteCategoriesCommandHandler.HandleAsync(
            new DeleteCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.FailureCount.ShouldBe(0);
        result.Value.Failures.ShouldBeEmpty();

        _repositoryMock.Verify(
            r =>
                r.DeleteRangeAsync(
                    It.Is<IEnumerable<Category>>(c => c.Any(x => x.Id == id)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task BatchDeleteAsync_WhenCategoryDoesNotExist_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        var batchRequest = new BatchDeleteRequest([id]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(It.IsAny<CategoriesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<Category>());

        var result = await DeleteCategoriesCommandHandler.HandleAsync(
            new DeleteCategoriesCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.FailureCount.ShouldBe(1);
        result.Value.Failures[0].Errors.ShouldContain(e => e.Contains("not found"));

        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetStatsAsync_WhenStatsExist_ReturnsMappedResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var categoryId = Guid.NewGuid();
        var stats = new ProductCategoryStats
        {
            CategoryId = categoryId,
            CategoryName = "Electronics",
            ProductCount = 5,
            AveragePrice = 199.99m,
            TotalReviews = 42,
        };

        _repositoryMock
            .Setup(r => r.GetStatsByIdAsync(categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await GetCategoryStatsQueryHandler.HandleAsync(
            new GetCategoryStatsQuery(categoryId),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.CategoryId.ShouldBe(categoryId);
        result.Value.CategoryName.ShouldBe("Electronics");
        result.Value.ProductCount.ShouldBe(5);
        result.Value.AveragePrice.ShouldBe(199.99m);
        result.Value.TotalReviews.ShouldBe(42);
    }

    [Fact]
    public async Task GetStatsAsync_WhenCategoryDoesNotExist_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _repositoryMock
            .Setup(r => r.GetStatsByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductCategoryStats?)null);

        var result = await GetCategoryStatsQueryHandler.HandleAsync(
            new GetCategoryStatsQuery(Guid.NewGuid()),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }
}
