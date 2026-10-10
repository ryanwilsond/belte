using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class DataContainerSymbol : Symbol, IDataContainerSymbol {
    public sealed override SymbolKind kind => SymbolKind.Local;

    public bool isConst => declarationKind.IsConstant();

    public bool isFinal => declarationKind.IsFinal();

    public bool isConstExpr => declarationKind == DataContainerDeclarationKind.ConstantExpression;

    public bool isNullable => typeWithAnnotations.isNullable;

    public bool isRef => refKind != RefKind.None;

    public abstract RefKind refKind { get; }

    public bool hasConstantValue {
        get {
            if (!isConstExpr)
                return false;

            var constant = GetConstantValue(null, null, null);
            return constant is not null;
        }
    }

    public object constantValue {
        get {
            if (!isConstExpr)
                return false;

            var constant = GetConstantValue(null, null, null);
            return constant?.value;
        }
    }

    internal sealed override bool isSealed => false;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isOverride => false;

    internal sealed override bool isVirtual => false;

    internal sealed override bool isStatic => false;

    internal sealed override bool isExtern => false;

    internal abstract bool isPinned { get; }

    internal sealed override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal abstract SyntaxToken identifierToken { get; }

    internal abstract TypeWithAnnotations typeWithAnnotations { get; }

    internal abstract DataContainerDeclarationKind declarationKind { get; }

    internal abstract bool hasSourceLocation { get; }

    internal abstract bool isCompilerGenerated { get; }

    internal abstract SyntaxNode scopeDesignator { get; }

    internal abstract ScopedKind scope { get; }

    internal virtual bool isWritableVariable {
        get {
            bool isWritable;

            switch (declarationKind) {
                case DataContainerDeclarationKind.Constant:
                case DataContainerDeclarationKind.Final:
                case DataContainerDeclarationKind.ConstantExpression:
                case DataContainerDeclarationKind.ConstantForEachLocal:
                case DataContainerDeclarationKind.ForEachLocal:
                case DataContainerDeclarationKind.ConstantNullBindingLocal:
                case DataContainerDeclarationKind.NullBindingLocal:
                case DataContainerDeclarationKind.ScopedLocal:
                    isWritable = false;
                    break;
                case DataContainerDeclarationKind.Variable:
                case DataContainerDeclarationKind.PatternLocal:
                case DataContainerDeclarationKind.OutVariable:
                case DataContainerDeclarationKind.DeclarationExpressionVariable:
                    isWritable = true;
                    break;
                case DataContainerDeclarationKind.None:
                    Debug.Assert(this is SynthesizedDataContainerSymbol);
                    isWritable = true;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(declarationKind);
            }

            Debug.Assert(isWritable == (!isFinal && !isConst && !isConstExpr));
            return isWritable;
        }
    }

    internal TypeSymbol type => typeWithAnnotations.type;

    internal bool isGlobal => containingSymbol is SynthesizedEntryPoint;

    internal abstract SynthesizedLocalKind synthesizedKind { get; }

    internal virtual TypeWithAnnotations GetTypeWithAnnotations(SyntaxNode reference, BelteDiagnosticQueue diagnostics) {
        return typeWithAnnotations;
    }

    internal sealed override void Accept(SymbolVisitor visitor) {
        visitor.VisitDataContainer(this);
    }

    internal sealed override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitDataContainer(this, argument);
    }

    internal virtual BelteDiagnostic GetForbiddenDiagnostic(TextLocation location) {
        return Error.LocalUsedBeforeDeclaration(location, this);
    }

    internal abstract ConstantValue GetConstantValue(
        SyntaxNode node,
        DataContainerSymbol inProgress,
        BelteDiagnosticQueue diagnostics);

    internal abstract BelteDiagnosticQueue GetConstantValueDiagnostics(BoundExpression boundInitValue);

    internal abstract SyntaxNode GetDeclarationSyntax();

    ITypeSymbol IDataContainerSymbol.type => type;
}
