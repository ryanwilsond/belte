using System;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class LabelSymbol : Symbol {
    public override string name { get; }

    public override SymbolKind kind => SymbolKind.Label;

    internal override bool isStatic => false;

    internal override bool isOverride => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isVirtual => false;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal virtual SyntaxNodeOrToken identifierNodeOrToken => default;

    internal virtual MethodSymbol containingMethod => throw new NotSupportedException();

    internal override Symbol containingSymbol => throw new NotSupportedException();
}
