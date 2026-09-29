using ErrorOr;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Service.Extensions;
using Moq;

namespace Maroik.Core.Service.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="UnitOfWorkExtensions"/> — the shared "roll back the current
/// transaction and return a failure" helper previously copy-pasted as a private method into
/// <c>IncomeService</c>, <c>ExpenditureService</c>, and <c>CalendarService</c>.
/// </summary>
public class UnitOfWorkExtensionsTests
{
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    // -- FailAsync(ServiceResult) -----------------------------------------------

    /// <summary>Rolls back the transaction and returns the given result unchanged.</summary>
    [Fact]
    public async Task FailAsync_WithServiceResult_RollsBackAndReturnsTheResult()
    {
        ServiceResult expected = ServiceResult.Conflict("Test.Conflict", "Already exists.");

        ServiceResult result = await _unitOfWork.Object.FailAsync(expected, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- FailAsync(Error) --------------------------------------------------------

    /// <summary>Rolls back the transaction and maps the domain error into an equivalent failed <see cref="ServiceResult"/>.</summary>
    [Fact]
    public async Task FailAsync_WithError_RollsBackAndReturnsAMappedFailure()
    {
        Error error = Error.Conflict("Test.Conflict", "Already exists.");

        ServiceResult result = await _unitOfWork.Object.FailAsync(error, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Test.Conflict", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
