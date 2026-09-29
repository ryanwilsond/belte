using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class CompileTimeLowerer : BoundTreeExpander {
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly Evaluator _evaluator;
    private readonly EvaluatorContext _context;
    private readonly BoundProgram _program;
    private readonly Compilation _compilation;
    private readonly Dictionary<Symbol, ConstantValue> _constantMap;

    private CompileTimeLowerer(
        MethodSymbol containingMethod,
        BoundProgram program,
        EvaluatorContext context,
        BelteDiagnosticQueue diagnostics,
        Compilation compilation,
        Dictionary<Symbol, ConstantValue> constantMap) {
        _diagnostics = diagnostics;
        _evaluator = new Evaluator(program, context, []);
        _context = context;
        _container = containingMethod;
        _program = program;
        _compilation = compilation;
        _constantMap = constantMap;
    }

    private protected override MethodSymbol _container { get; set; }

    internal static BoundBlockStatement Lower(
        MethodSymbol method,
        BoundBlockStatement statement,
        BelteDiagnosticQueue diagnostics,
        BoundProgram program,
        EvaluatorContext context,
        Compilation compilation,
        Dictionary<Symbol, ConstantValue> constantMap) {
        var lowerer = new CompileTimeLowerer(method, program, context, diagnostics, compilation, constantMap);
        lowerer._localNames.AddRange(statement.locals.Select(l => l.name));
        return lowerer.Expand(statement);
    }

    internal static bool IsValidCompileTimeExpressionType(TypeSymbol type) {
        return type.IsPrimitiveType() || type.IsStructType() || type.IsArray() || type.IsVoidType();
    }

    private BoundBlockStatement Expand(BoundBlockStatement statement) {
        return (BoundBlockStatement)Simplify(statement.syntax, ExpandStatement(statement));
    }

    private static bool IsValidCompileTimeExpression(BoundExpression expression, out Symbol state) {
        var walker = new CompileTimeExpressionWalker();
        walker.Visit(expression);
        state = walker.firstInvalidSymbol;
        return !walker.invalidNode;
    }

    private protected override List<BoundStatement> ExpandCompileTimeExpression(
        BoundCompileTimeExpression node,
        out BoundExpression replacement,
        UseKind useKind) {
        // Avoid evaluator if possible. This also gives better diagnostics by letting ConstantFoldingPass handle it
        if (!node.conditional && Binder.EnsureExpressionIsCompileTime(node.expression))
            return ExpandExpression(node.expression, out replacement);

        var statements = ExpandExpression(node.expression, out var newExpression);

        if (!IsValidCompileTimeExpression(newExpression, out var symbol)) {
            _diagnostics.Push(Error.InvalidCompileTimeExpressionState(node.syntax.location, symbol));
            replacement = newExpression;
            return [];
        }

        var methodLayout = _program.methodLayouts[_container.originalDefinition];
        var result = _evaluator.EvaluateExpression(newExpression, methodLayout);

        if (result.exceptions.Count > 0) {
            Debug.Assert(result.exceptions.Count == 1);
            var exception = result.exceptions[0];

            switch (exception) {
                case BelteInternalException:
                    // TODO Eventually we want the Evaluator to not just fail on us
                    // This is hard because to do that we would need full pointer support, so...
                    _diagnostics.Push(Error.InvalidCompileTimeExpression(node.syntax.location));
                    break;
                case BelteEvaluatorException evaluatorException:
                    if (evaluatorException.failCompileTimeExpressions) {
                        _diagnostics.Push(Error.InvalidCompileTimeExpressionWithReason(
                            node.syntax.location,
                            evaluatorException
                        ));
                    } else {
                        if (node.conditional) {
                            _diagnostics.Push(Info.CompileTimeExpressionThrew(node.syntax.location));
                        } else {
                            _diagnostics.Push(Error.CompileTimeExpressionThrew(
                                node.syntax.location,
                                evaluatorException
                            ));
                        }
                    }

                    break;
                case BoundTreeVisitor.CancelledByStackGuardException:
                    if (node.conditional)
                        _diagnostics.Push(Info.InvalidCompileTimeExpressionStack(node.syntax.location));
                    else
                        _diagnostics.Push(Error.InvalidCompileTimeExpressionStack(node.syntax.location));

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(exception);
            }

            replacement = newExpression;
            return [];
        }

        var value = (EvaluatorValue)result.value;

        if (node.type.IsVoidType()) {
            Debug.Assert(useKind == UseKind.None);
            replacement = null;
            return statements.Count == 0 ? [BoundFactory.Nop()] : statements;
        }

        var nodeType = node.StrippedType();

        if (!IsValidCompileTimeExpressionType(nodeType)) {
            _diagnostics.Push(Error.InvalidCompileTimeType(node.syntax.location));
            replacement = node;
            return statements.Count == 0 ? [BoundFactory.Nop()] : statements;
        }

        if (nodeType.IsArray()) {
            var isEvaluating = _compilation.options.buildMode.Evaluating();
            var syntax = node.syntax;
            statements.AddRange(BuildArray(isEvaluating, syntax, _context.heap[value.ptr], out replacement));
            return statements;
        }

        if (nodeType.IsPrimitiveType()) {
            replacement = Lowerer.VisitConstant(
                _compilation,
                BoundFactory.Literal(_compilation, node.syntax, EvaluatorValue.Format(value, _context), node.type)
            );

            return statements.Count == 0 ? [BoundFactory.Nop()] : statements;
        }

        if (nodeType.IsStructType()) {
            var isEvaluating = _compilation.options.buildMode.Evaluating();
            var syntax = node.syntax;
            statements.AddRange(BuildStruct(isEvaluating, syntax, value.@struct, out replacement));
            return statements;
        }

        throw ExceptionUtilities.UnexpectedValue(value.kind);
    }

    private List<BoundStatement> BuildArray(
        bool isEvaluating,
        SyntaxNode syntax,
        HeapObject array,
        out BoundExpression replacement) {
        var arrayType = (ArrayTypeSymbol)array.type;
        var fieldValues = array.fields;

        var methodLayout = _program.methodLayouts[_container.originalDefinition];

        var tempLocal = GenerateTempLocal(arrayType);
        var dataContainerExpression = new BoundDataContainerExpression(
            syntax,
            tempLocal,
            null,
            tempLocal.type
        );

        var stackSlot = methodLayout.DeclareLocal(
            dataContainerExpression.type,
            dataContainerExpression.dataContainer,
            dataContainerExpression.dataContainer.name,
            SynthesizedLocalKind.ExpanderTemp,
            CodeGeneration.LocalSlotConstraints.None,
            false
        );

        var statements = new List<BoundStatement>();

        if (isEvaluating) {
            replacement = new BoundStackSlotExpression(
                syntax,
                dataContainerExpression,
                dataContainerExpression.dataContainer,
                stackSlot.slot,
                dataContainerExpression.type
            );
        } else {
            replacement = dataContainerExpression;
        }

        var initializer = new BoundArrayCreationExpression(syntax,
            [BoundFactory.Literal(_compilation, syntax, fieldValues.Length, _compilation.GetSpecialType(SpecialType.Int))],
            new BoundInitializerList(syntax,
                fieldValues.Select(p => {
                    BoundExpression result;

                    if (arrayType.elementType.IsStructType()) {
                        statements.AddRange(BuildStruct(isEvaluating, syntax, p.@struct, out result));
                    } else if (arrayType.elementType.IsArray()) {
                        statements.AddRange(BuildArray(isEvaluating, syntax, _context.heap[p.ptr], out result));
                    } else if (arrayType.elementType.StrippedType().IsPrimitiveType()) {
                        result = (BoundExpression)Lowerer.VisitConstant(
                            _compilation,
                            BoundFactory.Literal(_compilation, syntax, EvaluatorValue.Format(p, _context), arrayType.elementType)
                        );
                    } else {
                        throw ExceptionUtilities.UnexpectedValue(p.kind);
                    }

                    return result;
                }).ToImmutableArray(),
                arrayType
            ),
            arrayType
        );

        if (isEvaluating) {
            statements.Add(
                new BoundExpressionStatement(syntax,
                    new BoundAssignmentOperator(syntax,
                        replacement,
                        initializer,
                        false,
                        arrayType
                    )
                )
            );
        } else {
            statements.Add(
                new BoundLocalDeclarationStatement(syntax,
                    new BoundDataContainerDeclaration(syntax,
                        tempLocal,
                        initializer
                    )
                )
            );
        }

        return statements;
    }

    private List<BoundStatement> BuildStruct(
        bool isEvaluating,
        SyntaxNode syntax,
        HeapObject @struct,
        out BoundExpression replacement) {

        var structType = (NamedTypeSymbol)@struct.type;
        var fieldValues = @struct.fields;

        if (!_program.TryGetTypeLayoutIncludingParents(structType, out var typeLayout))
            throw ExceptionUtilities.Unreachable();

        var methodLayout = _program.methodLayouts[_container.originalDefinition];

        var tempLocal = GenerateTempLocal(structType);
        var dataContainerExpression = new BoundDataContainerExpression(
            syntax,
            tempLocal,
            null,
            tempLocal.type
        );

        var stackSlot = methodLayout.DeclareLocal(
            dataContainerExpression.type,
            dataContainerExpression.dataContainer,
            dataContainerExpression.dataContainer.name,
            SynthesizedLocalKind.ExpanderTemp,
            CodeGeneration.LocalSlotConstraints.None,
            false
        );

        if (isEvaluating) {
            replacement = new BoundStackSlotExpression(
                syntax,
                dataContainerExpression,
                dataContainerExpression.dataContainer,
                stackSlot.slot,
                dataContainerExpression.type
            );
        } else {
            replacement = dataContainerExpression;
        }

        var statements = new List<BoundStatement>();
        var structFields = typeLayout.LocalsInOrder();

        var initializer = new BoundObjectCreationExpression(syntax,
            structType.instanceConstructors.Single(),
            [],
            [],
            [],
            BitVector.Empty,
            false,
            structType
        );

        if (isEvaluating) {
            statements.Add(
                new BoundExpressionStatement(syntax,
                    new BoundAssignmentOperator(syntax,
                        replacement,
                        initializer,
                        false,
                        structType
                    )
                )
            );
        } else {
            statements.Add(
                new BoundLocalDeclarationStatement(syntax,
                    new BoundDataContainerDeclaration(syntax,
                        tempLocal,
                        initializer
                    )
                )
            );
        }

        for (var i = 0; i < fieldValues.Length; i++) {
            var fieldValue = fieldValues[i];
            var field = (FieldSymbol)structFields[i].symbol;
            var fieldAccess = new BoundFieldAccessExpression(
                syntax,
                replacement,
                field,
                null,
                field.type
            );

            BoundExpression left = isEvaluating
                ? new BoundFieldSlotExpression(syntax,
                    fieldAccess,
                    replacement,
                    field,
                    i,
                    field.type)
                : fieldAccess;

            BoundExpression right;

            if (field.type.IsStructType()) {
                statements.AddRange(BuildStruct(isEvaluating, syntax, fieldValue.@struct, out right));
            } else if (field.type.IsArray()) {
                statements.AddRange(BuildArray(isEvaluating, syntax, _context.heap[fieldValue.ptr], out right));
            } else if (field.type.StrippedType().IsPrimitiveType()) {
                right = (BoundExpression)Lowerer.VisitConstant(
                    _compilation,
                    BoundFactory.Literal(_compilation, syntax, EvaluatorValue.Format(fieldValue, _context), field.type)
                );
            } else {
                throw ExceptionUtilities.UnexpectedValue(fieldValue.kind);
            }

            statements.Add(
                new BoundExpressionStatement(syntax,
                    new BoundAssignmentOperator(syntax,
                        left,
                        right,
                        false,
                        field.type
                    )
                )
            );
        }

        return statements;
    }

    // These would technically be discovered in ConstantFoldingPass anyway, but might as well add them to the map early
    private protected override List<BoundStatement> ExpandAssignmentOperator(
        BoundAssignmentOperator expression,
        out BoundExpression replacement,
        UseKind useKind) {
        var statements = ExpandExpression(expression.left, out var newLeft, UseKind.Writable);
        statements.AddRange(ExpandExpression(expression.right, out var newRight));

        if (newLeft.expressionSymbol is { } symbol && symbol.IsConstExpr() &&
            newRight.constantValue is { } constant) {
            _constantMap.Add(symbol, constant);
            replacement = null;
            return statements;
        }

        replacement = expression.Update(newLeft, newRight, expression.isRef, expression.type);
        return statements;
    }

    private protected override List<BoundStatement> ExpandLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement) {
        var statements = ExpandExpression(statement.declaration.initializer, out var newInitializer);
        var dataContainer = statement.declaration.dataContainer;

        if (dataContainer.isConstExpr && newInitializer.constantValue is { } constant) {
            _constantMap.Add(dataContainer, constant);
            return statements;
        }

        if (statements.Count > 0 || statement.declaration.initializer != newInitializer) {
            statements.Add(new BoundLocalDeclarationStatement(
                statement.syntax,
                new BoundDataContainerDeclaration(statement.syntax, statement.declaration.dataContainer, newInitializer),
                statement.isScoped,
                statement.disposeMethod
            ));

            return statements;
        }

        return [statement];
    }
}
