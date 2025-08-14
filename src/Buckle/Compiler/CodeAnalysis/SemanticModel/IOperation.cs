
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

public partial interface IOperation {
    IOperation parent { get; }

    OperationKind kind { get; }

    SyntaxNode syntax { get; }

    ITypeSymbol type { get; }

    Optional<object> constantValue { get; }

    OperationList childOperations { get; }

    bool isImplicit { get; }

    SemanticModel semanticModel { get; }

    void Accept(OperationVisitor visitor);

    TResult? Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument);
}
