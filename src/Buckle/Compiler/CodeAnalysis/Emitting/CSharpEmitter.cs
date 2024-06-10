using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Libraries.Standard;
using Buckle.Utilities;
using Shared;

namespace Buckle.CodeAnalysis.Emitting;

/// <summary>
/// Emits a bound program into a C# source.
/// </summary>
internal sealed class CSharpEmitter {
    private static readonly List<string> ValueTypes = new List<string>() { "bool", "double", "int" };
    private static readonly string IndentString = "    ";

    private bool _insideMain;
    private bool _insideReturningT;
    private ImmutableDictionary<MethodSymbol, BoundBlockStatement> _methods;

    /// <summary>
    /// Emits a program to a C# source.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="outputPath">Where to put the emitted assembly.</param>
    /// <returns>Diagnostics.</returns>
    internal static BelteDiagnosticQueue Emit(BoundProgram program, string outputPath) {
        if (program.diagnostics.Errors().Any())
            return program.diagnostics;

        var stringWriter = Emit(program, Path.GetFileNameWithoutExtension(outputPath), out var diagnostics);

        using (var writer = new StreamWriter(outputPath))
            writer.Write(stringWriter);

        return diagnostics;
    }

    /// <summary>
    /// Emits a program to a string.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="namespaceName">
    /// Name of the namespace that will contain the program; prevents symbol contamination.
    /// </param>
    /// <param name="diagnostics">Any produced diagnostics.</param>
    /// <returns>C# source as a string.</returns>
    internal static string Emit(
        BoundProgram program, string namespaceName, out BelteDiagnosticQueue diagnostics) {
        var emitter = new CSharpEmitter();
        return emitter.EmitInternal(program, namespaceName, out diagnostics);
    }

