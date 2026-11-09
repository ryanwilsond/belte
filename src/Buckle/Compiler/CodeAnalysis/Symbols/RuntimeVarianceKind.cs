
namespace Buckle.CodeAnalysis.Symbols;

// Not to be confused with VarianceKind concerning contravariance and covariance
internal enum RuntimeVarianceKind : byte {
    None = 0,
    // E.g. Object? -> Object!
    ToMoreConstrained = 1,
    // E.g. Object! -> Object?
    ToLessConstrained = 2,
}
