using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Tests.TestUtils;

/// <summary>
/// An <see cref="IFormSubmissionsLock"/> substitute that grants every add and deletion at once, all with one
/// <see cref="Section"/> a test can assert on.
/// </summary>
public sealed class FormSubmissionsLockStub
{
    public FormSubmissionsLockStub()
    {
        Lock.BeginAddingAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result<IFormSubmissionsSection>.Success(Section));
        Lock.BeginDeletingAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Result<IFormSubmissionsSection>.Success(Section));
    }

    public IFormSubmissionsLock Lock { get; } = Substitute.For<IFormSubmissionsLock>();

    public IFormSubmissionsSection Section { get; } = Substitute.For<IFormSubmissionsSection>();
}