    private string EmitInternal(BoundProgram program, string namespaceName, out BelteDiagnosticQueue diagnostics) {
        var stringWriter = new StringWriter();

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, IndentString)) {
            indentedTextWriter.WriteLine("using System;");
            indentedTextWriter.WriteLine();

            if (!program.usedLibraryTypes.IsEmpty && program.previous?.previous?.previous is null) {
                using (var libraryCurly = new CurlyIndenter(indentedTextWriter, "namespace Belte")) {
                    _methods = program.previous.methodBodies;
                    indentedTextWriter.WriteLine();

                    foreach (var @struct in program.usedLibraryTypes.Where(t => t is StructSymbol))
                        EmitStruct(indentedTextWriter, @struct as StructSymbol);

                    foreach (var @class in program.usedLibraryTypes.Where(t => t is ClassSymbol)) {
                        if (@class.name != "Exception")
                            EmitClass(indentedTextWriter, @class as ClassSymbol);
                    }
                }

                indentedTextWriter.WriteLine();
            }

            using (var namespaceCurly = new CurlyIndenter(
                indentedTextWriter, $"namespace {GetSafeName(namespaceName)}")) {
                _methods = program.methodBodies;
                indentedTextWriter.WriteLine();

                using (var programClassCurly = new CurlyIndenter(indentedTextWriter, "public static class Program")) {
                    indentedTextWriter.WriteLine();

                    foreach (var @struct in program.types.Where(t => t is StructSymbol))
                        EmitStruct(indentedTextWriter, @struct as StructSymbol);

                    foreach (var @class in program.types.Where(t => t is ClassSymbol))
                        EmitClass(indentedTextWriter, @class as ClassSymbol);

                    if (program.entryPoint != null) {
                        var mainBody = MethodUtilities.LookupMethod(_methods, program.entryPoint);
                        EmitMainMethod(indentedTextWriter, KeyValuePair.Create(program.entryPoint, mainBody));
                    } else {
                        EmitEmptyMainMethod(indentedTextWriter);
                    }

                    foreach (var methodWithBody in _methods) {
                        if (methodWithBody.Key != program.entryPoint)
                            EmitMethod(indentedTextWriter, methodWithBody);
                    }
                }

                indentedTextWriter.WriteLine();
            }

            indentedTextWriter.Flush();
        }

        stringWriter.Flush();
        diagnostics = new BelteDiagnosticQueue();

        var stringBuilder = new StringBuilder();

        foreach (var line in stringWriter.ToString().Split(Environment.NewLine)) {
            stringBuilder.Append(line.TrimEnd());
            stringBuilder.Append(Environment.NewLine);
        }

        // Adds a final trailing new line
        return stringBuilder.ToString().TrimEnd() + Environment.NewLine;
    }

    private string GetEquivalentType(
        BoundType type,
        bool makeReferenceExplicit = false,
        bool includeRankSizes = false) {

        string GetEquivalentTypeName(TypeSymbol typeSymbol) {
            if (typeSymbol is NamedTypeSymbol)
                return GetSafeName(typeSymbol.name);

            if (typeSymbol == TypeSymbol.Bool)
                return "bool";
            if (typeSymbol == TypeSymbol.Decimal)
                return "double";
            if (typeSymbol == TypeSymbol.Int)
                return "int";
            if (typeSymbol == TypeSymbol.String)
                return "string";

            return "object";
        }

        if (type.typeSymbol == TypeSymbol.Void)
            return "void";

        var equivalentType = new StringBuilder();
        var typeNameBuilder = new StringBuilder(GetEquivalentTypeName(type.typeSymbol));
        var isFirst = true;

        foreach (var templateParameter in type.templateArguments) {
            if (isFirst) {
                typeNameBuilder.Append('<');
                isFirst = false;
            } else {
                typeNameBuilder.Append(", ");
            }

            if (templateParameter.isConstant) {
                if (templateParameter.constant == null) {
                    var stringWriter = new StringWriter();
                    var indentedWriter = new IndentedTextWriter(stringWriter, IndentString);
                    EmitExpression(indentedWriter, templateParameter.expression);
                    indentedWriter.Flush();
                    stringWriter.Flush();
                    typeNameBuilder.Append(stringWriter.ToString());
                } else {
                    typeNameBuilder.Append(GetEquivalentConstant(templateParameter.constant, templateParameter.type));
                }
            } else {
                typeNameBuilder.Append(GetEquivalentType(templateParameter.type));
            }
        }

        if (!isFirst)
            typeNameBuilder.Append('>');

        var typeName = typeNameBuilder.ToString();

        // All logic relating to constants has already been handled by the Binder,
        // so specifying const here would not do anything

        if ((type.isExplicitReference ||
            type.isReference &&
            makeReferenceExplicit) &&
            (ValueTypes.Contains(typeName) || type.typeSymbol is StructSymbol)) {
            equivalentType.Append("ref ");
        }

        if (type.isNullable && (ValueTypes.Contains(typeName) || type.typeSymbol is StructSymbol))
            typeName = $"global::System.Nullable<{typeName}>";

        equivalentType.Append(typeName);

        if (type.dimensions > 0) {
            equivalentType.Append('[');
            isFirst = true;

            using (var typeWriter = new StringWriter())
            using (var indentedTypeWriter = new IndentedTextWriter(typeWriter)) {
                for (var i = 0; i < type.dimensions; i++) {
                    if (!isFirst)
                        indentedTypeWriter.Write(',');

                    if (isFirst)
                        isFirst = false;

                    if (includeRankSizes)
                        EmitExpression(indentedTypeWriter, type.sizes[i]);
                }

                indentedTypeWriter.Flush();
                equivalentType.Append(typeWriter.ToString());
            }

            equivalentType.Append(']');

            if (!includeRankSizes)
                equivalentType.Append('?');
        }

        return equivalentType.ToString();
    }

    private string GetEquivalentConstant(BoundConstant constant, BoundType type) {
        var builder = new StringBuilder();

        if (constant.value is ImmutableArray<BoundConstant> ia) {
            builder.Append($"new {GetEquivalentType(type)} {{ ");

            var isFirst = true;

            foreach (var item in ia) {
                if (isFirst)
                    isFirst = false;
                else
                    builder.Append(", ");

                builder.Append(GetEquivalentConstant(item, type.ChildType()));
            }

            builder.Append(" }");
        } else {
            builder.Append(DisplayText.FormatLiteral(constant.value));
        }

        return builder.ToString();
    }

    private string GetSafeName(string name) {
        var provider = CodeDomProvider.CreateProvider("C#");
        return (provider.IsValidIdentifier(name) ? name : "@" + name)
            .Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }

    private string GetModifiers(Symbol symbol) {
        var modifiers = new StringBuilder();

        switch (symbol.accessibility) {
            case Accessibility.Public:
            case Accessibility.NotApplicable when symbol is NamedTypeSymbol:
                modifiers.Append("public ");
                break;
            case Accessibility.Protected:
                modifiers.Append("protected ");
                break;
            case Accessibility.Private:
                modifiers.Append("private ");
                break;
        }

        if (symbol.isVirtual)
            modifiers.Append("virtual ");
        if (symbol.isSealed)
            modifiers.Append("sealed ");
        if (symbol.isAbstract)
            modifiers.Append("abstract ");
        if (symbol.isOverride)
            modifiers.Append("override ");
        if (symbol.isStatic)
            modifiers.Append("static ");

        return modifiers.ToString();
    }

    private void EmitStruct(IndentedTextWriter indentedTextWriter, StructSymbol @struct) {
        var signature = new StringBuilder($"{GetModifiers(@struct)}struct {GetSafeName(@struct.name)}");
        var firstTemplate = true;
        var needsCloseBracket = false;

        foreach (var templateType in @struct.members.OfType<TemplateTypeSymbol>()) {
            if (firstTemplate) {
                needsCloseBracket = true;
                signature.Append($"<{GetSafeName(templateType.name)}");
            } else {
                signature.Append($", {GetSafeName(templateType.name)}");
            }
        }

        if (needsCloseBracket)
            signature.Append('>');

        using (var structCurly = new CurlyIndenter(indentedTextWriter, signature.ToString())) {
            foreach (var field in @struct.members.OfType<FieldSymbol>())
                EmitField(indentedTextWriter, field);
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitClass(IndentedTextWriter indentedTextWriter, ClassSymbol @class) {
        var signature = new StringBuilder($"{GetModifiers(@class)}class {GetSafeName(@class.name)}");
        var firstTemplate = true;
        var needsCloseBracket = false;

        foreach (var templateType in @class.members.OfType<TemplateTypeSymbol>()) {
            if (firstTemplate) {
                needsCloseBracket = true;
                signature.Append($"<{GetSafeName(templateType.name)}");
            } else {
                signature.Append($", {GetSafeName(templateType.name)}");
            }
        }

        if (needsCloseBracket)
            signature.Append('>');

        signature.Append($" : {GetEquivalentType(@class.baseType)}");

        using (var classCurly = new CurlyIndenter(indentedTextWriter, signature.ToString())) {
            foreach (var parameter in @class.members.OfType<ParameterSymbol>()) {
                if (parameter.type.typeSymbol != TypeSymbol.Type)
                    EmitTemplateParameter(indentedTextWriter, parameter);
            }

            foreach (var field in @class.members.OfType<FieldSymbol>()) {
                if (!field.isConstant)
                    EmitField(indentedTextWriter, field);
            }

            indentedTextWriter.WriteLine();

            foreach (var constructor in @class.constructors)
                EmitConstructor(indentedTextWriter, @class.name, constructor, @class.templateParameters);

            foreach (var method in @class.members.OfType<MethodSymbol>()
                .Where(m => m.name != WellKnownMemberNames.InstanceConstructorName)) {
                EmitMethod(indentedTextWriter, KeyValuePair.Create(method, _methods[method]), false);
            }

            foreach (var type in @class.members.OfType<NamedTypeSymbol>()) {
                if (type is ClassSymbol c)
                    EmitClass(indentedTextWriter, c);
                if (type is StructSymbol s)
                    EmitStruct(indentedTextWriter, s);
            }
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitField(IndentedTextWriter indentedTextWriter, FieldSymbol field) {
        indentedTextWriter.WriteLine(
            $"{GetModifiers(field)}{GetEquivalentType(field.type)} {GetSafeName(field.name)};"
        );
    }

    private void EmitTemplateParameter(IndentedTextWriter indentedTextWriter, ParameterSymbol parameter) {
        indentedTextWriter.Write($"private {GetEquivalentType(parameter.type)} {GetSafeName(parameter.name)};");
    }

    private void EmitConstructor(
        IndentedTextWriter indentedTextWriter,
        string name,
        MethodSymbol constructor,
        ImmutableArray<ParameterSymbol> templateParameters) {
        var parametersSignature = new StringBuilder();
        var isFirst = true;

        void AddParameters(IEnumerable<ParameterSymbol> parameters) {
            foreach (var parameter in parameters) {
                if (isFirst)
                    isFirst = false;
                else
                    parametersSignature.Append(", ");

                parametersSignature.Append($"{GetEquivalentType(parameter.type)} {GetSafeName(parameter.name)}");
            }
        }

        AddParameters(templateParameters.Where(p => p.type.typeSymbol != TypeSymbol.Type));
        AddParameters(constructor.parameters);

        var constructorInitializer = _methods[constructor].statements[0] as BoundExpressionStatement;
        var initializer = constructorInitializer.expression as BoundCallExpression;
        var constructorKeyword = initializer.expression is BoundThisExpression ? "this" : "base";
        indentedTextWriter.Write(
            $"{GetModifiers(constructor)}{GetSafeName(name)}({parametersSignature}) : {constructorKeyword}"
        );

        EmitArguments(indentedTextWriter, initializer.arguments, initializer.method.parameters);
        indentedTextWriter.Write(" ");

        using (var methodCurly = new CurlyIndenter(indentedTextWriter)) {
            foreach (var parameter in templateParameters) {
                if (parameter.type.typeSymbol != TypeSymbol.Type)
                    indentedTextWriter.WriteLine($"this.{parameter.name} = {parameter.name};");
            }

            EmitBody(indentedTextWriter, _methods[constructor].statements.Skip(1));
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitMainMethod(
        IndentedTextWriter indentedTextWriter, KeyValuePair<MethodSymbol, BoundBlockStatement> method) {
        var typeName = method.Key.type.typeSymbol == TypeSymbol.Void ? "void" : "int";
        var signature = $"public static {typeName} Main()";

        using (var methodCurly = new CurlyIndenter(indentedTextWriter, signature)) {
            _insideMain = true;
            EmitBody(indentedTextWriter, method.Value);
            _insideMain = false;
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitEmptyMainMethod(IndentedTextWriter indentedTextWriter) {
        indentedTextWriter.WriteLine("public static void Main() { }");
        indentedTextWriter.WriteLine();
    }

    private void EmitMethod(
        IndentedTextWriter indentedTextWriter,
        KeyValuePair<MethodSymbol, BoundBlockStatement> method,
        bool ignoreContained = true) {
        if (method.Key.containingType != null && ignoreContained)
            return;

        var parameters = new StringBuilder();
        var isFirst = true;

        foreach (var parameter in method.Key.parameters) {
            if (isFirst)
                isFirst = false;
            else
                parameters.Append(", ");

            parameters.Append($"{GetEquivalentType(parameter.type)} {GetSafeName(parameter.name)}");
        }

        var signature = $"{GetModifiers(method.Key)}" +
            $"{GetEquivalentType(method.Key.type)} {GetSafeName(method.Key.name)}({parameters})";

        if (method.Key.type.typeSymbol is TemplateTypeSymbol)
            _insideReturningT = true;

        using (var methodCurly = new CurlyIndenter(indentedTextWriter, signature))
            EmitBody(indentedTextWriter, method.Value);

        _insideReturningT = false;
        indentedTextWriter.WriteLine();
    }

    private void EmitBody(IndentedTextWriter indentedTextWriter, BoundBlockStatement body) {
        foreach (var statement in body.statements)
            EmitStatement(indentedTextWriter, statement);
    }

    private void EmitBody(IndentedTextWriter indentedTextWriter, IEnumerable<BoundStatement> body) {
        foreach (var statement in body)
            EmitStatement(indentedTextWriter, statement);
    }

    private void EmitStatement(IndentedTextWriter indentedTextWriter, BoundStatement statement) {
        switch (statement.kind) {
            case BoundNodeKind.BlockStatement:
                EmitBody(indentedTextWriter, (BoundBlockStatement)statement);
                break;
            case BoundNodeKind.NopStatement:
                EmitNopStatement(indentedTextWriter, (BoundNopStatement)statement);
                break;
            case BoundNodeKind.ExpressionStatement:
                EmitExpressionStatement(indentedTextWriter, (BoundExpressionStatement)statement);
                break;
            case BoundNodeKind.LocalDeclarationStatement:
                EmitLocalDeclarationStatement(indentedTextWriter, (BoundLocalDeclarationStatement)statement);
                break;
            case BoundNodeKind.GotoStatement:
                EmitGotoStatement(indentedTextWriter, (BoundGotoStatement)statement);
                break;
            case BoundNodeKind.LabelStatement:
                EmitLabelStatement(indentedTextWriter, (BoundLabelStatement)statement);
                break;
            case BoundNodeKind.ConditionalGotoStatement:
                EmitConditionalGotoStatement(indentedTextWriter, (BoundConditionalGotoStatement)statement);
                break;
            case BoundNodeKind.ReturnStatement:
                EmitReturnStatement(indentedTextWriter, (BoundReturnStatement)statement);
                break;
            case BoundNodeKind.TryStatement:
                EmitTryStatement(indentedTextWriter, (BoundTryStatement)statement);
                break;
            case BoundNodeKind.IfStatement:
                EmitIfStatement(indentedTextWriter, (BoundIfStatement)statement);
                break;
            case BoundNodeKind.ForStatement:
                EmitForStatement(indentedTextWriter, (BoundForStatement)statement);
                break;
            case BoundNodeKind.WhileStatement:
                EmitWhileStatement(indentedTextWriter, (BoundWhileStatement)statement);
                break;
            case BoundNodeKind.DoWhileStatement:
                EmitDoWhileStatement(indentedTextWriter, (BoundDoWhileStatement)statement);
                break;
            case BoundNodeKind.BreakStatement:
                EmitBreakStatement(indentedTextWriter, (BoundBreakStatement)statement);
                break;
            case BoundNodeKind.ContinueStatement:
                EmitContinueStatement(indentedTextWriter, (BoundContinueStatement)statement);
                break;
            default:
                throw new BelteInternalException($"EmitStatement: unexpected node '{statement.kind}'");
        }
    }

    private void EmitNopStatement(IndentedTextWriter indentedTextWriter, BoundNopStatement _) {
        indentedTextWriter.WriteLine(";");
    }

    private void EmitExpressionStatement(IndentedTextWriter indentedTextWriter, BoundExpressionStatement statement) {
        if (statement.expression is not BoundEmptyExpression) {
            EmitExpression(indentedTextWriter, statement.expression);
            indentedTextWriter.WriteLine(";");
        }
    }

    private void EmitLocalDeclarationStatement(
        IndentedTextWriter indentedTextWriter, BoundLocalDeclarationStatement statement) {
        var variable = statement.declaration.variable;
        var initializer = statement.declaration.initializer;

        indentedTextWriter.Write(GetEquivalentType(variable.type, true));
        indentedTextWriter.Write($" {GetSafeName(variable.name)}");

        if (initializer != null) {
            indentedTextWriter.Write(" = ");
            EmitExpression(indentedTextWriter, initializer);
        }

        indentedTextWriter.WriteLine(";");
    }


    private void EmitGotoStatement(IndentedTextWriter indentedTextWriter, BoundGotoStatement statement) {
        indentedTextWriter.WriteLine($"goto {statement.label.name};");
    }

    private void EmitLabelStatement(IndentedTextWriter indentedTextWriter, BoundLabelStatement statement) {
        indentedTextWriter.Indent--;
        indentedTextWriter.WriteLine($"{statement.label.name}:");
        indentedTextWriter.Indent++;
    }

    private void EmitConditionalGotoStatement(
        IndentedTextWriter indentedTextWriter, BoundConditionalGotoStatement statement) {
        indentedTextWriter.Write($"if (");

        if (statement.jumpIfTrue)
            indentedTextWriter.Write("(");
        else
            indentedTextWriter.Write("!(");

        EmitExpression(indentedTextWriter, statement.condition);

        if (statement.condition.type.isNullable)
            indentedTextWriter.WriteLine(") ?? throw new global::System.NullReferenceException())");
        else
            indentedTextWriter.WriteLine("))");

        indentedTextWriter.Indent++;
        indentedTextWriter.WriteLine($"goto {statement.label.name};");
        indentedTextWriter.Indent--;
        indentedTextWriter.WriteLine();
    }

    private void EmitReturnStatement(IndentedTextWriter indentedTextWriter, BoundReturnStatement statement) {
        if (statement.expression is null) {
            indentedTextWriter.WriteLine("return;");
        } else {
            indentedTextWriter.Write("return ");

            if (_insideMain && BoundConstant.IsNull(statement.expression.constantValue)) {
                indentedTextWriter.WriteLine("0;");
                return;
            }

            if (_insideReturningT && BoundConstant.IsNull(statement.expression.constantValue)) {
                indentedTextWriter.WriteLine($"default({GetEquivalentType(statement.expression.type)});");
                return;
            }

            EmitExpression(indentedTextWriter, statement.expression);

            if (_insideMain && statement.expression.type.isNullable)
                indentedTextWriter.WriteLine(" ?? 0;");
            else if (_insideReturningT && statement.expression.type.isNullable)
                indentedTextWriter.WriteLine($" ?? default({GetEquivalentType(statement.expression.type)});");
            else
                indentedTextWriter.WriteLine(";");
        }
    }

    private void EmitTryStatement(IndentedTextWriter indentedTextWriter, BoundTryStatement statement) {
        using (var tryCurly = new CurlyIndenter(indentedTextWriter, "try"))
            EmitBody(indentedTextWriter, statement.body);

        if (statement.catchBody != null) {
            using var catchCurly = new CurlyIndenter(indentedTextWriter, "catch");
            EmitBody(indentedTextWriter, statement.catchBody);
        }

        if (statement.finallyBody != null) {
            using var finallyCurly = new CurlyIndenter(indentedTextWriter, "finally");
            EmitBody(indentedTextWriter, statement.finallyBody);
        }
    }

    private void EmitNullProtectedExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        if (expression.type.isNullable)
            indentedTextWriter.Write("(");

        EmitExpression(indentedTextWriter, expression);

        if (expression.type.isNullable)
            indentedTextWriter.Write(" ?? throw new global::System.NullReferenceException())");
    }

    private void EmitIfStatement(IndentedTextWriter indentedTextWriter, BoundIfStatement statement) {
        indentedTextWriter.Write("if (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);

        using (var ifCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.then);

        if (statement.elseStatement != null) {
            using var elseCurly = new CurlyIndenter(indentedTextWriter, "else");
            EmitStatement(indentedTextWriter, statement.elseStatement);
        }

        indentedTextWriter.WriteLine();
    }

    private void EmitForStatement(IndentedTextWriter indentedTextWriter, BoundForStatement statement) {
        indentedTextWriter.Write("for (");
        EmitStatement(indentedTextWriter, statement.initializer);
        indentedTextWriter.Indent++;
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);
        indentedTextWriter.Write("; ");
        EmitExpression(indentedTextWriter, statement.step);
        indentedTextWriter.Indent--;

        using (var forCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.body);

        indentedTextWriter.WriteLine();
    }

    private void EmitWhileStatement(IndentedTextWriter indentedTextWriter, BoundWhileStatement statement) {
        indentedTextWriter.Write("while (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);

        using (var forCurly = new CurlyIndenter(indentedTextWriter, ")"))
            EmitStatement(indentedTextWriter, statement.body);

        indentedTextWriter.WriteLine();
    }

    private void EmitDoWhileStatement(IndentedTextWriter indentedTextWriter, BoundDoWhileStatement statement) {
        using (var forCurly = new CurlyIndenter(indentedTextWriter, "do"))
            EmitStatement(indentedTextWriter, statement.body);

        indentedTextWriter.Write("while (");
        EmitNullProtectedExpression(indentedTextWriter, statement.condition);
        indentedTextWriter.WriteLine(");");
        indentedTextWriter.WriteLine();
    }

    private void EmitBreakStatement(IndentedTextWriter indentedTextWriter, BoundBreakStatement _) {
        indentedTextWriter.WriteLine("break;");
    }

    private void EmitContinueStatement(IndentedTextWriter indentedTextWriter, BoundContinueStatement _) {
        indentedTextWriter.WriteLine("continue;");
    }

    private void EmitExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        if (expression.constantValue != null) {
            EmitConstantExpression(indentedTextWriter, expression);
            return;
        }

        switch (expression.kind) {
            case BoundNodeKind.LiteralExpression:
                if (expression is BoundInitializerListExpression il) {
                    EmitInitializerListExpression(indentedTextWriter, il);
                    break;
                } else {
                    goto default;
                }
            case BoundNodeKind.UnaryExpression:
                EmitUnaryExpression(indentedTextWriter, (BoundUnaryExpression)expression);
                break;
            case BoundNodeKind.BinaryExpression:
                EmitBinaryExpression(indentedTextWriter, (BoundBinaryExpression)expression);
                break;
            case BoundNodeKind.VariableExpression:
                EmitVariableExpression(indentedTextWriter, (BoundVariableExpression)expression);
                break;
            case BoundNodeKind.AssignmentExpression:
                EmitAssignmentExpression(indentedTextWriter, (BoundAssignmentExpression)expression);
                break;
            case BoundNodeKind.EmptyExpression:
                EmitEmptyExpression(indentedTextWriter, (BoundEmptyExpression)expression);
                break;
            case BoundNodeKind.CallExpression:
                EmitCallExpression(indentedTextWriter, (BoundCallExpression)expression);
                break;
            case BoundNodeKind.IndexExpression:
                EmitIndexExpression(indentedTextWriter, (BoundIndexExpression)expression);
                break;
            case BoundNodeKind.CastExpression:
                EmitCastExpression(indentedTextWriter, (BoundCastExpression)expression);
                break;
            case BoundNodeKind.TernaryExpression:
                EmitTernaryExpression(indentedTextWriter, (BoundTernaryExpression)expression);
                break;
            case BoundNodeKind.PrefixExpression:
                EmitPrefixExpression(indentedTextWriter, (BoundPrefixExpression)expression);
                break;
            case BoundNodeKind.PostfixExpression:
                EmitPostfixExpression(indentedTextWriter, (BoundPostfixExpression)expression);
                break;
            case BoundNodeKind.CompoundAssignmentExpression:
                EmitCompoundAssignmentExpression(indentedTextWriter, (BoundCompoundAssignmentExpression)expression);
                break;
            case BoundNodeKind.ReferenceExpression:
                EmitReferenceExpression(indentedTextWriter, (BoundReferenceExpression)expression);
                break;
            case BoundNodeKind.ObjectCreationExpression:
                EmitObjectCreationExpression(indentedTextWriter, (BoundObjectCreationExpression)expression);
                break;
            case BoundNodeKind.MemberAccessExpression:
                EmitMemberAccessExpression(indentedTextWriter, (BoundMemberAccessExpression)expression);
                break;
            case BoundNodeKind.ThisExpression:
                EmitThisExpression(indentedTextWriter, (BoundThisExpression)expression);
                break;
            case BoundNodeKind.BaseExpression:
                EmitBaseExpression(indentedTextWriter, (BoundBaseExpression)expression);
                break;
            case BoundNodeKind.Type:
                EmitType(indentedTextWriter, (BoundType)expression);
                break;
            default:
                throw new BelteInternalException($"EmitExpression: unexpected node '{expression.kind}'");
        }
    }

    private void EmitConstantExpression(IndentedTextWriter indentedTextWriter, BoundExpression expression) {
        EmitBoundConstant(indentedTextWriter, expression.constantValue, expression.type);
    }

    private void EmitBoundConstant(IndentedTextWriter indentedTextWriter, BoundConstant constant, BoundType type) {
        indentedTextWriter.Write(GetEquivalentConstant(constant, type));
    }

    private void EmitInitializerListExpression(
        IndentedTextWriter indentedTextWriter, BoundInitializerListExpression expression) {
        indentedTextWriter.Write($"new {GetEquivalentType(expression.type)} {{ ");

        var isFirst = true;

        foreach (var item in expression.items) {
            if (isFirst)
                isFirst = false;
            else
                indentedTextWriter.Write(", ");

            EmitExpression(indentedTextWriter, item);
        }

        indentedTextWriter.Write(" }");
    }

    private void EmitUnaryExpression(IndentedTextWriter indentedTextWriter, BoundUnaryExpression expression) {
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
        EmitExpression(indentedTextWriter, expression.operand);
    }

    private void EmitBinaryExpression(IndentedTextWriter indentedTextWriter, BoundBinaryExpression expression) {
        indentedTextWriter.Write("(");
        EmitExpression(indentedTextWriter, expression.left);

        if (expression.op.opKind == BoundBinaryOperatorKind.Isnt)
            indentedTextWriter.Write(" is not ");
        else
            indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.kind)} ");

        EmitExpression(indentedTextWriter, expression.right);
        indentedTextWriter.Write(")");
    }

    private void EmitVariableExpression(IndentedTextWriter indentedTextWriter, BoundVariableExpression expression) {
        indentedTextWriter.Write(GetSafeName(expression.variable.name));
    }

    private void EmitAssignmentExpression(
        IndentedTextWriter indentedTextWriter, BoundAssignmentExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write(" = ");
        EmitExpression(indentedTextWriter, expression.right);
    }

    private void EmitEmptyExpression(IndentedTextWriter _, BoundEmptyExpression _1) { }

    private void EmitCallExpression(IndentedTextWriter indentedTextWriter, BoundCallExpression expression) {
        if (expression.method == BuiltinMethods.RandInt) {
            var signature = $"global::System.Func<{GetEquivalentType(expression.type)}>";
            indentedTextWriter.Write(
                $"(({signature})(() => {{ var random = new global::System.Random(); var temp = "
            );

            EmitExpression(indentedTextWriter, expression.arguments[0]);
            indentedTextWriter.Write("; ");

            if (expression.arguments[0].type.isNullable)
                indentedTextWriter.Write("return temp.HasValue ? random.Next(temp.Value) : random.Next();");
            else
                indentedTextWriter.Write("return random.Next(temp);");

            indentedTextWriter.Write(" }))()");

            return;
        } else if (expression.method == BuiltinMethods.ValueString ||
                   expression.method == BuiltinMethods.ValueDecimal ||
                   expression.method == BuiltinMethods.ValueAny ||
                   expression.method == BuiltinMethods.ValueBool ||
                   expression.method == BuiltinMethods.ValueInt) {
            EmitExpression(indentedTextWriter, expression.arguments[0]);

            if (GetEquivalentType(expression.arguments[0].type).StartsWith("global::System.Nullable"))
                indentedTextWriter.Write(".Value");

            return;
        } else if (expression.method == BuiltinMethods.HasValueAny ||
                   expression.method == BuiltinMethods.HasValueBool ||
                   expression.method == BuiltinMethods.HasValueDecimal ||
                   expression.method == BuiltinMethods.HasValueInt ||
                   expression.method == BuiltinMethods.HasValueString) {
            if (GetEquivalentType(expression.arguments[0].type).StartsWith("global::System.Nullable")) {
                EmitExpression(indentedTextWriter, expression.arguments[0]);
                indentedTextWriter.Write(".HasValue");
            } else if (expression.arguments[0].type.isNullable) {
                EmitExpression(indentedTextWriter, expression.arguments[0]);
                indentedTextWriter.Write($" is not null");
            } else {
                indentedTextWriter.Write("true");
            }

            return;
        } else if (expression.method == BuiltinMethods.Hex || expression.method == BuiltinMethods.NullableHex) {
            indentedTextWriter.Write("(");
            EmitExpression(indentedTextWriter, expression.arguments[1]);
            indentedTextWriter.Write(" ? \"0x\" + ");
            EmitExpression(indentedTextWriter, expression.arguments[0]);
            indentedTextWriter.Write(".ToString(\"X\") : ");
            EmitExpression(indentedTextWriter, expression.arguments[0]);
            indentedTextWriter.Write(".ToString(\"X\"))");
            return;
        } else if (expression.method == BuiltinMethods.Ascii || expression.method == BuiltinMethods.NullableAscii) {
            indentedTextWriter.Write("(int)char.Parse(");
            EmitExpression(indentedTextWriter, expression.arguments[0]);
            indentedTextWriter.Write(")");
            return;
        } else if (expression.method == BuiltinMethods.Char || expression.method == BuiltinMethods.NullableChar) {
            EmitExpression(indentedTextWriter, expression.arguments[0]);
            indentedTextWriter.Write(".ToString()");
            return;
        } else if (expression.method == BuiltinMethods.Length) {
            indentedTextWriter.Write(
                "((global::System.Func<object, int?>)((x) => {{ return x is object[] y ? y.Length : null; }} ))("
            );

            EmitExpression(indentedTextWriter, expression.arguments[0]);
            indentedTextWriter.Write(")");
            return;
        } else if (expression.method.originalDefinition == BuiltinMethods.ToAny ||
            expression.method.originalDefinition == BuiltinMethods.ToObject) {
            EmitExpression(indentedTextWriter, expression.arguments[0]);
            return;
        }

        if (expression.method.containingType == StandardLibrary.Console ||
            expression.method.containingType == StandardLibrary.Math) {
            indentedTextWriter.Write(StandardLibrary.CSharpEmitMethod(expression.method));
        } else {
            if (expression.expression is not BoundEmptyExpression) {
                if (!(expression.expression is BoundThisExpression or BoundBaseExpression &&
                    expression.method.isStatic)) {
                    EmitExpression(indentedTextWriter, expression.expression);
                    indentedTextWriter.Write(".");
                }
            }

            indentedTextWriter.Write(GetSafeName(expression.method.name));
        }

        EmitArguments(indentedTextWriter, expression.arguments, expression.method.parameters);
    }

    private void EmitArguments(
        IndentedTextWriter indentedTextWriter,
        ImmutableArray<BoundExpression> arguments,
        ImmutableArray<ParameterSymbol> parameters) {
        indentedTextWriter.Write("(");

        var isFirst = true;

        for (var i = 0; i < arguments.Length; i++) {
            if (isFirst)
                isFirst = false;
            else
                indentedTextWriter.Write(", ");

            var argument = arguments[i];
            var type = parameters.Length > i ? parameters[i].type : null;

            if (type is not null && !type.isExplicitReference && argument is BoundReferenceExpression r)
                EmitExpression(indentedTextWriter, r.expression);
            else
                EmitExpression(indentedTextWriter, argument);
        }

        indentedTextWriter.Write(")");
    }

    private void EmitIndexExpression(IndentedTextWriter indentedTextWriter, BoundIndexExpression expression) {
        EmitExpression(indentedTextWriter, expression.expression);
        indentedTextWriter.Write("[");
        EmitExpression(indentedTextWriter, expression.index);

        if (expression.index.type.isNullable)
            indentedTextWriter.Write(".Value");

        indentedTextWriter.Write("]");
    }

    private void EmitCastExpression(IndentedTextWriter indentedTextWriter, BoundCastExpression expression) {
        if (expression.type.isNullable)
            indentedTextWriter.Write($"({GetEquivalentType(expression.type)})");

        var neededParenthesis = 1;
        var typeSymbol = expression.type.typeSymbol;

        if (typeSymbol == TypeSymbol.Bool) {
            indentedTextWriter.Write("global::System.Convert.ToBoolean(");
        } else if (typeSymbol == TypeSymbol.Decimal) {
            indentedTextWriter.Write("global::System.Convert.ToDouble(");
        } else if (typeSymbol == TypeSymbol.String) {
            indentedTextWriter.Write("global::System.Convert.ToString(");
        } else if (typeSymbol == TypeSymbol.Int) {
            indentedTextWriter.Write("global::System.Convert.ToInt32(");

            if (expression.expression.type.typeSymbol == TypeSymbol.Decimal) {
                indentedTextWriter.Write("global::System.Math.Truncate(");
                neededParenthesis = 2;
            }
        } else {
            neededParenthesis = 0;
        }

        EmitExpression(indentedTextWriter, expression.expression);
        indentedTextWriter.Write(new string(')', neededParenthesis));
    }

    private void EmitTernaryExpression(IndentedTextWriter indentedTextWriter, BoundTernaryExpression expression) {
        indentedTextWriter.Write("(");

        if (expression.left.type.isNullable)
            indentedTextWriter.Write("((");

        EmitExpression(indentedTextWriter, expression.left);

        if (expression.left.type.isNullable)
            indentedTextWriter.Write(") ?? throw new global::System.NullReferenceException())");

        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.leftOpKind)} ");

        if (BoundConstant.IsNull(expression.right.constantValue)) {
            indentedTextWriter.Write("(");
            indentedTextWriter.Write(GetEquivalentType(expression.center.type));
            indentedTextWriter.Write(")");
        }

        EmitExpression(indentedTextWriter, expression.center);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.rightOpKind)} ");

        if (BoundConstant.IsNull(expression.center.constantValue)) {
            indentedTextWriter.Write("(");
            indentedTextWriter.Write(GetEquivalentType(expression.center.type));
            indentedTextWriter.Write(")");
        }

        EmitExpression(indentedTextWriter, expression.right);
        indentedTextWriter.Write(")");
    }

    private void EmitPrefixExpression(IndentedTextWriter indentedTextWriter, BoundPrefixExpression expression) {
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
        EmitExpression(indentedTextWriter, expression.operand);
    }

    private void EmitPostfixExpression(IndentedTextWriter indentedTextWriter, BoundPostfixExpression expression) {
        EmitExpression(indentedTextWriter, expression.operand);
        indentedTextWriter.Write(SyntaxFacts.GetText(expression.op.kind));
    }

    private void EmitCompoundAssignmentExpression(
        IndentedTextWriter indentedTextWriter, BoundCompoundAssignmentExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        indentedTextWriter.Write($" {SyntaxFacts.GetText(expression.op.kind)}= ");
        EmitExpression(indentedTextWriter, expression.right);
    }

    private void EmitReferenceExpression(IndentedTextWriter indentedTextWriter, BoundReferenceExpression expression) {
        if (expression.type.isExplicitReference &&
            (ValueTypes.Contains(expression.type.typeSymbol.name) || expression.type.typeSymbol is StructSymbol)) {
            indentedTextWriter.Write("ref ");
        }

        EmitExpression(indentedTextWriter, expression.expression);
    }

    private void EmitObjectCreationExpression(
        IndentedTextWriter indentedTextWriter,
        BoundObjectCreationExpression expression) {
        indentedTextWriter.Write($"new {GetEquivalentType(expression.type, includeRankSizes: true)}");

        if (!expression.viaConstructor)
            return;

        var arguments = ImmutableArray.CreateBuilder<BoundExpression>();
        arguments.AddRange(expression.arguments);

        foreach (var templateArgument in expression.type.templateArguments) {
            if (templateArgument.isConstant) {
                if (templateArgument.constant == null)
                    arguments.Add(templateArgument.expression);
                else
                    arguments.Add(new BoundLiteralExpression(templateArgument.constant.value));
            }
        }

        EmitArguments(
            indentedTextWriter,
            arguments.ToImmutable(),
            expression.constructor?.parameters ?? ImmutableArray<ParameterSymbol>.Empty
        );
    }

    private void EmitMemberAccessExpression(
        IndentedTextWriter indentedTextWriter, BoundMemberAccessExpression expression) {
        EmitExpression(indentedTextWriter, expression.left);
        if (expression.right is BoundVariableExpression v)
            indentedTextWriter.Write($".{GetSafeName(v.variable.name)}");
        else if (expression.right is BoundType t)
            indentedTextWriter.Write($".{GetEquivalentType(t)}");
    }

    private void EmitThisExpression(IndentedTextWriter indentedTextWriter, BoundThisExpression _) {
        indentedTextWriter.Write("this");
    }

    private void EmitBaseExpression(IndentedTextWriter indentedTextWriter, BoundBaseExpression _) {
        indentedTextWriter.Write("base");
    }

    private void EmitType(IndentedTextWriter indentedTextWriter, BoundType type) {
        indentedTextWriter.Write(GetEquivalentType(type));
    }
}
