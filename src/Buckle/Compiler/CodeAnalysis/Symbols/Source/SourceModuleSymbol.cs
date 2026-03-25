using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceModuleSymbol : NonMissingModuleSymbol, IAttributeTargetSymbol {
    private readonly DeclarationTable _sources;

    private SymbolCompletionState _state;
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;
    private ImmutableArray<TextLocation> _locations;
    private NamespaceSymbol _lazyGlobalNamespace;

    internal SourceModuleSymbol(SourceAssemblySymbol assembly, DeclarationTable declarationTable, string name) {
        containingAssembly = assembly;
        _sources = declarationTable;
        this.name = name;
    }

    public override string name { get; }

    internal override AssemblySymbol containingAssembly { get; }

    internal override Symbol containingSymbol => containingAssembly;

    internal override Compilation declaringCompilation => containingAssembly.declaringCompilation;

    internal override bool requiresCompletion => true;

    internal override int ordinal => 0;

    internal override bool bit32Required => false;

    internal override ICollection<string> typeNames => _sources.typeNames;

    internal override ICollection<string> namespaceNames => _sources.namespaceNames;

    internal override bool hasAssemblyCompilationRelaxationsAttribute => false;

    internal override bool hasAssemblyRuntimeCompatibilityAttribute => false;

    internal override CharSet? defaultMarshallingCharSet => null;

    internal override bool areLocalsZeroed => true;

    internal override bool useUpdatedEscapeRules => false;

    internal override ImmutableArray<TextLocation> locations {
        get {
            if (_locations.IsDefault) {
                ImmutableInterlocked.InterlockedInitialize(
                    ref _locations,
                    declaringCompilation.mergedRootDeclaration.declarations.SelectAsArray(d => d.location)
                );
            }

            return _locations;
        }
    }

    IAttributeTargetSymbol IAttributeTargetSymbol.attributesOwner => (SourceAssemblySymbol)containingAssembly;

    AttributeLocation IAttributeTargetSymbol.defaultAttributeLocation => AttributeLocation.Module;

    AttributeLocation IAttributeTargetSymbol.allowedAttributeLocations
        => AttributeLocation.Assembly | AttributeLocation.Module;

    internal override NamespaceSymbol globalNamespace {
        get {
            if (_lazyGlobalNamespace is null) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();

                var globalNS = new SourceNamespaceSymbol(
                    this,
                    this,
                    declaringCompilation.mergedRootDeclaration,
                    diagnostics
                );

                if (Interlocked.CompareExchange(ref _lazyGlobalNamespace, globalNS, null) is null)
                    AddDeclarationDiagnostics(diagnostics);

                diagnostics.Free();
            }

            return _lazyGlobalNamespace;
        }
    }

    internal override ModuleMetadata GetMetadata() {
        return null;
    }

    internal override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Attributes:
                    GetAttributes();
                    break;
                case CompletionParts.MembersCompleted:
                    globalNamespace.ForceComplete(location);

                    if (globalNamespace.HasComplete(CompletionParts.MembersCompleted)) {
                        _state.NotePartComplete(CompletionParts.MembersCompleted);
                        break;
                    } else {
                        return;
                    }
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(incompletePart);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag() {
        if (_lazyAttributesBag is not null && _lazyAttributesBag.isSealed)
            return _lazyAttributesBag;

        var mergedAttributes = ((SourceAssemblySymbol)containingAssembly).GetAttributeDeclarations();

        if (LoadAndValidateAttributes(OneOrMany.Create(mergedAttributes), ref _lazyAttributesBag))
            _state.NotePartComplete(CompletionParts.Attributes);

        return _lazyAttributesBag;
    }
}
