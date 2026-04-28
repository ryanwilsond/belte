using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Shared;

using IndentedTextWriter = System.CodeDom.Compiler.IndentedTextWriter;

namespace Buckle.CodeAnalysis.Emitting;

/// <summary>
/// Emits a bound program into a C# source.
/// </summary>
internal sealed class CSharpEmitter : SymbolVisitor<IndentedTextWriter, bool> {
    private static readonly string IndentString = "    ";

    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly BoundProgram _program;
    private readonly bool _debugMode;

    private readonly Dictionary<SpecialType, string> _specialTypes = new Dictionary<SpecialType, string>{
        { SpecialType.Object, "global::System.Object" },
        { SpecialType.Any, "global::System.Object" },
        { SpecialType.Bool, "global::System.Boolean" },
        { SpecialType.Int, "global::System.Int64" },
        { SpecialType.Int8, "global::System.SByte" },
        { SpecialType.Int16, "global::System.Int16" },
        { SpecialType.Int32, "global::System.Int32" },
        { SpecialType.Int64, "global::System.Int64" },
        { SpecialType.UInt8, "global::System.Byte" },
        { SpecialType.UInt16, "global::System.UInt16" },
        { SpecialType.UInt32, "global::System.UInt32" },
        { SpecialType.UInt64, "global::System.UIn64" },
        { SpecialType.Decimal, "global::System.Double" },
        { SpecialType.Float32, "global::System.Single" },
        { SpecialType.Float64, "global::System.Double" },
        { SpecialType.IntPtr, "nint" },
        { SpecialType.UIntPtr, "nuint" },
        { SpecialType.Nullable, "global::System.Nullable" },
        { SpecialType.Char, "global::System.Char" },
        { SpecialType.Void, "void" },
        { SpecialType.Type, "global::System.Type" },
        { SpecialType.String, "global::System.String" },
        { SpecialType.Exception, "global::System.Exception" },
    };

    private CSharpEmitter(
        BoundProgram program,
        bool debugMode,
        BelteDiagnosticQueue diagnostics) {
        _program = program;
        _debugMode = debugMode;
        _diagnostics = diagnostics;
    }

    internal static void Emit(
        BoundProgram program,
        string outputPath,
        BelteDiagnosticQueue diagnostics) {
        var debugMode = program.compilation.options.optimizationLevel == OptimizationLevel.Debug;
        var emitter = new CSharpEmitter(program, debugMode, diagnostics);

        if (SupportedProjectType(program, diagnostics))
            emitter.EmitToFile(outputPath);
    }

    internal static string EmitToString(
        BoundProgram program,
        bool programOnly,
        BelteDiagnosticQueue diagnostics) {
        var emitter = new CSharpEmitter(program, false, diagnostics);

        if (SupportedProjectType(program, diagnostics))
            return emitter.EmitToString(programOnly);

        return "<unsupported-project-type>";
    }

    private static bool SupportedProjectType(BoundProgram program, BelteDiagnosticQueue diagnostics) {
        var options = program.compilation.options;

        if (options.outputKind == OutputKind.GraphicsApplication && !options.isScript) {
            diagnostics.Push(Error.Unsupported.GraphicsDll());
            return false;
        }

        return true;
    }

    private void EmitToFile(string outputPath) {
        var text = EmitInternal();

        if (!_program.compilation.options.enableOutput)
            return;

        File.WriteAllText(outputPath, text);
    }

    private string EmitToString(bool programOnly) {
        return EmitInternal();
    }

