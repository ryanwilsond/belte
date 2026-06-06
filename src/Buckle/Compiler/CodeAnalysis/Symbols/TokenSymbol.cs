using System;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class TokenSymbol : Symbol {
    public override string name { get; }

    public override SymbolKind kind => SymbolKind.Token;

    internal override bool isStatic => false;

    internal override bool isOverride => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isVirtual => false;

    internal override bool isExtern => false;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal virtual SyntaxNodeOrToken identifierNodeOrToken => default;

    internal virtual MethodSymbol containingMethod => throw new NotSupportedException();

    internal override Symbol containingSymbol => throw new NotSupportedException();

    internal abstract MethodSymbol stateMethod { get; }

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitToken(this);
    }

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitToken(this, argument);
    }
}
