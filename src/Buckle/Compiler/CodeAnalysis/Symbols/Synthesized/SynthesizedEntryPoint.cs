using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedEntryPoint : SourceMemberMethodSymbol {
    private readonly TypeSymbol _returnType;
    private readonly SingleTypeDeclaration _declaration;

    private WeakReference<ExecutableCodeBinder> _weakBodyBinder;
    private WeakReference<ExecutableCodeBinder> _weakIgnoreAccessibilityBodyBinder;
    private Binder _lazyProgramBinder;

    internal SynthesizedEntryPoint(SourceMemberContainerTypeSymbol containingType, SingleTypeDeclaration declaration)
        : base(containingType, declaration.syntaxReference, MakeModifiersAndFlags(containingType, declaration)) {
        _returnType = CorLibrary.GetSpecialType(SpecialType.Void);
        _declaration = declaration;
    }

    public override string name => WellKnownMemberNames.EntryPointMethodName;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override bool returnsVoid => true;

    internal override TypeWithAnnotations returnTypeWithAnnotations => new TypeWithAnnotations(_returnType);

    internal override ImmutableArray<ParameterSymbol> parameters => [];

    internal override int parameterCount => 0;

    internal override TextLocation location => _declaration.syntaxReference.location;

    internal CompilationUnitSyntax compilationUnit => (CompilationUnitSyntax)syntaxNode;

    internal ImmutableArray<GlobalStatementSyntax> statements { get; }

    internal Binder programBinder {
        get {
            if (_lazyProgramBinder is null)
                Interlocked.CompareExchange(ref _lazyProgramBinder, CreateSimpleProgramBinder(false), null);

            return _lazyProgramBinder;
        }
    }

    internal ExecutableCodeBinder GetBodyBinder(bool ignoreAccessibility) {
        ref var weakBinder = ref ignoreAccessibility ? ref _weakIgnoreAccessibilityBodyBinder : ref _weakBodyBinder;

        while (true) {
            var previousWeakReference = weakBinder;

            if (previousWeakReference is not null && previousWeakReference.TryGetTarget(out var previousBinder))
                return previousBinder;

            var newBinder = CreateBodyBinder(ignoreAccessibility);

            if (Interlocked.CompareExchange(
                ref weakBinder,
                new WeakReference<ExecutableCodeBinder>(newBinder), previousWeakReference) == previousWeakReference) {
                return newBinder;
            }
        }
    }

    internal override ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds() {
        return [];
    }

    internal override ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes() {
        return [];
    }

    internal override ExecutableCodeBinder TryGetBodyBinder(
        BinderFactory binderFactory = null,
        bool ignoreAccessibility = false) {
        return GetBodyBinder(ignoreAccessibility);
    }

    private protected override void MethodChecks(BelteDiagnosticQueue diagnostics) { }

    private static (DeclarationModifiers, Flags) MakeModifiersAndFlags(
        SourceMemberContainerTypeSymbol containingType,
        SingleTypeDeclaration declaration) {
        var hasReturnWithExpression = declaration.hasReturnWithExpression;
        var declarationModifiers = DeclarationModifiers.Static | DeclarationModifiers.Private;
        var compilation = containingType.declaringCompilation;
        var compilationUnit = (CompilationUnitSyntax)declaration.syntaxReference.node;

        var flags = MakeFlags(
            MethodKind.Ordinary,
            RefKind.None,
            declarationModifiers,
            returnsVoid: !hasReturnWithExpression,
            returnsVoidIsSet: true,
            hasAnyBody: true,
            hasThisInitializer: false
        );

        return (declarationModifiers, flags);
    }

    private Binder CreateSimpleProgramBinder(bool ignoreAccessibility) {
        var compilation = declaringCompilation;
        var result = GetPreviousBinder() ?? new EndBinder(compilation, syntaxTree.text);

        if (compilation.options.isScript)
            result = result.WithAdditionalFlags(BinderFlags.IgnoreAccessibility);

        var globalNamespace = compilation.globalNamespaceInternal;
        result = new InContainerBinder(globalNamespace, result);
        result = new InContainerBinder(containingType, result);
        result = new InMethodBinder(this, result);
        result = result.WithAdditionalFlags(ignoreAccessibility ? BinderFlags.IgnoreAccessibility : BinderFlags.None);
        // TODO confirm need for this binder:
        _lazyProgramBinder = result;
        // _lazyProgramBinder = new SimpleProgramBinder(result, this);
        return _lazyProgramBinder;
    }

    private Binder GetPreviousBinder() {
        var previousCompilation = declaringCompilation.previous;
        return GetBinderFromCompilation(previousCompilation);
    }

    private static Binder GetBinderFromCompilation(Compilation compilation) {
        if (compilation is null || compilation.syntaxTrees.Length != 1)
            return null;

        var syntaxTree = compilation.syntaxTrees[0];
        var root = syntaxTree.GetCompilationUnitRoot();
        // We fallback to main entry point because we really do not want this to fail (otherwise everything haults)
        var programBinder = GetSimpleProgramEntryPoint(compilation, root, true)?.programBinder;

        return programBinder ?? new InContainerBinder(
            compilation.globalNamespaceInternal,
            GetBinderFromCompilation(compilation.previous) ?? new EndBinder(compilation, syntaxTree.text)
        );
    }

    private ExecutableCodeBinder CreateBodyBinder(bool ignoreAccessibility) {
        if (_lazyProgramBinder is null)
            Interlocked.CompareExchange(ref _lazyProgramBinder, CreateSimpleProgramBinder(ignoreAccessibility), null);

        return new ExecutableCodeBinder(syntaxNode, this, _lazyProgramBinder);
    }

    internal static SynthesizedEntryPoint GetSimpleProgramEntryPoint(
        Compilation compilation,
        CompilationUnitSyntax compilationUnit,
        bool fallbackToMainEntryPoint) {
        var type = GetSimpleProgramNamedTypeSymbol(compilation);

        if (type is null)
            return null;

        var entryPoints = type.GetSimpleProgramEntryPoints();

        foreach (var entryPoint in entryPoints) {
            if (entryPoint.syntaxTree == compilationUnit.syntaxTree && entryPoint.syntaxNode == compilationUnit)
                return entryPoint;
        }

        return fallbackToMainEntryPoint ? entryPoints[0] : null;
    }

    internal static SynthesizedEntryPoint GetSimpleProgramEntryPoint(Compilation compilation) {
        return GetSimpleProgramNamedTypeSymbol(compilation)?.GetSimpleProgramEntryPoints().First();
    }

    private static SourceNamedTypeSymbol GetSimpleProgramNamedTypeSymbol(Compilation compilation) {
        return compilation.globalNamespaceInternal
            .GetTypeMembers(WellKnownMemberNames.TopLevelStatementsEntryPointTypeName)
            .OfType<SourceNamedTypeSymbol>()
            .SingleOrDefault(s => s.isSimpleProgram);
    }
}
