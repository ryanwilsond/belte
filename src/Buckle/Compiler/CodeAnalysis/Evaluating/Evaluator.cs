using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries.Standard;
using Buckle.Utilities;
using Shared;
using static Buckle.Utilities.MethodUtilities;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private readonly BoundProgram _program;
    private readonly string[] _arguments;
    private readonly Dictionary<MethodSymbol, BoundBlockStatement> _methods =
        new Dictionary<MethodSymbol, BoundBlockStatement>();
    private readonly Dictionary<IVariableSymbol, EvaluatorObject> _globals;
    private readonly Stack<Dictionary<IVariableSymbol, EvaluatorObject>> _locals =
        new Stack<Dictionary<IVariableSymbol, EvaluatorObject>>();
    private readonly Stack<EvaluatorObject> _enclosingTypes = new Stack<EvaluatorObject>();

    private EvaluatorObject _lastValue;
    private Random _random;
    private bool _hasValue;
    private int _templateConstantDepth;

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" />.</param>
    /// <param name="globals">Globals.</param>
    /// <param name="arguments">Runtime arguments.</param>
    internal Evaluator(
        BoundProgram program,
        Dictionary<IVariableSymbol, EvaluatorObject> globals,
        string[] arguments) {
        diagnostics = new BelteDiagnosticQueue();
        exceptions = new List<Exception>();
        _arguments = arguments;
        _program = program;
        _globals = globals;
        _locals.Push(new Dictionary<IVariableSymbol, EvaluatorObject>());
        _templateConstantDepth = 0;

        var current = program;

        while (current != null) {
            foreach (var (method, body) in current.methodBodies)
                _methods.Add(method, body);

            current = current.previous;
        }
    }

    /// <summary>
    /// If the last output to the terminal was a `Print`, and not a `PrintLine`, meaning the caller might want to write
    /// an extra line to prevent formatting problems.
    /// </summary>
    internal bool lastOutputWasPrint { get; private set; }

    /// <summary>
    /// All thrown exceptions during evaluation.
    /// </summary>
    internal List<Exception> exceptions { get; set; }

    /// <summary>
    /// Diagnostics specific to the <see cref="Evaluator" />.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Evaluate the provided <see cref="BoundProgram" />.
    /// </summary>
    /// <param name="abort">External flag used to cancel evaluation.</param>
    /// <param name="hasValue">If the evaluation had a returned result.</param>
    /// <returns>Result of <see cref="BoundProgram" /> (if applicable).</returns>
    internal object Evaluate(ValueWrapper<bool> abort, out bool hasValue) {
        if (_program.entryPoint is null) {
            hasValue = false;
            return null;
        }

        var body = LookupMethod(_methods, _program.entryPoint);

        if (_program.entryPoint.parameters.Length == 1) {
            var args = ImmutableArray.CreateBuilder<BoundConstant>();

            foreach (var arg in _arguments)
                args.Add(new BoundConstant(arg));

            var list = EvaluateObjectCreationExpression(new BoundObjectCreationExpression(
                _program.entryPoint.parameters[0].type,
                (_program.entryPoint.parameters[0].type.typeSymbol as NamedTypeSymbol).constructors[2],
                [new BoundInitializerListExpression(
                    new BoundConstant(args.ToImmutable()),
                    new BoundType(
                        TypeSymbol.String,
                        dimensions: 1,
                        sizes: [new BoundLiteralExpression(_arguments.Length)]
                    )
                )]
            ), abort);

            _locals.Push(new Dictionary<IVariableSymbol, EvaluatorObject>() {
                [_program.entryPoint.parameters[0]] = list
            });
        }

        var result = EvaluateStatement(body, abort, out _);
        hasValue = _hasValue;

        return Value(result, true);
    }

    private object GetVariableValue(VariableSymbol variable, bool traceCollections = false) {
        var value = Get(variable);

        try {
            return Value(value, traceCollections);
        } catch (BelteInternalException) {
            throw new BelteEvaluatorException(
                $"Reference cannot be deferred (what it was referencing was likely redefined)"
            );
        }
    }

    private static bool TryGet(
        Dictionary<IVariableSymbol, EvaluatorObject> variables,
        VariableSymbol variable,
        out EvaluatorObject evaluatorObject) {
        if (variables.Count > 0 && variables.TryGetValue(variable, out var result)) {
            evaluatorObject = result;
            return true;
        }

        evaluatorObject = default;
        return false;
    }

    private EvaluatorObject Get(VariableSymbol variable, Dictionary<IVariableSymbol, EvaluatorObject> scope = null) {
        if (scope is not null) {
            if (TryGet(scope, variable, out var evaluatorObject))
                return evaluatorObject;
        }

        if (variable.kind == SymbolKind.GlobalVariable) {
            if (TryGet(_globals, variable, out var evaluatorObject))
                return evaluatorObject;
        } else {
            foreach (var frame in _locals) {
                if (TryGet(frame, variable, out var evaluatorObject))
                    return evaluatorObject;
            }
        }

        throw new BelteInternalException($"Get: '{variable.name}' was not found in any accessible scopes");
    }

    private EvaluatorObject Dereference(EvaluatorObject reference, bool dereferenceOnExplicit = true) {
        while (reference.isReference) {
            if (!dereferenceOnExplicit && reference.isExplicitReference)
                break;

            reference = Get(reference.reference);
        }

        return reference;
    }

    private Dictionary<object, object> DictionaryValue(
        Dictionary<Symbol, EvaluatorObject> value,
        BoundType containingType) {
        var dictionary = new Dictionary<object, object>();

        foreach (var pair in value) {
            if (pair.Key is FieldSymbol) {
                var name = pair.Key.containingType == containingType.typeSymbol
                    ? pair.Key.name
                    : $"{pair.Key.containingType.name}.{pair.Key.name}";

                dictionary.Add(name, Value(pair.Value, true));
            }
        }

        return dictionary;
    }

    private object[] CollectionValue(EvaluatorObject[] value) {
        var builder = new object[value.Length];

        for (var i = 0; i < value.Length; i++)
            builder[i] = Value(value[i], true);

        return builder;
    }

    private object Value(EvaluatorObject value, bool traceCollections = false) {
        if (value.isReference)
            return GetVariableValue(value.reference, traceCollections);
        else if (value.value is EvaluatorObject e)
            return Value(e, traceCollections);
        else if (value.value is EvaluatorObject[] && traceCollections)
            return CollectionValue(value.value as EvaluatorObject[]);
        else if (traceCollections && value.value is null && value.members != null)
            return DictionaryValue(value.members, value.trueType);
        else
            return value.value;
    }

    private EvaluatorObject Copy(EvaluatorObject value) {
        if (value.reference is not null && !value.isExplicitReference)
            return Copy(Get(value.reference));
        else if (value.reference is not null)
            return new EvaluatorObject(value.reference, isExplicitReference: true);
        else if (value.members is not null)
            return new EvaluatorObject(Copy(value.members), value.trueType);
        else
            return new EvaluatorObject(value.value);
    }

    private Dictionary<Symbol, EvaluatorObject> Copy(Dictionary<Symbol, EvaluatorObject> members) {
        var newMembers = new Dictionary<Symbol, EvaluatorObject>();

        foreach (var member in members)
            newMembers.Add(member.Key, Copy(member.Value));

        return newMembers;
    }

    private void Create(VariableSymbol left, EvaluatorObject right) {
        if (left.kind == SymbolKind.GlobalVariable) {
            var set = false;

            foreach (var global in _globals) {
                if (global.Key.name == left.name) {
                    _globals.Remove(global.Key);
                    _globals[left] = Copy(right);
                    set = true;

                    break;
                }
            }

            if (!set)
                _globals[left] = Copy(right);
        } else {
            var locals = _locals.Peek();
            var set = false;

            foreach (var local in locals) {
                if (local.Key.name == left.name) {
                    locals.Remove(local.Key);
                    locals[left] = Copy(right);
                    set = true;

                    break;
                }
            }

            if (!set)
                locals[left] = Copy(right);
        }
    }

    private void Assign(EvaluatorObject left, EvaluatorObject right) {
        right = Dereference(right, false);
        left = Dereference(left, false);

        if (right.isExplicitReference) {
            left.reference = right.reference;
            return;
        } else if (left.isExplicitReference) {
            left = Dereference(left);
        }

        if (right.members is null)
            left.members = null;

        if (right.value is null && right.members != null)
            left.members = Copy(right.members);
        else
            left.value = Value(right);
    }

    private void EnterClassScope(EvaluatorObject @class) {
        var classLocalBuffer = new Dictionary<IVariableSymbol, EvaluatorObject>();

        foreach (var member in @class.members) {
            if (member.Key is FieldSymbol f) {
                // If the symbol is already present it could be outdated and should be replaced
                // If it isn't outdated no harm in replacing it
                classLocalBuffer.Remove(f);
                classLocalBuffer.Add(f, member.Value);
            }
        }

        _enclosingTypes.Push(@class);
        _locals.Push(classLocalBuffer);
    }

    private void ExitClassScope() {
        _enclosingTypes.Pop();
        _locals.Pop();
    }

    private EvaluatorObject EvaluateCast(EvaluatorObject value, BoundType fromType, BoundType toType) {
        var dereferenced = Dereference(value);

        if (dereferenced.members is null || dereferenced.trueType is null) {
            var valueValue = Value(value);

            if (!toType.isNullable && value is null)
                throw new NullReferenceException();

            if (fromType.typeSymbol == toType.typeSymbol)
                return value;

            if (valueValue is EvaluatorObject[] v) {
                var builder = new EvaluatorObject[v.Length];

                for (var i = 0; i < v.Length; i++)
                    builder[i] = EvaluateCast(v[i], fromType.ChildType(), toType.ChildType());

                valueValue = builder;
            } else {
                valueValue = CastUtilities.CastIgnoringNull(valueValue, toType);
            }

            return new EvaluatorObject(valueValue);
        }

        if (TypeUtilities.TypeInheritsFrom(dereferenced.trueType, toType))
            return value;

        if (TypeUtilities.TypeInheritsFrom(toType, dereferenced.trueType))
            throw new InvalidCastException();

        throw ExceptionUtilities.Unreachable();
    }

    private static object EvaluateValueCast(object value, BoundType fromType, BoundType toType) {
        // TODO I sped this up for the future, but this method shouldn't be being called as much in general
        // TODO Look into optimizing type nullability in the binder, for example this shouldn't need to be called
        // TODO when doing something like `for (int! i = 0; i < 10; i++)` because i is not nullable,
        // TODO but this is being called for some reason
        if (!toType.isNullable && value is null)
            throw new NullReferenceException();

        if (fromType.typeSymbol == toType.typeSymbol)
            return value;

        return CastUtilities.CastIgnoringNull(value, toType);
    }

    private EvaluatorObject EvaluateStatement(
        BoundBlockStatement statement, ValueWrapper<bool> abort, out bool hasReturn, bool insideTry = false) {
        _hasValue = false;
        hasReturn = false;

        try {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (var i = 0; i < statement.statements.Length; i++) {
                if (statement.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;

            while (index < statement.statements.Length) {
                if (abort)
                    throw new BelteThreadException();

                var s = statement.statements[index];

                switch (s.kind) {
                    case BoundNodeKind.BlockStatement:
                        // TODO This is a temporary fix to nested blocks existing; SHOULD never occur after lowering
                        EvaluateStatement((BoundBlockStatement)s, abort, out hasReturn, insideTry: true);
                        index++;
                        break;
                    case BoundNodeKind.NopStatement:
                        index++;
                        break;
                    case BoundNodeKind.ExpressionStatement:
                        EvaluateExpressionStatement((BoundExpressionStatement)s, abort);
                        index++;
                        break;
                    case BoundNodeKind.LocalDeclarationStatement:
                        EvaluateLocalDeclarationStatement((BoundLocalDeclarationStatement)s, abort);
                        index++;
                        break;
                    case BoundNodeKind.GotoStatement:
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.label];
                        break;
                    case BoundNodeKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = EvaluateExpression(cgs.condition, abort);
                        var conditionValue = (bool)Value(condition);

                        if (conditionValue == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundNodeKind.LabelStatement:
                        index++;
                        break;
                    case BoundNodeKind.TryStatement:
                        EvaluateTryStatement((BoundTryStatement)s, abort, out var returned);

                        if (returned) {
                            hasReturn = true;
                            return _lastValue;
                        }

                        index++;

                        break;
                    case BoundNodeKind.ReturnStatement:
                        var returnStatement = (BoundReturnStatement)s;
                        _lastValue = returnStatement.expression is null
                            ? new EvaluatorObject()
                            : Copy(EvaluateExpression(returnStatement.expression, abort));

                        _hasValue = returnStatement.expression is not null and not BoundEmptyExpression;
                        hasReturn = true;

                        return _lastValue;
                    default:
                        throw new BelteInternalException($"EvaluateStatement: unexpected statement '{s.kind}'");
                }
            }

            return _lastValue;
        } catch (Exception e) when (e is not BelteInternalException) {
            if (e is BelteThreadException || abort)
                return new EvaluatorObject();

            if (insideTry)
                throw;

            exceptions.Add(e);
            lastOutputWasPrint = false;
            _hasValue = false;

            if (!Console.IsOutputRedirected) {
                // TODO Move this logic to the Repl
                if (Console.CursorLeft != 0)
                    Console.WriteLine();

                var previous = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"Unhandled exception: ");
                Console.ForegroundColor = previous;
                Console.WriteLine(e.Message);
            }

            return new EvaluatorObject();
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement statement, ValueWrapper<bool> abort) {
        _lastValue = EvaluateExpression(statement.expression, abort);
    }

    private void EvaluateTryStatement(BoundTryStatement statement, ValueWrapper<bool> abort, out bool hasReturn) {
        hasReturn = false;

        try {
            EvaluateStatement(statement.body, abort, out hasReturn, true);
        } catch (Exception e) when (e is not BelteException) {
            if (statement.catchBody != null && !hasReturn)
                EvaluateStatement(statement.catchBody, abort, out hasReturn);
            else
                throw;
        } finally {
            if (statement.finallyBody != null && !hasReturn)
                EvaluateStatement(statement.finallyBody, abort, out hasReturn);
        }
    }

    private void EvaluateLocalDeclarationStatement(
        BoundLocalDeclarationStatement statement,
        ValueWrapper<bool> abort) {
        var value = EvaluateExpression(statement.declaration.initializer, abort);
        _lastValue = default;
        Create(statement.declaration.variable, value);
    }

    private EvaluatorObject EvaluateExpression(BoundExpression node, ValueWrapper<bool> abort) {
        if (node.constantValue != null)
            return EvaluateConstantExpression(node, abort);

        switch (node.kind) {
            case BoundNodeKind.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    return new EvaluatorObject(EvaluateInitializerListExpression(il, abort));
                else
                    goto default;
            case BoundNodeKind.VariableExpression:
                return EvaluateVariableExpression((BoundVariableExpression)node, abort);
            case BoundNodeKind.AssignmentExpression:
                return EvaluateAssignmentExpression((BoundAssignmentExpression)node, abort);
            case BoundNodeKind.UnaryExpression:
                return EvaluateUnaryExpression((BoundUnaryExpression)node, abort);
            case BoundNodeKind.BinaryExpression:
                return EvaluateBinaryExpression((BoundBinaryExpression)node, abort);
            case BoundNodeKind.TernaryExpression:
                return EvaluateTernaryExpression((BoundTernaryExpression)node, abort);
            case BoundNodeKind.CallExpression:
                return EvaluateCallExpression((BoundCallExpression)node, abort);
            case BoundNodeKind.CastExpression:
                return EvaluateCastExpression((BoundCastExpression)node, abort);
            case BoundNodeKind.IndexExpression:
                return EvaluateIndexExpression((BoundIndexExpression)node, abort);
            case BoundNodeKind.ReferenceExpression:
                return EvaluateReferenceExpression((BoundReferenceExpression)node, abort);
            case BoundNodeKind.TypeOfExpression:
                return EvaluateTypeOfExpression((BoundTypeOfExpression)node, abort);
            case BoundNodeKind.EmptyExpression:
                return new EvaluatorObject();
            case BoundNodeKind.ObjectCreationExpression:
                return EvaluateObjectCreationExpression((BoundObjectCreationExpression)node, abort);
            case BoundNodeKind.MemberAccessExpression:
                return EvaluateMemberAccessExpression((BoundMemberAccessExpression)node, abort);
            case BoundNodeKind.ThisExpression:
                return EvaluateThisExpression((BoundThisExpression)node, abort);
            case BoundNodeKind.BaseExpression:
                return EvaluateBaseExpression((BoundBaseExpression)node, abort);
            case BoundNodeKind.ThrowExpression:
                return EvaluateThrowExpression((BoundThrowExpression)node, abort);
            case BoundNodeKind.Type:
                return EvaluateType((BoundType)node, abort);
            default:
                throw new BelteInternalException($"EvaluateExpression: unexpected node '{node.kind}'");
        }
    }

    private EvaluatorObject EvaluateType(BoundType node, ValueWrapper<bool> abort) {
        if (node.arity == 0)
            return new EvaluatorObject(members: [], node);

        var locals = new Dictionary<IVariableSymbol, EvaluatorObject>();
        var typeSymbol = node.typeSymbol as NamedTypeSymbol;
        AddTemplatesToLocals(typeSymbol.templateParameters, node.templateArguments, locals, abort);

        _locals.Push(locals);
        _templateConstantDepth++;

        return new EvaluatorObject(members: [], node);
    }

    private EvaluatorObject EvaluateThisExpression(BoundThisExpression _, ValueWrapper<bool> _1) {
        return _enclosingTypes.Peek();
    }

    private EvaluatorObject EvaluateBaseExpression(BoundBaseExpression _, ValueWrapper<bool> _1) {
        return _enclosingTypes.Peek();
    }

    private EvaluatorObject EvaluateThrowExpression(BoundThrowExpression node, ValueWrapper<bool> abort) {
        var exception = EvaluateExpression(node.exception, abort);
        var message = exception.members.Where(m => m.Key.name == "message").First();
        throw new Exception(message.Value.value as string);
    }

    private EvaluatorObject EvaluateMemberAccessExpression(BoundMemberAccessExpression node, ValueWrapper<bool> abort) {
        var operand = Dereference(EvaluateExpression(node.left, abort), true);

        if (node.type == BoundType.MethodGroup) {
            EnterClassScope(operand);
            return default;
        }

        if (node.right is BoundType)
            return operand.members[node.right.type.typeSymbol];

        var member = (node.right as BoundVariableExpression).variable;
        return operand.members[member];
    }

    private EvaluatorObject EvaluateObjectCreationExpression(
        BoundObjectCreationExpression node,
        ValueWrapper<bool> abort) {
        if (node.viaConstructor || (node.type.sizes.Length == 0 && node.type.typeSymbol is StructSymbol)) {
            var members = new Dictionary<Symbol, EvaluatorObject>();
            var typeMembers = (node.type.typeSymbol as NamedTypeSymbol).GetMembers();

            foreach (var field in typeMembers.Where(f => f is FieldSymbol)) {
                var value = new EvaluatorObject();

                if ((field as FieldSymbol).isReference) {
                    value.isReference = true;
                    value.isExplicitReference = true;
                }

                members.Add(field as Symbol, value);
            }

            var newObject = new EvaluatorObject(members, node.type);

            EnterClassScope(newObject);

            // structs don't have any methods, so no constructors
            if (node.type.typeSymbol is ClassSymbol)
                InvokeMethod(node.constructor, node.arguments, [], abort);

            ExitClassScope();

            return newObject;
        } else {
            var array = EvaluatorObject.Null;

            // TODO There is probably a more efficient algorithm for this
            foreach (var size in node.type.sizes) {
                var sizeValue = (int)Value(EvaluateExpression(size, abort));

                foreach (var element in IterateElements(array)) {
                    var members = new EvaluatorObject[sizeValue];

                    for (var i = 0; i < sizeValue; i++)
                        members[i] = EvaluatorObject.Null;

                    element.value = members;
                }
            }

            return array;
        }

        static List<EvaluatorObject> IterateElements(EvaluatorObject evaluatorObject) {
            if (evaluatorObject.value is null) {
                return [evaluatorObject];
            } else {
                var objects = new List<EvaluatorObject>();

                foreach (var subElement in evaluatorObject.value as EvaluatorObject[])
                    objects.AddRange(IterateElements(subElement));

                return objects;
            }
        }
    }

    private static EvaluatorObject EvaluateTypeOfExpression(BoundTypeOfExpression _, ValueWrapper<bool> _1) {
        // TODO Implement typeof and type types
        // ? Would just settings EvaluatorObject.value to a BoundType work?
        return new EvaluatorObject();
    }

    private EvaluatorObject EvaluateReferenceExpression(BoundReferenceExpression node, ValueWrapper<bool> abort) {
        if (node.expression is BoundVariableExpression v)
            return new EvaluatorObject(v.variable, isExplicitReference: true);
        else
            return EvaluateExpression(node.expression, abort);
    }

    private EvaluatorObject EvaluateIndexExpression(BoundIndexExpression node, ValueWrapper<bool> abort) {
        var variable = EvaluateExpression(node.expression, abort);
        var index = EvaluateExpression(node.index, abort);
        var array = (EvaluatorObject[])Value(variable);
        var indexValue = (int)Value(index);

        return array[indexValue];
    }

    private EvaluatorObject[] EvaluateInitializerListExpression(
        BoundInitializerListExpression node,
        ValueWrapper<bool> abort) {
        var builder = new EvaluatorObject[node.items.Length];

        for (var i = 0; i < node.items.Length; i++)
            builder[i] = EvaluateExpression(node.items[i], abort);

        return builder;
    }

    private EvaluatorObject EvaluateCastExpression(BoundCastExpression node, ValueWrapper<bool> abort) {
        var value = EvaluateExpression(node.expression, abort);

        return EvaluateCast(value, node.expression.type, node.type);
    }

    private EvaluatorObject EvaluateCallExpression(BoundCallExpression node, ValueWrapper<bool> abort) {
        if (node.method == BuiltinMethods.RandInt) {
            var max = (int)Value(EvaluateExpression(node.arguments[0], abort));
            _random ??= new Random();
            return new EvaluatorObject(_random.Next(max));
        } else if (node.method == BuiltinMethods.ValueAny ||
            node.method == BuiltinMethods.ValueBool ||
            node.method == BuiltinMethods.ValueDecimal ||
            node.method == BuiltinMethods.ValueInt ||
            node.method == BuiltinMethods.ValueString ||
            node.method == BuiltinMethods.ValueChar) {
            var value = EvaluateExpression(node.arguments[0], abort);
            var hasNoMembers = value.isReference ? Get(value.reference).members is null : value.members is null;

            if (Value(value) is null && hasNoMembers)
                throw new NullReferenceException();

            if (hasNoMembers)
                return new EvaluatorObject(Value(value));
            else
                return Copy(value);
        } else if (node.method == BuiltinMethods.HasValueAny ||
            node.method == BuiltinMethods.HasValueBool ||
            node.method == BuiltinMethods.HasValueDecimal ||
            node.method == BuiltinMethods.HasValueInt ||
            node.method == BuiltinMethods.HasValueString ||
            node.method == BuiltinMethods.HasValueChar) {
            var value = EvaluateExpression(node.arguments[0], abort);
            var hasNoMembers = value.isReference ? Get(value.reference).members is null : value.members is null;

            if (Value(value) is null && hasNoMembers)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        } else if (node.method == BuiltinMethods.Hex || node.method == BuiltinMethods.NullableHex) {
            var value = (int?)Value(EvaluateExpression(node.arguments[0], abort));

            if (!value.HasValue)
                return EvaluatorObject.Null;

            var addPrefix = (bool)Value(EvaluateExpression(node.arguments[1], abort));
            var hex = addPrefix ? $"0x{value.Value:X}" : value.Value.ToString("X");

            return new EvaluatorObject(hex);
        } else if (node.method == BuiltinMethods.Ascii || node.method == BuiltinMethods.NullableAscii) {
            var value = (string)Value(EvaluateExpression(node.arguments[0], abort));

            if (value is null || value.Length != 1)
                return EvaluatorObject.Null;

            return new EvaluatorObject((int)char.Parse(value));
        } else if (node.method == BuiltinMethods.Char || node.method == BuiltinMethods.NullableChar) {
            var value = (int?)Value(EvaluateExpression(node.arguments[0], abort));

            if (!value.HasValue)
                return EvaluatorObject.Null;

            return new EvaluatorObject(((char)value.Value).ToString());
        } else if (node.method == BuiltinMethods.Length) {
            var value = Value(EvaluateExpression(node.arguments[0], abort));

            if (value is object[] v)
                return new EvaluatorObject(v.Length);
            else
                return EvaluatorObject.Null;
        } else if (node.method.originalDefinition == BuiltinMethods.ToAny) {
            var value = Value(EvaluateExpression(node.arguments[0], abort));
            return new EvaluatorObject(value);
        } else if (node.method.originalDefinition == BuiltinMethods.ToObject) {
            return EvaluateExpression(node.arguments[0], abort);
        } else if (node.method == BuiltinMethods.ObjectsEqual) {
            var x = Dereference(EvaluateExpression(node.arguments[0], abort));
            var y = Dereference(EvaluateExpression(node.arguments[1], abort));

            if ((x.trueType is null && x.value is null) || (y.trueType is null && y.value is null))
                return EvaluatorObject.Null;

            return new EvaluatorObject(x.ValueEquals(y));
        } else if (node.method == BuiltinMethods.ObjectReferencesEqual) {
            var x = Dereference(EvaluateExpression(node.arguments[0], abort));
            var y = Dereference(EvaluateExpression(node.arguments[1], abort));

            if ((x.trueType is null && x.value is null) || (y.trueType is null && y.value is null))
                return EvaluatorObject.Null;

            if (x == y)
                return new EvaluatorObject(true);

            return new EvaluatorObject(false);
        } else {
            if (CheckStandardMap(node.method, node.arguments, abort, out var result, out var printed)) {
                lastOutputWasPrint = printed;
                return new EvaluatorObject(result);
            }

            return InvokeMethod(node.method, node.arguments, node.templateArguments, abort, node.expression);
        }
    }

    private EvaluatorObject InvokeMethod(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ImmutableArray<BoundTypeOrConstant> templateArguments,
        ValueWrapper<bool> abort,
        BoundExpression expression = null) {
        var receiver = default(EvaluatorObject);

        if (expression is not null && expression is not BoundEmptyExpression) {
            receiver = EvaluateExpression(expression, abort);
            var dereferencedReceiver = Dereference(receiver);

            if (dereferencedReceiver.members is null)
                throw new NullReferenceException();
        }

        if (method.isAbstract) {
            var trueType = Dereference(receiver).trueType;
            var newMethod = (trueType.typeSymbol as ClassSymbol)
                .GetMembers()
                .Where(s => s is MethodSymbol m && m.Signature() == method.Signature() && m.isOverride)
                .First() as MethodSymbol;

            method = newMethod;
        }

        var locals = new Dictionary<IVariableSymbol, EvaluatorObject>();
        AddTemplatesToLocals(method.templateParameters, templateArguments, locals, abort);

        for (var i = 0; i < arguments.Length; i++) {
            var parameter = method.parameters[i];
            var value = EvaluateExpression(arguments[i], abort);

            while (!parameter.type.isReference && value.isReference)
                value = Get(value.reference);

            locals.Add(parameter, Copy(value));
        }

        _locals.Push(locals);

        var statement = LookupMethod(_methods, method);
        var templateConstantDepth = _templateConstantDepth;
        var enteredScope = false;

        if (receiver is not null && receiver.isReference) {
            // On an expression such as 'myInstance.Method()', we need to enter the 'myInstance' class scope
            // in case 'Method' uses 'this'
            // If what we get here is not a reference, it is a static accession and the needed scoped members have
            // already been pushed by 'EvaluateType'.
            receiver = Dereference(receiver);

            if (receiver.members is not null) {
                EnterClassScope(receiver);
                enteredScope = true;
            }
        }

        var result = EvaluateStatement(statement, abort, out _);

        while (_templateConstantDepth > templateConstantDepth) {
            _templateConstantDepth--;
            _locals.Pop();
        }

        _locals.Pop();

        if (enteredScope)
            ExitClassScope();

        return result;
    }

    private void AddTemplatesToLocals(
        ImmutableArray<ParameterSymbol> templateParameters,
        ImmutableArray<BoundTypeOrConstant> templateArguments,
        Dictionary<IVariableSymbol, EvaluatorObject> locals,
        ValueWrapper<bool> abort) {
        for (var i = 0; i < templateArguments.Length; i++) {
            EvaluatorObject value;

            if (templateArguments[i].isConstant)
                value = EvaluateBoundConstant(templateArguments[i].constant);
            else
                value = EvaluateType(templateArguments[i].type, abort);

            locals.Add(templateParameters[i], value);
        }
    }

    private EvaluatorObject EvaluateConstantExpression(BoundExpression expression, ValueWrapper<bool> _) {
        return EvaluateBoundConstant(expression.constantValue);
    }

    private static EvaluatorObject EvaluateBoundConstant(BoundConstant constant) {
        if (constant.value is ImmutableArray<BoundConstant> ia) {
            var builder = new EvaluatorObject[ia.Length];

            for (var i = 0; i < ia.Length; i++)
                builder[i] = EvaluateBoundConstant(ia[i]);

            return new EvaluatorObject(builder);
        } else {
            return new EvaluatorObject(constant.value);
        }
    }

    private static EvaluatorObject EvaluateVariableExpression(
        BoundVariableExpression expression,
        ValueWrapper<bool> _) {
        return new EvaluatorObject(expression.variable);
    }

    private EvaluatorObject EvaluateAssignmentExpression(
        BoundAssignmentExpression expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var right = EvaluateExpression(expression.right, abort);
        Assign(left, right);

        return right;
    }

    private EvaluatorObject EvaluateUnaryExpression(BoundUnaryExpression expression, ValueWrapper<bool> abort) {
        var operand = EvaluateExpression(expression.operand, abort);
        var operandValue = Value(operand);

        if (operandValue is null)
            return new EvaluatorObject();

        operandValue = EvaluateValueCast(operandValue, expression.operand.type, expression.op.operandType);

        switch (expression.op.opKind) {
            case BoundUnaryOperatorKind.NumericalIdentity:
                if (expression.operand.type.typeSymbol == TypeSymbol.Int)
                    return new EvaluatorObject((int)operandValue);
                else
                    return new EvaluatorObject((double)operandValue);
            case BoundUnaryOperatorKind.NumericalNegation:
                if (expression.operand.type.typeSymbol == TypeSymbol.Int)
                    return new EvaluatorObject(-(int)operandValue);
                else
                    return new EvaluatorObject(-(double)operandValue);
            case BoundUnaryOperatorKind.BooleanNegation:
                return new EvaluatorObject(!(bool)operandValue);
            case BoundUnaryOperatorKind.BitwiseCompliment:
                return new EvaluatorObject(~(int)operandValue);
            default:
                throw new BelteInternalException($"EvaluateUnaryExpression: unknown unary operator '{expression.op}'");
        }
    }

    private EvaluatorObject EvaluateTernaryExpression(BoundTernaryExpression expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var leftValue = Value(left);
        leftValue = EvaluateValueCast(leftValue, expression.left.type, expression.op.leftType);

        switch (expression.op.opKind) {
            case BoundTernaryOperatorKind.Conditional:
                // This is so unused sides do not get evaluated (incase they would throw)
                if ((bool)leftValue)
                    return EvaluateExpression(expression.center, abort);
                else
                    return EvaluateExpression(expression.right, abort);
            default:
                throw new BelteInternalException(
                    $"EvaluateTernaryExpression: unknown ternary operator '{expression.op}'"
                );
        }
    }

    private EvaluatorObject EvaluateBinaryExpression(BoundBinaryExpression expression, ValueWrapper<bool> abort) {
        var left = EvaluateExpression(expression.left, abort);
        var leftValue = Value(left);

        // Only evaluates right side if necessary
        if (expression.op.opKind == BoundBinaryOperatorKind.ConditionalAnd) {
            if (leftValue is null || !(bool)leftValue)
                return new EvaluatorObject(false);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue is null || !(bool)shortCircuitRightValue)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        }

        if (expression.op.opKind == BoundBinaryOperatorKind.ConditionalOr) {
            if (leftValue != null && (bool)leftValue)
                return new EvaluatorObject(true);

            var shortCircuitRight = EvaluateExpression(expression.right, abort);
            var shortCircuitRightValue = Value(shortCircuitRight);

            if (shortCircuitRightValue != null && (bool)shortCircuitRightValue)
                return new EvaluatorObject(true);

            return new EvaluatorObject(false);
        }

        if (expression.op.opKind == BoundBinaryOperatorKind.As) {
            var dereferenced = Dereference(left);

            if (dereferenced.members is null || dereferenced.trueType is null)
                // Primitive
                return new EvaluatorObject(leftValue);

            if (TypeUtilities.TypeInheritsFrom(dereferenced.trueType, expression.right.type))
                return left;

            return EvaluatorObject.Null;
        }

        var right = EvaluateExpression(expression.right, abort);
        var rightValue = Value(right);

        if (expression.op.opKind is BoundBinaryOperatorKind.Is or BoundBinaryOperatorKind.Isnt) {
            var dereferenced = Dereference(left);

            if (leftValue is null && dereferenced.members is null)
                return new EvaluatorObject(false);

            if (left.members is null) {
                return new EvaluatorObject(
                    (right.trueType.Equals(BoundType.NullableAny) ||
                    right.trueType.Equals(BoundType.Any) ||
                    BoundType.Assume(leftValue).Equals(right.trueType, isTypeCheck: true)) ^
                    expression.op.opKind is not BoundBinaryOperatorKind.Is
                );
            }

            if (TypeUtilities.TypeInheritsFrom(left.trueType, right.trueType))
                return new EvaluatorObject(expression.op.opKind is BoundBinaryOperatorKind.Is);
            else
                return new EvaluatorObject(expression.op.opKind is BoundBinaryOperatorKind.Isnt);
        }

        if (leftValue is null || rightValue is null)
            return new EvaluatorObject();

        var expressionType = expression.type.typeSymbol;
        var leftType = expression.op.leftType.typeSymbol;

        leftValue = EvaluateValueCast(leftValue, expression.left.type, expression.op.leftType);
        rightValue = EvaluateValueCast(rightValue, expression.right.type, expression.op.rightType);

        switch (expression.op.opKind) {
            case BoundBinaryOperatorKind.Addition:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue + (int)rightValue);
                else if (expressionType == TypeSymbol.String)
                    return new EvaluatorObject((string)leftValue + (string)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) + Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.Subtraction:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue - (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) - Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.Multiplication:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue * (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) * Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.Division:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue / (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) / Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.ConditionalAnd:
                return new EvaluatorObject((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorKind.ConditionalOr:
                return new EvaluatorObject((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorKind.EqualityEquals:
                return new EvaluatorObject(Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.EqualityNotEquals:
                return new EvaluatorObject(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.LessThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue < (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) < Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue > (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) > Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue <= (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) <= Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue >= (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) >= Convert.ToDouble(rightValue));
            case BoundBinaryOperatorKind.LogicalAnd:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue & (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue & (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalOr:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue | (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue | (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalXor:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue ^ (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue ^ (bool)rightValue);
            case BoundBinaryOperatorKind.LeftShift:
                return new EvaluatorObject((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorKind.RightShift:
                return new EvaluatorObject((int)leftValue >> (int)rightValue);
            case BoundBinaryOperatorKind.UnsignedRightShift:
                return new EvaluatorObject((int)leftValue >>> (int)rightValue);
            case BoundBinaryOperatorKind.Modulo:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue % (int)rightValue);
                else
                    return new EvaluatorObject(Convert.ToDouble(leftValue) % Convert.ToDouble(rightValue));
            default:
                throw new BelteInternalException(
                    $"EvaluateBinaryExpression: unknown binary operator '{expression.op}'"
                );
        }
    }

    private bool CheckStandardMap(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ValueWrapper<bool> abort,
        out object result,
        out bool printed) {
        result = null;
        printed = false;

        if (method.containingType == StandardLibrary.Console || method.containingType == StandardLibrary.Math) {
            if (method == StandardLibrary.Console.members[5] ||
                method == StandardLibrary.Console.members[6] ||
                method == StandardLibrary.Console.members[7]) {
                printed = true;
            }

            // TODO Right now this approach seems fragile, should at some point reevaluate how this should be done
            if (method == StandardLibrary.Console.members[3] || method == StandardLibrary.Console.members[7]) {
                var receiver = Dereference(EvaluateExpression(arguments[0], abort));

                // Calling ToString on objects
                // There should always be exactly one method that fits these search criteria
                var toString = (receiver.trueType.typeSymbol as NamedTypeSymbol)
                    .GetMembers(WellKnownMemberNames.ToString)
                    .Where(f => f is MethodSymbol)
                    .Select(f => f as MethodSymbol)
                    .Where(m => m.parameters.Length == 0)
                    .First();

                EnterClassScope(receiver);
                var argument = InvokeMethod(toString, [], [], abort);
                ExitClassScope();

                result = StandardLibrary.MethodEvaluatorMap[method.GetHashCode()](Value(argument), null, null);
                return true;
            }

            result = arguments.Length switch {
                0 => StandardLibrary.MethodEvaluatorMap[method.GetHashCode()](null, null, null),
                1 => StandardLibrary.MethodEvaluatorMap[method.GetHashCode()]
                    (Value(EvaluateExpression(arguments[0], abort)), null, null),
                2 => StandardLibrary.MethodEvaluatorMap[method.GetHashCode()](
                        Value(EvaluateExpression(arguments[0], abort)),
                        Value(EvaluateExpression(arguments[1], abort)),
                        null
                    ),
                3 => StandardLibrary.MethodEvaluatorMap[method.GetHashCode()](
                        Value(EvaluateExpression(arguments[0], abort)),
                        Value(EvaluateExpression(arguments[1], abort)),
                        Value(EvaluateExpression(arguments[2], abort))
                    ),
                _ => throw ExceptionUtilities.Unreachable()
            };

            return true;
        }

        return false;
    }
}
