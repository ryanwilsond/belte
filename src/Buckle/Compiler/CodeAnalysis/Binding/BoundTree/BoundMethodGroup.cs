using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BoundMethodGroup {
    internal BoundMethodGroup(
        SyntaxNode syntax,
        string name,
        ImmutableArray<MethodSymbol> methods,
        ImmutableArray<TypeOrConstant> templateArguments,
        Symbol lookupSymbol,
        BelteDiagnostic lookupError,
        BoundMethodGroupFlags flags,
        BoundExpression receiver,
        LookupResultKind resultKind,
        bool hasErrors = false)
        : this(
            syntax,
            name,
            methods,
            templateArguments,
            lookupSymbol,
            lookupError,
            flags,
            functionType: GetFunctionType(methods),
            receiver,
            resultKind,
            hasErrors
        ) { }

    internal MemberAccessExpressionSyntax memberAccessExpressionSyntax => syntax as MemberAccessExpressionSyntax;

    internal SyntaxNode nameSyntax {
        get {
            var memberAccess = memberAccessExpressionSyntax;

            if (memberAccess is not null)
                return memberAccess.name;
            else
                return syntax;
        }
    }

    private static FunctionTypeSymbol GetFunctionType(ImmutableArray<MethodSymbol> methods) {
        if (methods.Length == 1) {
            var method = methods[0];
            var paramRefKinds = !method.parameterRefKinds.IsDefault
                ? method.parameterRefKinds
                : method.parameterTypesWithAnnotations.Select(p => RefKind.None).ToImmutableArray();

            return FunctionTypeSymbol.CreateFromParts(
                method.returnTypeWithAnnotations,
                method.refKind,
                method.parameterTypesWithAnnotations,
                paramRefKinds
            );
        }

        return null;
    }
}
