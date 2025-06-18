using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class AssemblySymbol : Symbol {
    public override string name => identity.name;

    public sealed override SymbolKind kind => SymbolKind.Assembly;

    internal abstract AssemblyIdentity identity { get; }

    internal abstract NamespaceSymbol globalNamespace { get; }

    internal sealed override Symbol containingSymbol => null;

    internal sealed override AssemblySymbol containingAssembly => null;

    internal abstract bool isMissing { get; }

    internal sealed override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal sealed override bool isSealed => false;

    internal sealed override bool isStatic => false;

    internal sealed override bool isVirtual => false;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isOverride => false;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal abstract ICollection<string> typeNames { get; }

    internal abstract ICollection<string> namespaceNames { get; }

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitAssembly(this, argument);
    }
}
