using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedEntryPoint : SynthesizedInstanceMethodSymbol {
    private WeakReference<ExecutableCodeBinder> _weakBodyBinder;
    private TypeWithAnnotations _returnType;
    private Binder _lazyProgramBinder;

    internal SynthesizedEntryPoint(
        Symbol containingSymbol,
        TypeWithAnnotations returnType,
        ImmutableArray<GlobalStatementSyntax> syntax,
        CompilationUnitSyntax syntaxNode) {
        this.containingSymbol = containingSymbol;
        _returnType = returnType;
        statements = syntax;
        syntaxReference = new SyntaxReference(syntaxNode);
        compilationUnit = syntaxNode;
    }

    public override string name => WellKnownMemberNames.EntryPointMethodName;

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override RefKind refKind => RefKind.None;

    public override bool returnsVoid => returnTypeWithAnnotations.IsVoidType();

    public override MethodKind methodKind => MethodKind.Ordinary;

    public override int arity => 0;

    internal override Symbol containingSymbol { get; }

    internal override TypeWithAnnotations returnTypeWithAnnotations => _returnType;

    internal override ImmutableArray<ParameterSymbol> parameters => [];

    internal override int parameterCount => 0;

    internal override bool hidesBaseMethodsByName => false;

    internal override bool hasSpecialName => false;

    internal override bool isDeclaredConst => false;

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override bool isStatic => false;

    internal override bool isVirtual => false;

    internal override bool isAbstract => false;

    internal override bool isOverride => false;

    internal override bool isSealed => false;

    internal override bool requiresInstanceReceiver => false;

    internal override SyntaxReference syntaxReference { get; }

    internal override TextLocation location => null;

    internal override CallingConvention callingConvention => 0;

    internal SyntaxTree syntaxTree => syntaxNode.syntaxTree;

    internal SyntaxNode syntaxNode => compilationUnit;

    internal CompilationUnitSyntax compilationUnit { get; }

    internal ImmutableArray<GlobalStatementSyntax> statements { get; }

    internal Binder programBinder {
        get {
            if (_lazyProgramBinder is null)
                Interlocked.CompareExchange(ref _lazyProgramBinder, CreateSimpleProgramBinder(), null);

            return _lazyProgramBinder;
        }
    }

    internal ExecutableCodeBinder GetBodyBinder() {
        ref var weakBinder = ref _weakBodyBinder;

        while (true) {
            var previousWeakReference = weakBinder;

            if (previousWeakReference is not null && previousWeakReference.TryGetTarget(out var previousBinder))
                return previousBinder;

            var newBinder = CreateBodyBinder();

            if (Interlocked.CompareExchange(
                ref weakBinder,
                new WeakReference<ExecutableCodeBinder>(newBinder), previousWeakReference) == previousWeakReference) {
                return newBinder;
            }
        }
    }

    private Binder CreateSimpleProgramBinder() {
        var compilation = declaringCompilation;
        var result = GetPreviousBinder() ?? new EndBinder(compilation, syntaxTree.text);

        if (compilation.options.isScript)
            result = result.WithAdditionalFlags(BinderFlags.IgnoreAccessibility);

        var globalNamespace = compilation.globalNamespaceInternal;
        result = new InContainerBinder(globalNamespace, result);
        result = new InContainerBinder(containingType, result);
        result = new InMethodBinder(this, result);
        // TODO Do we actually need the SimpleProgramBinder ever?
        // _lazyProgramBinder = new SimpleProgramBinder(result, this);
        _lazyProgramBinder = result;
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
        var programBinder = GetEntryPoint(compilation, root)?.programBinder;

        return programBinder ?? new InContainerBinder(
            compilation.globalNamespaceInternal,
            GetBinderFromCompilation(compilation.previous) ?? new EndBinder(compilation, syntaxTree.text)
        );
    }

    private ExecutableCodeBinder CreateBodyBinder() {
        return new ExecutableCodeBinder(syntaxNode, this, programBinder);
    }

    internal void CorrectReturnType(TypeWithAnnotations type) {
        _returnType = type;
    }

    internal static SynthesizedEntryPoint GetEntryPoint(
        Compilation compilation,
        CompilationUnitSyntax compilationUnit) {
        var type = GetSimpleProgramNamedTypeSymbol(compilation);

        if (type is null)
            return null;

        var entryPoints = type.GetMembers(WellKnownMemberNames.EntryPointMethodName).OfType<SynthesizedEntryPoint>();

        foreach (var entryPoint in entryPoints) {
            if (entryPoint.syntaxTree == compilationUnit.syntaxTree && entryPoint.syntaxNode == compilationUnit)
                return entryPoint;
        }

        return null;
    }

    private static NamedTypeSymbol GetSimpleProgramNamedTypeSymbol(Compilation compilation) {
        return compilation.globalNamespaceInternal
            .GetTypeMembers(WellKnownMemberNames.TopLevelStatementsEntryPointTypeName)
            .SingleOrDefault(s => s.isSimpleProgram);
    }
}
