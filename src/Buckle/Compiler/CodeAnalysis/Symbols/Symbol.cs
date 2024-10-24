using System.Collections.Immutable;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A base symbol.
/// </summary>
internal abstract class Symbol : ISymbol {
    /// <summary>
    /// Name of the symbol.
    /// </summary>
    public virtual string name => "";

    public virtual string metadataName => name;

    public ITypeSymbolWithMembers parent => containingType;

    /// <summary>
    /// The accessibility/protection level of the symbol.
    /// </summary> <summary>
    internal abstract Accessibility declaredAccessibility { get; }

    internal abstract Symbol containingSymbol { get; }

    /// <summary>
    /// The type that contains this symbol, or null if nothing is containing this symbol.
    /// </summary>
    internal virtual NamedTypeSymbol containingType {
        get {
            var containerAsType = containingSymbol as NamedTypeSymbol;

            if ((object)containerAsType == containingSymbol)
                return containerAsType;

            return containingSymbol.containingType;
        }
    }

    internal virtual bool requiresCompletion => false;

    internal virtual bool isImplicitlyDeclared => false;

    /// <summary>
    /// Gets the original definition of the symbol.
    /// </summary>
    internal Symbol originalDefinition => _originalSymbolDefinition;

    internal bool isDefinition => (object)this == originalDefinition;

    private protected virtual Symbol _originalSymbolDefinition => this;

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    public abstract SymbolKind kind { get; }

    /// <summary>
    /// If the symbol is "static", i.e. declared with the static modifier.
    /// </summary>
    internal abstract bool isStatic { get; }

    /// <summary>
    /// If the symbol is "virtual", i.e. is defined but can be overridden
    /// </summary>
    internal abstract bool isVirtual { get; }

    /// <summary>
    /// If the symbol is "abstract", i.e. must be overridden or cannot be constructed directly.
    /// </summary>
    internal abstract bool isAbstract { get; }

    /// <summary>
    /// If the symbol is "override", i.e. overriding a virtual or abstract symbol.
    /// </summary>
    internal abstract bool isOverride { get; }

    /// <summary>
    /// If the symbol is "sealed", i.e. cannot have child classes.
    /// </summary>
    internal abstract bool isSealed { get; }

    internal virtual Compilation declaringCompilation {
        get {
            if (!isDefinition)
                return originalDefinition.declaringCompilation;

            if (this is NamespaceSymbol @namespace)
                return @namespace.containingCompilation;

            return containingSymbol.declaringCompilation;
        }
    }

    internal virtual NamespaceSymbol containingNamespace {
        get {
            for (var container = containingSymbol; container is not null; container = container.containingSymbol) {
                if (container is NamespaceSymbol ns)
                    return ns;
            }

            return null;
        }
    }

    // TODO Will need to change this to an immutable array when `partial` keyword is added
    internal abstract SyntaxReference syntaxReference { get; }

    internal virtual void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        if (diagnostics.Count > 0)
            declaringCompilation.diagnostics.Move(diagnostics);
    }

    internal virtual void ForceComplete(TextLocation location) { }

    internal virtual bool HasComplete(CompletionParts part) {
        return true;
    }

    internal virtual void AfterAddingTypeMembersChecks(BelteDiagnosticQueue diagnostics) { }

    internal NamespaceOrTypeSymbol ContainingNamespaceOrType() {
        if (containingSymbol is not null) {
            switch (containingSymbol.kind) {
                case SymbolKind.Namespace:
                case SymbolKind.NamedType:
                case SymbolKind.ErrorType:
                    return (NamespaceOrTypeSymbol)containingSymbol;
            }
        }

        return null;
    }

    internal LexicalSortKey GetLexicalSortKey() {
        var declaringCompilation = this.declaringCompilation;
        return new LexicalSortKey(syntaxReference, declaringCompilation);
    }

    internal int GetMemberArity() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).arity,
            SymbolKind.NamedType or SymbolKind.ErrorType => ((NamedTypeSymbol)this).arity,
            _ => 0,
        };
    }

    internal ImmutableArray<TemplateParameterSymbol> GetMemberTemplateParameters() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).templateParameters,
            SymbolKind.NamedType or SymbolKind.ErrorType => ((NamedTypeSymbol)this).templateParameters,
            SymbolKind.Field => [],
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal int GetParameterCount() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameterCount,
            SymbolKind.Field => 0,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal ImmutableArray<ParameterSymbol> GetParameters() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameters,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal TypeWithAnnotations GetTypeOrReturnType() {
        GetTypeOrReturnType(out _, out var returnType);
        return returnType;
    }

    internal void GetTypeOrReturnType(
        out RefKind refKind,
        out TypeWithAnnotations returnType) {
        switch (kind) {
            case SymbolKind.Field:
                var field = (FieldSymbol)this;
                // TODO Why is this None?
                refKind = RefKind.None;
                returnType = field.typeWithAnnotations;
                break;
            case SymbolKind.Method:
                var method = (MethodSymbol)this;
                refKind = method.refKind;
                returnType = method.returnTypeWithAnnotations;
                break;
            case SymbolKind.Local:
                var local = (LocalSymbol)this;
                refKind = local.refKind;
                returnType = local.typeWithAnnotations;
                break;
            case SymbolKind.Parameter:
                var parameter = (ParameterSymbol)this;
                refKind = parameter.refKind;
                returnType = parameter.typeWithAnnotations;
                break;
            case SymbolKind.ErrorType:
                refKind = RefKind.None;
                returnType = new TypeWithAnnotations((TypeSymbol)this);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }

    internal bool RequiresInstanceReceiver() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).requiresInstanceReceiver,
            SymbolKind.Field => ((FieldSymbol)this).requiresInstanceReceiver,
            _ => throw ExceptionUtilities.UnexpectedValue(kind)
        };
    }

    internal Symbol SymbolAsMember(NamedTypeSymbol newOwner) {
        return kind switch {
            SymbolKind.Field => ((FieldSymbol)this).AsMember(newOwner),
            SymbolKind.Method => ((MethodSymbol)this).AsMember(newOwner),
            SymbolKind.NamedType => ((NamedTypeSymbol)this).AsMember(newOwner),
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal bool IsNoMoreVisibleThan(TypeSymbol type) {
        return type.IsAtLeastAsVisibleAs(this);
    }

    internal bool IsFromCompilation(Compilation compilation) {
        return compilation == declaringCompilation;
    }

    internal bool Equals(Symbol other) {
        return Equals(other, SymbolEqualityComparer.Default.compareKind);
    }

    internal bool Equals(Symbol other, SymbolEqualityComparer comparer) {
        return Equals(other, comparer.compareKind);
    }

    internal virtual bool Equals(Symbol other, TypeCompareKind compareKind) {
        return (object)this == other;
    }

    public string ToDisplayString(SymbolDisplayFormat format) {
        return SymbolDisplay.ToDisplayString(this, format);
    }

    public override string ToString() {
        return ToDisplayString(null);
    }

    public override int GetHashCode() {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    public sealed override bool Equals(object obj) {
        return Equals(obj as Symbol, SymbolEqualityComparer.Default.compareKind);
    }

    public static bool operator ==(Symbol left, Symbol right) {
        if (right is null)
            return left is null;

        return (object)left == right || right.Equals(left);
    }

    public static bool operator !=(Symbol left, Symbol right) {
        if (right is null)
            return left is not null;

        return (object)left != right && !right.Equals(left);
    }
}
