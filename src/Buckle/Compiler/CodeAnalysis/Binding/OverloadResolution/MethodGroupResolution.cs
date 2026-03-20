using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct MethodGroupResolution {
    internal readonly MethodGroup methodGroup;
    internal readonly Symbol otherSymbol;
    internal readonly OverloadResolutionResult<MethodSymbol> overloadResolutionResult;
    internal readonly AnalyzedArguments analyzedArguments;
    internal readonly BelteDiagnosticQueue diagnostics;
    internal readonly LookupResultKind resultKind;

    internal MethodGroupResolution(MethodGroup methodGroup, BelteDiagnosticQueue diagnostics)
        : this(methodGroup, null, null, null, methodGroup.resultKind, diagnostics) {
    }

    internal MethodGroupResolution(Symbol otherSymbol, LookupResultKind resultKind, BelteDiagnosticQueue diagnostics)
        : this(null, otherSymbol, null, null, resultKind, diagnostics) {
    }

    internal MethodGroupResolution(
        MethodGroup methodGroup,
        Symbol otherSymbol,
        OverloadResolutionResult<MethodSymbol> overloadResolutionResult,
        AnalyzedArguments analyzedArguments,
        LookupResultKind resultKind,
        BelteDiagnosticQueue diagnostics) {
        this.methodGroup = methodGroup;
        this.otherSymbol = otherSymbol;
        this.overloadResolutionResult = overloadResolutionResult;
        this.analyzedArguments = analyzedArguments;
        this.resultKind = resultKind;
        this.diagnostics = diagnostics;
    }

    internal bool isEmpty => (methodGroup is null) && (otherSymbol is null);

    internal bool hasAnyErrors => diagnostics.AnyErrors();

    internal bool hasAnyApplicableMethod => (methodGroup is not null) &&
        (resultKind == LookupResultKind.Viable) &&
        ((overloadResolutionResult is null) || overloadResolutionResult.hasAnyApplicableMember);

    internal bool isLocalFunctionInvocation => methodGroup?.methods.Count == 1 &&
        methodGroup.methods[0].methodKind == MethodKind.LocalFunction;

    internal void Free() {
        analyzedArguments?.Free();
        methodGroup?.Free();
        overloadResolutionResult?.Free();
        diagnostics?.Free();
    }
}
