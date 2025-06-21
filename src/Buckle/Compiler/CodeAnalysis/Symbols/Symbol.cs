using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A base symbol.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal abstract class Symbol : ISymbol {
    /// <summary>
    /// Name of the symbol.
    /// </summary>
    public virtual string name => "";

    public virtual string metadataName => name;

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

            if (this is AssemblySymbol assembly)
                return assembly.declaringCompilation;

            return containingSymbol.declaringCompilation;
        }
    }

    internal virtual AssemblySymbol containingAssembly => containingSymbol?.containingAssembly;

    internal virtual NamespaceSymbol containingNamespace {
        get {
            for (var container = containingSymbol; container is not null; container = container.containingSymbol) {
                if (container is NamespaceSymbol ns)
                    return ns;
            }

            return null;
        }
    }

    internal virtual ImmutableArray<SyntaxReference> declaringSyntaxReferences => [syntaxReference];

    internal virtual ImmutableArray<TextLocation> locations => [location];

    internal abstract SyntaxReference syntaxReference { get; }

    internal abstract TextLocation location { get; }

    internal abstract TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument a);

    internal virtual void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        if (diagnostics.Count > 0)
            declaringCompilation.declarationDiagnostics.Move(diagnostics);
    }

    internal virtual void ForceComplete(TextLocation location) { }

    internal virtual bool HasComplete(CompletionParts part) {
        return true;
    }

    internal virtual void AfterAddingTypeMembersChecks(BelteDiagnosticQueue diagnostics) { }

    internal TemplateParameterSymbol FindEnclosingTemplateParameter(string name) {
        var methodOrType = this;

        while (methodOrType is not null) {
            switch (methodOrType.kind) {
                case SymbolKind.Method:
                case SymbolKind.NamedType:
                case SymbolKind.ErrorType:
                case SymbolKind.Field:
                    break;
                default:
                    return null;
            }

            foreach (var templateParameter in methodOrType.GetMemberTemplateParameters()) {
                if (templateParameter.name == name)
                    return templateParameter;
            }

            methodOrType = methodOrType.containingSymbol;
        }

        return null;
    }

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

    internal virtual LexicalSortKey GetLexicalSortKey() {
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

    internal ImmutableArray<TypeWithAnnotations> GetParameterTypes() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameterTypesWithAnnotations,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal ImmutableArray<RefKind> GetParameterRefKinds() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameterRefKinds,
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
                var local = (DataContainerSymbol)this;
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

    internal bool IsOperator() {
        return this is MethodSymbol m && m.IsOperator();
    }

    internal ParameterSymbol EnclosingThisSymbol() {
        var symbol = this;

        while (true) {
            switch (symbol.kind) {
                case SymbolKind.Method:
                    var method = (MethodSymbol)symbol;

                    if (method.methodKind == MethodKind.LocalFunction) {
                        symbol = method.containingSymbol;
                        continue;
                    }

                    return method.thisParameter;
                default:
                    return null;
            }
        }
    }

    internal bool IsNoMoreVisibleThan(TypeSymbol type) {
        return type.IsAtLeastAsVisibleAs(this);
    }

    internal Symbol GetLeastOverriddenMember(NamedTypeSymbol accessingTypeOpt) {
        switch (kind) {
            case SymbolKind.Method:
                var method = (MethodSymbol)this;
                return method.GetConstructedLeastOverriddenMethod(accessingTypeOpt, requireSameReturnType: false);
            default:
                return this;
        }
    }

    internal bool LoadAndValidateAttributes(
        OneOrMany<SyntaxList<AttributeListSyntax>> attributesSyntaxLists,
        ref CustomAttributesBag<AttributeData> lazyCustomAttributesBag,
        AttributeLocation symbolPart = AttributeLocation.None,
        bool earlyDecodingOnly = false,
        Binder? binderOpt = null,
        Func<AttributeSyntax, bool> attributeMatchesOpt = null,
        Action<AttributeSyntax> beforeAttributePartBound = null,
        Action<AttributeSyntax> afterAttributePartBound = null) {
        // TODO
        return true;
    }

    // For PE interop use only
    internal virtual byte? GetNullableContextValue() {
        return GetLocalNullableContextValue() ?? containingSymbol?.GetNullableContextValue();
    }

    // For PE interop use only
    internal virtual byte? GetLocalNullableContextValue() {
        return null;
    }

    private protected static bool IsLocationContainedWithin(
        TextLocation location,
        SyntaxTree tree,
        TextSpan declarationSpan,
        out bool wasZeroWidthMatch) {
        if (location.isInSource && location.tree == tree && declarationSpan.Contains(location.span)) {
            wasZeroWidthMatch = location.span.length == 0 && location.span.end == declarationSpan.start;
            return true;
        }

        wasZeroWidthMatch = false;
        return false;
    }

    internal static ImmutableArray<SyntaxReference> GetDeclaringSyntaxReferenceHelper<TNode>(
        ImmutableArray<TextLocation> locations)
        where TNode : BelteSyntaxNode {
        if (locations.IsEmpty)
            return [];

        var builder = ArrayBuilder<SyntaxReference>.GetInstance();

        foreach (var location in locations) {
            if (location is null || !location.isInSource) {
                continue;
            }

            if (location.span.length != 0) {
                var token = location.tree.GetRoot().FindToken(location.span.start);

                if (token.kind != SyntaxKind.None) {
                    var node = token.parent.FirstAncestorOrSelf<TNode>();

                    if (node is not null)
                        builder.Add(new SyntaxReference(node));
                }
            } else {
                SyntaxNode parent = location.tree.GetRoot();
                SyntaxNode found = null;

                foreach (var descendant in parent.DescendantNodesAndSelf(
                    c => c.location.span.Contains(location.span))) {
                    if (descendant is TNode && descendant.location.span.Contains(location.span))
                        found = descendant;
                }

                if (found is not null)
                    builder.Add(new SyntaxReference(found));
            }
        }

        return builder.ToImmutableAndFree();
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

    internal static bool Equals(Symbol first, Symbol second, TypeCompareKind compareKind) {
        if (first is null)
            return second is null;

        return first.Equals(second, compareKind);
    }

    internal virtual bool Equals(Symbol other, TypeCompareKind compareKind) {
        return (object)this == other;
    }

    private string GetDebuggerDisplay() {
        return $"{kind} {ToDisplayString(SymbolDisplayFormat.Everything)}";
    }

    public string ToDisplayString(SymbolDisplayFormat format) {
        return SymbolDisplay.ToDisplayString(this, format);
    }

    public ImmutableArray<DisplayTextSegment> ToDisplaySegments(SymbolDisplayFormat format) {
        return SymbolDisplay.ToDisplaySegments(this, format);
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

    ISymbol ISymbol.containingSymbol => containingSymbol;

    Compilation ISymbol.declaringCompilation => declaringCompilation;
}
