using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Text;
using Shared;
using static Buckle.CodeAnalysis.Binding.Binder;
using static Buckle.CodeAnalysis.CodeGeneration.CodeGenerator;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private readonly BoundProgram _program;
    private readonly EvaluatorContext _context;
    private readonly Stack<StackFrame> _stack;
    private readonly string[] _args;
    private readonly bool _isScript;

    private EvaluatorValue _programObject;
    private EvaluatorValue _lastValue;
    private bool _hasValue;
    private MethodSymbol _lazyToString;
    private Random _lazyRandom;
    private bool _insideTry;
    private bool _insideUpdate;
    private bool _insideExpressionEvaluation;

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
    internal Evaluator(BoundProgram program, EvaluatorContext context, string[] arguments) {
        _context = context;
        _context.program = program;
        _program = program;
        _args = arguments;
        _stack = [];
        exceptions = [];
        _isScript = _context.options.isScript;
    }

    /// <summary>
    /// If the last output to the terminal was a `Print`, and not a `PrintLine`, meaning the caller might want to write
    /// an extra line to prevent formatting problems.
    /// </summary>
    internal bool lastOutputWasPrint { get; private set; }

    /// <summary>
    /// If the submission contains File/Directory IO.
    /// </summary>
    internal bool containsIO { get; private set; }

    /// <summary>
    /// All thrown exceptions during evaluation.
    /// </summary>
    internal List<Exception> exceptions { get; set; }

    private MethodSymbol _toStringMethod {
        get {
            if (_lazyToString is null) {
                var toString = CorLibrary.GetSpecialType(SpecialType.Object)
                    .GetMembers(WellKnownMemberNames.ToString).First() as MethodSymbol;

                Interlocked.CompareExchange(ref _lazyToString, toString, null);
            }

            return _lazyToString;
        }
    }

    /// <summary>
    /// Evaluate the provided <see cref="BoundProgram" />.
    /// </summary>
    /// <param name="abort">External flag used to cancel evaluation.</param>
    /// <param name="hasValue">If the evaluation had a returned result.</param>
    /// <returns>Result of <see cref="BoundProgram" /> (if applicable).</returns>
    internal object Evaluate(ValueWrapper<bool> abort, out bool hasValue, out TypeSymbol resultType) {
        var entryPoint = _program.entryPoint;

        if (entryPoint is null) {
            hasValue = false;
            resultType = null;
            return null;
        }

        var entryPointBody = _program.methodBodies[entryPoint];
        var programType = entryPoint.containingType;
        var result = EvaluatorValue.None;

        EvaluatorValue[] arguments;

        if (entryPoint.parameterCount == 0) {
            arguments = [];
        } else {
            var rootLayout = new EvaluatorSlotManager(programType);

            var argsType = LibraryHelpers.StringArray.knownType;
            var args = new HeapObject(argsType, _args.Select(a => EvaluatorValue.Literal(a)).ToArray());

            var index = _context.heap.Allocate(args, _stack, _context);
            var argsPtr = EvaluatorValue.HeapPtr(index);
            arguments = [argsPtr];

            rootLayout.AllocateSlot(argsType, LocalSlotConstraints.None);

            var rootFrame = new StackFrame(rootLayout);
            rootFrame.values[0] = argsPtr;

            _stack.Push(rootFrame);
        }

        if (!programType.isStatic) {
            _programObject = CreateObject(programType);
            var constructor = programType.instanceConstructors.Where(c => c.parameterCount == 0).FirstOrDefault();

            if (constructor is not null)
                InvokeMethod(constructor, _programObject, [], abort);

            if (!entryPoint.isStatic)
                result = InvokeMethod(entryPoint, _programObject, arguments, abort);
        }

        if (entryPoint.isStatic)
            result = InvokeMethod(entryPoint, EvaluatorValue.None, arguments, abort);

        // Wait until Main finishes before the first call of Update
        if (_context.maintainThread) {
            while (_context.graphicsHandler is null)
                ;
        }

        if (_program.updatePoint is not null)
            _context.graphicsHandler?.SetUpdateHandler(UpdateCaller);

        hasValue = _hasValue;
        resultType = GetResultType(result);
        return hasValue ? EvaluatorValue.Format(result, _context) : null;
    }

    internal EvaluatorValue EvaluateExpression(
        BoundExpression expression,
        EvaluatorSlotManager layout,
        out bool hasValue) {
        _insideExpressionEvaluation = true;
        _hasValue = true;
        _stack.Push(new StackFrame(layout));

        var result = EvaluateExpression(expression, true, false);

        _stack.Pop();
        hasValue = _hasValue;
        _insideExpressionEvaluation = false;
        return result;
    }

    private TypeSymbol GetResultType(EvaluatorValue result) {
        switch (result.kind) {
            case ValueKind.Int8:
                return CorLibrary.GetSpecialType(SpecialType.Int8);
            case ValueKind.Int16:
                return CorLibrary.GetSpecialType(SpecialType.Int16);
            case ValueKind.Int32:
                return CorLibrary.GetSpecialType(SpecialType.Int32);
            case ValueKind.Int64:
                return CorLibrary.GetSpecialType(SpecialType.Int64);
            case ValueKind.UInt8:
                return CorLibrary.GetSpecialType(SpecialType.UInt8);
            case ValueKind.UInt16:
                return CorLibrary.GetSpecialType(SpecialType.UInt16);
            case ValueKind.UInt32:
                return CorLibrary.GetSpecialType(SpecialType.UInt32);
            case ValueKind.UInt64:
                return CorLibrary.GetSpecialType(SpecialType.UInt64);
            case ValueKind.Float32:
                return CorLibrary.GetSpecialType(SpecialType.Float32);
            case ValueKind.Float64:
                return CorLibrary.GetSpecialType(SpecialType.Float64);
            case ValueKind.Bool:
                return CorLibrary.GetSpecialType(SpecialType.Bool);
            case ValueKind.Char:
                return CorLibrary.GetSpecialType(SpecialType.Char);
            case ValueKind.String:
                return CorLibrary.GetSpecialType(SpecialType.String);
            case ValueKind.Type:
                return CorLibrary.GetSpecialType(SpecialType.Type);
            case ValueKind.Struct:
                return result.@struct.type;
            case ValueKind.HeapPtr:
                return _context.heap[result.ptr].type;
            case ValueKind.Ref:
                var innerType = GetResultType(result.loc[result.ptr]);
                return new PointerTypeSymbol(new TypeWithAnnotations(innerType));
            case ValueKind.MethodGroup:
            case ValueKind.Null:
            default:
                return null;
        }
    }

    private EvaluatorValue GetOrCreateStaticType(NamedTypeSymbol type, ValueWrapper<bool> abort) {
        if (_context.TryGetStaticType(type, out var value))
            return value;

        var staticConstructor = type.staticConstructors.SingleOrDefault();

        if (staticConstructor is not null) {
            if (type.IsStructType()) {
                var structValue = CreateStruct(type);
                _context.AddStaticType(type, structValue);
                return structValue;
            }

            if (type.IsEnumType()) {
                var enumValue = CreateEnum(type);
                _context.AddStaticType(type, enumValue);
                return enumValue;
            }

            var ptr = CreateObject(type);
            _context.AddStaticType(type, ptr);
            InvokeMethod(staticConstructor, ptr, [], abort);
            return ptr;
        }

        return EvaluatorValue.None;
    }
    private EvaluatorValue CreateObject(NamedTypeSymbol type) {
        var heapObject = CreateHeapObject(type);
        var index = _context.heap.Allocate(heapObject, _stack, _context);
        return EvaluatorValue.HeapPtr(index);
    }

    private EvaluatorValue CreateEnum(NamedTypeSymbol type) {
        return EvaluatorValue.Struct(CreateHeapObject(type));
    }

    private EvaluatorValue CreateStruct(NamedTypeSymbol type) {
        return EvaluatorValue.Struct(CreateHeapObject(type));
    }

    private HeapObject CreateHeapObject(NamedTypeSymbol type) {
        if (!_program.TryGetTypeLayoutIncludingParents(type, out var layout)) {
            _program.TryGetTypeLayoutIncludingParents(type, out _);
            throw new BelteInternalException($"Failed to get type layout ({type}).");
        }

        var fields = layout.LocalsInOrder();
        var heapObject = new HeapObject(type, fields.Length);

        foreach (var field in fields) {
            var fieldType = field.type;

            if (type.templateSubstitution is not null)
                fieldType = type.templateSubstitution.SubstituteType(field.type).type.type;

            heapObject.fields[field.slot] = GetDefaultValue(
                fieldType,
                (field.symbol as FieldSymbol)?.constantValue
            );
        }

        if (type.arity > 0) {
            for (var i = 0; i < type.arity; i++) {
                var parameter = type.templateParameters[i];
                var argument = type.templateArguments[i];
                var arg = layout.GetLocal(parameter);

                if (argument.isType) {
                    heapObject.fields[arg.slot] = EvaluatorValue.Type(argument.type.type);
                } else {
                    heapObject.fields[arg.slot] = EvaluatorValue.Literal(
                        argument.constant.value,
                        argument.constant.specialType
                    );
                }
            }
        }

        return heapObject;
    }

    private EvaluatorValue GetHeapFieldSlotOrStructFieldSlot(EvaluatorValue ptr, int slot) {
        if (ptr.kind == ValueKind.Ref)
            return GetHeapFieldSlotOrStructFieldSlot(ptr.loc[ptr.ptr], slot);
        else if (ptr.kind == ValueKind.Struct)
            return ptr.@struct.fields[slot];
        else if (ptr.kind == ValueKind.HeapPtr)
            return _context.heap[ptr.ptr].fields[slot];
        else
            throw ExceptionUtilities.UnexpectedValue(ptr.kind);
    }

    private EvaluatorValue GetHeapFieldSlotOrStructFieldSlotRef(EvaluatorValue ptr, int slot) {
        if (ptr.kind == ValueKind.Ref)
            return GetHeapFieldSlotOrStructFieldSlotRef(ptr.loc[ptr.ptr], slot);
        else if (ptr.kind == ValueKind.Struct)
            return EvaluatorValue.Ref(ptr.@struct.fields, slot);
        else if (ptr.kind == ValueKind.HeapPtr)
            return EvaluatorValue.Ref(_context.heap[ptr.ptr].fields, slot);
        else
            throw ExceptionUtilities.UnexpectedValue(ptr.kind);
    }

    private void IndirectStore(TextLocation location, EvaluatorValue lhs, EvaluatorValue value) {
        if (lhs.kind == ValueKind.Null)
            throw new BelteNullReferenceException(location);

        if (lhs.kind != ValueKind.Ref)
            throw ExceptionUtilities.UnexpectedValue(lhs.kind);

        lhs.loc[lhs.ptr] = value;
    }

    #region Statements

    private EvaluatorValue EvaluateStatement(
        MethodSymbol method,
        BoundBlockStatement block,
        ValueWrapper<bool> abort,
        out bool returned) {
        _hasValue = false;
        returned = false;

        try {
            if (block.statements.Length == 0)
                return _lastValue;

            var labelToIndex = new Dictionary<LabelSymbol, int>();
            var statements = block.statements.Select(
                s => (s is BoundSequencePoint p && p.statement is not null)
                    ? p.statement
                    : (s is BoundSequencePointWithLocation p2 && p2.statement is not null)
                        ? p2.statement
                        : s
                ).Where(s => s.kind is not BoundKind.SequencePoint and not BoundKind.SequencePointWithLocation)
                .ToArray();

            for (var i = 0; i < statements.Length; i++) {
                if (statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;

            while (index < statements.Length) {
                if (abort)
                    throw new BelteThreadException();

                var s = statements[index];

                switch (s.kind) {
                    case BoundKind.NopStatement:
                        _lastValue = EvaluatorValue.None;
                        index++;
                        break;
                    case BoundKind.TryStatement:
                        var node = (BoundTryStatement)s;
                        var previousInsideTry = _insideTry;
                        _insideTry = true;

                        try {
                            _lastValue = EvaluateStatement(
                                method,
                                (BoundBlockStatement)node.body,
                                abort,
                                out returned
                            );
                        } catch (BelteException) {
                            if (node.catchBody is null)
                                throw;

                            _lastValue = EvaluateStatement(
                                method,
                                (BoundBlockStatement)node.catchBody,
                                abort,
                                out returned
                            );
                        } finally {
                            if (node.finallyBody is not null) {
                                var previousHasValue = _hasValue;
                                var previousLastValue = _lastValue;

                                EvaluateStatement(
                                    method,
                                    (BoundBlockStatement)node.finallyBody,
                                    abort,
                                    out returned
                                );

                                _hasValue = previousHasValue;
                                _lastValue = previousLastValue;
                            }

                            _insideTry = previousInsideTry;
                        }

                        index++;

                        if (returned)
                            return _lastValue;

                        break;
                    case BoundKind.ExpressionStatement:
                        _lastValue = EvaluatorValue.None;
                        EvaluateExpressionStatement((BoundExpressionStatement)s, abort);
                        index++;
                        break;
                    case BoundKind.LabelStatement:
                        _lastValue = EvaluatorValue.None;
                        index++;
                        break;
                    case BoundKind.GotoStatement:
                        _lastValue = EvaluatorValue.None;
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.label];
                        break;
                    case BoundKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = EvaluateExpression(cgs.condition, true, abort);

                        if (condition.kind == ValueKind.Null)
                            throw new BelteNullConditionException(cgs.condition.syntax.location);

                        if (condition.@bool == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        _lastValue = EvaluatorValue.None;
                        break;
                    case BoundKind.ReturnStatement: {
                            _hasValue = true;
                            returned = true;

                            if (method.returnsVoid) {
                                if (_lastValue.Equals(EvaluatorValue.None) || !_isScript)
                                    _hasValue = false;

                                return _lastValue;
                            }

                            var returnStatement = (BoundReturnStatement)s;
                            var expression = returnStatement.expression;

                            if (returnStatement.refKind == RefKind.None) {
                                _lastValue = EvaluateExpression(expression, true, abort);
                            } else {
                                _lastValue = EvaluateAddress(
                                    expression,
                                    method.refKind == RefKind.RefConst ? AddressKind.ReadOnlyStrict : AddressKind.Writeable,
                                    abort
                                );
                            }
                        }

                        return _lastValue;
                    case BoundKind.InlineILStatement:
                        throw new BelteEvaluatorException("Inline IL is not supported in the Evaluator.", ((InlineILStatementSyntax)s.syntax).keyword.location);
                    case BoundKind.SwitchDispatch: {
                            // TODO This is currently just a linear scan of the cases which is the slowest approach
                            // This is a lot of the time faster than computing a more optimal bucket strategy and only is worse with large switches
                            _lastValue = EvaluatorValue.None;
                            var dispatch = (BoundSwitchDispatch)s;
                            var expression = EvaluateExpression(dispatch.expression, true, abort);

                            index = labelToIndex[dispatch.defaultLabel];

                            foreach (var (value, label) in dispatch.cases) {
                                var op = RelationalOperatorType(
                                    dispatch.expression.StrippedType().EnumUnderlyingTypeOrSelf().StrippedType()
                                );

                                var comparison = EvaluateEqualityOperator(
                                    false,
                                    true,
                                    expression,
                                    EvaluatorValue.Literal(value.value, value.specialType),
                                    op
                                );

                                if (comparison.@bool) {
                                    index = labelToIndex[label];
                                    break;
                                }
                            }
                        }

                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(s.kind);
                }
            }

            return _lastValue;
        } catch (Exception e) {
            if (abort)
                return EvaluatorValue.None;

            if (_insideTry || _insideExpressionEvaluation)
                throw;

            exceptions.Add(e);
            lastOutputWasPrint = false;
            _hasValue = false;

            return EvaluatorValue.None;
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement node, ValueWrapper<bool> abort) {
        var expression = node.expression;
        var value = EvaluateExpression(expression, _isScript, abort);

        if (expression.syntax.kind != SyntaxKind.LocalDeclarationStatement)
            _lastValue = value;
    }

    #endregion

    #region Expressions

    private EvaluatorValue EvaluateExpression(BoundExpression node, bool used, ValueWrapper<bool> abort) {
        if (abort.Value)
            throw new BelteThreadException();

        if (node.constantValue is not null)
            return EvaluatorValue.Literal(node.constantValue.value, node.constantValue.specialType);

        return node.kind switch {
            BoundKind.DefaultExpression => EvaluateDefaultExpression((BoundDefaultExpression)node, used),
            BoundKind.ThisExpression => EvaluateThisExpression((BoundThisExpression)node),
            BoundKind.BaseExpression => EvaluateBaseExpression((BoundBaseExpression)node),
            BoundKind.DataContainerExpression => EvaluateDataContainerExpression((BoundDataContainerExpression)node, used),
            BoundKind.StackSlotExpression => EvaluateStackSlotExpression((BoundStackSlotExpression)node, used),
            BoundKind.FieldSlotExpression => EvaluateFieldSlotExpression((BoundFieldSlotExpression)node, used, abort),
            BoundKind.CastExpression => EvaluateCastExpression((BoundCastExpression)node, used, abort),
            BoundKind.AssignmentOperator => EvaluateAssignmentOperator((BoundAssignmentOperator)node, used ? UseKind.UsedAsValue : UseKind.Unused, abort),
            BoundKind.UnaryOperator => EvaluateUnaryOperator((BoundUnaryOperator)node, used, abort),
            BoundKind.BinaryOperator => EvaluateBinaryOperator((BoundBinaryOperator)node, used, abort),
            BoundKind.AsOperator => EvaluateAsOperator((BoundAsOperator)node, used, abort),
            BoundKind.IsOperator => EvaluateIsOperator((BoundIsOperator)node, used, abort),
            BoundKind.AddressOfOperator => EvaluateAddressOfOperator((BoundAddressOfOperator)node, used, abort),
            BoundKind.PointerIndirectionOperator => EvaluatePointerIndirectionOperator((BoundPointerIndirectionOperator)node, used, abort),
            BoundKind.ConditionalOperator => EvaluateConditionalOperator((BoundConditionalOperator)node, used, abort),
            BoundKind.NullAssertOperator => EvaluateNullAssertOperator((BoundNullAssertOperator)node, used, abort),
            BoundKind.CallExpression => EvaluateCallExpression((BoundCallExpression)node, used ? UseKind.UsedAsValue : UseKind.Unused, abort),
            BoundKind.ObjectCreationExpression => EvaluateObjectCreationExpression((BoundObjectCreationExpression)node, used, abort),
            BoundKind.ArrayCreationExpression => EvaluateArrayCreationExpression((BoundArrayCreationExpression)node, abort),
            BoundKind.ArrayAccessExpression => EvaluateArrayAccessExpression((BoundArrayAccessExpression)node, used, abort),
            BoundKind.IndexerAccessExpression => EvaluateIndexerAccessExpression((BoundIndexerAccessExpression)node, used, abort),
            BoundKind.TypeExpression => EvaluateTypeExpression((BoundTypeExpression)node),
            BoundKind.TypeOfExpression => EvaluateTypeOfExpression((BoundTypeOfExpression)node, used),
            BoundKind.MethodGroup => EvaluateMethodGroup((BoundMethodGroup)node),
            BoundKind.ThrowExpression => EvaluateThrowExpression((BoundThrowExpression)node, abort),
            BoundKind.CompileTimeExpression => EvaluateCompileTimeExpression((BoundCompileTimeExpression)node, used, abort),
            BoundKind.UnconvertedNullptrExpression => EvaluatorValue.Null,
            BoundKind.ConvertedStackAllocExpression => throw new BelteEvaluatorException("Stackalloc is not supported in the Evaluator.", node.syntax.location),
            BoundKind.FunctionPointerLoad => throw new BelteEvaluatorException("Function pointers are not supported in the Evaluator.", node.syntax.location),
            BoundKind.FunctionLoad => EvaluateFunctionLoad((BoundFunctionLoad)node, used),
            _ => throw ExceptionUtilities.UnexpectedValue(node.kind),
        };
    }

    private EvaluatorValue EvaluateFunctionLoad(BoundFunctionLoad node, bool used) {
        if (used)
            return new EvaluatorValue() { kind = ValueKind.MethodGroup, data = node.targetMethod };

        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateCompileTimeExpression(BoundCompileTimeExpression node, bool used, ValueWrapper<bool> abort) {
        if (_insideExpressionEvaluation)
            return EvaluateExpression(node.expression, used, abort);

        throw ExceptionUtilities.Unreachable();
    }

    private EvaluatorValue EvaluateDefaultExpression(BoundDefaultExpression node, bool used) {
        if (!used)
            return EvaluatorValue.None;

        return EvaluateDefaultExpression(node.type);
    }

    private EvaluatorValue EvaluateDefaultExpression(TypeSymbol type) {
        if (type.IsNullableType())
            return EvaluatorValue.Null;

        if (!type.IsTemplateParameter()) {
            var constantValue = type.IsVerifierValue() ? LiteralUtilities.GetDefaultValue(type.specialType) : null;

            if (constantValue is not null)
                return EvaluatorValue.Literal(constantValue, type.specialType);
        }

        if (type.IsPointerOrFunctionPointer() || type.specialType is SpecialType.UIntPtr or SpecialType.IntPtr) {
            return new EvaluatorValue() { kind = ValueKind.Ref, uint64 = 0 };
        } else if (type.IsTemplateParameter()) {
            var targetType = SubstituteTemplateParameter((TemplateParameterSymbol)type);
            return EvaluateDefaultExpression(targetType);
        } else {
            return CreateObject((NamedTypeSymbol)type);
        }
    }

    private EvaluatorValue EvaluateThisExpression(BoundThisExpression node) {
        var value = _stack.Peek().values[0];

        if (IsValueType(node.type))
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateBaseExpression(BoundBaseExpression node) {
        var value = _stack.Peek().values[0];

        if (IsValueType(node.type))
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateTypeOfExpression(BoundTypeOfExpression node, bool used) {
        if (!used)
            return EvaluatorValue.None;

        var type = node.sourceType.type;

        if (type.StrippedType() is TemplateParameterSymbol t) {
            var substituted = SubstituteTemplateParameter(t);

            if (type.IsNullableType())
                substituted = CorLibrary.GetOrCreateNullableType(substituted);

            type = substituted;
        }

        return EvaluatorValue.Type(type);
    }

    private TypeSymbol SubstituteTemplateParameter(TemplateParameterSymbol templateParameter) {
        if (templateParameter.templateParameterKind == TemplateParameterKind.Method)
            return (TypeSymbol)_stack.Peek().values[templateParameter.ordinal + 1].type;

        var thisParameter = _stack.Peek().values[0];
        var heapObject = _context.heap[thisParameter.ptr];

        if (!_program.TryGetTypeLayoutIncludingParents((NamedTypeSymbol)heapObject.type, out var layout))
            throw new BelteInternalException($"Failed to get type layout ({heapObject.type}).");

        var field = layout.GetLocal(templateParameter);
        return (TypeSymbol)heapObject.fields[field.slot].type;
    }

    private EvaluatorValue EvaluateMethodGroup(BoundMethodGroup node) {
        return EvaluatorValue.MethodGroup(node);
    }

    private EvaluatorValue EvaluateThrowExpression(BoundThrowExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node.expression, true, abort);
        var location = node.syntax.location;

        if (value.kind == ValueKind.Null)
            throw new BelteNullReferenceException(location);

        var exception = _context.heap[value.ptr];
        // The message will always be the first field as Object has no fields
        var message = exception.fields[0].@string;

        throw new BelteEvaluatorException(message, location);
    }

    private EvaluatorValue EvaluateTypeExpression(BoundTypeExpression node) {
        if (node.type.kind == SymbolKind.TemplateParameter) {
            var template = (TemplateParameterSymbol)node.type;

            if (template.templateParameterKind == TemplateParameterKind.Method)
                return _stack.Peek().values[template.ordinal + 1];

            var thisParameter = _stack.Peek().values[0];
            var heapObject = _context.heap[thisParameter.ptr];

            if (!_program.TryGetTypeLayoutIncludingParents((NamedTypeSymbol)heapObject.type, out var layout))
                throw new BelteInternalException($"Failed to get type layout ({heapObject.type}).");

            var field = layout.GetLocal(node.type);
            return heapObject.fields[field.slot];
        }

        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateDataContainerExpression(BoundDataContainerExpression node, bool used) {
        var global = node.dataContainer;
        var isRefLocal = global.refKind != RefKind.None;

        if (used || isRefLocal) {
            if (!_context.TryGetGlobal(node.dataContainer, out var value))
                throw new BelteInternalException($"Attempted to find global '{node.dataContainer.name}' that doesn't exist.");

            if (isRefLocal)
                return value.loc[value.ptr];

            return value;
        }

        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateStackSlotExpression(BoundStackSlotExpression node, bool used) {
        var local = node.symbol;

        if (local.kind == SymbolKind.Parameter && !used)
            return EvaluatorValue.None;

        var isRefLocal = local.GetRefKind() != RefKind.None;

        if (used || isRefLocal) {
            var value = _stack.Peek().values[node.slot];

            if (isRefLocal)
                return value.loc[value.ptr];

            return value;
        }

        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateFieldSlotExpression(BoundFieldSlotExpression node, bool used, ValueWrapper<bool> abort) {
        var field = node.field;

        if (!used) {
            if (!field.isStatic && node.receiver.type.IsVerifierValue() && field.refKind == RefKind.None)
                return EvaluateExpression(node.receiver, false, abort);
        }

        var value = EvaluateFieldNoIndirection(node, used, abort);

        if (field.refKind != RefKind.None)
            return value.loc[value.ptr];

        if (field.containingType.isUnionStruct || field.isAnonymousUnionMember)
            value.kind = ValueKindExtensions.FromSpecialType(field.type.StrippedType().specialType, value.kind);

        return value;
    }

    private EvaluatorValue EvaluateFieldNoIndirection(BoundFieldSlotExpression node, bool used, ValueWrapper<bool> abort) {
        var field = node.field;

        if (field.isStatic) {
            var ptr = GetOrCreateStaticType(field.containingType.originalDefinition, abort);
            return GetHeapFieldSlotOrStructFieldSlot(ptr, node.slot);
        } else {
            var receiver = node.receiver;
            var fieldType = field.type;

            if (IsValueType(fieldType) && (object)fieldType == receiver.type) {
                return EvaluateExpression(receiver, used, abort);
            } else {
                var receiverValue = EvaluateFieldLoadReceiver(receiver, abort);
                return GetHeapFieldSlotOrStructFieldSlot(receiverValue, node.slot);
            }
        }
    }

    private EvaluatorValue EvaluateFieldLoadReceiver(BoundExpression receiver, ValueWrapper<bool> abort) {
        if (FieldLoadMustUseRef(receiver) || FieldLoadPrefersRef(receiver)) {
            return EvaluateFieldLoadReceiverAddress(receiver, abort, out var expr)
                ? expr
                : EvaluateReceiverRef(receiver, AddressKind.ReadOnly, abort);
        }

        return EvaluateExpression(receiver, true, abort);
    }

    private bool EvaluateFieldLoadReceiverAddress(
        BoundExpression receiver,
        ValueWrapper<bool> abort,
        out EvaluatorValue expr) {
        if (receiver is null || !IsValueType(receiver.Type())) {
            expr = EvaluatorValue.None;
            return false;
        } else if (receiver.kind == BoundKind.CastExpression) {
            var conversion = (BoundCastExpression)receiver;

            if (conversion.conversion.kind == ConversionKind.AnyUnboxing) {
                expr = EvaluateExpression(conversion.operand, true, abort);
                return true;
            }
        } else if (receiver.kind == BoundKind.FieldSlotExpression) {
            var fieldSlot = (BoundFieldSlotExpression)receiver;
            var field = fieldSlot.field;

            if (!field.isStatic && EvaluateFieldLoadReceiverAddress(fieldSlot.receiver, abort, out var nestedExpr)) {
                expr = GetHeapFieldSlotOrStructFieldSlotRef(nestedExpr, fieldSlot.slot);
                return true;
            }
        }

        expr = EvaluatorValue.None;
        return false;
    }

    private bool FieldLoadPrefersRef(BoundExpression receiver) {
        if (!receiver.Type().IsVerifierValue())
            return true;

        if (receiver.kind == BoundKind.CastExpression &&
            ((BoundCastExpression)receiver).conversion.kind == ConversionKind.AnyUnboxing) {
            return true;
        }

        if (!HasHome(receiver, AddressKind.ReadOnly))
            return false;

        switch (receiver.kind) {
            case BoundKind.ParameterExpression:
                return ((BoundParameterExpression)receiver).parameter.refKind != RefKind.None;
            case BoundKind.DataContainerExpression:
                return ((BoundDataContainerExpression)receiver).dataContainer.refKind != RefKind.None;
            case BoundKind.FieldAccessExpression:
                var fieldAccess = (BoundFieldAccessExpression)receiver;
                var field = fieldAccess.field;

                if (field.isStatic || field.refKind != RefKind.None)
                    return true;

                return FieldLoadPrefersRef(fieldAccess.receiver);
        }

        return true;
    }

    private EvaluatorValue EvaluateCastExpression(BoundCastExpression node, bool used, ValueWrapper<bool> abort) {
        if (!used && !node.ConversionHasSideEffects()) {
            EvaluateExpression(node.operand, false, abort);
            return EvaluatorValue.None;
        }

        if (node.conversion.kind == ConversionKind.ImplicitNullToPointer)
            return EvaluatorValue.Ref(_stack.Peek().values, 0);

        var value = EvaluateExpression(node.operand, true, abort);

        if (IsReferenceType(node.operand.Type())) {
            if (node.Type().specialType == SpecialType.Nullable)
                return value;
        }

        var isCastable = node.operand.Type().specialType == SpecialType.String && node.Type().IsPrimitiveType() ||
            node.Type().specialType == SpecialType.String && node.operand.Type().IsPrimitiveType();

        var involvesRefTypes = !isCastable && (node.operand.Type().IsVerifierReference() ||
            (node.Type().IsVerifierReference() && node.Type().specialType != SpecialType.String));

        switch (node.conversion.kind) {
            case ConversionKind.Identity:
            case ConversionKind.Implicit when involvesRefTypes:
            case ConversionKind.ImplicitReference:
            case ConversionKind.AnyBoxing:
            case ConversionKind.Explicit when involvesRefTypes:
            case ConversionKind.ExplicitReference:
            case ConversionKind.AnyUnboxing:
                return value;
            case ConversionKind.ImplicitEnum:
            case ConversionKind.ExplicitEnum:
            case ConversionKind.Implicit:
            case ConversionKind.Explicit:
            case ConversionKind.ImplicitNumeric:
            case ConversionKind.ExplicitNumeric:
            case ConversionKind.ExplicitPointerToInteger:
            case ConversionKind.ExplicitIntegerToPointer:
                return EvaluateConvertCallOrNumericConversion(node, value);
            case ConversionKind.ExplicitPointerToPointer:
            case ConversionKind.ImplicitPointerToVoid:
                return value;
            default:
                throw ExceptionUtilities.UnexpectedValue(node.conversion.kind);
        }
    }

    private EvaluatorValue EvaluateConvertCallOrNumericConversion(BoundCastExpression node, EvaluatorValue value) {
        if (node.operand.StrippedType().IsEnumType()) {
            if (node.type.specialType == SpecialType.String) {
                var type = node.operand.StrippedType();
                var underlyingType = type.GetEnumUnderlyingType().StrippedType();
                var op = RelationalOperatorType(underlyingType);

                foreach (var member in type.GetMembers()) {
                    if (member is FieldSymbol f && f.isStatic) {
                        if (EvaluateEqualityOperator(
                            false,
                            true,
                            value,
                            EvaluatorValue.Literal(f.constantValue, underlyingType.specialType),
                            op).@bool) {
                            return EvaluatorValue.Literal(f.name);
                        }
                    }
                }
            }
        }

        var fromTypeSymbol = node.operand.Type();

        if (fromTypeSymbol.IsEnumType())
            fromTypeSymbol = ((NamedTypeSymbol)fromTypeSymbol).enumUnderlyingType;

        var toTypeSymbol = node.Type();

        if (toTypeSymbol.IsEnumType())
            toTypeSymbol = ((NamedTypeSymbol)toTypeSymbol).enumUnderlyingType;

        var fromType = NormalizeNumericType(fromTypeSymbol.specialType);
        var toType = NormalizeNumericType(toTypeSymbol.specialType);

        switch (toType) {
            case SpecialType.Bool:
                value.kind = ValueKind.Bool;
                value.@bool = fromType switch {
                    SpecialType.String => Convert.ToBoolean(value.@string),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.String:
                value.kind = ValueKind.String;
                value.@string = fromType switch {
                    SpecialType.Bool => Convert.ToString(value.@bool),
                    SpecialType.Char => Convert.ToString(value.@char),
                    SpecialType.Int8 => Convert.ToString(value.int8),
                    SpecialType.Int16 => Convert.ToString(value.int16),
                    SpecialType.Int32 => Convert.ToString(value.int32),
                    SpecialType.Int64 => Convert.ToString(value.int64),
                    SpecialType.UInt8 => Convert.ToString(value.uint8),
                    SpecialType.UInt16 => Convert.ToString(value.uint16),
                    SpecialType.UInt32 => Convert.ToString(value.uint32),
                    SpecialType.UInt64 => Convert.ToString(value.uint64),
                    SpecialType.Float32 => Convert.ToString(value.single),
                    SpecialType.Float64 => Convert.ToString(value.@double),
                    SpecialType.Pointer => Convert.ToString(value.ptr),
                    SpecialType.String => value.@string,
                    SpecialType.FunctionPointer => Convert.ToString(value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Char:
                value.kind = ValueKind.Char;
                value.@char = fromType switch {
                    SpecialType.Int8 => unchecked((char)value.int8),
                    SpecialType.Int16 => unchecked((char)value.int16),
                    SpecialType.Int32 => unchecked((char)value.int32),
                    SpecialType.Int64 => unchecked((char)value.int64),
                    SpecialType.UInt8 => (char)value.uint8,
                    SpecialType.UInt16 => (char)value.uint16,
                    SpecialType.Char => value.@char,
                    SpecialType.UInt32 => unchecked((char)value.uint32),
                    SpecialType.UInt64 => unchecked((char)value.uint64),
                    SpecialType.Float32 => unchecked((char)value.single),
                    SpecialType.Float64 => unchecked((char)value.@double),
                    SpecialType.Pointer => unchecked((char)value.ptr),
                    SpecialType.FunctionPointer => unchecked((char)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Int8:
                value.kind = ValueKind.Int8;
                value.int8 = fromType switch {
                    SpecialType.String => Convert.ToSByte(value.@string),
                    SpecialType.Char => unchecked((sbyte)value.@char),
                    SpecialType.Int8 => value.int8,
                    SpecialType.Int16 => unchecked((sbyte)value.int16),
                    SpecialType.Int32 => unchecked((sbyte)value.int32),
                    SpecialType.Int64 => unchecked((sbyte)value.int64),
                    SpecialType.UInt8 => unchecked((sbyte)value.uint8),
                    SpecialType.UInt16 => unchecked((sbyte)value.uint16),
                    SpecialType.UInt32 => unchecked((sbyte)value.uint32),
                    SpecialType.UInt64 => unchecked((sbyte)value.uint64),
                    SpecialType.Float32 => unchecked((sbyte)value.@single),
                    SpecialType.Float64 => unchecked((sbyte)value.@double),
                    SpecialType.Pointer => unchecked((sbyte)value.ptr),
                    SpecialType.FunctionPointer => unchecked((sbyte)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Int16:
                value.kind = ValueKind.Int16;
                value.int16 = fromType switch {
                    SpecialType.String => Convert.ToInt16(value.@string),
                    SpecialType.Char => unchecked((short)value.@char),
                    SpecialType.Int8 => unchecked((short)value.int8),
                    SpecialType.Int16 => value.int16,
                    SpecialType.Int32 => unchecked((short)value.int32),
                    SpecialType.Int64 => unchecked((short)value.int64),
                    SpecialType.UInt8 => (short)value.uint8,
                    SpecialType.UInt16 => unchecked((short)value.uint16),
                    SpecialType.UInt32 => unchecked((short)value.uint32),
                    SpecialType.UInt64 => unchecked((short)value.uint64),
                    SpecialType.Float32 => unchecked((short)value.single),
                    SpecialType.Float64 => unchecked((short)value.@double),
                    SpecialType.Pointer => unchecked((short)value.ptr),
                    SpecialType.FunctionPointer => unchecked((short)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Int32:
                value.kind = ValueKind.Int32;
                value.int32 = fromType switch {
                    SpecialType.String => Convert.ToInt32(value.@string),
                    SpecialType.Char => unchecked((int)value.@char),
                    SpecialType.Int8 => (int)value.int8,
                    SpecialType.Int16 => (int)value.int16,
                    SpecialType.Int32 => value.int32,
                    SpecialType.Int64 => unchecked((int)value.int32),
                    SpecialType.UInt8 => (int)value.uint8,
                    SpecialType.UInt16 => (int)value.uint16,
                    SpecialType.UInt32 => unchecked((int)value.uint32),
                    SpecialType.UInt64 => unchecked((int)value.uint64),
                    SpecialType.Float32 => unchecked((int)value.@single),
                    SpecialType.Float64 => unchecked((int)value.@double),
                    SpecialType.Pointer => value.ptr,
                    SpecialType.FunctionPointer => value.ptr,
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Int64:
                value.kind = ValueKind.Int64;
                value.int64 = fromType switch {
                    SpecialType.String => Convert.ToInt64(value.@string),
                    SpecialType.Char => (long)value.@char,
                    SpecialType.Int8 => (long)value.int8,
                    SpecialType.Int16 => (long)value.int16,
                    SpecialType.Int32 => (long)value.int32,
                    SpecialType.UInt8 => (long)value.uint8,
                    SpecialType.UInt16 => (long)value.uint16,
                    SpecialType.UInt32 => (long)value.uint32,
                    SpecialType.Int64 => value.int64,
                    SpecialType.UInt64 => unchecked((long)value.uint64),
                    SpecialType.Float32 => unchecked((long)value.@single),
                    SpecialType.Float64 => unchecked((long)value.@double),
                    SpecialType.Pointer => (long)value.ptr,
                    SpecialType.FunctionPointer => (long)value.ptr,
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.UInt8:
                value.kind = ValueKind.UInt8;
                value.uint8 = fromType switch {
                    SpecialType.String => Convert.ToByte(value.@string),
                    SpecialType.Char => unchecked((byte)value.@char),
                    SpecialType.Int8 => unchecked((byte)value.int8),
                    SpecialType.UInt8 => value.uint8,
                    SpecialType.Int16 => unchecked((byte)value.int16),
                    SpecialType.Int32 => unchecked((byte)value.int32),
                    SpecialType.Int64 => unchecked((byte)value.int64),
                    SpecialType.UInt16 => unchecked((byte)value.uint16),
                    SpecialType.UInt32 => unchecked((byte)value.uint32),
                    SpecialType.UInt64 => unchecked((byte)value.uint64),
                    SpecialType.Float32 => unchecked((byte)value.@single),
                    SpecialType.Float64 => unchecked((byte)value.@double),
                    SpecialType.Pointer => unchecked((byte)value.ptr),
                    SpecialType.FunctionPointer => unchecked((byte)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.UInt16:
                value.kind = ValueKind.UInt16;
                value.uint16 = fromType switch {
                    SpecialType.String => Convert.ToUInt16(value.@string),
                    SpecialType.Char => (ushort)value.@char,
                    SpecialType.Int8 => unchecked((ushort)value.int8),
                    SpecialType.Int16 => unchecked((ushort)value.int16),
                    SpecialType.UInt16 => value.uint16,
                    SpecialType.Int32 => unchecked((ushort)value.int32),
                    SpecialType.Int64 => unchecked((ushort)value.int64),
                    SpecialType.UInt8 => (ushort)value.uint8,
                    SpecialType.UInt32 => unchecked((ushort)value.uint32),
                    SpecialType.UInt64 => unchecked((ushort)value.uint64),
                    SpecialType.Float32 => unchecked((ushort)value.single),
                    SpecialType.Float64 => unchecked((ushort)value.@double),
                    SpecialType.Pointer => unchecked((ushort)value.ptr),
                    SpecialType.FunctionPointer => unchecked((ushort)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.UInt32:
                value.kind = ValueKind.UInt32;
                value.uint32 = fromType switch {
                    SpecialType.String => Convert.ToUInt32(value.@string),
                    SpecialType.Char => (uint)value.@char,
                    SpecialType.Int8 => unchecked((uint)value.int8),
                    SpecialType.Int16 => unchecked((uint)value.int16),
                    SpecialType.Int32 => unchecked((uint)value.int32),
                    SpecialType.UInt32 => value.uint32,
                    SpecialType.Int64 => unchecked((uint)value.int64),
                    SpecialType.UInt8 => (uint)value.uint8,
                    SpecialType.UInt16 => (uint)value.uint16,
                    SpecialType.UInt64 => unchecked((uint)value.uint64),
                    SpecialType.Float32 => unchecked((uint)value.@single),
                    SpecialType.Float64 => unchecked((uint)value.@double),
                    SpecialType.Pointer => unchecked((uint)value.ptr),
                    SpecialType.FunctionPointer => unchecked((uint)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.UInt64:
                value.kind = ValueKind.UInt64;
                value.uint64 = fromType switch {
                    SpecialType.String => Convert.ToUInt64(value.@string),
                    SpecialType.Char => (ulong)value.@char,
                    SpecialType.Int8 => unchecked((ulong)value.int8),
                    SpecialType.Int16 => unchecked((ulong)value.int16),
                    SpecialType.Int32 => unchecked((ulong)value.int32),
                    SpecialType.Int64 => unchecked((ulong)value.int64),
                    SpecialType.UInt64 => value.uint64,
                    SpecialType.UInt8 => (ulong)value.uint8,
                    SpecialType.UInt16 => (ulong)value.uint16,
                    SpecialType.UInt32 => (ulong)value.uint32,
                    SpecialType.Float32 => unchecked((ulong)value.@single),
                    SpecialType.Float64 => unchecked((ulong)value.@double),
                    SpecialType.Pointer => unchecked((ulong)value.ptr),
                    SpecialType.FunctionPointer => unchecked((ulong)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Float32:
                value.kind = ValueKind.Float32;
                value.single = fromType switch {
                    SpecialType.String => Convert.ToSingle(value.@string),
                    SpecialType.Char => (float)value.@char,
                    SpecialType.Int8 => (float)value.int8,
                    SpecialType.Int16 => (float)value.int16,
                    SpecialType.Int32 => unchecked((float)value.int32),
                    SpecialType.Int64 => unchecked((float)value.int64),
                    SpecialType.UInt8 => (float)value.uint8,
                    SpecialType.UInt16 => (float)value.uint16,
                    SpecialType.UInt32 => unchecked((float)value.uint32),
                    SpecialType.UInt64 => unchecked((float)value.uint64),
                    SpecialType.Float32 => value.single,
                    SpecialType.Float64 => unchecked((float)value.@double),
                    SpecialType.Pointer => unchecked((float)value.ptr),
                    SpecialType.FunctionPointer => unchecked((float)value.ptr),
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Float64:
                value.kind = ValueKind.Float64;
                value.@double = fromType switch {
                    SpecialType.String => Convert.ToDouble(value.@string),
                    SpecialType.Char => (double)value.@char,
                    SpecialType.Int8 => (double)value.int8,
                    SpecialType.Int16 => (double)value.int16,
                    SpecialType.Int32 => (double)value.int32,
                    SpecialType.Int64 => unchecked((double)value.int64),
                    SpecialType.UInt8 => (double)value.uint8,
                    SpecialType.UInt16 => (double)value.uint16,
                    SpecialType.UInt32 => (double)value.uint32,
                    SpecialType.UInt64 => unchecked((double)value.uint64),
                    SpecialType.Float32 => (double)value.single,
                    SpecialType.Float64 => value.@double,
                    SpecialType.Pointer => (double)value.ptr,
                    SpecialType.FunctionPointer => (double)value.ptr,
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            case SpecialType.Pointer:
            case SpecialType.FunctionPointer:
                value.kind = ValueKind.Ref;
                value.loc ??= _stack.Peek().values;
                value.ptr = fromType switch {
                    SpecialType.Char => (int)value.@char,
                    SpecialType.Int8 => (int)value.int8,
                    SpecialType.Int16 => (int)value.int16,
                    SpecialType.Int32 => value.int32,
                    SpecialType.Int64 => unchecked((int)value.int64),
                    SpecialType.UInt8 => (int)value.uint8,
                    SpecialType.UInt16 => (int)value.uint16,
                    SpecialType.UInt32 => unchecked((int)value.uint32),
                    SpecialType.UInt64 => unchecked((int)value.uint64),
                    SpecialType.Float32 => unchecked((int)value.single),
                    SpecialType.Pointer => value.ptr,
                    SpecialType.FunctionPointer => value.ptr,
                    _ => throw ExceptionUtilities.UnexpectedValue(fromType),
                };

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(toType);
        }

        return value;
    }

    private EvaluatorValue EvaluateObjectCreationExpression(
        BoundObjectCreationExpression node,
        bool used,
        ValueWrapper<bool> abort) {
        if (node.type.IsStructType()) {
            if (used)
                return CreateStruct((NamedTypeSymbol)node.type);

            return EvaluatorValue.None;
        }

        if (node.constructor.originalDefinition == CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_ctor))
            return EvaluateExpression(node.arguments[0], used, abort);

        var type = (NamedTypeSymbol)node.StrippedType();
        var ptr = CreateObject(type);

        var temp = AllocateTemp(type);
        _stack.Peek().values[temp.slot] = ptr;

        var method = node.constructor;
        var evaluatedArguments = EvaluateArguments(node.arguments, method.parameters, node.argumentRefKinds, abort);
        InvokeMethod(method, ptr, evaluatedArguments, abort);

        _stack.Peek().layout.FreeSlot(temp);

        return ptr;
    }

    private EvaluatorValue EvaluateArrayAccessExpression(BoundArrayAccessExpression node, bool used, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, true, abort);

        if (receiver.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.syntax.location);

        var index = (int)EvaluateExpression(node.index, true, abort).int64;

        if (index >= _context.heap[receiver.ptr].fields.Length)
            throw new BelteIndexOutOfRangeException(node.syntax.location);

        if (node.type.IsVerifierReference() || node.type.specialType is
            SpecialType.Int or SpecialType.Bool or SpecialType.Decimal || used) {
            return _context.heap[receiver.ptr].fields[index];
        } else {
            return EvaluatorValue.Ref(_context.heap[receiver.ptr].fields, index);
        }
    }

    private EvaluatorValue EvaluateIndexerAccessExpression(
        BoundIndexerAccessExpression node,
        bool used,
        ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, true, abort);

        if (receiver.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.syntax.location);

        var index = (int)EvaluateExpression(node.index, true, abort).int64;

        if (index >= receiver.@string.Length)
            throw new BelteIndexOutOfRangeException(node.syntax.location);

        if (used)
            return EvaluatorValue.Literal(receiver.@string[index]);
        else
            return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateArrayCreationExpression(
        BoundArrayCreationExpression node,
        ValueWrapper<bool> abort) {
        var sizes = node.sizes.Select(s => (int)EvaluateExpression(s, true, abort).int64);
        var heapObject = CreateArray((ArrayTypeSymbol)node.StrippedType(), sizes.ToArray(), 0);
        var index = _context.heap.Allocate(heapObject, _stack, _context);
        var ptr = EvaluatorValue.HeapPtr(index);

        var temp = AllocateTemp(node.Type());
        _stack.Peek().values[temp.slot] = ptr;

        if (node.initializer is BoundInitializerList initList)
            EvaluateInitializerList(heapObject, initList, abort);

        _stack.Peek().layout.FreeSlot(temp);

        return ptr;
    }

    private void EvaluateInitializerList(
        HeapObject array,
        BoundInitializerList node,
        ValueWrapper<bool> abort,
        int depth = 0) {
        var elements = array.fields;

        for (var i = 0; i < node.items.Length; i++) {
            var item = node.items[i];

            if (item is BoundInitializerList nested) {
                var child = _context.heap[elements[i].ptr];
                EvaluateInitializerList(child, nested, abort, depth + 1);
            } else {
                elements[i] = EvaluateExpression(item, true, abort);
            }
        }
    }

    private HeapObject CreateArray(ArrayTypeSymbol type, int[] sizes, int depth) {
        var length = sizes[depth];
        var elements = new EvaluatorValue[length];

        if (depth == sizes.Length - 1) {
            for (var i = 0; i < length; i++)
                elements[i] = GetDefaultValue(type.elementType, null);
        } else {
            for (var i = 0; i < length; i++) {
                var array = CreateArray(type, sizes, depth + 1);
                var index = _context.heap.Allocate(array, _stack, _context);
                elements[i] = EvaluatorValue.HeapPtr(index);
            }
        }

        return new HeapObject(type, elements);
    }

    private EvaluatorValue GetDefaultValue(TypeSymbol type, object constantValueForEnum) {
        if (type.IsStructType())
            return CreateStruct((NamedTypeSymbol)type);

        if (type is PointerTypeSymbol)
            return EvaluatorValue.Literal(value: 0);

        if (type.IsEnumType()) {
            return EvaluatorValue.Literal(
                constantValueForEnum,
                (type as NamedTypeSymbol).enumUnderlyingType.StrippedType().specialType
            );
        }

        return (!type.IsNullableType() && type.IsVerifierValue())
            ? EvaluatorValue.Literal(type.specialType)
            : EvaluatorValue.Null;
    }

    #endregion

    #region Operators

    private EvaluatorValue EvaluateConditionalOperator(BoundConditionalOperator node, bool used, ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(node.condition, true, abort);

        if (condition.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.condition.syntax.location);

        if (condition.@bool)
            return EvaluateExpression(node.trueExpression, used, abort);
        else
            return EvaluateExpression(node.falseExpression, used, abort);
    }

    private EvaluatorValue EvaluateNullAssertOperator(BoundNullAssertOperator node, bool used, ValueWrapper<bool> abort) {
        if (!node.throwIfNull)
            return EvaluateExpression(node.operand, used, abort);

        return NullAssertValue(node.operand, abort);
    }

    private EvaluatorValue NullAssertValue(BoundExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node, true, abort);

        if (value.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.syntax.location);

        return value;
    }

    private EvaluatorValue EvaluateIsOperator(BoundIsOperator node, bool used, ValueWrapper<bool> abort) {
        var operand = node.left;
        var value = EvaluateExpression(operand, used, abort);

        if (!used)
            return EvaluatorValue.None;

        if (node.right.IsLiteralNull())
            return EvaluatorValue.Literal(value.kind == ValueKind.Null == (!node.isNot));

        var targetType = node.right.StrippedType();
        var targetSpecialType = NormalizeNumericType(targetType.specialType);

        if (value.kind == ValueKind.Null) {
            value.@bool = node.isNot;
            value.kind = ValueKind.Bool;
            return value;
        }

        if (value.kind == ValueKind.Int8 && targetSpecialType == SpecialType.Int8 ||
            value.kind == ValueKind.Int16 && targetSpecialType == SpecialType.Int16 ||
            value.kind == ValueKind.Int32 && targetSpecialType == SpecialType.Int32 ||
            value.kind == ValueKind.Int64 && targetSpecialType == SpecialType.Int64 ||
            value.kind == ValueKind.UInt8 && targetSpecialType == SpecialType.UInt8 ||
            value.kind == ValueKind.UInt16 && targetSpecialType == SpecialType.UInt16 ||
            value.kind == ValueKind.UInt32 && targetSpecialType == SpecialType.UInt32 ||
            value.kind == ValueKind.UInt64 && targetSpecialType == SpecialType.UInt64 ||
            value.kind == ValueKind.Float32 && targetSpecialType == SpecialType.Float32 ||
            value.kind == ValueKind.Float64 && targetSpecialType == SpecialType.Float64 ||
            value.kind == ValueKind.Bool && targetSpecialType == SpecialType.Bool ||
            value.kind == ValueKind.String && targetSpecialType == SpecialType.String ||
            targetSpecialType == SpecialType.Any) {
            value.@bool = !node.isNot;
            value.kind = ValueKind.Bool;
            return value;
        }

        if (value.kind == ValueKind.HeapPtr) {
            var operandType = _context.heap[value.ptr].type.StrippedType();

            if (operandType.Equals(targetType) ||
                targetType is NamedTypeSymbol t && operandType.InheritsFromIgnoringConstruction(t)) {
                value.@bool = !node.isNot;
                value.kind = ValueKind.Bool;
                return value;
            }
        }

        if (value.kind == ValueKind.Struct) {
            var operandType = value.@struct.type.StrippedType();

            if (operandType.Equals(targetType) ||
                targetType is NamedTypeSymbol t && operandType.InheritsFromIgnoringConstruction(t)) {
                value.@bool = !node.isNot;
                value.kind = ValueKind.Bool;
                return value;
            }
        }

        value.@bool = node.isNot;
        value.kind = ValueKind.Bool;
        return value;
    }

    private EvaluatorValue EvaluateAsOperator(BoundAsOperator node, bool used, ValueWrapper<bool> abort) {
        var operand = node.left;
        var value = EvaluateExpression(operand, used, abort);

        if (!used)
            return EvaluatorValue.None;

        if (value.kind == ValueKind.Null)
            return EvaluatorValue.Null;

        var targetType = node.StrippedType();

        var targetSpecialType = targetType.specialType;

        if (value.kind == ValueKind.Int8 && targetSpecialType == SpecialType.Int8 ||
            value.kind == ValueKind.Int16 && targetSpecialType == SpecialType.Int16 ||
            value.kind == ValueKind.Int32 && targetSpecialType == SpecialType.Int32 ||
            value.kind == ValueKind.Int64 && targetSpecialType == SpecialType.Int64 ||
            value.kind == ValueKind.UInt8 && targetSpecialType == SpecialType.UInt8 ||
            value.kind == ValueKind.UInt16 && targetSpecialType == SpecialType.UInt16 ||
            value.kind == ValueKind.UInt32 && targetSpecialType == SpecialType.UInt32 ||
            value.kind == ValueKind.UInt64 && targetSpecialType == SpecialType.UInt64 ||
            value.kind == ValueKind.Float32 && targetSpecialType == SpecialType.Float32 ||
            value.kind == ValueKind.Float64 && targetSpecialType == SpecialType.Float64 ||
            value.kind == ValueKind.Bool && targetSpecialType == SpecialType.Bool ||
            value.kind == ValueKind.String && targetSpecialType == SpecialType.String ||
            targetSpecialType == SpecialType.Any) {
            return value;
        }

        if (value.kind == ValueKind.HeapPtr) {
            var operandType = _context.heap[value.ptr].type.StrippedType();

            if (operandType.Equals(targetType) ||
                targetType is NamedTypeSymbol t && operandType.InheritsFromIgnoringConstruction(t)) {
                return value;
            }
        }

        if (value.kind == ValueKind.Struct) {
            var operandType = value.@struct.type.StrippedType();

            if (operandType.Equals(targetType) ||
                targetType is NamedTypeSymbol t && operandType.InheritsFromIgnoringConstruction(t)) {
                return value;
            }
        }

        return EvaluatorValue.Null;
    }

    private EvaluatorValue EvaluateAddressOfOperator(BoundAddressOfOperator node, bool used, ValueWrapper<bool> abort) {
        var address = EvaluateAddress(node.operand, AddressKind.ReadOnlyStrict, abort);

        if (used)
            return address;
        else
            return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluatePointerIndirectionOperator(
        BoundPointerIndirectionOperator node,
        bool used,
        ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node.operand, true, abort);

        if (!node.refersToLocation) {
            if (value.kind == ValueKind.Ref) {
                if (value.ptr >= value.loc.Length)
                    throw new BelteInvalidPointerIndirection(node.syntax.location);
                else
                    value = value.loc[value.ptr];
            } else if (value.kind == ValueKind.Null) {
                throw new BelteInvalidPointerIndirection(node.syntax.location);
            } else {
                throw ExceptionUtilities.UnexpectedValue(value.kind);
            }
        }

        if (!used)
            return EvaluatorValue.None;

        value.kind = ValueKindExtensions.FromSpecialType(node.StrippedType().specialType, value.kind);

        return value;
    }

    private EvaluatorValue EvaluateAssignmentOperator(
        BoundAssignmentOperator node,
        UseKind useKind,
        ValueWrapper<bool> abort) {
        if (node.left is BoundDataContainerExpression)
            return EvaluateGlobalAssignment(node, useKind, abort);

        var lhs = EvaluateAssignmentPreamble(node, abort);
        var value = EvaluateAssignmentValue(node, abort);
        value = EvaluateAssignmentDuplication(useKind, value);

        EvaluateStore(node, lhs, value);

        return EvaluateAssignmentPostfix(node, value, useKind);
    }

    private EvaluatorValue EvaluateAssignmentDuplication(UseKind useKind, EvaluatorValue value) {
        if (useKind != UseKind.Unused && value.kind == ValueKind.Struct) {
            var original = value.@struct;
            var slotCount = original.fields.Length;
            var duplicate = new HeapObject(original.type, slotCount);

            for (var i = 0; i < slotCount; i++)
                duplicate.fields[i] = EvaluateAssignmentDuplication(useKind, original.fields[i]);

            return EvaluatorValue.Struct(duplicate);
        }

        return value;
    }

    private void EvaluateStore(BoundAssignmentOperator node, EvaluatorValue lhs, EvaluatorValue value) {
        var expression = node.left;
        var location = expression.syntax.location;

        switch (expression.kind) {
            case BoundKind.FieldSlotExpression:
                var field = ((BoundFieldSlotExpression)expression).field;

                if (field.refKind != RefKind.None && !node.isRef)
                    IndirectStore(location, lhs.loc[lhs.ptr], value);
                else
                    IndirectStore(location, lhs, value);

                break;
            case BoundKind.DataContainerExpression:
                var local = (BoundDataContainerExpression)expression;

                if (local.dataContainer.refKind != RefKind.None && !node.isRef)
                    IndirectStore(location, lhs.loc[lhs.ptr], value);
                else
                    IndirectStore(location, lhs, value);

                break;
            case BoundKind.StackSlotExpression:
                var symbol = ((BoundStackSlotExpression)expression).symbol;

                if (symbol.GetRefKind() != RefKind.None && !node.isRef)
                    IndirectStore(location, lhs.loc[lhs.ptr], value);
                else
                    IndirectStore(location, lhs, value);

                break;
            case BoundKind.ArrayAccessExpression:
            case BoundKind.CallExpression:
            case BoundKind.ConditionalOperator:
            case BoundKind.PointerIndirectionOperator:
                IndirectStore(location, lhs, value);
                break;
            case BoundKind.ThisExpression:
                lhs.ptr = value.ptr;
                break;
            case BoundKind.AssignmentOperator:
                var nested = (BoundAssignmentOperator)expression;

                if (!nested.isRef)
                    goto default;

                IndirectStore(location, lhs, value);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.kind);
        }
    }

    private EvaluatorValue EvaluateGlobalAssignment(
        BoundAssignmentOperator node,
        UseKind useKind,
        ValueWrapper<bool> abort) {
        var global = (node.left as BoundDataContainerExpression).dataContainer;
        var value = EvaluateAssignmentValue(node, abort);
        value = EvaluateAssignmentDuplication(useKind, value);

        if (global.refKind != RefKind.None && !node.isRef) {
            _context.TryGetGlobal(global, out var indirect);
            IndirectStore(node.syntax.location, indirect, value);
        } else {
            _context.AddOrUpdateGlobal(global, value);
        }

        return EvaluateAssignmentPostfix(node, value, useKind);
    }

    private EvaluatorValue EvaluateAssignmentPostfix(
        BoundAssignmentOperator node,
        EvaluatorValue value,
        UseKind useKind) {
        if (node.syntax.kind == Syntax.SyntaxKind.LocalDeclarationStatement)
            _lastValue = EvaluatorValue.None;

        if (useKind == UseKind.UsedAsValue && node.isRef)
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateAssignmentPreamble(BoundAssignmentOperator node, ValueWrapper<bool> abort) {
        var assignmentTarget = node.left;
        EvaluatorValue expr;

        switch (assignmentTarget.kind) {
            case BoundKind.FieldSlotExpression: {
                    var left = (BoundFieldSlotExpression)assignmentTarget;

                    if (left.field.refKind != RefKind.None && !node.isRef)
                        expr = EvaluateFieldNoIndirection(left, true, abort);
                    else if (!left.field.isStatic)
                        expr = EvaluateReceiverRef(left.receiver, AddressKind.Writeable, abort);
                    else
                        expr = GetOrCreateStaticType(left.field.containingType.originalDefinition, abort);

                    if (expr.kind == ValueKind.Null)
                        throw new BelteNullReferenceException(node.syntax.location);

                    expr = GetHeapFieldSlotOrStructFieldSlotRef(expr, left.slot);
                }

                break;
            case BoundKind.StackSlotExpression: {
                    var left = (BoundStackSlotExpression)assignmentTarget;
                    expr = EvaluatorValue.Ref(_stack.Peek().values, left.slot);
                }

                break;
            case BoundKind.PointerIndirectionOperator: {
                    expr = EvaluatePointerIndirectionOperator(
                        (BoundPointerIndirectionOperator)assignmentTarget,
                        true,
                        abort
                    );
                }

                break;
            case BoundKind.ArrayAccessExpression: {
                    var left = (BoundArrayAccessExpression)assignmentTarget;
                    var receiver = EvaluateExpression(left.receiver, true, abort);
                    var index = (int)EvaluateExpression(left.index, true, abort).int64;
                    expr = EvaluatorValue.Ref(_context.heap[receiver.ptr].fields, index);
                }

                break;
            case BoundKind.ThisExpression: {
                    var left = (BoundThisExpression)assignmentTarget;
                    expr = EvaluateAddress(left, AddressKind.Writeable, abort);
                }

                break;
            case BoundKind.ConditionalOperator: {
                    var left = (BoundConditionalOperator)assignmentTarget;
                    expr = EvaluateAddress(left, AddressKind.Writeable, abort);
                }

                break;
            case BoundKind.CallExpression: {
                    var left = (BoundCallExpression)assignmentTarget;
                    expr = EvaluateCallExpression(left, UseKind.UsedAsAddress, abort);
                }

                break;
            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)assignmentTarget;

                if (!assignment.isRef)
                    goto default;

                expr = EvaluateAssignmentOperator(assignment, UseKind.UsedAsAddress, abort);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(assignmentTarget.kind);
        }

        return expr;
    }

    private EvaluatorValue EvaluateAssignmentValue(BoundAssignmentOperator node, ValueWrapper<bool> abort) {
        if (!node.isRef) {
            return EvaluateExpression(node.right, true, abort);
        } else {
            var lhs = node.left;
            return EvaluateAddress(
                node.right,
                lhs.GetRefKind() is RefKind.RefConst or RefKind.RefConstParameter
                    ? AddressKind.ReadOnlyStrict
                    : AddressKind.Writeable,
                abort
            );
        }
    }

    private EvaluatorValue EvaluateUnaryOperator(BoundUnaryOperator node, bool used, ValueWrapper<bool> abort) {
        if (!used) {
            EvaluateExpression(node.operand, false, abort);
            return EvaluatorValue.None;
        }

        var operatorKind = node.operatorKind;
        var operand = EvaluateExpression(node.operand, true, abort);

        switch (operatorKind.Operator()) {
            case UnaryOperatorKind.UnaryMinus:
                if (operatorKind.OperandTypes() == UnaryOperatorKind.Int)
                    operand.int64 = -operand.int64;
                else
                    operand.@double = -operand.@double;

                break;
            case UnaryOperatorKind.LogicalNegation:
                operand.@bool = !operand.@bool;
                break;
            case UnaryOperatorKind.BitwiseComplement:
                operand.int64 = ~operand.int64;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(operatorKind);
        }

        return operand;
    }

    private EvaluatorValue EvaluateBinaryOperator(BoundBinaryOperator node, bool used, ValueWrapper<bool> abort) {
        var operatorKind = node.operatorKind;

        if (!used && !operatorKind.IsConditional() && !OperatorHasSideEffects(operatorKind)) {
            EvaluateExpression(node.left, false, abort);
            EvaluateExpression(node.right, false, abort);
            return EvaluatorValue.None;
        }

        var op = operatorKind.Operator();
        var left = EvaluateExpression(node.left, true, abort);

        if (left.kind == ValueKind.Null)
            return left;

        if (operatorKind == BinaryOperatorKind.BoolConditionalAnd) {
            if (!left.@bool)
                return left;

            return EvaluateExpression(node.right, true, abort);
        }

        if (operatorKind == BinaryOperatorKind.BoolConditionalOr) {
            if (left.@bool)
                return left;

            return EvaluateExpression(node.right, true, abort);
        }

        var right = EvaluateExpression(node.right, true, abort);

        if (right.kind == ValueKind.Null)
            return right;

        var operandType = operatorKind.OperandTypes();

        if (operandType is BinaryOperatorKind.Enum or
            BinaryOperatorKind.EnumAndUnderlying or BinaryOperatorKind.UnderlyingAndEnum) {
            operandType = GetEnumPromotedType(node.left.StrippedType().GetEnumUnderlyingType().StrippedType().specialType) switch {
                SpecialType.Int32 => BinaryOperatorKind.Int,
                SpecialType.UInt32 => BinaryOperatorKind.UInt,
                SpecialType.Int64 => BinaryOperatorKind.Int,
                SpecialType.UInt64 => BinaryOperatorKind.UInt,
                SpecialType.Int => BinaryOperatorKind.Int,
                SpecialType.String => BinaryOperatorKind.String,
                _ => throw ExceptionUtilities.Unreachable(),
            };
        }

        if (op is BinaryOperatorKind.Equal or BinaryOperatorKind.NotEqual) {
            return EvaluateEqualityOperator(
                node.right.IsLiteralNull(),
                op == BinaryOperatorKind.Equal,
                left,
                right,
                operandType
            );
        }

        switch (op) {
            case BinaryOperatorKind.Addition:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 += right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 += right.uint64;
                        break;
                    case BinaryOperatorKind.String:
                        left.@string += right.@string;
                        break;
                    case BinaryOperatorKind.Float32:
                        left.single += right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@double += right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.Subtraction:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 -= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 -= right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        left.single -= right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@double -= right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.Multiplication:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 *= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 *= right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        left.single *= right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@double *= right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.Division:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        if (right.int64 == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.int64 /= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        if (right.uint64 == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.uint64 /= right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        if (right.single == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.single /= right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        if (right.@double == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.@double /= right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.LessThan:
                left.kind = ValueKind.Bool;

                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.@bool = left.int64 < right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.@bool = left.uint64 < right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        left.@bool = left.single < right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@bool = left.@double < right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.GreaterThan:
                left.kind = ValueKind.Bool;

                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.@bool = left.int64 > right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.@bool = left.uint64 > right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        left.@bool = left.single > right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@bool = left.@double > right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.LessThanOrEqual:
                left.kind = ValueKind.Bool;

                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.@bool = left.int64 <= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.@bool = left.uint64 <= right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        left.@bool = left.single <= right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@bool = left.@double <= right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.GreaterThanOrEqual:
                left.kind = ValueKind.Bool;

                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.@bool = left.int64 >= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.@bool = left.uint64 >= right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        left.@bool = left.single >= right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@bool = left.@double >= right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.And:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 &= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 &= right.uint64;
                        break;
                    case BinaryOperatorKind.Bool:
                        left.@bool &= right.@bool;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.Or:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 |= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 |= right.uint64;
                        break;
                    case BinaryOperatorKind.Bool:
                        left.@bool |= right.@bool;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.Xor:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 ^= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 ^= right.uint64;
                        break;
                    case BinaryOperatorKind.Bool:
                        left.@bool ^= right.@bool;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.LeftShift:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 <<= Convert.ToInt32(right.int64);
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 <<= Convert.ToInt32(right.uint64);
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.RightShift:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 >>= Convert.ToInt32(right.int64);
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 >>= Convert.ToInt32(right.uint64);
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.UnsignedRightShift:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 >>>= Convert.ToInt32(right.int64);
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 >>>= Convert.ToInt32(right.uint64);
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.Modulo:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        if (right.int64 == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.int64 %= right.int64;
                        break;
                    case BinaryOperatorKind.UInt:
                        if (right.uint64 == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.uint64 %= right.uint64;
                        break;
                    case BinaryOperatorKind.Float32:
                        if (right.single == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.single %= right.single;
                        break;
                    case BinaryOperatorKind.Float64:
                        if (right.@double == 0)
                            throw new BelteDivideByZeroException(node.syntax.location);

                        left.@double %= right.@double;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            case BinaryOperatorKind.Power:
                switch (operandType) {
                    case BinaryOperatorKind.Int:
                        left.int64 = (long)Math.Pow(left.int64, right.int64);
                        break;
                    case BinaryOperatorKind.UInt:
                        left.uint64 = (ulong)Math.Pow(left.uint64, right.uint64);
                        break;
                    case BinaryOperatorKind.Float32:
                        left.single = (float)Math.Pow(left.single, right.single);
                        break;
                    case BinaryOperatorKind.Float64:
                        left.@double = Math.Pow(left.@double, right.@double);
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(operandType);
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(operatorKind);
        }

        return left;
    }

    private static EvaluatorValue EvaluateEqualityOperator(
        bool rightIsLiteralNull,
        bool isEqual,
        EvaluatorValue left,
        EvaluatorValue right,
        BinaryOperatorKind operandType) {
        if (rightIsLiteralNull)
            return EvaluatorValue.Literal(left.kind == ValueKind.Null == isEqual);

        switch (operandType) {
            case BinaryOperatorKind.Int:
                left.@bool = left.int64 == right.int64;
                break;
            case BinaryOperatorKind.UInt:
                left.@bool = left.uint64 == right.uint64;
                break;
            case BinaryOperatorKind.Float64:
                left.@bool = left.@double == right.@double;
                break;
            case BinaryOperatorKind.Float32:
                left.@bool = left.@single == right.@single;
                break;
            case BinaryOperatorKind.Bool:
                left.@bool = left.@bool == right.@bool;
                break;
            case BinaryOperatorKind.String:
                left.@bool = left.@string == right.@string;
                break;
            case BinaryOperatorKind.Char:
                left.@bool = left.@char == right.@char;
                break;
            case BinaryOperatorKind.Object:
                left.@bool = left.ptr == right.ptr;
                break;
            case BinaryOperatorKind.Type:
                left.@bool = ((TypeSymbol)left.type).Equals(
                    (TypeSymbol)right.type, SymbolEqualityComparer.ConsiderEverything
                );

                break;
        }

        left.kind = ValueKind.Bool;

        if (!isEqual)
            left.@bool = !left.@bool;

        return left;
    }

    #endregion

    #region Addresses

    private bool HasHome(BoundExpression expression, AddressKind addressKind) {
        var frame = _stack.Peek();
        return Binder.HasHome(expression, addressKind, frame.layout.symbol, []);
    }

    private EvaluatorValue EvaluateAddress(BoundExpression node, AddressKind addressKind, ValueWrapper<bool> abort) {
        switch (node.kind) {
            case BoundKind.DataContainerExpression:
                return EvaluateGlobalAddress((BoundDataContainerExpression)node);
            case BoundKind.StackSlotExpression:
                return EvaluateStackAddress((BoundStackSlotExpression)node, addressKind, abort);
            case BoundKind.FieldSlotExpression:
                return EvaluateFieldAddress((BoundFieldSlotExpression)node, addressKind, abort);
            case BoundKind.ArrayAccessExpression:
                if (!HasHome(node, addressKind))
                    goto default;

                return EvaluateArrayElementAddress((BoundArrayAccessExpression)node, abort);
            case BoundKind.ThisExpression:
                if (IsValueType(node.Type())) {
                    if (!HasHome(node, addressKind))
                        goto default;

                    return _stack.Peek().values[0];
                } else {
                    return EvaluatorValue.Ref(_stack.Peek().values, 0);
                }
            case BoundKind.BaseExpression:
                return EvaluatorValue.None;
            case BoundKind.CallExpression:
                var call = (BoundCallExpression)node;

                if (UseCallResultAsAddress(call, addressKind))
                    return EvaluateCallExpression(call, UseKind.UsedAsAddress, abort);

                goto default;
            case BoundKind.ConditionalOperator:
                if (!HasHome(node, addressKind))
                    goto default;

                return EvaluateConditionalOperatorAddress((BoundConditionalOperator)node, addressKind, abort);
            case BoundKind.ThrowExpression:
                return EvaluateExpression(node, true, abort);
            case BoundKind.PointerIndirectionOperator:
                var operand = ((BoundPointerIndirectionOperator)node).operand;
                return EvaluateExpression(operand, true, abort);
            default:
                return EvaluateAddressOfTempClone(node, abort);
        }
    }

    private EvaluatorValue EvaluateConditionalOperatorAddress(
        BoundConditionalOperator node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(node.condition, true, abort).@bool;

        if (condition)
            return EvaluateAddress(node.trueExpression, addressKind, abort);
        else
            return EvaluateAddress(node.falseExpression, addressKind, abort);
    }

    private EvaluatorValue EvaluateArrayElementAddress(BoundArrayAccessExpression node, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, true, abort);
        var index = (int)EvaluateExpression(node.index, true, abort).int64;

        if (((ArrayTypeSymbol)node.receiver.StrippedType()).isSZArray)
            return EvaluatorValue.Ref(_context.heap[receiver.ptr].fields, index);
        else
            return _context.heap[receiver.ptr].fields[index];
    }

    private EvaluatorValue EvaluateFieldAddress(
        BoundFieldSlotExpression node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        if (!HasHome(node.original, addressKind))
            return EvaluateAddressOfTempClone(node, abort);
        else
            return EvaluateInstanceFieldAddress(node, addressKind, abort);
    }

    private EvaluatorValue EvaluateInstanceFieldAddress(
        BoundFieldSlotExpression node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var field = node.field;

        var receiver = EvaluateReceiverRef(
            node.receiver,
            field.refKind == RefKind.None
                ? (addressKind == AddressKind.Constrained ? AddressKind.Writeable : addressKind)
                : (addressKind != AddressKind.ReadOnlyStrict ? AddressKind.ReadOnly : addressKind),
            abort
        );

        if (field.refKind == RefKind.None)
            return GetHeapFieldSlotOrStructFieldSlotRef(receiver, node.slot);

        return GetHeapFieldSlotOrStructFieldSlot(receiver, node.slot);
    }

    private EvaluatorValue EvaluateReceiverRef(
        BoundExpression receiver,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var receiverType = receiver.Type();

        if (receiverType.IsVerifierReference())
            return EvaluateExpression(receiver, true, abort);

        return EvaluateAddress(receiver, addressKind, abort);
    }

    private EvaluatorValue EvaluateGlobalAddress(BoundDataContainerExpression node) {
        return EvaluatorValue.Ref(_context.globalSlots, _context.GetSlotOfGlobal(node.dataContainer));
    }

    private EvaluatorValue EvaluateStackAddress(
        BoundStackSlotExpression node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        if (!HasHome(node.original, addressKind))
            return EvaluateAddressOfTempClone(node, abort);

        if (node.symbol is ParameterSymbol p && p.refKind != RefKind.None)
            return _stack.Peek().values[node.slot];

        return EvaluatorValue.Ref(_stack.Peek().values, node.slot);
    }

    private EvaluatorValue EvaluateAddressOfTempClone(BoundExpression node, ValueWrapper<bool> abort) {
        // Should only be reachable with uninitialized ref locals and structs
        if (!node.IsLiteralNull() && !(node is BoundCallExpression c && c.receiver.type.StrippedType().IsStructType()))
            throw ExceptionUtilities.UnexpectedValue(node.kind);

        var value = EvaluateExpression(node, true, abort);
        var temp = AllocateTemp(node.type);
        var frame = _stack.Peek();
        frame.values[temp.slot] = value;
        return EvaluatorValue.Ref(frame.values, temp.slot);
    }

    private VariableDefinition AllocateTemp(
        TypeSymbol type,
        LocalSlotConstraints slotConstraints = LocalSlotConstraints.None) {
        var frame = _stack.Peek();
        var temp = frame.layout.AllocateSlot(type, slotConstraints);

        if (frame.values.Length < frame.layout.LocalsInOrder().Length)
            throw ExceptionUtilities.Unreachable();

        return temp;
    }

    #endregion

    #region Calls

    private EvaluatorValue EvaluateCallExpression(
        BoundCallExpression node,
        UseKind useKind,
        ValueWrapper<bool> abort) {
        if (CheckStandardMap(
            node.syntax.location,
            node.method,
            node.receiver,
            node.arguments,
            abort,
            out var result,
            out var printed,
            out var io)) {
            lastOutputWasPrint = printed;
            containsIO = io;

            if (result is EvaluatorValue e) {
                return e;
            } else if (result is EvaluatorValue[] a) {
                var array = new HeapObject((ArrayTypeSymbol)node.StrippedType(), a);
                var index = _context.heap.Allocate(array, _stack, _context);
                return EvaluatorValue.HeapPtr(index);
            } else if (!node.method.returnsVoid) {
                return EvaluatorValue.Literal(result, node.method.returnType.StrippedType().specialType);
            } else {
                return EvaluatorValue.None;
            }
        }

        if (node.method.RequiresInstanceReceiver())
            return EvaluateInstanceCallExpression(node, useKind, abort);
        else
            return EvaluateStaticCallExpression(node, useKind, abort);
    }

    private EvaluatorValue EvaluateStaticCallExpression(
        BoundCallExpression node,
        UseKind useKind,
        ValueWrapper<bool> abort) {
        var method = node.method;
        var arguments = node.arguments;

        var evaluatedArguments = EvaluateArguments(arguments, method.parameters, node.argumentRefKinds, abort);

        if (method.isExtern)
            throw new BelteEvaluatorException("Extern method calls are not supported in the Evaluator.", node.syntax.location);

        var value = InvokeMethod(method, SynthesizeCallObject(method.containingType), evaluatedArguments, abort);

        if (exceptions.Count == 0 && useKind == UseKind.UsedAsValue && method.refKind != RefKind.None)
            return value.loc[value.ptr];
        else
            return value;
    }

    private EvaluatorValue SynthesizeCallObject(NamedTypeSymbol type) {
        var layout = new EvaluatorSlotManager(type);
        var current = type;
        var builder = ArrayBuilder<EvaluatorValue>.GetInstance();

        while (current is not null) {
            if (current.arity > 0) {
                for (var i = 0; i < current.arity; i++) {
                    var parameter = current.templateParameters[i];
                    var argument = current.templateArguments[i];

                    layout.DeclareLocal(
                        parameter.underlyingType.type,
                        parameter,
                        parameter.name,
                        SynthesizedLocalKind.UserDefined,
                        LocalSlotConstraints.None,
                        false
                    );

                    if (argument.isType) {
                        builder.Add(EvaluatorValue.Type(argument.type.type));
                    } else {
                        builder.Add(EvaluatorValue.Literal(
                            argument.constant.value,
                            argument.constant.specialType
                        ));
                    }
                }
            }

            current = current.containingType;
        }

        var fields = layout.LocalsInOrder();
        var heapObject = new HeapObject(type, fields.Length);

        for (var i = 0; i < builder.Count; i++)
            heapObject.fields[i] = builder[i];

        builder.Free();

        var index = _context.heap.Allocate(heapObject, _stack, _context);
        return EvaluatorValue.HeapPtr(index);
    }

    private EvaluatorValue EvaluateInstanceCallExpression(
        BoundCallExpression node,
        UseKind useKind,
        ValueWrapper<bool> abort) {
        var method = node.method;
        var arguments = node.arguments;
        var receiver = node.receiver;

        var thisParameter = EvaluateExpression(receiver, true, abort);

        if (thisParameter.kind == ValueKind.Null)
            throw new BelteNullReferenceException(receiver.syntax.location);

        var evaluatedArguments = EvaluateArguments(arguments, method.parameters, node.argumentRefKinds, abort);

        if (method.isExtern)
            throw new BelteEvaluatorException("Extern method calls are not supported in the Evaluator.", node.syntax.location);

        method = thisParameter.kind == ValueKind.MethodGroup
            ? thisParameter.data as MethodSymbol
            : ResolveVirtualMethod(method, receiver, thisParameter);

        var value = InvokeMethod(method, thisParameter, evaluatedArguments, abort);

        if (exceptions.Count == 0 && useKind == UseKind.UsedAsValue && method.refKind != RefKind.None)
            return value.loc[value.ptr];
        else
            return value;
    }

    private MethodSymbol ResolveVirtualMethod(
        MethodSymbol method,
        BoundExpression receiver,
        EvaluatorValue thisParameter) {
        if ((method.isAbstract || method.isVirtual) &&
            receiver?.StrippedType()?.typeKind != TypeKind.TemplateParameter) {
            var typeToLookup = receiver?.kind == BoundKind.BaseExpression
                ? receiver.StrippedType()
                : _context.heap[thisParameter.ptr].type.StrippedType();

            var newMethod = typeToLookup
                .GetMembersUnordered()
                .Where(s => s is MethodSymbol m &&
                    m.overriddenMethod?.originalDefinition?.Equals(method.originalDefinition) == true)
                .FirstOrDefault() as MethodSymbol;

            if (newMethod is not null)
                return newMethod;
        }

        return method;
    }

    private EvaluatorValue[] EvaluateArguments(
        ImmutableArray<BoundExpression> arguments,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<RefKind> argRefKindsOpt,
        ValueWrapper<bool> abort) {
        var builder = ArrayBuilder<EvaluatorValue>.GetInstance(arguments.Length);

        for (var i = 0; i < arguments.Length; i++) {
            var argRefKind = GetArgumentRefKind(parameters, argRefKindsOpt, i);
            builder.Add(EvaluateArgument(arguments[i], argRefKind, abort));
        }

        return builder.ToArray();
    }

    private static RefKind GetArgumentRefKind(
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<RefKind> argRefKindsOpt,
        int i) {
        RefKind argRefKind;

        if (i < parameters.Length) {
            if (!argRefKindsOpt.IsDefault && i < argRefKindsOpt.Length)
                argRefKind = argRefKindsOpt[i];
            else
                argRefKind = parameters[i].refKind;
        } else {
            argRefKind = RefKind.None;
        }

        return argRefKind;
    }

    private EvaluatorValue EvaluateArgument(BoundExpression argument, RefKind refKind, ValueWrapper<bool> abort) {
        if (refKind == RefKind.None)
            return EvaluateExpression(argument, true, abort);

        return EvaluateAddress(argument, AddressKind.Writeable, abort);
    }

    private EvaluatorValue InvokeMethod(
        MethodSymbol method,
        EvaluatorValue thisParameter,
        EvaluatorValue[] arguments,
        ValueWrapper<bool> abort) {
        if (!_program.TryGetMethodBodyIncludingParents(method, out var body))
            throw new BelteInternalException($"Failed to get method body ({method}).");

        if (!_program.TryGetMethodLayoutIncludingParents(method, out var layout)) {
            layout = new EvaluatorSlotManager(method);

            if (!thisParameter.Equals(EvaluatorValue.None))
                layout.AllocateSlot(method.thisParameter.type, LocalSlotConstraints.None);
        }

        var frame = new StackFrame(layout);

        if (!thisParameter.Equals(EvaluatorValue.None))
            frame.values[0] = thisParameter;

        if (method.arity > 0) {
            for (var i = 0; i < method.arity; i++) {
                var templateArgument = method.templateArguments[i];

                if (templateArgument.isType) {
                    frame.values[i + 1] = EvaluatorValue.Type(templateArgument.type.type);
                } else {
                    frame.values[i + 1] = EvaluatorValue.Literal(
                        templateArgument.constant.value,
                        templateArgument.constant.specialType
                    );
                }
            }
        }

        for (var i = 0; i < arguments.Length; i++) {
            var slot = layout.GetLocal(method.parameters[i]).slot;
            frame.values[slot] = arguments[i];
        }

        _stack.Push(frame);

        var result = EvaluateStatement(method, body, abort, out _);

        _stack.Pop();

        return result;
    }

    #endregion

    #region Libraries

    private bool CheckStandardMap(
        TextLocation location,
        MethodSymbol method,
        BoundExpression receiver,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        out object result,
        out bool printed,
        out bool io) {
        printed = false;
        io = false;
        result = null;

        if ((object)method.containingNamespace != LibraryHelpers.BelteNamespace.originalDefinition) {
            if (method.containingType?.specialType != SpecialType.Nullable &&
                method.containingType?.specialType != SpecialType.Object) {
                return false;
            }
        }

        var reduced = _program.compilation.options.noStdLib;

        if (!reduced && (object)method.containingType == GraphicsLibrary.Graphics.underlyingNamedType)
            return HandleGraphicsCall(location, method, arguments, abort, out result);

        // TODO If we deem these string checks too slow, we could probably compute unique Int64 mapKeys instead
        var mapKey = LibraryHelpers.BuildMapKey(method);

        if ((object)method.containingNamespace == LibraryHelpers.BelteNamespace.originalDefinition) {
            switch (mapKey) {
                case "LowLevel_GetHashCode_O": {
                        var argument = EvaluateExpression(arguments[0], true, abort);

                        if (argument.kind == ValueKind.HeapPtr) {
                            result = _context.heap[argument.ptr].GetHashCode();
                        } else if (argument.kind == ValueKind.Struct) {
                            result = argument.@struct.GetHashCode();
                        } else {
                            switch (argument.kind) {
                                case ValueKind.Int8:
                                case ValueKind.Int16:
                                case ValueKind.Int32:
                                case ValueKind.Int64:
                                case ValueKind.UInt8:
                                case ValueKind.UInt16:
                                case ValueKind.UInt32:
                                case ValueKind.UInt64:
                                case ValueKind.Float32:
                                case ValueKind.Float64:
                                case ValueKind.Bool:
                                case ValueKind.Char:
                                    result = argument.int64;
                                    break;
                                case ValueKind.String:
                                    result = argument.@string.GetHashCode();
                                    break;
                                default:
                                    throw ExceptionUtilities.UnexpectedValue(argument.kind);
                            }
                        }
                    }

                    return true;
                case "LowLevel_GetType_A": {
                        var argument = EvaluateExpression(arguments[0], true, abort);
                        TypeSymbol type;

                        if (argument.kind == ValueKind.HeapPtr) {
                            type = (NamedTypeSymbol)_context.heap[argument.ptr].type;
                        } else if (argument.kind == ValueKind.Struct) {
                            type = argument.@struct.type;
                        } else {
                            type = argument.kind switch {
                                ValueKind.Int8 => CorLibrary.GetSpecialType(SpecialType.Int8),
                                ValueKind.Int16 => CorLibrary.GetSpecialType(SpecialType.Int16),
                                ValueKind.Int32 => CorLibrary.GetSpecialType(SpecialType.Int32),
                                ValueKind.Int64 => CorLibrary.GetSpecialType(SpecialType.Int64),
                                ValueKind.UInt8 => CorLibrary.GetSpecialType(SpecialType.UInt8),
                                ValueKind.UInt16 => CorLibrary.GetSpecialType(SpecialType.UInt16),
                                ValueKind.UInt32 => CorLibrary.GetSpecialType(SpecialType.UInt32),
                                ValueKind.UInt64 => CorLibrary.GetSpecialType(SpecialType.UInt64),
                                ValueKind.Float32 => CorLibrary.GetSpecialType(SpecialType.Float32),
                                ValueKind.Float64 => CorLibrary.GetSpecialType(SpecialType.Float64),
                                ValueKind.Bool => CorLibrary.GetSpecialType(SpecialType.Bool),
                                ValueKind.Char => CorLibrary.GetSpecialType(SpecialType.Char),
                                ValueKind.String => CorLibrary.GetSpecialType(SpecialType.String),
                                _ => throw ExceptionUtilities.UnexpectedValue(argument.kind)
                            };
                        }

                        result = EvaluatorValue.Type(type);
                    }

                    return true;
                case "LowLevel_GetTypeName_O": {
                        var argument = EvaluateExpression(arguments[0], true, abort);

                        if (argument.kind == ValueKind.HeapPtr) {
                            var type = (NamedTypeSymbol)_context.heap[argument.ptr].type;
                            result = GetTypeName(type);
                        } else if (argument.kind == ValueKind.Struct) {
                            var type = argument.@struct.type;
                            result = GetTypeName(type);
                        } else {
                            // TODO These are .NET types not Belte types! (to ensure parity with IL code gen)
                            result = argument.kind switch {
                                ValueKind.Int8 => "SByte",
                                ValueKind.Int16 => "Int16",
                                ValueKind.Int32 => "Int32",
                                ValueKind.Int64 => "Int64",
                                ValueKind.UInt8 => "Byte",
                                ValueKind.UInt16 => "UInt16",
                                ValueKind.UInt32 => "UInt32",
                                ValueKind.UInt64 => "UInt64",
                                ValueKind.Float32 => "Single",
                                ValueKind.Float64 => "Double",
                                ValueKind.Bool => "Boolean",
                                ValueKind.Char => "Char",
                                ValueKind.String => "String",
                                _ => throw ExceptionUtilities.UnexpectedValue(argument.kind)
                            };
                        }
                    }

                    return true;
                case "LowLevel_Length_[?":
                case "LowLevel_Length_[": {
                        var argument = EvaluateExpression(arguments[0], true, abort);

                        if (argument.kind != ValueKind.HeapPtr) {
                            result = 0;
                            return true;
                        }

                        var array = _context.heap[argument.ptr];

                        if (array.type.kind != SymbolKind.ArrayType) {
                            result = 0;
                            return true;
                        }

                        result = array.fields.Length;
                    }

                    return true;
                case "Random_RandInt_I?":
                    _lazyRandom ??= new Random();
                    var max = (int)EvaluateExpression(arguments[0], true, abort).int64;
                    result = _lazyRandom.NextInt64(max);
                    return true;
                case "LowLevel_ThrowNullConditionException":
                    throw new BelteNullConditionException(location);
                case "Random_Random":
                    _lazyRandom ??= new Random();
                    result = _lazyRandom.NextDouble();
                    return true;
                case "LowLevel_Sort_[?": {
                        var arrayPtr = EvaluateExpression(arguments[0], true, abort);

                        if (arrayPtr.kind != ValueKind.HeapPtr)
                            return true;

                        var array = _context.heap[arrayPtr.ptr];

                        if (array.type.kind != SymbolKind.ArrayType)
                            return true;

                        var elementSpecialType = ((ArrayTypeSymbol)array.type).elementType.StrippedType().specialType;
                        Comparison<EvaluatorValue> comparison;

                        if (elementSpecialType == SpecialType.Int)
                            comparison = (a, b) => a.int64.CompareTo(b.int64);
                        else if (elementSpecialType == SpecialType.Decimal)
                            comparison = (a, b) => a.@double.CompareTo(b.@double);
                        else
                            throw new BelteInvalidCastException(location);

                        Array.Sort(array.fields, comparison);
                    }

                    return true;
                case "String_Split_SS": {
                        var args = arguments.Select(a => EvaluateExpression(a, true, abort).@string).ToArray();
                        var text = args[0];
                        var separator = args[1];
                        var res = text.Split(separator);

                        result = res.Select(r => EvaluatorValue.Literal(r)).ToArray();
                    }

                    return true;
                case "Console_Print_S?":
                case "Console_Print_A?":
                case "Console_Print_O?":
                    printed = true;
                    goto case "Console_PrintLine_S?";
                case "Console_PrintLine_S?":
                case "Console_PrintLine_A?":
                case "Console_PrintLine_O?":
                    if (arguments[0].StrippedType().isObjectType) {
                        var argument = EvaluateExpression(arguments[0], true, abort);
                        var toStringMethod = ResolveVirtualMethod(_toStringMethod, null, argument);
                        var toStringResult = InvokeMethod(toStringMethod, argument, [], abort);
                        var func = StandardLibrary.EvaluatorMap[mapKey];
                        result = func(toStringResult.@string, null, null);
                        return true;
                    }

                    break;
            }

            var function = StandardLibrary.EvaluatorMap[mapKey];
            var valueArguments = arguments
                .Select(a => EvaluatorValue.Format(EvaluateExpression(a, true, abort), _context))
                .ToArray();

            switch (mapKey) {
                case "File_Copy_SS":
                    valueArguments[0] = GetFilePath((string)valueArguments[0], location);
                    valueArguments[1] = GetFilePath((string)valueArguments[1], location);
                    break;
                case "Directory_Create_S":
                case "Directory_Delete_S":
                case "Directory_Exists_S":
                case "File_AppendText_SS":
                case "File_Create_S":
                case "File_Delete_S":
                case "File_Exists_S":
                case "File_ReadText_S":
                case "File_WriteText_SS":
                    valueArguments[0] = GetFilePath((string)valueArguments[0], location);
                    break;
            }

            result = arguments.Length switch {
                0 => function(null, null, null),
                1 => function(valueArguments[0], null, null),
                2 => function(valueArguments[0], valueArguments[1], null),
                3 => function(valueArguments[0], valueArguments[1], valueArguments[2]),
                _ => throw ExceptionUtilities.UnexpectedValue(arguments.Length),
            };

            return true;
        } else {
            switch (mapKey) {
                case "Nullable<>_get_Value":
                    result = NullAssertValue(receiver, abort);
                    return true;
                case "Nullable<>_get_HasValue":
                    var receiverValue = EvaluateExpression(receiver, true, abort);
                    result = EvaluatorValue.Literal(receiverValue.kind != ValueKind.Null);
                    return true;
                case "Nullable<>_GetValueOrDefault":
                    result = EvaluateExpression(receiver, true, abort);
                    return true;
                case "Object<>_ToString":
                    var thisParameter = EvaluateExpression(receiver, true, abort);

                    if (thisParameter.kind == ValueKind.Null)
                        throw new BelteNullReferenceException(receiver.syntax.location);

                    result = thisParameter.kind == ValueKind.HeapPtr
                        ? InvokeMethod(ResolveVirtualMethod(method, receiver, thisParameter), thisParameter, [], abort)
                        : EvaluatorValue.Format(thisParameter, _context);

                    return true;
                default:
                    return false;
            }
        }
    }

    private static string GetTypeName(TypeSymbol type) {
        var builder = new StringBuilder();
        GetTypeNameCore(type, builder);
        return builder.ToString();
    }

    private static void GetTypeNameCore(TypeSymbol type, StringBuilder builder) {
        // ? The goal here is IL parity, not Belte-correctness
        // TODO This always adds the arity distinguisher (`1) even if the type name is unique unlike .NET
        switch (type.typeKind) {
            case TypeKind.Array:
                GetTypeNameCore(((ArrayTypeSymbol)type).elementType, builder);
                builder.Append("[]");
                break;
            case TypeKind.Primitive:
                builder.Append(type.specialType switch {
                    SpecialType.String => "System.String",
                    SpecialType.Int => "System.Int64",
                    SpecialType.Bool => "System.Boolean",
                    SpecialType.Decimal => "System.Double",
                    SpecialType.Char => "System.Char",
                    _ => throw ExceptionUtilities.UnexpectedValue(type.typeKind)
                });

                break;
            case TypeKind.Struct:
            case TypeKind.Class:
                if (type.specialType == SpecialType.Nullable)
                    builder.Append("System.");

                var namedType = (NamedTypeSymbol)type;
                builder.Append(namedType.ToDisplayString(SymbolDisplayFormat.ToStringNameFormat));

                var arity = namedType.arity;

                if (arity == 0)
                    return;

                builder.Append('`');
                builder.Append(arity);
                builder.Append('[');

                for (var i = 0; i < arity; i++) {
                    var argument = namedType.templateArguments[i];

                    if (argument.isConstant)
                        builder.Append(argument.constant.value);
                    else
                        GetTypeNameCore(argument.type.type, builder);

                    if (i < arity - 1)
                        builder.Append(',');
                }

                builder.Append(']');

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(type.typeKind);
        }
    }

    private static string GetFilePath(string path, TextLocation location) {
        if (File.Exists(path))
            return path;

        var filePath = string.Join(
            Path.DirectorySeparatorChar,
            location.fileName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).SkipLast(1)
        );

        var appendedPath = Path.Combine(filePath, path);

        if (File.Exists(appendedPath))
            return appendedPath;

        return null;
    }

    private bool HandleGraphicsCall(
        TextLocation location,
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        out object result) {
        result = null;

        if (_program.compilation.options.outputKind != OutputKind.GraphicsApplication)
            throw new InvalidOperationException("Cannot make Graphics calls when the output kind is not graphics");

        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (mapKey == "Graphics_Initialize_SIIB") {
            var valueArguments = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();

            StartGraphics(
                valueArguments[0].@string,
                (int)valueArguments[1].int64,
                (int)valueArguments[2].int64,
                valueArguments[3].@bool,
                abort
            );

            return true;
        }

        if (_context.graphicsThread is null)
            throw new BelteEvaluatorException("All Graphics calls must come after Graphics.Initialize.", location);

        while (_context.graphicsHandler?.GraphicsDevice is null)
            Thread.SpinWait(1);

        switch (mapKey) {
            case "Graphics_LoadTexture_S": {
                    var path = GetFilePath(EvaluateExpression(arguments[0], true, abort).@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load texture path does not exist.", location);

                    result = LoadTexture(path);
                }

                break;
            case "Graphics_LoadTexture_SIII": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();
                    var path = GetFilePath(evaluatedArguments[0].@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load texture path does not exist.", location);

                    var r = evaluatedArguments[1].int64;
                    var g = evaluatedArguments[2].int64;
                    var b = evaluatedArguments[3].int64;

                    result = LoadTexture(path, true, r, g, b);
                }

                break;
            case "Graphics_LoadSprite_SV?V?I?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();
                    var path = GetFilePath(evaluatedArguments[0].@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load sprite: path does not exist.", location);

                    var spriteType = CorLibrary.GetSpecialType(SpecialType.Sprite);
                    var sprite = CreateObject(spriteType);

                    var temp = AllocateTemp(spriteType);
                    _stack.Peek().values[temp.slot] = sprite;

                    InvokeMethod(
                        spriteType.instanceConstructors[0],
                        sprite,
                        [
                            LoadTexture(path),
                            evaluatedArguments[1],
                            evaluatedArguments[2],
                            evaluatedArguments[3]
                        ],
                        abort
                    );

                    _stack.Peek().layout.FreeSlot(temp);

                    result = sprite;
                }

                break;
            case "Graphics_DrawSprite_S?": {
                    var argument = EvaluateExpression(arguments[0], true, abort);

                    if (argument.kind == ValueKind.Null)
                        return true;

                    DrawSprite(argument, EvaluatorValue.None, out result);
                }

                break;
            case "Graphics_DrawSprite_S?V?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();
                    var spritePtr = evaluatedArguments[0];

                    if (spritePtr.kind == ValueKind.Null)
                        return true;

                    DrawSprite(spritePtr, evaluatedArguments[1], out result);
                }

                break;
            case "Graphics_StopDraw_I?": {
                    var argument = EvaluateExpression(arguments[0], true, abort);

                    if (argument.kind == ValueKind.Null)
                        return true;

                    _context.graphicsHandler.RemoveAction((int)argument.int64);
                }

                break;
            case "Graphics_LoadText_S?SV?DD?I?I?I?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();
                    var path = GetFilePath(evaluatedArguments[1].@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load text: path does not exist.", location);

                    var textType = CorLibrary.GetSpecialType(SpecialType.Text);
                    var textPtr = CreateObject(textType);
                    var text = H(textPtr);

                    var fontSize = (float)evaluatedArguments[3].@double;

                    text[1] = evaluatedArguments[0];
                    text[2] = evaluatedArguments[1];
                    text[3] = evaluatedArguments[2];
                    text[4] = evaluatedArguments[3];
                    text[5] = evaluatedArguments[4];
                    text[6] = evaluatedArguments[5];
                    text[7] = evaluatedArguments[6];
                    text[8] = evaluatedArguments[7];

                    text[0].data = _context.graphicsHandler.LoadText(path, fontSize);

                    if (text[0].data is not DynamicSpriteFont spriteFont)
                        throw new BelteEvaluatorException("Failed to create text object.", location);

                    result = textPtr;
                }

                break;
            case "Graphics_DrawText_T?": {
                    var argument = EvaluateExpression(arguments[0], true, abort);

                    if (argument.kind == ValueKind.Null)
                        return true;

                    var fields = H(argument);

                    var text = fields[1].@string;
                    var posXf = H(fields[3])[0];
                    double? posX = posXf.kind == ValueKind.Null ? null : posXf.@double;
                    var posYf = H(fields[3])[1];
                    double? posY = posYf.kind == ValueKind.Null ? null : posYf.@double;
                    long? r = fields[6].kind == ValueKind.Null ? null : fields[6].int64;
                    long? g = fields[7].kind == ValueKind.Null ? null : fields[7].int64;
                    long? b = fields[8].kind == ValueKind.Null ? null : fields[8].int64;

                    var spriteFont = (DynamicSpriteFont)fields[0].data;

                    if (_isScript && !_insideUpdate) {
                        result = _context.graphicsHandler.AddAction(
                            () => { _context.graphicsHandler.DrawText(spriteFont, text, posX, posY, r, g, b); }
                        );
                    } else {
                        _context.graphicsHandler.DrawText(spriteFont, text, posX, posY, r, g, b);
                    }
                }

                break;
            case "Graphics_GetKey_S": {
                    var argument = EvaluateExpression(arguments[0], true, abort).@string;
                    result = _context.graphicsHandler.GetKey(argument);
                }

                break;
            case "Graphics_GetMouseButton_S": {
                    var argument = EvaluateExpression(arguments[0], true, abort).@string;
                    result = _context.graphicsHandler.GetMouseButton(argument);
                }

                break;
            case "Graphics_GetScroll": {
                    result = (long)_context.graphicsHandler.GetScroll();
                }

                break;
            case "Graphics_GetMousePosition": {
                    var (x, y) = _context.graphicsHandler.GetMousePosition();
                    var vecType = CorLibrary.GetSpecialType(SpecialType.Vec2);
                    var vec = CreateObject(vecType);

                    var temp = AllocateTemp(vecType);
                    _stack.Peek().values[temp.slot] = vec;

                    InvokeMethod(
                        vecType.instanceConstructors[0],
                        vec,
                        [
                            EvaluatorValue.Literal(Convert.ToDouble(x)),
                            EvaluatorValue.Literal(Convert.ToDouble(y))
                        ],
                        abort
                    );

                    _stack.Peek().layout.FreeSlot(temp);

                    result = vec;
                }

                break;
            case "Graphics_DrawRect_R?I?I?I?":
                DrawRect(false, out result);
                break;
            case "Graphics_DrawRect_R?I?I?I?I?":
                DrawRect(true, out result);
                break;
            case "Graphics_Fill_III": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();

                    var r = evaluatedArguments[0].int64;
                    var g = evaluatedArguments[1].int64;
                    var b = evaluatedArguments[2].int64;

                    if (_isScript && !_insideUpdate) {
                        result = _context.graphicsHandler.AddAction(
                            () => { _context.graphicsHandler.Fill(r, g, b); }
                        );
                    } else {
                        _context.graphicsHandler.Fill(r, g, b);
                    }
                }

                break;
            case "Graphics_Draw_T?R?R?I?B?D?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();
                    var texturePtr = evaluatedArguments[0];

                    var texture2D = (Texture2D)H(texturePtr)[0].data;

                    var srcRect = evaluatedArguments[1];
                    var dstRect = evaluatedArguments[2];
                    long? rotation = (evaluatedArguments[3].kind == ValueKind.Null) ? null : evaluatedArguments[3].int64;
                    bool? flip = (evaluatedArguments[4].kind == ValueKind.Null) ? null : evaluatedArguments[4].@bool;
                    double? alpha = (evaluatedArguments[5].kind == ValueKind.Null) ? null : evaluatedArguments[5].@double;

                    Microsoft.Xna.Framework.Rectangle? src = null;

                    if (srcRect.kind != ValueKind.Null) {
                        var (sx, sy, sw, sh) = ExtRect(srcRect);
                        src = new Microsoft.Xna.Framework.Rectangle(sx, sy, sw, sh);
                    }

                    var (dx, dy, dw, dh) = ExtRect(dstRect);
                    var dst = new Microsoft.Xna.Framework.Rectangle(dx, dy, dw, dh);

                    if (_isScript && !_insideUpdate) {
                        result = _context.graphicsHandler.AddAction(
                            () => {
                                _context.graphicsHandler.Draw(
                                    texture2D,
                                    src,
                                    dst,
                                    rotation,
                                    flip,
                                    alpha
                                );
                            }
                        );
                    } else {
                        _context.graphicsHandler.Draw(
                            texture2D,
                            src,
                            dst,
                            rotation,
                            flip,
                            alpha
                        );

                        result = null;
                    }
                }

                break;
            case "Graphics_LoadSound_S": {
                    var path = GetFilePath(EvaluateExpression(arguments[0], true, abort).@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load sound: path does not exist.", location);

                    var soundType = CorLibrary.GetSpecialType(SpecialType.Sound);
                    var soundPtr = CreateObject(soundType);
                    var sound = H(soundPtr);

                    sound[0].data = _context.graphicsHandler.LoadSound(path);

                    result = soundPtr;
                }

                break;
            case "Graphics_PlaySound_S": {
                    var argument = EvaluateExpression(arguments[0], true, abort);
                    var fields = H(argument);
                    double? volume = fields[1].kind == ValueKind.Null ? null : fields[1].@double;
                    bool? loop = fields[2].kind == ValueKind.Null ? null : fields[2].@bool;
                    var soundInstance = fields[0].data;
                    _context.graphicsHandler.PlaySound((SoundEffect)soundInstance, volume, loop);
                }

                break;
            case "Graphics_SetCursorVisibility_B": {
                    var argument = EvaluateExpression(arguments[0], true, abort).@bool;
                    _context.graphicsHandler.SetCursorVisibility(argument);
                }

                break;
            case "Graphics_LockFramerate_I": {
                    var argument = EvaluateExpression(arguments[0], true, abort).int64;
                    _context.graphicsHandler.LockFramerate((int)argument);
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(mapKey);
        }

        return true;

        void DrawRect(bool includeAlpha, out object result) {
            result = null;
            var fields = arguments.Select(a => EvaluateExpression(a, true, abort)).ToArray();
            var rectPtr = fields[0];

            if (rectPtr.kind == ValueKind.Null)
                return;

            var (x, y, w, h) = ExtRect(rectPtr);
            long? r = fields[1].kind == ValueKind.Null ? null : fields[1].int64;
            long? g = fields[2].kind == ValueKind.Null ? null : fields[2].int64;
            long? b = fields[3].kind == ValueKind.Null ? null : fields[3].int64;
            long? a = includeAlpha ? (fields[4].kind == ValueKind.Null ? null : fields[4].int64) : 255;

            if (_isScript && !_insideUpdate) {
                result = _context.graphicsHandler.AddAction(
                    () => { _context.graphicsHandler.DrawRect(x, y, w, h, r, g, b, a); }
                );
            } else {
                _context.graphicsHandler.DrawRect(x, y, w, h, r, g, b, a);
            }
        }

        EvaluatorValue LoadTexture(string path, bool useColorKey = false, long r = 255, long g = 255, long b = 255) {
            var textureType = CorLibrary.GetSpecialType(SpecialType.Texture);
            var texturePointer = CreateObject(textureType);
            var texture = _context.heap[texturePointer.ptr];
            var texture2D = (_context.graphicsHandler?.LoadTexture(path, useColorKey, r, g, b))
                ?? throw new BelteEvaluatorException("Failed to load texture.", location);

            texture.fields[0].data = texture2D;
            texture.fields[1].int64 = texture2D.Width;
            texture.fields[1].kind = ValueKind.Int64;
            texture.fields[2].int64 = texture2D.Height;
            texture.fields[2].kind = ValueKind.Int64;

            return texturePointer;
        }

        void DrawSprite(EvaluatorValue sprite, EvaluatorValue offsetVec, out object result) {
            var fields = H(sprite);
            var (sx, sy, sw, sh) = ExtRect(fields[2]);
            var (dx, dy, dw, dh) = ExtRect(fields[3]);
            long? rotation = fields[1].kind == ValueKind.Null ? null : fields[1].int64;

            var texture = (Texture2D)H(fields[4])[0].data;

            if (!offsetVec.Equals(EvaluatorValue.None)) {
                dx -= (int)H(offsetVec)[0].@double;
                dy -= (int)H(offsetVec)[1].@double;
            }

            if (_isScript && !_insideUpdate) {
                result = _context.graphicsHandler.AddAction(
                    () => {
                        _context.graphicsHandler.DrawSprite(texture, sx, sy, sw, sh, dx, dy, dw, dh, rotation);
                    }
                );
            } else {
                _context.graphicsHandler.DrawSprite(texture, sx, sy, sw, sh, dx, dy, dw, dh, rotation);
                result = null;
            }
        }
    }

    private (int x, int y, int w, int h) ExtRect(EvaluatorValue rect) {
        var fields = H(rect);
        return ((int)fields[0].int64, (int)fields[1].int64, (int)fields[2].int64, (int)fields[3].int64);
    }

    private EvaluatorValue[] H(EvaluatorValue ptr) => _context.heap[ptr.ptr].fields;

    private void StartGraphics(string title, int width, int height, bool usePointClamp, ValueWrapper<bool> abort) {
        _context.maintainThread = true;

        GraphicsHandler.Title = title;
        GraphicsHandler.Width = width;
        GraphicsHandler.Height = height;

        if (_context.graphicsThread is not null && _context.graphicsThread.IsAlive) {
            _context.createWindow = true;
            _context.graphicsHandler?.Exit();
            return;
        }

        _context.graphicsThread = new Thread(() => {
            while (_context.maintainThread) {
                if (_context.createWindow) {
                    _context.createWindow = false;
                    using var graphicsHandler = new GraphicsHandler(this, abort, usePointClamp);
                    _context.graphicsHandler = graphicsHandler;
                    graphicsHandler.Run();
                } else {
                    Thread.Sleep(100);
                }
            }
        }) {
            Name = "Evaluator.GraphicsThread",
            IsBackground = true
        };

        if (OperatingSystem.IsWindows())
            _context.graphicsThread.SetApartmentState(ApartmentState.STA);

        _context.graphicsThread.Start();
    }

    private void UpdateCaller(double deltaTime, ValueWrapper<bool> abort) {
        if (_program.updatePoint is null)
            return;

        var argument = EvaluatorValue.Literal(deltaTime);

        // _insideUpdate prevents adding Graphics calls to _updateActions
        _insideUpdate = true;
        InvokeMethod(_program.updatePoint, _programObject, [argument], abort);
        _insideUpdate = false;

        if (exceptions.Count > 0)
            abort.Value = true;
    }

    #endregion
}
