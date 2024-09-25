using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A method symbol.
/// </summary>
internal abstract class MethodSymbol : Symbol, IMethodSymbol, ISymbolWithTemplates {
    private string _signature = null;

    /// <summary>
    /// Creates a <see cref="MethodSymbol" />.
    /// </summary>
    internal MethodSymbol(
        string name,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<ParameterSymbol> parameters,
        TypeWithAnnotations returnType,
        BaseMethodDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base(name, accessibility) {
        typeWithAnnotations = returnType;
        this.parameters = parameters;
        this.declaration = declaration;
        this.modifiers = modifiers;
        this.templateParameters = templateParameters;
        this.templateConstraints = templateConstraints;
    }

    public override SymbolKind kind => SymbolKind.Method;

    public ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public ImmutableArray<BoundExpression> templateConstraints { get; }

    public abstract ImmutableArray<TypeOrConstant> templateArguments { get; }

    public abstract TemplateMap templateSubstitution { get; }

    internal override bool isStatic => (modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isAbstract => (modifiers & DeclarationModifiers.Abstract) != 0;

    internal override bool isVirtual => (modifiers & DeclarationModifiers.Virtual) != 0;

    internal override bool isOverride => (modifiers & DeclarationModifiers.Override) != 0;

    internal override bool isSealed => false;

    internal DeclarationModifiers modifiers { get; }

    internal new MethodSymbol originalDefinition => originalMethodDefinition;

    internal virtual MethodSymbol originalMethodDefinition => this;

    internal abstract MethodKind methodKind { get; }

    internal override Symbol originalSymbolDefinition => originalMethodDefinition;

    internal bool isConstant => (modifiers & DeclarationModifiers.Const) != 0;

    internal bool isLowLevel => (modifiers & DeclarationModifiers.LowLevel) != 0;

    internal int arity => templateParameters.Length;

    /// <summary>
    /// All parameters (see <see cref="ParameterSymbol" />).
    /// </summary>
    internal ImmutableArray<ParameterSymbol> parameters { get; }

    internal TypeWithAnnotations typeWithAnnotations { get; }

    internal TypeSymbol type { get; }

    /// <summary>
    /// Declaration of method (see <see cref="BaseMethodDeclarationSyntax">).
    /// </summary>
    internal BaseMethodDeclarationSyntax declaration { get; }

    /// <summary>
    /// Gets a string representation of the method signature without the return type or parameter names.
    /// </summary>
    public string Signature() {
        if (_signature is null)
            GenerateSignature();

        return _signature;
    }

    /// <summary>
    /// If the given symbol refers to this one.
    /// </summary>
    internal bool RefersTo(MethodSymbol symbol) {
        if (symbol is null)
            return false;

        return GetRootMethod() == symbol.GetRootMethod();
    }

    /// <summary>
    /// Gets the most original definition (recursively) of this method. If no explicit original definition exists,
    /// returns this.
    /// </summary>
    internal MethodSymbol GetRootMethod() {
        if (originalDefinition == null)
            return this;

        return originalDefinition.GetRootMethod();
    }

    public override int GetHashCode() {
        unchecked {
            var hash = 17;

            hash = hash * 23 + templateParameters.GetHashCode();
            hash = hash * 23 + templateConstraints.GetHashCode();
            hash = hash * 23 + modifiers.GetHashCode();
            hash = hash * 23 + parameters.GetHashCode();
            hash = hash * 23 + type.GetHashCode();
            hash = hash * 23 + name.GetHashCode();
            hash = hash * 23 + accessibility.GetHashCode();
            hash = hash * 23 + (originalDefinition is null ? 0 : originalDefinition.GetHashCode());
            hash = hash * 23 + (declaration is null ? 0 : declaration.GetHashCode());

            return hash;
        }
    }

    private void GenerateSignature() {
        var signature = new StringBuilder(name);
        var isFirst = true;

        if (templateParameters.Length > 0) {
            signature.Append('<');

            foreach (var templateParameter in templateParameters) {
                if (isFirst)
                    isFirst = false;
                else
                    signature.Append(',');

                signature.Append(templateParameter);
            }

            signature.Append('>');
        }

        signature.Append('(');
        isFirst = true;

        foreach (var parameter in parameters) {
            if (isFirst)
                isFirst = false;
            else
                signature.Append(',');

            signature.Append(parameter.type.ToString());
        }

        signature.Append(')');
        _signature = signature.ToString();
    }
}
