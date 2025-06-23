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

    internal SynthesizedEntryPoint(SourceMemberContainerTypeSymbol containingType, SingleTypeDeclaration declaration)
        : base(containingType, declaration.syntaxReference, MakeModifiersAndFlags(containingType, declaration)) {
        _returnType = declaration.hasReturnWithExpression
            ? CorLibrary.GetNullableType(SpecialType.Any)
            : CorLibrary.GetSpecialType(SpecialType.Void);

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

    internal override bool isMetadataFinal => false;

    // TODO Reference says members.First is what we want, but why?? Double check this
    internal SyntaxNode returnTypeSyntax => compilationUnit.members.Last(m => m.kind == SyntaxKind.GlobalStatement);

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

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        return localPosition;
    }

    internal override bool IsMetadataVirtual(bool forceComplete = false) => false;

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

    private ExecutableCodeBinder CreateBodyBinder(bool ignoreAccessibility) {
        var compilation = declaringCompilation;
        var syntaxNode = this.syntaxNode;
        Binder result = new EndBinder(compilation, syntaxNode.syntaxTree.text);

        for (var current = compilation; current is not null; current = current.previous)
            result = new InContainerBinder(current.globalNamespaceInternal, result);

        result = new InContainerBinder(containingType, result);
        result = new InMethodBinder(this, result);
        result = result.WithAdditionalFlags(ignoreAccessibility ? BinderFlags.IgnoreAccessibility : BinderFlags.None);

        return new ExecutableCodeBinder(syntaxNode, this, result);
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

    internal static SourceNamedTypeSymbol GetSimpleProgramNamedTypeSymbol(Compilation compilation) {
        return compilation.globalNamespaceInternal
            .GetTypeMembers(WellKnownMemberNames.TopLevelStatementsEntryPointTypeName)
            .OfType<SourceNamedTypeSymbol>()
            .SingleOrDefault(s => s.isSimpleProgram);
    }
}