    private string EmitInternal() {
        var stringWriter = new StringWriter();

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, IndentString)) {
            foreach (var member in _program.compilation.globalNamespaceInternal.GetMembers())
                member.Accept(this, indentedTextWriter);
        }

        stringWriter.Flush();
        return stringWriter.ToString();
    }

    internal string GetSafeName(string name) {
        var provider = System.CodeDom.Compiler.CodeDomProvider.CreateProvider("C#");
        return (provider.IsValidIdentifier(name) ? name : "@" + name)
            .Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }

    private string GetTypeAttributes(NamedTypeSymbol type) {
        var builder = new StringBuilder();

        if (type.IsStructType())
            builder.Append($"[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.{(type.isUnionStruct ? "Explicit" : "Sequential")})]");

        builder.Append(type.declaredAccessibility switch {
            Accessibility.Private => "private ",
            Accessibility.Protected => "protected ",
            Accessibility.Public => "public ",
            _ => "public "
        });

        builder.Append("unsafe ");

        if (type.isStatic)
            builder.Append("static ");
        if (type.isAbstract)
            builder.Append("abstract ");
        if (type.isSealed)
            builder.Append("sealed ");

        builder.Append(type.typeKind switch {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            _ => throw ExceptionUtilities.UnexpectedValue(type.typeKind)
        });

        return builder.ToString();
    }

    private string GetFieldAttributes(FieldSymbol field) {
        var builder = new StringBuilder();

        if (field.isAnonymousUnionMember || field.containingType.isUnionStruct)
            builder.Append("[global::System.Runtime.InteropServices.FieldOffset(0)]");

        builder.Append(field.declaredAccessibility switch {
            Accessibility.Private => "private ",
            Accessibility.Protected => "protected ",
            Accessibility.Public => "public ",
            _ => "public "
        });

        if (field.isStatic)
            builder.Append("static ");

        return builder.ToString();
    }

    internal string GetMethodAttributes(MethodSymbol method, bool includeAccessibility = true) {
        var builder = new StringBuilder();

        if (includeAccessibility) {
            builder.Append(method.declaredAccessibility switch {
                Accessibility.Private => "private ",
                Accessibility.Protected => "protected ",
                Accessibility.Public => "public ",
                _ => "public "
            });
        }

        if (method.isStatic)
            builder.Append("static ");
        if (method.isSealed)
            builder.Append("sealed ");
        if (method.isAbstract)
            builder.Append("abstract ");
        if (method.isVirtual)
            builder.Append("virtual ");
        if (method.isOverride)
            builder.Append("override ");

        return builder.ToString();
    }

    internal string GetMethodSignature(MethodSymbol method) {
        StringBuilder builder;

        if (method.IsConstructor()) {
            builder = new StringBuilder(GetSafeName(method.containingType.name));
        } else {
            builder = new StringBuilder(GetType(method.returnType, method.returnsByRef));

            builder.Append(' ');
            builder.Append(GetSafeName(method.name));

            if (method.isTemplateMethod)
                builder.Append($"<{string.Join(", ", method.templateArguments.Select(t => GetType(t.type.type)))}>");
        }

        builder.Append($"({string.Join(", ", method.GetParameters().Select(p => GetParameterSignature(p)))})");

        return builder.ToString();
    }

    private string GetParameterSignature(ParameterSymbol parameter) {
        return $"{GetType(parameter.type, parameter.refKind != RefKind.None)} {GetSafeName(parameter.name)}";
    }

    private string GetFieldSignature(FieldSymbol field) {
        return $"{GetType(field.type, field.refKind != RefKind.None)} {GetSafeName(field.name)}";
    }

    private string GetBaseList(NamedTypeSymbol type) {
        return type.IsStructType() ? "global::System.ValueType" : GetType(type.baseType);
    }

    internal string GetType(TypeSymbol type, bool byRef = false) {
        var typeStr = GetTypeCore(type);

        if (byRef)
            return $"ref {typeStr}";

        return typeStr;

        string GetTypeCore(TypeSymbol type) {
            if (type.specialType == SpecialType.Nullable) {
                var underlyingType = type.GetNullableUnderlyingType();
                var genericArgumentType = GetType(underlyingType);

                if (!CodeGenerator.IsValueType(underlyingType))
                    return genericArgumentType;

                return $"global::System.Nullable<{genericArgumentType}>";
            }

            if (type is ArrayTypeSymbol array) {
                var elementType = GetType(array.elementType);
                return $"{elementType}[]";
            }

            if (type is PointerTypeSymbol pointer) {
                var elementType = GetType(pointer.pointedAtType);
                return $"{elementType}*";
            }

            if (type is FunctionPointerTypeSymbol fp)
                return GetFuncPtrType(fp.signature);

            if (type is FunctionTypeSymbol f)
                return GetFuncType(f.signature);

            if (type.specialType != SpecialType.None && _specialTypes.TryGetValue(type.specialType, out var value))
                return value;

            if (type is TemplateParameterSymbol t)
                return GetSafeName(t.name);

            return GetTypeWithContainingGenerics((NamedTypeSymbol)type);
        }

        string GetTypeWithContainingGenerics(NamedTypeSymbol type) {
            var foundType = GetTypeCoreInternal(type);

            if (type.ContainsErrorType() || type.IsEnumType())
                return foundType;

            var chain = new Stack<NamedTypeSymbol>();
            var current = type;

            while (current is not null) {
                chain.Push(current);
                current = current.containingType;
            }

            var allTypeArgs = new List<string>();

            while (chain.Count > 0) {
                var s = chain.Pop();

                if (s.arity > 0) {
                    foreach (var arg in s.templateArguments)
                        allTypeArgs.Add(GetType(arg.type.type));
                }
            }

            if (allTypeArgs.Count > 0)
                return $"{foundType}<{string.Join(", ", allTypeArgs)}>";

            return foundType;
        }

        string GetTypeCoreInternal(NamedTypeSymbol type) {
            return GetSafeName(type.name);
        }
    }

    private string GetFuncPtrType(FunctionPointerMethodSymbol signature) {
        var callingConvention = signature.callingConvention == CallingConvention.Unmanaged
            ? $"unmanaged{GetUnmanagedCallingConvention(signature.unmanagedCallingConvention)}"
            : "managed";

        var typeParameters = string.Join(", ", signature.GetParameterTypes().Select(p => GetType(p.type)));
        return $"delegate* {callingConvention}<{typeParameters}, {GetType(signature.returnType)}>";

        static string GetUnmanagedCallingConvention(CallingConvention callingConvention) {
            return callingConvention switch {
                CallingConvention.Cdecl => "[Cdecl]",
                CallingConvention.FastCall => "[Fastcall]",
                CallingConvention.ThisCall => "[Thiscall]",
                CallingConvention.StdCall => "[Stdcall]",
                _ => "",
            };
        }
    }

    private string GetFuncType(FunctionMethodSymbol signature) {
        if (signature.returnsVoid && signature.parameterCount == 0) {
            return "global::System.Action";
        } else if (signature.returnsVoid) {
            var typeParameters = string.Join(", ", signature.GetParameterTypes().Select(p => GetType(p.type)));
            return $"global::System.Action<{typeParameters}>";
        } else {
            var typeParameters = string.Join(", ", signature.GetParameterTypes().Select(p => GetType(p.type)));
            return $"global::System.Func<{typeParameters}, {GetType(signature.returnType)}>";
        }
    }

    internal override bool VisitNamespace(NamespaceSymbol symbol, IndentedTextWriter argument) {
        using var curly = new CurlyIndenter(argument, $"namespace {GetSafeName(symbol.name)}");

        foreach (var member in symbol.GetMembers())
            member.Accept(this, argument);

        return false;
    }

    internal override bool VisitNamedType(NamedTypeSymbol symbol, IndentedTextWriter argument) {
        using var curly = new CurlyIndenter(
            argument,
            $"{GetTypeAttributes(symbol)} {GetSafeName(symbol.name)} : {GetBaseList(symbol)}"
        );

        foreach (var member in symbol.GetMembers())
            member.Accept(this, argument);

        return false;
    }

    internal override bool VisitField(FieldSymbol symbol, IndentedTextWriter argument) {
        argument.WriteLine($"{GetFieldAttributes(symbol)}{GetFieldSignature(symbol)};");
        return false;
    }

    internal override bool VisitMethod(MethodSymbol symbol, IndentedTextWriter argument) {
        using var curly = new CurlyIndenter(argument, $"{GetMethodAttributes(symbol)}{GetMethodSignature(symbol)}");

        var generator = new CSharpCodeGenerator(this, argument, symbol, _program.methodBodies[symbol], _debugMode);
        generator.Generate();

        return false;
    }
}
