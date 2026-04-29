using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
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
    private readonly ImmutableDictionary<MethodSymbol, BoundBlockStatement> _methodBodies;

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

    private Dictionary<string, string> _stlMap;
    private string _lazyRandomField;

    private CSharpEmitter(
        BoundProgram program,
        bool debugMode,
        BelteDiagnosticQueue diagnostics) {
        _program = program;
        _debugMode = debugMode;
        _diagnostics = diagnostics;

        _methodBodies = _program.GetAllMethodBodies().ToImmutableDictionary(x => x.Item1, x => x.Item2);
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
        return EmitInternal(programOnly);
    }

    private string EmitInternal(bool includePreviousCompilations = true) {
        GenerateSTLMap();

        var stringWriter = new StringWriter();

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, IndentString)) {
            if (includePreviousCompilations) {
                var current = _program.compilation;

                do {
                    foreach (var member in current.globalNamespaceInternal.GetMembers())
                        member.Accept(this, indentedTextWriter);

                    current = current.previous;
                } while (current is not null);
            } else {
                foreach (var member in _program.compilation.globalNamespaceInternal.GetMembers())
                    member.Accept(this, indentedTextWriter);
            }

            if (_lazyRandomField is not null) {
                using var curly = new CurlyIndenter(indentedTextWriter, $"public static class _GlobalsClass_");
                indentedTextWriter.WriteLine("public static global::System.Random random;");
                using var innerCurly = new CurlyIndenter(indentedTextWriter, $"static _GlobalsClass_()");
                indentedTextWriter.WriteLine("this.random = new global::System.Random();");
            }
        }

        stringWriter.Flush();
        return stringWriter.ToString();
    }

    internal string GetSafeName(string name) {
        var provider = System.CodeDom.Compiler.CodeDomProvider.CreateProvider("C#");
        return (provider.IsValidIdentifier(name) ? name : "@" + name)
            .Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }

    internal string EnsureGlobalsClassIsBuilt(IndentedTextWriter writer) {
        if (_lazyRandomField is null)
            _lazyRandomField = "global::_GlobalsClass_.random";

        return _lazyRandomField;
    }

    internal string GetMethodName(MethodSymbol method) {
        if ((object)method.containingNamespace == LibraryHelpers.BelteNamespace.originalDefinition)
            return CheckStandardMap(method);

        string name;

        if (method.isStatic)
            name = $"{GetType(method.containingType)}.{GetSafeName(method.name)}";
        else
            name = GetSafeName(method.name);

        if (method.isTemplateMethod)
            return $"{name}<{string.Join(", ", method.templateArguments.Select(t => GetType(t.type.type)))}>";

        return name;
    }

    private string CheckStandardMap(MethodSymbol method) {
        var mapKey = LibraryHelpers.BuildMapKey(method);

        if (mapKey == "LowLevel_SizeOf_T?")
            return $"sizeof({GetType(method.templateArguments[0].type.type)})";

        var name = _stlMap[mapKey];

        if (method.isTemplateMethod)
            return $"{name}<{string.Join(", ", method.templateArguments.Select(t => GetType(t.type.type)))}>";

        return name;
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

        if (!type.IsStructType()) {
            if (type.isStatic)
                builder.Append("static ");
            if (type.isAbstract)
                builder.Append("abstract ");
            if (type.isSealed)
                builder.Append("sealed ");
        }

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

        if (method.isExtern) {
            var dllImportData = method.GetDllImportData();
            var moduleName = dllImportData.moduleName;
            var callingConvention = GetCallingConvention(dllImportData.callingConvention);
            var charSet = GetCharSet(dllImportData.characterSet);

            if (charSet is null)
                builder.Append($"[global::System.Runtime.InteropServices.DllImport(\"{moduleName}\", CallingConvention = {callingConvention})]");
            else
                builder.Append($"[global::System.Runtime.InteropServices.DllImport(\"{moduleName}\", CallingConvention = {callingConvention}, CharSet = {charSet})]");
        }

        var unmanagedAttribute = method.GetUnmanagedCallersOnlyAttributeData(true);

        if (unmanagedAttribute is not null && unmanagedAttribute != UnmanagedCallersOnlyAttributeData.Uninitialized) {
            builder.Append("[global::System.Runtime.InteropServices.UnmanagedCallersOnly([typeof(global::System.Runtime.CompilerServices.CallConvCdecl)])]");
        }

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
        if (method.isExtern)
            builder.Append("extern ");
        if (method.isSealed)
            builder.Append("sealed ");
        if (method.isAbstract)
            builder.Append("abstract ");
        if (method.isVirtual)
            builder.Append("virtual ");
        if (method.isOverride)
            builder.Append("override ");

        return builder.ToString();

        string GetCallingConvention(CallingConvention callingConvention) {
            return callingConvention switch {
                CallingConvention.Winapi => "global::System.Runtime.InteropServices.CallingConvention.Winapi",
                CallingConvention.FastCall => "global::System.Runtime.InteropServices.CallingConvention.Fastcall",
                CallingConvention.Cdecl => "global::System.Runtime.InteropServices.CallingConvention.Cdecl",
                CallingConvention.StdCall => "global::System.Runtime.InteropServices.CallingConvention.StdCall",
                CallingConvention.ThisCall => "global::System.Runtime.InteropServices.CallingConvention.Thiscall",
                _ => throw ExceptionUtilities.UnexpectedValue(callingConvention)
            };
        }

        string GetCharSet(System.Runtime.InteropServices.CharSet charSet) {
            return charSet switch {
                System.Runtime.InteropServices.CharSet.Ansi => "global::System.Runtime.InteropServices.CharSet.Ansi",
                System.Runtime.InteropServices.CharSet.Auto => "global::System.Runtime.InteropServices.CharSet.Auto",
                System.Runtime.InteropServices.CharSet.None => null,
                System.Runtime.InteropServices.CharSet.Unicode => "global::System.Runtime.InteropServices.CharSet.Unicode",
                _ => throw ExceptionUtilities.UnexpectedValue(charSet),
            };
        }
    }

    internal string GetMethodSignature(
        MethodSymbol method,
        BoundStatement initializer = null,
        CSharpCodeGenerator generator = null) {
        StringBuilder builder;
        var isConstructor = method.IsConstructor();

        if (isConstructor) {
            builder = new StringBuilder(GetSafeName(method.containingType.name));
        } else {
            builder = new StringBuilder(GetType(method.returnType, method.returnsByRef));

            builder.Append(' ');
            builder.Append(GetSafeName(method.name));

            if (method.isTemplateMethod)
                builder.Append($"<{string.Join(", ", method.templateArguments.Select(t => GetType(t.type.type)))}>");
        }

        builder.Append($"({string.Join(", ", method.GetParameters().Select(p => GetParameterSignature(p)))})");

        if (isConstructor && initializer is BoundExpressionStatement statement) {
            if (statement.expression is BoundCallExpression ctorCall && ctorCall.method.IsConstructor()) {
                var keyword = ctorCall.receiver.kind == BoundKind.ThisExpression ? "this" : "base";
                var arguments = generator.EmitArguments(ctorCall.arguments, ctorCall.argumentRefKinds);
                builder.Append($" : {keyword}({arguments})");
            }
        }

        return builder.ToString();
    }

    private string GetTypeSignature(NamedTypeSymbol type) {
        if (type.arity == 0)
            return GetSafeName(type.name);

        return $"{GetSafeName(type.name)}<{string.Join(", ", type.templateParameters.Select(t => GetSafeName(t.name)))}>";
    }

    private string GetParameterSignature(ParameterSymbol parameter) {
        return $"{GetType(parameter.type, parameter.refKind != RefKind.None)} {GetSafeName(parameter.name)}";
    }

    private string GetFieldSignature(FieldSymbol field) {
        return $"{GetType(field.type, field.refKind != RefKind.None)} {GetSafeName(field.name)}";
    }

    private string GetBaseList(NamedTypeSymbol type) {
        return type.IsStructType() ? "" : $" : {GetType(type.baseType)}";
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

            var allTypeArgs = new List<string>();

            if (type.arity > 0) {
                foreach (var arg in type.templateArguments)
                    allTypeArgs.Add(GetType(arg.type.type));
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
        if (symbol.specialType is SpecialType.Object or SpecialType.Exception)
            return false;

        if (symbol is PENamedTypeSymbol or SynthesizedFinishedNamedTypeSymbol)
            return false;

        using var curly = new CurlyIndenter(
            argument,
            $"{GetTypeAttributes(symbol)} {GetTypeSignature(symbol)}{GetBaseList(symbol)}"
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
        if (symbol.isAbstract || symbol.isExtern) {
            argument.WriteLine($"{GetMethodAttributes(symbol)}{GetMethodSignature(symbol)};");
            return false;
        }

        var body = _methodBodies[symbol];
        var generator = new CSharpCodeGenerator(this, argument, symbol, body, _debugMode);
        var includeAccessibility = symbol.methodKind != MethodKind.StaticConstructor;

        var initializer = body.statements.Length > 0 ? body.statements[0] : null;
        using var curly = new CurlyIndenter(argument, $"{GetMethodAttributes(symbol, includeAccessibility)}{GetMethodSignature(symbol, initializer, generator)}");

        generator.Generate();

        return false;
    }

    private void GenerateSTLMap() {
        _stlMap = new Dictionary<string, string>() {
            { "Console_Clear", "global::System.Console.Clear" },
            { "Console_GetWidth", "global::Belte.Runtime.Console.GetWidth" },
            { "Console_GetHeight", "global::Belte.Runtime.Console.GetHeight" },
            { "Console_Print_S?", "global::System.Console.Write" },
            { "Console_Print_A?", "global::System.Console.Write" },
            { "Console_Print_O?", "global::System.Console.Write" },
            { "Console_Print_[?", "global::System.Console.Write" },
            { "Console_PrintLine", "global::System.Console.WriteLine" },
            { "Console_PrintLine_S?", "global::System.Console.WriteLine" },
            { "Console_PrintLine_A?", "global::System.Console.WriteLine" },
            { "Console_PrintLine_O?", "global::System.Console.WriteLine" },
            { "Console_PrintLine_[?", "global::System.Console.WriteLine" },
            { "Console_Input", "global::System.Console.ReadLine" },
            { "Console_ResetColor", "global::System.Console.ResetColor" },
            { "Console_SetForegroundColor_I", "global::Belte.Runtime.Console.SetForegroundColor" },
            { "Console_SetBackgroundColor_I", "global::Belte.Runtime.Console.SetBackgroundColor" },
            { "Console_SetCursorPosition_I?I?", "global::Belte.Runtime.Console.SetCursorPosition" },
            { "Console_SetCursorVisibility_B", "global::Belte.Runtime.Console.SetCursorVisibility" },
            { "Directory_Create_S", "global::Belte.Runtime.Utilities.CreateDirectory" },
            { "Directory_Delete_S", "global::Belte.Runtime.Utilities.DeleteDirectory" },
            { "Directory_Exists_S", "global::Directory.Exists" },
            { "File_AppendText_SS", "global::File.AppendAllText" },
            { "File_Create_S", "global::File.Create" },
            { "File_Copy_SS", "global::File.Copy" },
            { "File_Delete_S", "global::File.Delete" },
            { "File_Exists_S", "global::File.Exists" },
            { "File_ReadText_S", "global::File.ReadAllText" },
            { "File_WriteText_SS", "global::File.WriteAllText" },
            { "Math_Clamp_D?D?D?", "global::Belte.Runtime.Math.Clamp" },
            { "Math_Clamp_DDD", "global::Math.Clamp" },
            { "Math_Clamp_I?I?I?", "global::Belte.Runtime.Math.Clamp" },
            { "Math_Clamp_III", "global::Math.Clamp" },
            { "Math_Lerp_D?D?D?", "global::Belte.Runtime.Math.Lerp" },
            { "Math_Lerp_DDD", "global::Belte.Runtime.Math.Lerp" },
            { "Math_Cos_D", "global::Math.Cos" },
            { "Math_Cos_D?", "global::Belte.Runtime.Math.Cos" },
            { "Math_Cosh_D", "global::Math.Cosh" },
            { "Math_Cosh_D?", "global::Belte.Runtime.Math.Cosh" },
            { "Math_Acos_D", "global::Math.Acos" },
            { "Math_Acos_D?", "global::Belte.Runtime.Math.Acos" },
            { "Math_Acosh_D", "global::Math.Acosh" },
            { "Math_Acosh_D?", "global::Belte.Runtime.Math.Acosh" },
            { "Math_Sin_D", "global::Math.Sin" },
            { "Math_Sin_D?", "global::Belte.Runtime.Math.Sin" },
            { "Math_Sinh_D", "global::Math.Sinh" },
            { "Math_Sinh_D?", "global::Belte.Runtime.Math.Sinh" },
            { "Math_Asin_D", "global::Math.Asin" },
            { "Math_Asin_D?", "global::Belte.Runtime.Math.Asin" },
            { "Math_Asinh_D", "global::Math.Asinh" },
            { "Math_Asinh_D?", "global::Belte.Runtime.Math.Asinh" },
            { "Math_Tan_D", "global::Math.Tan" },
            { "Math_Tan_D?", "global::Belte.Runtime.Math.Tan" },
            { "Math_Tanh_D", "global::Math.Tanh" },
            { "Math_Tanh_D?", "global::Belte.Runtime.Math.Tanh" },
            { "Math_Atan_D", "global::Math.Atan" },
            { "Math_Atan_D?", "global::Belte.Runtime.Math.Atan" },
            { "Math_Atanh_D", "global::Math.Atanh" },
            { "Math_Atanh_D?", "global::Belte.Runtime.Math.Atanh" },
            { "Math_Pow_DD", "global::Math.Pow" },
            { "Math_Pow_D?D?", "global::Belte.Runtime.Math.Pow" },
            { "Math_Pow_II", "global::Belte.Runtime.Math.Pow" },
            { "Math_Pow_I?I?", "global::Belte.Runtime.Math.Pow" },
            { "Math_Max_D?D?", "global::Belte.Runtime.Math.Max" },
            { "Math_Max_DD", "global::Math.Max" },
            { "Math_Max_I?I?", "global::Belte.Runtime.Math.Max" },
            { "Math_Max_II", "global::Math.Max" },
            { "Math_Min_D?D?", "global::Belte.Runtime.Math.Min" },
            { "Math_Min_DD", "global::Math.Min" },
            { "Math_Min_I?I?", "global::Belte.Runtime.Math.Min" },
            { "Math_Min_II", "global::Math.Min" },
            { "Math_Abs_D?", "global::Belte.Runtime.Math.Abs" },
            { "Math_Abs_D", "global::Math.Abs" },
            { "Math_Abs_I?", "global::Belte.Runtime.Math.Abs" },
            { "Math_Abs_I", "global::Math.Abs" },
            { "Math_Round_D?", "global::Belte.Runtime.Math.Round" },
            { "Math_Round_D", "global::Math.Round" },
            { "Math_Floor_D?", "global::Belte.Runtime.Math.Floor" },
            { "Math_Floor_D", "global::Math.Floor" },
            { "Math_Ceiling_D?", "global::Belte.Runtime.Math.Ceiling" },
            { "Math_Ceiling_D", "global::Math.Ceiling" },
            { "Math_Sign_D?", "global::Belte.Runtime.Math.Sign" },
            { "Math_Sign_D", "global::Math.Sign" },
            { "Math_Sign_I?", "global::Belte.Runtime.Math.Sign" },
            { "Math_Sign_I", "global::Math.Sign" },
            { "Math_Exp_D?", "global::Belte.Runtime.Math.Exp" },
            { "Math_Exp_D", "global::Math.Exp" },
            { "Math_Log_D?D?", "global::Belte.Runtime.Math.Log" },
            { "Math_Log_DD", "global::Math.Log" },
            { "Math_Log_D?", "global::Belte.Runtime.Math.Log" },
            { "Math_Log_D", "global::Math.Log" },
            { "Math_Sqrt_D?", "global::Belte.Runtime.Math.Sqrt" },
            { "Math_Sqrt_D", "global::Math.Sqrt" },
            { "Math_Truncate_D?", "global::Belte.Runtime.Math.Truncate" },
            { "Math_Truncate_D", "global::Math.Truncate" },
            { "Math_DegToRad_D?", "global::Belte.Runtime.Math.DegToRad" },
            { "Math_DegToRad_D", "global::double.DegreesToRadians" },
            { "Math_RadToDeg_D?", "global::Belte.Runtime.Math.RadToDeg" },
            { "Math_RadToDeg_D", "global::double.RadiansToDegrees" },
            { "LowLevel_GetHashCode_O", "global::Belte.Runtime.Utilities.GetHashCode" },
            { "LowLevel_GetTypeName_O", "global::Belte.Runtime.Utilities.GetTypeName" },
            { "LowLevel_GetType_A", "global::Belte.Runtime.Utilities.AnyGetType" },
            { "LowLevel_CreateLPCSTR_S", "global::Belte.Runtime.Utilities.CreateLPCSTR" },
            { "LowLevel_CreateLPCWSTR_S", "global::Belte.Runtime.Utilities.CreateLPCWSTR" },
            { "LowLevel_FreeLPCSTR_U*", "global::Belte.Runtime.Utilities.FreeLPCSTR" },
            { "LowLevel_FreeLPCWSTR_C*", "global::Belte.Runtime.Utilities.FreeLPCWSTR" },
            { "LowLevel_ReadLPCSTR_U*", "global::Belte.Runtime.Utilities.ReadLPCSTR" },
            { "LowLevel_ReadLPCWSTR_C*", "global::Belte.Runtime.Utilities.ReadLPCWSTR" },
            { "LowLevel_GetGCPtr_O", "global::Belte.Runtime.Utilities.GetGCPtr" },
            { "LowLevel_FreeGCHandle_V*", "global::Belte.Runtime.Utilities.FreeGCHandle" },
            { "LowLevel_GetObject_V*", "global::Belte.Runtime.Utilities.GetObject" },
            { "LowLevel_ThrowNullConditionException", "throw new global::Belte.Runtime.NullConditionException" },
            { "LowLevel_Length_[", "global::Belte.Runtime.Utilities.Length" },
            { "LowLevel_Length_[?", "global::Belte.Runtime.Utilities.Length" },
            { "LowLevel_Sort_[?", "global::Belte.Runtime.Utilities.Sort" },
            { "Time_Now", "global::Belte.Runtime.Utilities.TimeNow" },
            { "Time_Sleep_I", "global::Belte.Runtime.Utilities.TimeSleep" },
            { "String_Ascii_S", "global::Belte.Runtime.Utilities.Ascii" },
            { "String_Char_I", "global::Belte.Runtime.Utilities.Char" },
            { "String_Split_SS", "global::Belte.Runtime.Utilities.Split" },
            { "String_Length_S", "global::Belte.Runtime.Utilities.StringLength" },
            { "String_IsNullOrWhiteSpace_S?", "global::string.IsNullOrWhiteSpace" },
            { "String_IsNullOrWhiteSpace_C?", "global::Belte.Runtime.Utilities.IsNullOrWhiteSpace" },
            { "String_IsDigit_C?", "global::Belte.Runtime.Utilities.IsDigit" },
            { "String_Substring_SI?I?", "global::Belte.Runtime.Utilities.Substring" },
            { "Int_Parse_S?", "global::Belte.Runtime.Utilities.IntParse" },
            { "Object<>_ToString", "global::object.ToString" },
            { "Object<>_Equals_O?", "global::object.Equals" },
            { "Object<>_GetHashCode", "global::object.GetHashCode" },
        };
    }
}
