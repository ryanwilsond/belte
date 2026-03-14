using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
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

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private readonly BoundProgram _program;
    private readonly EvaluatorContext _context;
    private readonly Stack<StackFrame> _stack;

    private EvaluatorValue _programObject;
    private EvaluatorValue _lastValue;
    private bool _hasValue;
    private MethodSymbol _lazyToString;
    private Random _lazyRandom;

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
    internal Evaluator(BoundProgram program, EvaluatorContext context, string[] _) {
        _context = context;
        _context.program = program;
        _program = program;
        _stack = [];
        exceptions = [];
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
    internal object Evaluate(ValueWrapper<bool> abort, out bool hasValue) {
        var entryPoint = _program.entryPoint;

        if (entryPoint is null) {
            hasValue = false;
            return null;
        }

        var entryPointBody = _program.methodBodies[entryPoint];
        var programType = entryPoint.containingType;
        var result = EvaluatorValue.None;

        if (!programType.isStatic) {
            _programObject = CreateObject(programType);
            var constructor = programType.constructors.Where(c => c.parameterCount == 0).FirstOrDefault();

            if (constructor is not null)
                InvokeInstanceMethod(constructor, _programObject, [], abort);

            if (!entryPoint.isStatic)
                result = InvokeInstanceMethod(entryPoint, _programObject, [], abort);
        }

        if (entryPoint.isStatic)
            result = InvokeStaticMethod(entryPoint, [], abort);

        // Wait until Main finishes before the first call of Update
        if (_context.maintainThread) {
            while (_context.graphicsHandler is null)
                ;
        }

        if (_program.updatePoint is not null)
            _context.graphicsHandler?.SetUpdateHandler(UpdateCaller);

        hasValue = _hasValue;
        return hasValue ? EvaluatorValue.Format(result, _context) : null;
    }

    private EvaluatorValue CreateObject(NamedTypeSymbol type) {
        if (!_program.TryGetTypeLayoutIncludingParents(type, out var layout))
            throw new BelteInternalException($"Failed to get type layout ({type})");

        var heapObject = new HeapObject(type, layout.LocalsInOrder().Length);
        var index = _context.heap.Allocate(heapObject, _stack, _context);
        return EvaluatorValue.HeapPtr(index);
    }

    private void IndirectStore(EvaluatorValue lhs, EvaluatorValue value) {
        if (lhs.kind != ValueKind.Ref)
            throw ExceptionUtilities.UnexpectedValue(lhs.kind);

        lhs.loc[lhs.ptr] = value;
    }

    #region Statements

    private EvaluatorValue EvaluateStatement(
        MethodSymbol method,
        BoundBlockStatement block,
        ValueWrapper<bool> abort) {
        _hasValue = false;
        var insideTry = false;

        try {
            var labelToIndex = new Dictionary<LabelSymbol, int>();

            for (var i = 0; i < block.statements.Length; i++) {
                if (block.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;

            while (index < block.statements.Length) {
                if (abort)
                    throw new BelteThreadException();

                var s = block.statements[index];

                if (s.kind is not BoundKind.ReturnStatement)
                    _lastValue = EvaluatorValue.None;

                switch (s.kind) {
                    case BoundKind.NopStatement:
                        index++;
                        break;
                    case BoundKind.ExpressionStatement:
                        EvaluateExpressionStatement((BoundExpressionStatement)s, abort);
                        index++;
                        break;
                    case BoundKind.LabelStatement:
                        index++;
                        break;
                    case BoundKind.GotoStatement:
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.label];
                        break;
                    case BoundKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = EvaluateExpression(cgs.condition, abort);

                        if (condition.kind == ValueKind.Null)
                            throw new BelteNullReferenceException(cgs.condition.syntax.location);

                        if (condition.@bool == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundKind.ReturnStatement:
                        _hasValue = true;

                        if (method.returnsVoid) {
                            if (_lastValue.Equals(EvaluatorValue.None) || !_context.options.isScript)
                                _hasValue = false;

                            return _lastValue;
                        }

                        var returnStatement = (BoundReturnStatement)s;
                        var expression = returnStatement.expression;

                        if (returnStatement.refKind == RefKind.None) {
                            _lastValue = EvaluateExpression(expression, abort);
                        } else {
                            _lastValue = EvaluateAddress(
                                expression,
                                method.refKind == RefKind.RefConst ? AddressKind.ReadOnlyStrict : AddressKind.Writeable,
                                abort
                            );
                        }

                        return _lastValue;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(s.kind);
                }
            }

            return _lastValue;
        } catch (Exception e) {
            if (abort)
                return EvaluatorValue.None;

            if (insideTry)
                throw;

            exceptions.Add(e);
            lastOutputWasPrint = false;
            _hasValue = false;

            if (!_context.options.isScript)
                abort.Value = true;

            return EvaluatorValue.None;
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement node, ValueWrapper<bool> abort) {
        var expression = node.expression;
        var value = EvaluateExpression(expression, abort);

        if (expression.syntax.kind != Syntax.SyntaxKind.LocalDeclarationStatement)
            _lastValue = value;
    }

    #endregion

    #region Expressions

    private EvaluatorValue EvaluateExpression(BoundExpression node, ValueWrapper<bool> abort) {
        if (node.constantValue is not null)
            return EvaluatorValue.Literal(node.constantValue.value, node.constantValue.specialType);

        return node.kind switch {
            BoundKind.ThisExpression => EvaluateThisExpression((BoundThisExpression)node),
            BoundKind.BaseExpression => EvaluateBaseExpression((BoundBaseExpression)node),
            BoundKind.DataContainerExpression => EvaluateDataContainerExpression((BoundDataContainerExpression)node),
            BoundKind.StackSlotExpression => EvaluateStackSlotExpression((BoundStackSlotExpression)node),
            BoundKind.FieldSlotExpression => EvaluateFieldSlotExpression((BoundFieldSlotExpression)node, abort),
            BoundKind.CastExpression => EvaluateCastExpression((BoundCastExpression)node, abort),
            BoundKind.AssignmentOperator => EvaluateAssignmentOperator((BoundAssignmentOperator)node, CodeGenerator.UseKind.UsedAsValue, abort),
            BoundKind.UnaryOperator => EvaluateUnaryOperator((BoundUnaryOperator)node, abort),
            BoundKind.BinaryOperator => EvaluateBinaryOperator((BoundBinaryOperator)node, abort),
            BoundKind.AsOperator => EvaluateAsOperator((BoundAsOperator)node, abort),
            BoundKind.IsOperator => EvaluateIsOperator((BoundIsOperator)node, abort),
            BoundKind.ConditionalOperator => EvaluateConditionalOperator((BoundConditionalOperator)node, abort),
            BoundKind.NullAssertOperator => EvaluateNullAssertOperator((BoundNullAssertOperator)node, abort),
            BoundKind.CallExpression => EvaluateCallExpression((BoundCallExpression)node, CodeGenerator.UseKind.UsedAsValue, abort),
            BoundKind.ObjectCreationExpression => EvaluateObjectCreationExpression((BoundObjectCreationExpression)node, abort),
            BoundKind.ArrayCreationExpression => EvaluateArrayCreationExpression((BoundArrayCreationExpression)node, abort),
            BoundKind.ArrayAccessExpression => EvaluateArrayAccessExpression((BoundArrayAccessExpression)node, abort),
            BoundKind.TypeExpression => EvaluateTypeExpression(),
            BoundKind.TypeOfExpression => EvaluateTypeOfExpression((BoundTypeOfExpression)node),
            BoundKind.MethodGroup => EvaluateMethodGroup((BoundMethodGroup)node),
            _ => throw ExceptionUtilities.UnexpectedValue(node.kind),
        };
    }

    private EvaluatorValue EvaluateThisExpression(BoundThisExpression node) {
        var value = _stack.Peek().values[0];

        if (CodeGenerator.IsValueType(node.type))
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateBaseExpression(BoundBaseExpression node) {
        var value = _stack.Peek().values[0];

        if (CodeGenerator.IsValueType(node.type))
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateTypeOfExpression(BoundTypeOfExpression expression) {
        return EvaluatorValue.Type(expression.sourceType.type);
    }

    private EvaluatorValue EvaluateMethodGroup(BoundMethodGroup methodGroup) {
        return EvaluatorValue.MethodGroup(methodGroup);
    }

    private EvaluatorValue EvaluateTypeExpression() {
        // This should only ever be called when an invalid expression statement makes it through binding without err
        // because script compilation ignores normal expression statement restrictions.
        //
        // `Console;`
        //
        return EvaluatorValue.None;
    }

    private EvaluatorValue EvaluateDataContainerExpression(BoundDataContainerExpression node) {
        var global = node.dataContainer;
        var isRefLocal = global.refKind != RefKind.None;

        if (!_context.TryGetGlobal(node.dataContainer, out var value))
            throw new BelteInternalException($"Attempted to find global '{node.dataContainer.name}' that doesn't exist");

        if (isRefLocal)
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateStackSlotExpression(BoundStackSlotExpression node) {
        var local = node.symbol;
        var isRefLocal = local.GetRefKind() != RefKind.None;

        var value = _stack.Peek().values[node.slot];

        if (isRefLocal)
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateFieldSlotExpression(BoundFieldSlotExpression node, ValueWrapper<bool> abort) {
        var field = node.field;

        var value = EvaluateFieldNoIndirection(node, abort);

        if (field.refKind != RefKind.None)
            return value.loc[value.ptr];

        return value;
    }

    private EvaluatorValue EvaluateFieldNoIndirection(BoundFieldSlotExpression node, ValueWrapper<bool> abort) {
        var field = node.field;
        var receiver = node.receiver;
        var fieldType = field.type;

        if (CodeGenerator.IsValueType(fieldType) && (object)fieldType == receiver.type) {
            return EvaluateExpression(receiver, abort);
        } else {
            var receiverValue = EvaluateFieldLoadReceiver(receiver, abort);
            return _context.heap[receiverValue.ptr].fields[node.slot];
        }
    }

    private EvaluatorValue EvaluateFieldLoadReceiver(BoundExpression receiver, ValueWrapper<bool> abort) {
        if (CodeGenerator.FieldLoadMustUseRef(receiver) || FieldLoadPrefersRef(receiver)) {
            return EvaluateFieldLoadReceiverAddress(receiver, abort, out var expr)
                ? expr
                : EvaluateReceiverRef(receiver, AddressKind.ReadOnly, abort);
        }

        return EvaluateExpression(receiver, abort);
    }

    private bool EvaluateFieldLoadReceiverAddress(
        BoundExpression receiver,
        ValueWrapper<bool> abort,
        out EvaluatorValue expr) {
        if (receiver is null || !CodeGenerator.IsValueType(receiver.type)) {
            expr = EvaluatorValue.None;
            return false;
        } else if (receiver.kind == BoundKind.CastExpression) {
            var conversion = (BoundCastExpression)receiver;

            if (conversion.conversion.kind == ConversionKind.AnyUnboxing) {
                expr = EvaluateExpression(conversion.operand, abort);
                return true;
            }
        } else if (receiver.kind == BoundKind.FieldSlotExpression) {
            var fieldSlot = (BoundFieldSlotExpression)receiver;
            var field = fieldSlot.field;

            if (!field.isStatic && EvaluateFieldLoadReceiverAddress(fieldSlot.receiver, abort, out var nestedExpr)) {
                expr = EvaluatorValue.Ref(_context.heap[nestedExpr.ptr].fields, fieldSlot.slot);
                return true;
            }
        }

        expr = EvaluatorValue.None;
        return false;
    }

    private bool FieldLoadPrefersRef(BoundExpression receiver) {
        if (!receiver.type.IsVerifierValue())
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

    private EvaluatorValue EvaluateCastExpression(BoundCastExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node.operand, abort);

        if (CodeGenerator.IsReferenceType(node.operand.type)) {
            if (node.type.specialType == SpecialType.Nullable)
                return value;
        }

        var isCastable = node.operand.type.specialType == SpecialType.String && node.type.IsPrimitiveType() ||
            node.type.specialType == SpecialType.String && node.operand.type.IsPrimitiveType();

        var involvesRefTypes = !isCastable && (node.operand.type.IsVerifierReference() ||
            (node.type.IsVerifierReference() && node.type.specialType != SpecialType.String));

        switch (node.conversion.kind) {
            case ConversionKind.Identity:
            case ConversionKind.Implicit when involvesRefTypes:
            case ConversionKind.ImplicitReference:
            case ConversionKind.AnyBoxing:
            case ConversionKind.Explicit when involvesRefTypes:
            case ConversionKind.ExplicitReference:
            case ConversionKind.AnyUnboxing:
                return value;
            case ConversionKind.Implicit:
            case ConversionKind.Explicit:
                return EvaluateConvertCallOrNumericConversion(node, value);
            default:
                throw ExceptionUtilities.UnexpectedValue(node.conversion.kind);
        }
    }

    private EvaluatorValue EvaluateConvertCallOrNumericConversion(BoundCastExpression node, EvaluatorValue value) {
        var fromType = node.operand.type.specialType;
        var toType = node.type.specialType;

        switch (fromType, toType) {
            case (SpecialType.String, SpecialType.Bool):
                value.@bool = Convert.ToBoolean(value.@string);
                value.kind = ValueKind.Bool;
                break;
            case (SpecialType.String, SpecialType.Int):
                value.int64 = Convert.ToInt64(value.@string);
                value.kind = ValueKind.Int64;
                break;
            case (SpecialType.Decimal, SpecialType.Int):
                value.int64 = Convert.ToInt64(value.@double);
                value.kind = ValueKind.Int64;
                break;
            case (SpecialType.String, SpecialType.Decimal):
                value.@double = Convert.ToDouble(value.@string);
                value.kind = ValueKind.Double;
                break;
            case (SpecialType.Int, SpecialType.Decimal):
                value.@double = Convert.ToDouble(value.int64);
                value.kind = ValueKind.Double;
                break;
            case (SpecialType.Int, SpecialType.String):
                value.@string = Convert.ToString(value.int64);
                value.kind = ValueKind.String;
                break;
            case (SpecialType.Decimal, SpecialType.String):
                value.@string = Convert.ToString(value.@double);
                value.kind = ValueKind.String;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue((fromType, toType));
        }

        return value;
    }

    private EvaluatorValue EvaluateObjectCreationExpression(
        BoundObjectCreationExpression node,
        ValueWrapper<bool> abort) {
        if (node.constructor.originalDefinition == CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_ctor))
            return EvaluateExpression(node.arguments[0], abort);

        var type = (NamedTypeSymbol)node.type;
        var value = CreateObject(type);

        var method = node.constructor;
        var evaluatedArguments = EvaluateArguments(node.arguments, method.parameters, node.argumentRefKinds, abort);
        InvokeInstanceMethod(method, value, evaluatedArguments, abort);

        return value;
    }

    private EvaluatorValue EvaluateArrayAccessExpression(BoundArrayAccessExpression node, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, abort);

        if (receiver.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.syntax.location);

        var index = (int)EvaluateExpression(node.index, abort).int64;

        if (index >= _context.heap[receiver.ptr].fields.Length)
            throw new BelteIndexOutOfRangeException(node.syntax.location);

        return _context.heap[receiver.ptr].fields[index];
    }

    private EvaluatorValue EvaluateArrayCreationExpression(
        BoundArrayCreationExpression node,
        ValueWrapper<bool> abort) {
        var sizes = node.sizes.Select(s => (int)EvaluateExpression(s, abort).int64);
        var array = CreateArray((ArrayTypeSymbol)node.type.StrippedType(), sizes.ToArray(), 0);

        if (node.initializer is BoundInitializerList initList)
            EvaluateInitializerList(array, initList, abort);

        var index = _context.heap.Allocate(array, _stack, _context);
        return EvaluatorValue.HeapPtr(index);
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
                elements[i] = EvaluateExpression(item, abort);
            }
        }
    }

    private HeapObject CreateArray(ArrayTypeSymbol type, int[] sizes, int depth) {
        var length = sizes[depth];
        var elements = new EvaluatorValue[length];

        if (depth == sizes.Length - 1) {
            for (var i = 0; i < length; i++)
                elements[i] = GetDefaultValue(type.elementType);
        } else {
            for (var i = 0; i < length; i++) {
                var array = CreateArray(type, sizes, depth + 1);
                var index = _context.heap.Allocate(array, _stack, _context);
                elements[i] = EvaluatorValue.HeapPtr(index);
            }
        }

        return new HeapObject(type, elements);
    }

    private EvaluatorValue GetDefaultValue(TypeSymbol type) {
        return (!type.IsNullableType() && type.IsVerifierValue())
            ? EvaluatorValue.Literal(LiteralUtilities.GetDefaultValue(type.specialType), type.specialType)
            : EvaluatorValue.Null;
    }

    #endregion

    #region Operators

    private EvaluatorValue EvaluateConditionalOperator(BoundConditionalOperator node, ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(node.condition, abort).@bool;

        if (condition)
            return EvaluateExpression(node.trueExpression, abort);
        else
            return EvaluateExpression(node.falseExpression, abort);
    }

    private EvaluatorValue EvaluateNullAssertOperator(BoundNullAssertOperator node, ValueWrapper<bool> abort) {
        if (!node.throwIfNull)
            return EvaluateExpression(node.operand, abort);

        return NullAssertValue(node.operand, abort);
    }

    private EvaluatorValue NullAssertValue(BoundExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node, abort);

        if (value.kind == ValueKind.Null)
            throw new BelteNullReferenceException(node.syntax.location);

        return value;
    }

    private EvaluatorValue EvaluateIsOperator(BoundIsOperator node, ValueWrapper<bool> abort) {
        var operand = node.left;
        var value = EvaluateExpression(operand, abort);
        var targetType = node.right.type.StrippedType();

        if (value.kind == ValueKind.Null) {
            value.@bool = node.isNot;
            value.kind = ValueKind.Bool;
            return value;
        }

        if (value.kind == ValueKind.Int64 && targetType.specialType == SpecialType.Int ||
            value.kind == ValueKind.Bool && targetType.specialType == SpecialType.Bool ||
            value.kind == ValueKind.String && targetType.specialType == SpecialType.String ||
            value.kind == ValueKind.Double && targetType.specialType == SpecialType.Decimal ||
            targetType.specialType == SpecialType.Any) {
            value.@bool = !node.isNot;
            value.kind = ValueKind.Bool;
            return value;
        }

        if (value.kind == ValueKind.HeapPtr) {
            var operandType = _context.heap[value.ptr].type;

            if (operandType.InheritsFromIgnoringConstruction((NamedTypeSymbol)targetType)) {
                value.@bool = !node.isNot;
                value.kind = ValueKind.Bool;
                return value;
            }
        }

        value.@bool = node.isNot;
        value.kind = ValueKind.Bool;
        return value;
    }

    private EvaluatorValue EvaluateAsOperator(BoundAsOperator node, ValueWrapper<bool> abort) {
        var operand = node.left;
        var value = EvaluateExpression(operand, abort);

        if (value.kind == ValueKind.Null)
            return value;

        var operandType = operand.type;
        var targetType = node.type;

        if (operandType.InheritsFromIgnoringConstruction((NamedTypeSymbol)targetType))
            return value;

        return EvaluatorValue.Null;
    }

    private EvaluatorValue EvaluateAssignmentOperator(
        BoundAssignmentOperator node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        if (node.left is BoundDataContainerExpression)
            return EvaluateGlobalAssignment(node, useKind, abort);

        var lhs = EvaluateAssignmentPreamble(node, abort);
        var value = EvaluateAssignmentValue(node, abort);

        EvaluateStore(node, lhs, value);

        return EvaluateAssignmentPostfix(node, value, useKind);
    }

    private void EvaluateStore(BoundAssignmentOperator node, EvaluatorValue lhs, EvaluatorValue value) {
        var expression = node.left;

        switch (expression.kind) {
            case BoundKind.FieldSlotExpression:
                var field = ((BoundFieldSlotExpression)expression).field;

                if (field.refKind != RefKind.None && !node.isRef) {
                    IndirectStore(lhs, value);
                    // TODO Is this correct? Or is it really a double indirect store above
                    throw ExceptionUtilities.Unreachable();
                } else {
                    IndirectStore(lhs, value);
                }

                break;
            case BoundKind.DataContainerExpression:
                var local = (BoundDataContainerExpression)expression;

                if (local.dataContainer.refKind != RefKind.None && !node.isRef) {
                    IndirectStore(lhs, value);
                    // TODO Is this correct? Or is it really a double indirect store above
                    throw ExceptionUtilities.Unreachable();
                } else {
                    IndirectStore(lhs, value);
                }

                break;
            case BoundKind.StackSlotExpression:
                var symbol = ((BoundStackSlotExpression)expression).symbol;

                if (symbol.GetRefKind() != RefKind.None && !node.isRef) {
                    IndirectStore(lhs, value);
                    // TODO Is this correct? Or is it really a double indirect store above
                    throw ExceptionUtilities.Unreachable();
                } else {
                    IndirectStore(lhs, value);
                }

                break;
            case BoundKind.ArrayAccessExpression:
                IndirectStore(lhs, value);
                break;
            case BoundKind.ThisExpression:
                lhs.ptr = value.ptr;
                break;
            case BoundKind.ConditionalOperator:
                IndirectStore(lhs, value);
                break;
            case BoundKind.CallExpression:
                IndirectStore(lhs, value);
                break;
            case BoundKind.AssignmentOperator:
                var nested = (BoundAssignmentOperator)expression;

                if (!nested.isRef)
                    goto default;

                IndirectStore(lhs, value);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.kind);
        }
    }

    private EvaluatorValue EvaluateGlobalAssignment(
        BoundAssignmentOperator node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        var global = (node.left as BoundDataContainerExpression).dataContainer;
        var value = EvaluateAssignmentValue(node, abort);

        if (global.refKind != RefKind.None && !node.isRef) {
            _context.TryGetGlobal(global, out var indirect);
            IndirectStore(indirect, value);
        } else {
            _context.AddOrUpdateGlobal(global, value);
        }

        return EvaluateAssignmentPostfix(node, value, useKind);
    }

    private EvaluatorValue EvaluateAssignmentPostfix(
        BoundAssignmentOperator node,
        EvaluatorValue value,
        CodeGenerator.UseKind useKind) {
        if (useKind == CodeGenerator.UseKind.UsedAsValue && node.isRef)
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
                        expr = EvaluateFieldNoIndirection(left, abort);
                    else
                        expr = EvaluateReceiverRef(left.receiver, AddressKind.Writeable, abort);

                    expr = EvaluatorValue.Ref(_context.heap[expr.ptr].fields, left.slot);
                }

                break;
            case BoundKind.StackSlotExpression: {
                    var left = (BoundStackSlotExpression)assignmentTarget;
                    expr = EvaluatorValue.Ref(_stack.Peek().values, left.slot);
                }

                break;
            case BoundKind.ArrayAccessExpression: {
                    var left = (BoundArrayAccessExpression)assignmentTarget;
                    var receiver = EvaluateExpression(left.receiver, abort);
                    var index = (int)EvaluateExpression(left.index, abort).int64;
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
                    expr = EvaluateCallExpression(left, CodeGenerator.UseKind.UsedAsAddress, abort);
                }

                break;
            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)assignmentTarget;

                if (!assignment.isRef)
                    goto default;

                expr = EvaluateAssignmentOperator(assignment, CodeGenerator.UseKind.UsedAsAddress, abort);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(assignmentTarget.kind);
        }

        return expr;
    }

    private EvaluatorValue EvaluateAssignmentValue(BoundAssignmentOperator node, ValueWrapper<bool> abort) {
        if (!node.isRef) {
            return EvaluateExpression(node.right, abort);
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

    private EvaluatorValue EvaluateUnaryOperator(BoundUnaryOperator node, ValueWrapper<bool> abort) {
        var operatorKind = node.operatorKind;
        var operand = EvaluateExpression(node.operand, abort);

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

    private EvaluatorValue EvaluateBinaryOperator(BoundBinaryOperator node, ValueWrapper<bool> abort) {
        var operatorKind = node.operatorKind;
        var op = operatorKind.Operator();
        var left = EvaluateExpression(node.left, abort);

        if (op is BinaryOperatorKind.Equal or BinaryOperatorKind.NotEqual) {
            if (node.right.IsLiteralNull()) {
                return EvaluatorValue.Literal(
                    left.kind == ValueKind.Null == (op == BinaryOperatorKind.Equal),
                    SpecialType.Bool
                );
            }
        }

        if (left.kind == ValueKind.Null)
            return left;

        if (operatorKind == BinaryOperatorKind.BoolConditionalAnd) {
            if (!left.@bool)
                return left;

            return EvaluateExpression(node.right, abort);
        }

        if (operatorKind == BinaryOperatorKind.BoolConditionalOr) {
            if (left.@bool)
                return left;

            return EvaluateExpression(node.right, abort);
        }

        var right = EvaluateExpression(node.right, abort);

        if (right.kind == ValueKind.Null)
            return right;

        var operandType = operatorKind.OperandTypes();

        if (op is BinaryOperatorKind.Equal or BinaryOperatorKind.NotEqual) {
            if (node.right.IsLiteralNull()) {
                var isNull = left.kind == ValueKind.Null;
                return EvaluatorValue.Literal(isNull == (op == BinaryOperatorKind.Equal), SpecialType.Bool);
            }

            switch (operandType) {
                case BinaryOperatorKind.Int:
                    left.@bool = left.int64 == right.int64;
                    break;
                case BinaryOperatorKind.Decimal:
                    left.@bool = left.@double == right.@double;
                    break;
                case BinaryOperatorKind.Bool:
                    left.@bool = left.@bool == right.@bool;
                    break;
                case BinaryOperatorKind.String:
                    left.@bool = left.@string == right.@string;
                    break;
                case BinaryOperatorKind.Object:
                    left.@bool = left.ptr == right.ptr;
                    break;
                case BinaryOperatorKind.Type:
                    left.@bool = ((TypeSymbol)left.type).Equals((TypeSymbol)right.type);
                    break;
            }

            left.kind = ValueKind.Bool;

            if (op == BinaryOperatorKind.NotEqual)
                left.@bool = !left.@bool;

            return left;
        }

        switch (op) {
            case BinaryOperatorKind.Addition:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 += right.int64;
                else if (operandType == BinaryOperatorKind.String)
                    left.@string += right.@string;
                else
                    left.@double += right.@double;

                break;
            case BinaryOperatorKind.Subtraction:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 -= right.int64;
                else
                    left.@double -= right.@double;

                break;
            case BinaryOperatorKind.Multiplication:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 *= right.int64;
                else
                    left.@double *= right.@double;

                break;
            case BinaryOperatorKind.Division:
                if (operandType == BinaryOperatorKind.Int) {
                    if (right.int64 == 0)
                        throw new BelteDivideByZeroException(node.syntax.location);

                    left.int64 /= right.int64;
                } else {
                    if (right.@double == 0)
                        throw new BelteDivideByZeroException(node.syntax.location);

                    left.@double /= right.@double;
                }

                break;
            case BinaryOperatorKind.LessThan:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 < right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double < right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.GreaterThan:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 > right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double > right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.LessThanOrEqual:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 <= right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double <= right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.GreaterThanOrEqual:
                if (operandType == BinaryOperatorKind.Int) {
                    left.@bool = left.int64 >= right.int64;
                    left.kind = ValueKind.Bool;
                } else {
                    left.@bool = left.@double >= right.@double;
                    left.kind = ValueKind.Bool;
                }

                break;
            case BinaryOperatorKind.And:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 &= right.int64;
                else
                    left.@bool &= right.@bool;

                break;
            case BinaryOperatorKind.Or:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 |= right.int64;
                else
                    left.@bool |= right.@bool;

                break;
            case BinaryOperatorKind.Xor:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 ^= right.int64;
                else
                    left.@bool ^= right.@bool;

                break;
            case BinaryOperatorKind.LeftShift:
                left.int64 <<= (int)right.int64;
                break;
            case BinaryOperatorKind.RightShift:
                left.int64 >>= (int)right.int64;
                break;
            case BinaryOperatorKind.UnsignedRightShift:
                left.int64 >>>= (int)right.int64;
                break;
            case BinaryOperatorKind.Modulo:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 %= right.int64;
                else
                    left.@double %= right.@double;

                break;
            case BinaryOperatorKind.Power:
                if (operandType == BinaryOperatorKind.Int)
                    left.int64 = (long)Math.Pow(left.int64, right.int64);
                else
                    left.@double = Math.Pow(left.@double, right.@double);

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(operatorKind);
        }

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
                if (CodeGenerator.IsValueType(node.type)) {
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

                if (CodeGenerator.UseCallResultAsAddress(call, addressKind))
                    return EvaluateCallExpression(call, CodeGenerator.UseKind.UsedAsAddress, abort);

                goto default;
            case BoundKind.ConditionalOperator:
                if (!HasHome(node, addressKind))
                    goto default;

                return EvaluateConditionalOperatorAddress((BoundConditionalOperator)node, addressKind, abort);
            case BoundKind.ThrowExpression:
                return EvaluateExpression(node, abort);
            default:
                return EvaluateAddressOfTempClone(node, abort);
        }
    }

    private EvaluatorValue EvaluateConditionalOperatorAddress(
        BoundConditionalOperator node,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var condition = EvaluateExpression(node.condition, abort).@bool;

        if (condition)
            return EvaluateAddress(node.trueExpression, addressKind, abort);
        else
            return EvaluateAddress(node.falseExpression, addressKind, abort);
    }

    private EvaluatorValue EvaluateArrayElementAddress(BoundArrayAccessExpression node, ValueWrapper<bool> abort) {
        var receiver = EvaluateExpression(node.receiver, abort);
        var index = (int)EvaluateExpression(node.index, abort).int64;

        if (((ArrayTypeSymbol)node.receiver.type.StrippedType()).isSZArray)
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

        throw ExceptionUtilities.Unreachable();

        // TODO what is receiver exactly?
        // if (field.refKind == RefKind.None)
        //     return ;
    }

    private EvaluatorValue EvaluateReceiverRef(
        BoundExpression receiver,
        AddressKind addressKind,
        ValueWrapper<bool> abort) {
        var receiverType = receiver.type;

        if (receiverType.IsVerifierReference())
            return EvaluateExpression(receiver, abort);

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

        return EvaluatorValue.Ref(_stack.Peek().values, node.slot);
    }

    private EvaluatorValue EvaluateAddressOfTempClone(BoundExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node, abort);
        var temp = AllocateTemp(node.type);
        var frame = _stack.Peek();
        frame.values[temp.slot] = value;
        return EvaluatorValue.Ref(frame.values, temp.slot);
    }

    private VariableDefinition AllocateTemp(
        TypeSymbol type,
        LocalSlotConstraints slotConstraints = LocalSlotConstraints.None) {
        var frame = _stack.Peek();
        frame.Resize();
        return frame.layout.AllocateSlot(type, slotConstraints);
    }

    #endregion

    #region Calls

    private EvaluatorValue EvaluateCallExpression(
        BoundCallExpression node,
        CodeGenerator.UseKind useKind,
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
                var array = new HeapObject((ArrayTypeSymbol)node.type.StrippedType(), a);
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
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        var method = node.method;
        var arguments = node.arguments;

        var evaluatedArguments = EvaluateArguments(arguments, method.parameters, node.argumentRefKinds, abort);

        var value = InvokeStaticMethod(method, evaluatedArguments, abort);

        if (useKind == CodeGenerator.UseKind.UsedAsValue && method.refKind != RefKind.None)
            return value.loc[value.ptr];
        else
            return value;
    }

    private EvaluatorValue EvaluateInstanceCallExpression(
        BoundCallExpression node,
        CodeGenerator.UseKind useKind,
        ValueWrapper<bool> abort) {
        var method = node.method;
        var arguments = node.arguments;
        var receiver = node.receiver;

        var thisParameter = EvaluateExpression(receiver, abort);

        if (thisParameter.kind == ValueKind.Null)
            throw new BelteNullReferenceException(receiver.syntax.location);

        var evaluatedArguments = EvaluateArguments(arguments, method.parameters, node.argumentRefKinds, abort);

        if (method.isAbstract || method.isVirtual) {
            var typeToLookup = receiver.kind == BoundKind.BaseExpression
                ? receiver.type.StrippedType()
                : _context.heap[thisParameter.ptr].type.StrippedType();

            var newMethod = typeToLookup
                .GetMembersUnordered()
                .Where(s => s is MethodSymbol m && m.overriddenMethod == method)
                .FirstOrDefault() as MethodSymbol;

            if (newMethod is not null)
                method = newMethod;
        }

        var value = InvokeInstanceMethod(method, thisParameter, evaluatedArguments, abort);

        if (useKind == CodeGenerator.UseKind.UsedAsValue && method.refKind != RefKind.None)
            return value.loc[value.ptr];
        else
            return value;
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
            return EvaluateExpression(argument, abort);

        return EvaluateAddress(argument, AddressKind.Writeable, abort);
    }

    private EvaluatorValue InvokeInstanceMethod(
        MethodSymbol method,
        EvaluatorValue thisParameter,
        EvaluatorValue[] arguments,
        ValueWrapper<bool> abort) {
        if (!_program.TryGetMethodBodyIncludingParents(method, out var body))
            throw new BelteInternalException($"Failed to get method body ({method})");

        if (!_program.TryGetMethodLayoutIncludingParents(method, out var layout)) {
            layout = new Lowering.EvaluatorSlotManager(method);

            if (!thisParameter.Equals(EvaluatorValue.None))
                layout.AllocateSlot(method.thisParameter.type, LocalSlotConstraints.None);
        }

        var frame = new StackFrame(layout);

        if (!thisParameter.Equals(EvaluatorValue.None))
            frame.values[0] = thisParameter;

        for (var i = 0; i < arguments.Length; i++) {
            var slot = layout.GetLocal(method.parameters[i]).slot;
            frame.values[slot] = arguments[i];
        }

        _stack.Push(frame);

        var result = EvaluateStatement(method, body, abort);

        _stack.Pop();

        return result;
    }

    private EvaluatorValue InvokeStaticMethod(
        MethodSymbol method,
        EvaluatorValue[] arguments,
        ValueWrapper<bool> abort) {
        return InvokeInstanceMethod(method, EvaluatorValue.None, arguments, abort);
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

        // First check if we are in a graphics project before comparing
        // (otherwise would unnecessarily create the overhead of constructing the Graphics type)
        if (_context.options.outputKind == OutputKind.GraphicsApplication) {
            if (method.containingType.Equals(GraphicsLibrary.Graphics.underlyingNamedType))
                return HandleGraphicsCall(location, method, arguments, abort, out result);
        }

        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (mapKey == "Nullable_get_Value") {
            result = NullAssertValue(receiver, abort);
            return true;
        }

        if (mapKey == "Nullable_get_HasValue") {
            var receiverValue = EvaluateExpression(receiver, abort);
            result = EvaluatorValue.Literal(receiverValue.kind != ValueKind.Null, SpecialType.Bool);
            return true;
        }

        if (method.containingType.Equals(StandardLibrary.Console.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Math.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.LowLevel.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Time.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Directory.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.File.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.String.underlyingNamedType) ||
            method.containingType.Equals(StandardLibrary.Random.underlyingNamedType)) {
            switch (mapKey) {
                case "LowLevel_GetHashCode_O":
                    result = _context.heap[EvaluateExpression(arguments[0], abort).ptr].GetHashCode();
                    return true;
                case "LowLevel_GetTypeName_O":
                    result = _context.heap[EvaluateExpression(arguments[0], abort).ptr].type.name;
                    return true;
                case "Random_RandInt_I?":
                    _lazyRandom ??= new Random();
                    var max = (int)EvaluateExpression(arguments[0], abort).int64;
                    result = _lazyRandom.NextInt64(max);
                    return true;
                case "LowLevel_ThrowNullConditionException":
                    throw new BelteNullConditionException(location);
                case "Random_Random":
                    _lazyRandom ??= new Random();
                    result = _lazyRandom.NextDouble();
                    return true;
                case "LowLevel_Sort_A?": {
                        var arrayPtr = EvaluateExpression(arguments[0], abort);

                        if (arrayPtr.kind != ValueKind.HeapPtr)
                            return true;

                        var array = _context.heap[arrayPtr.ptr];
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
                        var args = arguments.Select(a => EvaluateExpression(a, abort).@string).ToArray();
                        var text = args[0];
                        var separator = args[1];
                        var res = text.Split(separator);

                        result = res.Select(r => EvaluatorValue.Literal(r, SpecialType.String)).ToArray();
                    }

                    return true;
                case "Console_Print_S?":
                case "Console_Print_A?":
                case "Console_Print_O?":
                    printed = true;

                    if (arguments[0].type.StrippedType().isObjectType) {
                        var argument = EvaluateExpression(arguments[0], abort);
                        var toStringResult = InvokeStaticMethod(_toStringMethod, [argument], abort);
                        var func = StandardLibrary.EvaluatorMap[mapKey];
                        result = func(toStringResult, null, null);
                        return true;
                    }

                    break;
            }

            var function = StandardLibrary.EvaluatorMap[mapKey];
            var valueArguments = arguments
                .Select(a => EvaluatorValue.Format(EvaluateExpression(a, abort), _context))
                .ToArray();

            switch (mapKey) {
                case "File_Copy_SS":
                    valueArguments[1] = GetFilePath((string)valueArguments[1], location);
                    goto case "Directory_Create_S";
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
            return false;
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
        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (mapKey == "Graphics_Initialize_SIIB") {
            var valueArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();

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
            throw new BelteEvaluatorException("All Graphics calls must come after Graphics.Initialize", location);

        while (_context.graphicsHandler?.GraphicsDevice is null)
            Thread.SpinWait(1);

        switch (mapKey) {
            case "Graphics_LoadTexture_S": {
                    var path = GetFilePath(EvaluateExpression(arguments[0], abort).@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load texture path does not exist", location);

                    result = LoadTexture(path);
                }

                break;
            case "Graphics_LoadTexture_SIII": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var path = GetFilePath(evaluatedArguments[0].@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load texture path does not exist", location);

                    var r = evaluatedArguments[1].int64;
                    var g = evaluatedArguments[2].int64;
                    var b = evaluatedArguments[3].int64;

                    result = LoadTexture(path, true, r, g, b);
                }

                break;
            case "Graphics_LoadSprite_SV?V?I?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var path = GetFilePath(evaluatedArguments[0].@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load sprite: path does not exist", location);

                    var spriteType = CorLibrary.GetSpecialType(SpecialType.Sprite);
                    var sprite = CreateObject(spriteType);

                    InvokeInstanceMethod(
                        spriteType.constructors[0],
                        sprite,
                        [
                            LoadTexture(path),
                            evaluatedArguments[1],
                            evaluatedArguments[2],
                            evaluatedArguments[3]
                        ],
                        abort
                    );

                    result = sprite;
                }

                break;
            case "Graphics_DrawSprite_S?": {
                    var argument = EvaluateExpression(arguments[0], abort);

                    if (argument.kind == ValueKind.Null)
                        return true;

                    DrawSprite(argument, EvaluatorValue.None, out result);
                }

                break;
            case "Graphics_DrawSprite_S?V?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var spritePtr = evaluatedArguments[0];

                    if (spritePtr.kind == ValueKind.Null)
                        return true;

                    DrawSprite(spritePtr, evaluatedArguments[1], out result);
                }

                break;
            case "Graphics_StopDraw_I?": {
                    var argument = EvaluateExpression(arguments[0], abort);

                    if (argument.kind == ValueKind.Null)
                        return true;

                    _context.graphicsHandler.RemoveAction((int)argument.int64);
                }

                break;
            case "Graphics_LoadText_S?SV?DD?I?I?I?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var path = GetFilePath(evaluatedArguments[1].@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load text: path does not exist", location);

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

                    result = textPtr;
                }

                break;
            case "Graphics_DrawText_T?": {
                    var argument = EvaluateExpression(arguments[0], abort);

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

                    if (fields[0].data is not DynamicSpriteFont spriteFont)
                        throw new BelteEvaluatorException("Cannot draw text: invalid text object", location);

                    if (_context.options.isScript) {
                        result = _context.graphicsHandler.AddAction(
                            () => { _context.graphicsHandler.DrawText(spriteFont, text, posX, posY, r, g, b); }
                        );
                    } else {
                        _context.graphicsHandler.DrawText(spriteFont, text, posX, posY, r, g, b);
                    }
                }

                break;
            case "Graphics_GetKey_S": {
                    var argument = EvaluateExpression(arguments[0], abort).@string;
                    result = _context.graphicsHandler.GetKey(argument);
                }

                break;
            case "Graphics_GetMouseButton_S": {
                    var argument = EvaluateExpression(arguments[0], abort).@string;
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

                    InvokeInstanceMethod(
                        vecType.constructors[0],
                        vec,
                        [
                            EvaluatorValue.Literal(Convert.ToDouble(x), SpecialType.Decimal),
                            EvaluatorValue.Literal(Convert.ToDouble(y), SpecialType.Decimal)
                        ],
                        abort
                    );

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
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();

                    var r = evaluatedArguments[0].int64;
                    var g = evaluatedArguments[1].int64;
                    var b = evaluatedArguments[2].int64;

                    if (_context.options.isScript) {
                        result = _context.graphicsHandler.AddAction(
                            () => { _context.graphicsHandler.Fill(r, g, b); }
                        );
                    } else {
                        _context.graphicsHandler.Fill(r, g, b);
                    }
                }

                break;
            case "Graphics_Draw_T?R?R?I?B?D?": {
                    var evaluatedArguments = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
                    var texturePtr = evaluatedArguments[0];

                    if (H(texturePtr)[0].data is not Texture2D texture2D)
                        throw new BelteEvaluatorException("Cannot draw: null texture", location);

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

                    if (_context.options.isScript) {
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
                    var path = GetFilePath(EvaluateExpression(arguments[0], abort).@string, location)
                        ?? throw new BelteEvaluatorException("Cannot load sound: path does not exist", location);

                    var soundType = CorLibrary.GetSpecialType(SpecialType.Sound);
                    var soundPtr = CreateObject(soundType);
                    var sound = H(soundPtr);

                    sound[0].data = _context.graphicsHandler.LoadSound(path);

                    result = soundPtr;
                }

                break;
            case "Graphics_PlaySound_S": {
                    var argument = EvaluateExpression(arguments[0], abort);
                    var fields = H(argument);
                    double? volume = fields[1].kind == ValueKind.Null ? null : fields[1].@double;
                    bool? loop = fields[2].kind == ValueKind.Null ? null : fields[2].@bool;
                    var soundInstance = fields[0].data;
                    _context.graphicsHandler.PlaySound((SoundEffect)soundInstance, volume, loop);
                }

                break;
            case "Graphics_SetCursorVisibility_B": {
                    var argument = EvaluateExpression(arguments[0], abort).@bool;
                    _context.graphicsHandler.SetCursorVisibility(argument);
                }

                break;
            case "Graphics_LockFramerate_I": {
                    var argument = EvaluateExpression(arguments[0], abort).int64;
                    _context.graphicsHandler.LockFramerate((int)argument);
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(mapKey);
        }

        return true;

        void DrawRect(bool includeAlpha, out object result) {
            result = null;
            var fields = arguments.Select(a => EvaluateExpression(a, abort)).ToArray();
            var rectPtr = fields[0];

            if (rectPtr.kind == ValueKind.Null)
                return;

            var (x, y, w, h) = ExtRect(rectPtr);
            long? r = fields[1].kind == ValueKind.Null ? null : fields[1].int64;
            long? g = fields[2].kind == ValueKind.Null ? null : fields[2].int64;
            long? b = fields[3].kind == ValueKind.Null ? null : fields[3].int64;
            long? a = includeAlpha ? (fields[4].kind == ValueKind.Null ? null : fields[4].int64) : 255;

            if (_context.options.isScript) {
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
            var texture2D = _context.graphicsHandler?.LoadTexture(path, useColorKey, r, g, b);

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

            if (H(fields[4])[0].data is not Texture2D texture)
                throw new BelteEvaluatorException("Cannot draw sprite: it has a null texture", location);

            if (!offsetVec.Equals(EvaluatorValue.None)) {
                dx -= (int)H(offsetVec)[0].int64;
                dy -= (int)H(offsetVec)[1].int64;
            }

            if (_context.options.isScript) {
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

        var argument = EvaluatorValue.Literal(deltaTime, SpecialType.Decimal);
        InvokeInstanceMethod(_program.updatePoint, _programObject, [argument], abort);

        if (exceptions.Count > 0)
            abort.Value = true;
    }

    #endregion
}
