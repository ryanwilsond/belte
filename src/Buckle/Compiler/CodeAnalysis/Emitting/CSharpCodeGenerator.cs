using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Shared;
using IndentedTextWriter = System.CodeDom.Compiler.IndentedTextWriter;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CSharpCodeGenerator {
    private readonly ImmutableArray<string> _localNames;
    private int _tempCount = 0;

    private readonly CSharpEmitter _module;
    private readonly MethodSymbol _method;
    private readonly BoundBlockStatement _body;
    private readonly bool _debugMode;
    private readonly IndentedTextWriter _writer;

    private static readonly Dictionary<SpecialType, string> ConvertMethods = new Dictionary<SpecialType, string>{
        { SpecialType.Bool, "global::System.Convert.ToBoolean" },
        { SpecialType.Int, "global::System.Convert.ToInt64" },
        { SpecialType.Int8, "global::System.Convert.ToSByte" },
        { SpecialType.Int16, "global::System.Convert.ToInt16" },
        { SpecialType.Int32, "global::System.Convert.ToInt32" },
        { SpecialType.Int64, "global::System.Convert.ToInt64" },
        { SpecialType.UInt8, "global::System.Convert.ToByte" },
        { SpecialType.UInt16, "global::System.Convert.ToUInt16" },
        { SpecialType.UInt32, "global::System.Convert.ToUInt32" },
        { SpecialType.UInt64, "global::System.Convert.ToUIn64" },
        { SpecialType.Decimal, "global::System.Convert.ToDouble" },
        { SpecialType.Float32, "global::System.Convert.ToSingle" },
        { SpecialType.Float64, "global::System.Convert.ToDouble" },
        { SpecialType.Char, "global::System.Convert.ToChar" },
    };

    internal CSharpCodeGenerator(
        CSharpEmitter module,
        IndentedTextWriter writer,
        MethodSymbol method,
        BoundBlockStatement methodBody,
        bool debugMode) {
        _module = module;
        _method = method;
        _body = methodBody;
        _debugMode = debugMode;
        _writer = writer;

        _localNames = methodBody.locals.Select(l => _module.GetSafeName(l.name))
            .Concat(methodBody.localFunctions.Select(l => _module.GetSafeName(l.name)))
            .ToImmutableArray();
    }

    internal void Generate() {
        EmitBlockStatement(_body);
    }

    private string EmitTemp(BoundExpression initializer) {
        string name;

        do {
            name = $"temp{_tempCount++}";
        } while (_localNames.Contains(name));

        _writer.WriteLine($"{_module.GetType(initializer.type, false)} {name} = {EmitExpression(initializer)}");

        return name;
    }

    #region Statements

    private void EmitStatement(BoundStatement statement) {
        switch (statement.kind) {
            case BoundKind.NopStatement:
                EmitNopStatement();
                break;
            case BoundKind.BlockStatement:
                EmitBlockStatement((BoundBlockStatement)statement);
                break;
            case BoundKind.LocalDeclarationStatement:
                EmitLocalDeclarationStatement((BoundLocalDeclarationStatement)statement);
                break;
            case BoundKind.IfStatement:
                EmitIfStatement((BoundIfStatement)statement);
                break;
            case BoundKind.NullBindingStatement:
                EmitNullBindingStatement((BoundNullBindingStatement)statement);
                break;
            case BoundKind.WhileStatement:
                EmitWhileStatement((BoundWhileStatement)statement);
                break;
            case BoundKind.ForStatement:
                EmitForStatement((BoundForStatement)statement);
                break;
            case BoundKind.ExpressionStatement:
                EmitExpressionStatement((BoundExpressionStatement)statement);
                break;
            case BoundKind.LabelStatement:
                EmitLabelStatement((BoundLabelStatement)statement);
                break;
            case BoundKind.GotoStatement:
                EmitGotoStatement((BoundGotoStatement)statement);
                break;
            case BoundKind.ConditionalGotoStatement:
                EmitConditionalGotoStatement((BoundConditionalGotoStatement)statement);
                break;
            case BoundKind.DoWhileStatement:
                EmitDoWhileStatement((BoundDoWhileStatement)statement);
                break;
            case BoundKind.ReturnStatement:
                EmitReturnStatement((BoundReturnStatement)statement);
                break;
            case BoundKind.TryStatement:
                EmitTryStatement((BoundTryStatement)statement);
                break;
            case BoundKind.BreakStatement:
                EmitBreakStatement((BoundBreakStatement)statement);
                break;
            case BoundKind.ContinueStatement:
                EmitContinueStatement((BoundContinueStatement)statement);
                break;
            case BoundKind.LocalFunctionStatement:
                EmitLocalFunctionStatement((BoundLocalFunctionStatement)statement);
                break;
            case BoundKind.SequencePoint:
                EmitSequencePoint((BoundSequencePoint)statement);
                break;
            case BoundKind.SequencePointWithLocation:
                EmitSequencePointWithLocation((BoundSequencePointWithLocation)statement);
                break;
            case BoundKind.SwitchStatement:
                EmitSwitchStatement((BoundSwitchStatement)statement);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(statement.kind);
        }
    }

    private void EmitBlockStatement(BoundBlockStatement node) {
        foreach (var statement in node.statements)
            EmitStatement(statement);
    }

    private void EmitNopStatement() {
        if (_debugMode)
            _writer.WriteLine(';');
    }

    private void EmitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        var local = node.declaration.dataContainer;
        var initializer = local.isRef ? $"ref {EmitExpression(node.declaration.initializer)}" : EmitExpression(node.declaration.initializer);
        _writer.WriteLine($"{_module.GetType(local.type, local.isRef)} {_module.GetSafeName(local.name)} = {initializer};");
    }

    private void EmitIfStatement(BoundIfStatement node) {
        using (var curly = new CurlyIndenter(_writer, $"if ({EmitExpression(node.condition)})"))
            EmitStatement(node.consequence);

        if (node.alternative is not null) {
            using var curly = new CurlyIndenter(_writer, "else");
            EmitStatement(node.alternative);
        }
    }

    private void EmitNullBindingStatement(BoundNullBindingStatement node) {
        var temp = EmitTemp(node.expression);

        using (var curly = new CurlyIndenter(_writer, $"if ({temp} is not null)")) {
            var local = node.valueLocal;
            _writer.WriteLine($"{_module.GetType(local.type, local.isRef)} {_module.GetSafeName(local.name)} = {EmitNullAssert(temp, node.expression.StrippedType())};");
            EmitStatement(node.consequence);
        }

        if (node.alternative is not null) {
            using var curly = new CurlyIndenter(_writer, "else");
            EmitStatement(node.alternative);
        }
    }

    private void EmitWhileStatement(BoundWhileStatement node) {
        using var curly = new CurlyIndenter(_writer, $"while ({EmitExpression(node.condition)})");
        EmitStatement(node.body);
    }

    private void EmitForStatement(BoundForStatement node) {
        using var outerCurly = new CurlyIndenter(_writer);
        EmitStatement(node.initializer);

        using var innerCurly = new CurlyIndenter(_writer, $"for (; {EmitExpression(node.condition)};)");
        EmitStatement(node.body);
        EmitStatement(node.step);
    }

    private void EmitExpressionStatement(BoundExpressionStatement node) {
        _writer.WriteLine($"{EmitExpression(node.expression)};");
    }

    private void EmitLabelStatement(BoundLabelStatement node) {
        _writer.WriteLine($"{node.label.name};");
    }

    private void EmitGotoStatement(BoundGotoStatement node) {
        var name = node.label.name;
        _writer.WriteLine($"goto {(name.EndsWith(':') ? name[..^1] : name)};");
    }

    private void EmitConditionalGotoStatement(BoundConditionalGotoStatement node) {
        var name = node.label.name;
        _writer.WriteLine($"if ({EmitExpression(node.condition)}) goto {(name.EndsWith(':') ? name[..^1] : name)};");
    }

    private void EmitDoWhileStatement(BoundDoWhileStatement node) {
        using (var curly = new CurlyIndenter(_writer, "do", sameLine: true))
            EmitStatement(node.body);

        _writer.WriteLine($"while ({EmitExpression(node.condition)});");
    }

    private void EmitReturnStatement(BoundReturnStatement node) {
        if (node.expression is null)
            _writer.WriteLine("return;");
        else if (node.refKind != RefKind.None)
            _writer.WriteLine($"return ref {EmitExpression(node.expression)};");
        else
            _writer.WriteLine($"return {EmitExpression(node.expression)};");
    }

    private void EmitTryStatement(BoundTryStatement node) {
        using (var curly = new CurlyIndenter(_writer, "try", sameLine: true))
            EmitStatement(node.body);

        if (node.catchBody is not null) {
            using var catchCurly = new CurlyIndenter(_writer, "catch", sameLine: node.finallyBody is not null);
            EmitStatement(node.catchBody);
        }

        if (node.finallyBody is not null) {
            using var finallyCurly = new CurlyIndenter(_writer, "finally");
            EmitStatement(node.finallyBody);
        }
    }

    private void EmitBreakStatement(BoundBreakStatement _) {
        _writer.WriteLine("break;");
    }

    private void EmitContinueStatement(BoundContinueStatement _) {
        _writer.WriteLine("continue;");
    }

    private void EmitLocalFunctionStatement(BoundLocalFunctionStatement node) {
        var method = node.symbol;
        using var curly = new CurlyIndenter(_writer, $"{_module.GetMethodAttributes(method, includeAccessibility: false)}{_module.GetMethodSignature(method)}");
        EmitStatement(node.body);
    }

    private void EmitSequencePoint(BoundSequencePoint node) {
        EmitStatement(node.statement);
    }

    private void EmitSequencePointWithLocation(BoundSequencePointWithLocation node) {
        EmitStatement(node.statement);
    }

    private void EmitSwitchStatement(BoundSwitchStatement node) {
        using var curly = new CurlyIndenter(_writer, $"switch ({EmitExpression(node.expression)})");

        foreach (var section in node.switchSections) {
            foreach (var label in section.switchLabels) {
                var name = label.label.name;

                if (name.EndsWith(':'))
                    _writer.WriteLine(name);
                else
                    _writer.WriteLine($"{name}:");
            }

            _writer.Indent++;

            foreach (var statement in section.statements)
                EmitStatement(statement);

            _writer.Indent--;
        }
    }

    #endregion

    #region Expressions

    private string EmitExpression(BoundExpression expression) {
        var constantValue = expression.constantValue;

        if (constantValue is not null)
            return EmitConstantExpression(constantValue);

        return expression.kind switch {
            BoundKind.DefaultExpression => EmitDefaultExpression((BoundDefaultExpression)expression),
            BoundKind.InitializerList => EmitInitializerList((BoundInitializerList)expression),
            BoundKind.DataContainerExpression => EmitDataContainerExpression((BoundDataContainerExpression)expression),
            BoundKind.AssignmentOperator => EmitAssignmentOperator((BoundAssignmentOperator)expression),
            BoundKind.UnaryOperator => EmitUnaryOperator((BoundUnaryOperator)expression),
            BoundKind.IncrementOperator => EmitIncrementOperator((BoundIncrementOperator)expression),
            BoundKind.BinaryOperator => EmitBinaryOperator((BoundBinaryOperator)expression),
            BoundKind.AsOperator => EmitAsOperator((BoundAsOperator)expression),
            BoundKind.IsOperator => EmitIsOperator((BoundIsOperator)expression),
            BoundKind.NullCoalescingOperator => EmitNullCoalescingOperator((BoundNullCoalescingOperator)expression),
            BoundKind.NullCoalescingAssignmentOperator => EmitNullCoalescingAssignmentOperator((BoundNullCoalescingAssignmentOperator)expression),
            BoundKind.NullAssertOperator => EmitNullAssertOperator((BoundNullAssertOperator)expression),
            BoundKind.NullErasureOperator => EmitNullErasureOperator((BoundNullErasureOperator)expression),
            BoundKind.AddressOfOperator => EmitAddressOfOperator((BoundAddressOfOperator)expression),
            BoundKind.PointerIndirectionOperator => EmitPointerIndirectionOperator((BoundPointerIndirectionOperator)expression),
            BoundKind.CallExpression => EmitCallExpression((BoundCallExpression)expression),
            BoundKind.CastExpression => EmitCastExpression((BoundCastExpression)expression),
            BoundKind.ArrayAccessExpression => EmitArrayAccessExpression((BoundArrayAccessExpression)expression),
            BoundKind.IndexerAccessExpression => EmitIndexerAccessExpression((BoundIndexerAccessExpression)expression),
            BoundKind.PointerIndexAccessExpression => EmitPointerIndexAccessExpression((BoundPointerIndexAccessExpression)expression),
            BoundKind.CompoundAssignmentOperator => EmitCompoundAssignmentOperator((BoundCompoundAssignmentOperator)expression),
            BoundKind.ReferenceExpression => EmitReferenceExpression((BoundReferenceExpression)expression),
            BoundKind.TypeOfExpression => EmitTypeOfExpression((BoundTypeOfExpression)expression),
            BoundKind.ConditionalOperator => EmitConditionalOperator((BoundConditionalOperator)expression),
            BoundKind.ObjectCreationExpression => EmitObjectCreationExpression((BoundObjectCreationExpression)expression),
            BoundKind.ArrayCreationExpression => EmitArrayCreationExpression((BoundArrayCreationExpression)expression),
            BoundKind.FieldAccessExpression => EmitFieldAccessExpression((BoundFieldAccessExpression)expression),
            BoundKind.ConditionalAccessExpression => EmitConditionalAccessExpression((BoundConditionalAccessExpression)expression),
            BoundKind.ThisExpression => EmitThisExpression((BoundThisExpression)expression),
            BoundKind.BaseExpression => EmitBaseExpression((BoundBaseExpression)expression),
            BoundKind.ThrowExpression => EmitThrowExpression((BoundThrowExpression)expression),
            BoundKind.TypeExpression => EmitTypeExpression((BoundTypeExpression)expression),
            BoundKind.NamespaceExpression => EmitNamespaceExpression((BoundNamespaceExpression)expression),
            BoundKind.ParameterExpression => EmitParameterExpression((BoundParameterExpression)expression),
            BoundKind.MethodGroup => EmitMethodGroup((BoundMethodGroup)expression),
            BoundKind.FunctionPointerLoad => EmitFunctionPointerLoad((BoundFunctionPointerLoad)expression),
            BoundKind.FunctionPointerCallExpression => EmitFunctionPointerCallExpression((BoundFunctionPointerCallExpression)expression),
            BoundKind.SizeOfOperator => EmitSizeOfOperator((BoundSizeOfOperator)expression),
            BoundKind.ConvertedStackAllocExpression => EmitConvertedStackAllocExpression((BoundConvertedStackAllocExpression)expression),
            BoundKind.InterpolatedStringExpression => EmitInterpolatedStringExpression((BoundInterpolatedStringExpression)expression),
            BoundKind.FunctionLoad => EmitFunctionLoad((BoundFunctionLoad)expression),
            BoundKind.IsPatternExpression => EmitIsPatternExpression((BoundIsPatternExpression)expression),
            _ => throw ExceptionUtilities.UnexpectedValue(expression.kind),
        };
    }

    private string EmitConstantExpression(ConstantValue constantValue) {
        return DisplayText.FormatLiteral(constantValue.value);
    }

    private string EmitDefaultExpression(BoundDefaultExpression _) {
        return "default";
    }

    private string EmitInitializerList(BoundInitializerList node) {
        return $"{{ {string.Join(", ", node.items.Select(i => EmitExpression(i)))} }}";
    }

    private string EmitDataContainerExpression(BoundDataContainerExpression node) {
        return _module.GetSafeName(node.dataContainer.name);
    }

    private string EmitAssignmentOperator(BoundAssignmentOperator node) {
        if (node.isRef)
            return $"{EmitExpression(node.left)} = ref {EmitExpression(node.right)}";
        else
            return $"{EmitExpression(node.left)} = {EmitExpression(node.right)}";
    }

    private string EmitUnaryOperator(BoundUnaryOperator node) {
        return $"({SyntaxFacts.GetText(node.operatorKind.ToSyntaxKind())}{EmitExpression(node.operand)})";
    }

    private string EmitIncrementOperator(BoundIncrementOperator node) {
        var op = node.operatorKind.Operator();
        var operand = EmitExpression(node.operand);

        return op switch {
            UnaryOperatorKind.PrefixIncrement => $"++{operand}",
            UnaryOperatorKind.PrefixDecrement => $"--{operand}",
            UnaryOperatorKind.PostfixIncrement => $"{operand}++",
            UnaryOperatorKind.PostfixDecrement => $"{operand}--",
            _ => throw ExceptionUtilities.UnexpectedValue(op)
        };
    }

    private string EmitBinaryOperator(BoundBinaryOperator node) {
        var right = node.operatorKind.IsShift() && node.right.StrippedType().specialType.IsLongIntegral()
            ? $"(global::System.Int32){EmitExpression(node.right)}"
            : EmitExpression(node.right);

        return $"({EmitExpression(node.left)} {SyntaxFacts.GetText(node.operatorKind.ToSyntaxKind())} {right})";
    }

    private string EmitAsOperator(BoundAsOperator node) {
        return $"{EmitExpression(node.left)} as {EmitExpression(node.right)}";
    }

    private string EmitIsOperator(BoundIsOperator node) {
        var op = node.isNot ? "is not" : "is";
        return $"{EmitExpression(node.left)} {op} {EmitExpression(node.right)}";
    }

    private string EmitNullCoalescingOperator(BoundNullCoalescingOperator node) {
        return $"{EmitExpression(node.left)} ?? {EmitExpression(node.right)}";
    }

    private string EmitNullCoalescingAssignmentOperator(BoundNullCoalescingAssignmentOperator node) {
        return $"{EmitExpression(node.left)} ??= {EmitExpression(node.right)}";
    }

    private string EmitNullAssertOperator(BoundNullAssertOperator node) {
        return EmitNullAssert(node.operand, node.throwIfNull);
    }

    private string EmitNullAssert(BoundExpression operand, bool throwIfNull = true) {
        var expression = EmitExpression(operand);
        return EmitNullAssert(expression, operand.StrippedType(), throwIfNull);
    }

    private string EmitNullAssert(string expression, TypeSymbol strippedType, bool throwIfNull = true) {
        if (strippedType.IsVerifierValue())
            return throwIfNull ? $"{expression}.Value" : $"{expression}.GetValueOrDefault()";
        else
            return throwIfNull ? $"{expression} ?? throw new global::System.NullReferenceException()" : expression;
    }

    private string EmitNullErasureOperator(BoundNullErasureOperator node) {
        return $"{EmitExpression(node.operand)} ?? default";
    }

    private string EmitAddressOfOperator(BoundAddressOfOperator node) {
        return $"&{EmitExpression(node.operand)}";
    }

    private string EmitPointerIndirectionOperator(BoundPointerIndirectionOperator node) {
        return $"*{EmitExpression(node.operand)}";
    }

    private string EmitCallExpression(BoundCallExpression node, bool skipReceiver = false) {
        if (node.method.containingType.Equals(StandardLibrary.Random.underlyingNamedType))
            return EmitRandomCall(node.method, node.arguments, node.argumentRefKinds);

        if (node.method.IsConstructor())
            return "/* ctor call */";

        var arguments = EmitArguments(node.arguments, node.argumentRefKinds);

        if ((object)node.method.originalDefinition ==
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getValue).originalDefinition) {
            return $"{EmitExpression(node.receiver)}.Value";
        }

        if ((object)node.method.originalDefinition ==
            CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getHasValue).originalDefinition) {
            return $"{EmitExpression(node.receiver)}.HasValue";
        }

        var method = _module.GetMethodName(node.method);

        if ((object)node.method.containingType == StandardLibrary.LowLevel.originalDefinition) {
            if (node.method.name == "SizeOf")
                return method;
        }

        if (node.receiver is null || skipReceiver)
            return $"{method}({arguments})";
        else
            return $"{EmitExpression(node.receiver)}.{method}({arguments})";
    }

    private string EmitRandomCall(
        MethodSymbol method,
        ImmutableArray<BoundExpression> arguments,
        ImmutableArray<RefKind> refKinds) {
        var random = _module.EnsureGlobalsClassIsBuilt(_writer);

        switch (method.name) {
            case "RandInt":
                return $"{random}.NextInt64({EmitArguments(arguments, refKinds)})";
            case "Random":
                return $"{random}.NextDouble({EmitArguments(arguments, refKinds)})";
            default:
                throw ExceptionUtilities.UnexpectedValue(method.name);
        }
    }

    internal string EmitArguments(ImmutableArray<BoundExpression> arguments, ImmutableArray<RefKind> refKinds) {
        if (refKinds.IsDefault)
            return string.Join(", ", arguments.Select(a => EmitExpression(a)));

        var builder = ArrayBuilder<string>.GetInstance();

        for (var i = 0; i < arguments.Length; i++) {
            if (refKinds[i] == RefKind.None)
                builder.Add(EmitExpression(arguments[i]));
            else
                builder.Add($"ref {EmitExpression(arguments[i])}");
        }

        return string.Join(", ", builder.ToArrayAndFree());
    }

    private string EmitCastExpression(BoundCastExpression node) {
        if (node.conversion.isImplicit)
            return EmitExpression(node.operand);

        if (node.conversion.kind is ConversionKind.ImplicitNullToPointer)
            return $"({_module.GetType(node.StrippedType())})0";

        if (node.StrippedType().specialType == SpecialType.String)
            return $"{EmitExpression(node.operand)}.ToString()";

        if (node.operand.StrippedType().specialType == SpecialType.String)
            return EmitConvertCall(node);

        return $"({_module.GetType(node.StrippedType())}){EmitExpression(node.operand)}";
    }

    private string EmitConvertCall(BoundCastExpression node) {
        var argument = EmitExpression(node.operand);
        var method = ConvertMethods[node.StrippedType().specialType];
        return $"{method}({argument})";
    }

    private string EmitArrayAccessExpression(BoundArrayAccessExpression node) {
        return $"{EmitExpression(node.receiver)}[{EmitExpression(node.index)}]";
    }

    private string EmitIndexerAccessExpression(BoundIndexerAccessExpression node) {
        return $"{EmitExpression(node.receiver)}[{EmitExpression(node.index)}]";
    }

    private string EmitPointerIndexAccessExpression(BoundPointerIndexAccessExpression node) {
        return $"{EmitExpression(node.receiver)}[{EmitExpression(node.index)}]";
    }

    private string EmitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node) {
        var op = SyntaxFacts.GetText(node.op.kind.ToSyntaxKind());
        return $"{EmitExpression(node.left)} {op}= {EmitExpression(node.right)}";
    }

    private string EmitReferenceExpression(BoundReferenceExpression node) {
        return $"ref {EmitExpression(node.expression)}";
    }

    private string EmitTypeOfExpression(BoundTypeOfExpression node) {
        return $"typeof({EmitExpression(node.sourceType)})";
    }

    private string EmitConditionalOperator(BoundConditionalOperator node) {
        return $"({EmitExpression(node.condition)} ? {EmitExpression(node.trueExpression)} : {EmitExpression(node.falseExpression)})";
    }

    private string EmitObjectCreationExpression(BoundObjectCreationExpression node) {
        return $"new {_module.GetType(node.constructor.containingType)}({EmitArguments(node.arguments, node.argumentRefKinds)})";
    }

    private string EmitArrayCreationExpression(BoundArrayCreationExpression node) {
        var type = _module.GetType(((ArrayTypeSymbol)node.type).elementType);
        var sizes = string.Join(", ", node.sizes.Select(i => EmitExpression(i)));

        if (node.initializer is not null)
            return $"new {type}[{sizes}] {EmitExpression(node.initializer)}";
        else
            return $"new {type}[{sizes}]";
    }

    private string EmitFieldAccessExpression(BoundFieldAccessExpression node) {
        if (node.receiver is null)
            return $"{_module.GetType(node.field.containingType)}.{_module.GetSafeName(node.field.name)}";

        return $"{EmitExpression(node.receiver)}.{_module.GetSafeName(node.field.name)}";
    }

    private string EmitConditionalAccessExpression(BoundConditionalAccessExpression node) {
        var receiver = EmitExpression(node.receiver);
        var access = node.accessExpression;

        switch (access.kind) {
            case BoundKind.FieldAccessExpression:
                var fieldAccess = (BoundFieldAccessExpression)access;
                return $"{receiver}?.{_module.GetSafeName(fieldAccess.field.name)}";
            case BoundKind.ArrayAccessExpression:
                var arrayAccess = (BoundArrayAccessExpression)access;
                return $"{receiver}?[{EmitExpression(arrayAccess.index)}]";
            case BoundKind.CallExpression:
                var callAccess = (BoundCallExpression)access;
                return $"{receiver}?.{EmitCallExpression(callAccess, skipReceiver: true)}";
            default:
                throw ExceptionUtilities.UnexpectedValue(access.kind);
        }
    }

    private string EmitThisExpression(BoundThisExpression _) {
        return "this";
    }

    private string EmitBaseExpression(BoundBaseExpression _) {
        return "base";
    }

    private string EmitThrowExpression(BoundThrowExpression node) {
        return $"throw {EmitExpression(node.expression)}";
    }

    private string EmitTypeExpression(BoundTypeExpression node) {
        return _module.GetType(node.type);
    }

    private string EmitNamespaceExpression(BoundNamespaceExpression node) {
        return _module.GetSafeName(node.namespaceSymbol.name);
    }

    private string EmitParameterExpression(BoundParameterExpression node) {
        return _module.GetSafeName(node.parameter.name);
    }

    private string EmitMethodGroup(BoundMethodGroup node) {
        return _module.GetSafeName(node.methods[0].name);
    }

    private string EmitFunctionPointerLoad(BoundFunctionPointerLoad node) {
        return $"&{_module.GetSafeName(node.targetMethod.name)}";
    }

    private string EmitFunctionPointerCallExpression(BoundFunctionPointerCallExpression node) {
        var method = _module.GetSafeName(node.functionPointer.name);
        var arguments = EmitArguments(node.arguments, node.argumentRefKindsOpt);
        return $"{method}({arguments})";
    }

    private string EmitSizeOfOperator(BoundSizeOfOperator node) {
        return $"sizeof({EmitExpression(node.sourceType)})";
    }

    private string EmitConvertedStackAllocExpression(BoundConvertedStackAllocExpression node) {
        return $"stackalloc {_module.GetType(node.elementType)}[{EmitExpression(node.count)}]";
    }

    private string EmitInterpolatedStringExpression(BoundInterpolatedStringExpression node) {
        return $"$\"{node.contents.Select(c => FormatContent(c))}\"";

        string FormatContent(BoundExpression expression) {
            if (expression.constantValue?.specialType == SpecialType.String)
                return (string)expression.constantValue.value;

            return $"{{{EmitExpression(expression)}}}";
        }
    }

    private string EmitFunctionLoad(BoundFunctionLoad node) {
        return _module.GetSafeName(node.targetMethod.name);
    }

    private string EmitIsPatternExpression(BoundIsPatternExpression node) {
        var local = node.local;
        return $"{EmitExpression(node.expression)} is {_module.GetType(local.type)} {_module.GetSafeName(local.name)}";
    }

    #endregion
}
