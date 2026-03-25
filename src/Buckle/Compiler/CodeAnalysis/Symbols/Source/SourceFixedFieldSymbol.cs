using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;

internal class SourceFixedFieldSymbol : SourceMemberFieldSymbolFromDeclarator {
    private const int FixedSizeNotInitialized = -1;
    private int _fixedSize = FixedSizeNotInitialized;

    internal SourceFixedFieldSymbol(
        SourceMemberContainerTypeSymbol containingType,
        VariableDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        bool modifierErrors,
        BelteDiagnosticQueue diagnostics)
        : base(containingType, declaration, modifiers, modifierErrors, diagnostics) { }

    internal sealed override bool isFixedSizeBuffer => true;

    internal sealed override int fixedSize {
        get {
            if (_fixedSize == FixedSizeNotInitialized) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();
                var size = 0;

                var declarator = (VariableDeclarationSyntax)syntaxReference.node;
                var arguments = declarator.argumentList.arguments;

                if (arguments.Count > 1)
                    diagnostics.Push(Error.FixedBufferTooManyDimensions(declarator.argumentList.location));

                var sizeExpression = ((ArgumentSyntax)arguments[0]).expression;

                var binderFactory = declaringCompilation.GetBinderFactory(syntaxTree);
                var binder = binderFactory.GetBinder(sizeExpression);
                binder = new ExecutableCodeBinder(sizeExpression, binder.containingMember, binder).GetBinder(sizeExpression);

                var intType = CorLibrary.GetSpecialType(SpecialType.Int32);

                var boundSizeExpression = binder.GenerateConversionForAssignment(
                    intType,
                    binder.BindValue(sizeExpression, diagnostics, Binder.BindValueKind.RValue),
                    diagnostics
                );

                var sizeConstant = ConstantValueHelpers.GetAndValidateConstantValue(
                    boundSizeExpression,
                    this,
                    intType,
                    sizeExpression,
                    diagnostics
                );

                if (sizeConstant.specialType.IsNumeric()) {
                    var int32Value = (int)sizeConstant.value;

                    if (int32Value > 0) {
                        size = int32Value;

                        var elementType = ((PointerTypeSymbol)type).pointedAtType;
                        var elementSize = elementType.FixedBufferElementSizeInBytes();
                        var totalSize = elementSize * 1L * int32Value;

                        if (totalSize > int.MaxValue)
                            diagnostics.Push(Error.FixedOverflow(sizeExpression.location, int32Value, elementType));
                    } else {
                        diagnostics.Push(Error.InvalidFixedArraySize(sizeExpression.location));
                    }
                }

                if (Interlocked.CompareExchange(ref _fixedSize, size, FixedSizeNotInitialized) == FixedSizeNotInitialized) {
                    AddDeclarationDiagnostics(diagnostics);
                    _state.NotePartComplete(CompletionParts.FixedSize);
                }

                diagnostics.Free();
            }

            return _fixedSize;
        }
    }
}
