
namespace Buckle.CodeAnalysis;

internal partial class AssemblyIdentityComparer {
    internal enum ComparisonResult : byte {
        NotEquivalent = 0,
        Equivalent = 1,
        EquivalentIgnoringVersion = 2
    }
}
