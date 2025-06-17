using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class AliasSymbolFromSyntax : AliasSymbol {
    private readonly SyntaxReference _directive;
    private SymbolCompletionState _state;
    private NamespaceOrTypeSymbol _aliasTarget;
    private BelteDiagnosticQueue _aliasTargetDiagnostics;

    internal AliasSymbolFromSyntax(SourceNamespaceSymbol containingSymbol, UsingDirectiveSyntax syntax)
        : base(syntax.alias.name.identifier.text, containingSymbol, [syntax.alias.name.identifier.location]) {
        _directive = new SyntaxReference(syntax);
    }

    public override NamespaceOrTypeSymbol target => GetAliasTarget(basesBeingResolved: null);

    internal override bool requiresCompletion => true;

    internal override NamespaceOrTypeSymbol GetAliasTarget(ConsList<TypeSymbol>? basesBeingResolved) {
        if (!_state.HasComplete(CompletionParts.AliasTarget)) {
            var newDiagnostics = BelteDiagnosticQueue.GetInstance();

            var symbol = ResolveAliasTarget((UsingDirectiveSyntax)_directive.node, newDiagnostics, basesBeingResolved);

            if (Interlocked.CompareExchange(ref _aliasTarget, symbol, null) is null) {
                Interlocked.Exchange(ref _aliasTargetDiagnostics, newDiagnostics);
                _state.NotePartComplete(CompletionParts.AliasTarget);
            } else {
                newDiagnostics.Free();
                _state.SpinWaitComplete(CompletionParts.AliasTarget);
            }
        }

        return _aliasTarget;
    }

    internal BelteDiagnosticQueue aliasTargetDiagnostics {
        get {
            GetAliasTarget(null);
            return _aliasTargetDiagnostics;
        }
    }

    private NamespaceOrTypeSymbol ResolveAliasTarget(
        UsingDirectiveSyntax usingDirective,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol>? basesBeingResolved) {
        var syntax = usingDirective.namespaceOrType;
        var flags = BinderFlags.SuppressConstraintChecks;

        var declarationBinder = containingSymbol.declaringCompilation
            .GetBinderFactory(syntax.syntaxTree)
            .GetBinder(syntax)
            .WithAdditionalFlags(flags);

        var annotatedNamespaceOrType = declarationBinder.BindNamespaceOrTypeSymbol(
            syntax,
            diagnostics,
            basesBeingResolved
        );

        return annotatedNamespaceOrType.namespaceOrTypeSymbol;
    }
}
