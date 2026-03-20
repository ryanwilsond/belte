
namespace Buckle.CodeAnalysis.Binding;

internal enum OperatorAnalysisResultKind : byte {
    Undefined = 0,
    Inapplicable,
    Worse,
    Applicable,
}
