using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceDataContainerSymbol : DataContainerSymbol {
    private readonly TypeSyntax _typeSyntax;

    private TypeWithAnnotations _type;

    private SourceDataContainerSymbol(
        Symbol containingSymbol,
        Binder scopeBinder,
        bool allowRefKind,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        DataContainerDeclarationKind declarationKind,
        SyntaxTokenList modifiers) {
        this.containingSymbol = containingSymbol;
        this.scopeBinder = scopeBinder;
        this.declarationKind = declarationKind;
        this.identifierToken = identifierToken;
        _typeSyntax = typeSyntax;

        if (allowRefKind) {
            // TODO see todo in field/method
            // typeSyntax.SkipRef(out var refKind);
            // this.refKind = refKind;
            if (modifiers is null)
                refKind = RefKind.None;
            else
                refKind = modifiers.Any(m => m.kind == SyntaxKind.RefKeyword) ? RefKind.Ref : RefKind.None;
        }

        scope = refKind == RefKind.None ? ScopedKind.Value : ScopedKind.Ref;
    }

    public override string name => identifierToken.text;

    public override RefKind refKind { get; }

    internal Binder scopeBinder { get; }

    internal override Symbol containingSymbol { get; }

    internal override SyntaxNode scopeDesignator => scopeBinder.scopeDesignator;

    internal override DataContainerDeclarationKind declarationKind { get; }

    internal override ScopedKind scope { get; }

    internal override SyntaxToken identifierToken { get; }

    internal override bool hasSourceLocation => true;

    internal override SyntaxReference syntaxReference => new SyntaxReference(GetDeclarationSyntax());

    internal override TextLocation location => identifierToken.location;

    internal override bool isCompilerGenerated => false;

    internal override TypeWithAnnotations typeWithAnnotations {
        get {
            if (_type is null) {
                var localType = GetTypeSymbol();
                SetTypeWithAnnotations(localType);
            }

            return _type;
        }
    }

    internal override SynthesizedLocalKind synthesizedKind => SynthesizedLocalKind.UserDefined;

    internal bool isImplicitlyTyped {
        get {
            if (_typeSyntax is null)
                return true;

            var typeSyntax = _typeSyntax.SkipRef(out _);

            if (typeSyntax.isImplicitlyTyped) {
                scopeBinder.BindTypeOrImplicitType(
                    typeSyntax,
                    BelteDiagnosticQueue.Discarded,
                    out var result
                );

                return result;
            }

            return false;
        }
    }

    internal static SourceDataContainerSymbol MakeLocal(
        Symbol containingSymbol,
        Binder scopeBinder,
        bool allowRefKind,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        DataContainerDeclarationKind declarationKind,
        EqualsValueClauseSyntax initializer,
        SyntaxTokenList modifiers,
        Binder initializerBinder = null,
        Binder nodeBinder = null,
        SyntaxNode nodeToBind = null,
        SyntaxNode forbiddenZone = null) {
        if (nodeBinder is not null) {
            return new LocalSymbolWithEnclosingContext(
                containingSymbol,
                scopeBinder,
                nodeBinder,
                typeSyntax,
                identifierToken,
                declarationKind,
                modifiers,
                nodeToBind,
                forbiddenZone
            );
        }

        return MakeDataContainer(
            containingSymbol,
            scopeBinder,
            allowRefKind,
            typeSyntax,
            identifierToken,
            declarationKind,
            initializer,
            modifiers,
            initializerBinder ?? scopeBinder
        );
    }

    internal sealed override SyntaxNode GetDeclarationSyntax() {
        return identifierToken.parent;
    }

    internal override ConstantValue GetConstantValue(
        SyntaxNode node,
        DataContainerSymbol inProgress,
        BelteDiagnosticQueue diagnostics) {
        return null;
    }

    internal override BelteDiagnosticQueue GetConstantValueDiagnostics(BoundExpression boundInitValue) {
        return BelteDiagnosticQueue.Discarded;
    }

    internal void SetTypeWithAnnotations(TypeWithAnnotations newType) {
        if (_type is null)
            Interlocked.CompareExchange(ref _type, newType, null);
    }

    private protected virtual TypeWithAnnotations InferTypeOfImplicit(BelteDiagnosticQueue diagnostics) {
        return _type;
    }

    private static SourceDataContainerSymbol MakeDataContainer(
        Symbol containingSymbol,
        Binder scopeBinder,
        bool allowRefKind,
        TypeSyntax typeSyntax,
        SyntaxToken identifierToken,
        DataContainerDeclarationKind declarationKind,
        EqualsValueClauseSyntax initializer,
        SyntaxTokenList modifiers,
        Binder initializerBinder) {
        return initializer is null
            ? new SourceDataContainerSymbol(
                containingSymbol,
                scopeBinder,
                allowRefKind,
                typeSyntax,
                identifierToken,
                declarationKind,
                modifiers
              )
            : new SourceDataContainerWithInitializerSymbol(
                containingSymbol,
                scopeBinder,
                typeSyntax,
                identifierToken,
                initializer,
                initializerBinder,
                declarationKind,
                modifiers
              );
    }

    private TypeWithAnnotations GetTypeSymbol() {
        var diagnostics = BelteDiagnosticQueue.Discarded;

        bool isImplicitlyTyped;
        TypeWithAnnotations declarationType;

        if (_typeSyntax is null) {
            isImplicitlyTyped = true;
            declarationType = default;
        } else {
            declarationType = scopeBinder.BindTypeOrImplicitType(
                _typeSyntax.SkipRef(out _),
                diagnostics,
                out isImplicitlyTyped
            );
        }

        if (isImplicitlyTyped) {
            var inferredType = InferTypeOfImplicit(diagnostics);

            if (inferredType.hasType && !inferredType.IsVoidType())
                declarationType = inferredType;
            else
                declarationType = new TypeWithAnnotations(scopeBinder.CreateErrorType("var"));
        }

        return declarationType;
    }

    internal sealed override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if ((object)obj == this)
            return true;

        return obj is SourceDataContainerSymbol symbol
            && symbol.identifierToken.Equals(identifierToken)
            && symbol.containingSymbol.Equals(containingSymbol, compareKind);
    }

    public sealed override int GetHashCode() {
        return Hash.Combine(identifierToken.GetHashCode(), containingSymbol.GetHashCode());
    }

    private sealed class LocalSymbolWithEnclosingContext : SourceDataContainerSymbol {
        private readonly Binder _nodeBinder;
        private readonly SyntaxNode _nodeToBind;

        internal LocalSymbolWithEnclosingContext(
            Symbol containingSymbol,
            Binder scopeBinder,
            Binder nodeBinder,
            TypeSyntax typeSyntax,
            SyntaxToken identifierToken,
            DataContainerDeclarationKind declarationKind,
            SyntaxTokenList modifiers,
            SyntaxNode nodeToBind,
            SyntaxNode forbiddenZone)
            : base(
                containingSymbol,
                scopeBinder,
                allowRefKind: false,
                typeSyntax,
                identifierToken,
                declarationKind,
                modifiers
            ) {
            _nodeBinder = nodeBinder;
            _nodeToBind = nodeToBind;
            this.forbiddenZone = forbiddenZone;
        }

        internal override SyntaxNode forbiddenZone { get; }

        internal override BelteDiagnostic forbiddenDiagnostic => null;

        private protected override TypeWithAnnotations InferTypeOfImplicit(BelteDiagnosticQueue diagnostics) {
            switch (_nodeToBind.kind) {
                case SyntaxKind.ArgumentList:
                    switch (_nodeToBind.parent) {
                        case ConstructorInitializerSyntax ctorInitializer:
                            _nodeBinder.BindConstructorInitializer(ctorInitializer, diagnostics);
                            break;
                        default:
                            throw ExceptionUtilities.UnexpectedValue(_nodeToBind.parent);
                    }
                    break;
                default:
                    _nodeBinder.BindExpression((ExpressionSyntax)_nodeToBind, diagnostics);
                    break;
            }

            if (_type is null)
                SetTypeWithAnnotations(new TypeWithAnnotations(_nodeBinder.CreateErrorType("var")));

            return _type;
        }
    }
}
