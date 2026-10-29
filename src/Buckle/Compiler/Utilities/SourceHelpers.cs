using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Buckle.Utilities;

namespace Buckle;

/// <summary>
/// Runtime helpers for Belte source code.
/// </summary>
public static class SourceHelpers {
    /// <summary>
    /// Turns a .NET Type into the corresponding Belte source code
    /// </summary>
    [return: Belte.Nullability([1])]
    public static string TypeToSourceString(Type type) {
        var builder = new StringBuilder();
        TypeToSourceStringCore(builder, type, 0, null);
        return builder.ToString();
    }

    [return: Belte.Nullability([1])]
    public static string TypeToSourceString(Type type, byte[] nullableTransformFlags) {
        var builder = new StringBuilder();
        TypeToSourceStringCore(builder, type, 0, nullableTransformFlags);
        return builder.ToString();
    }

    [return: Belte.Nullability([1])]
    public static string TypeToSourceString(FieldInfo fieldInfo) {
        var builder = new StringBuilder();
        var nullableTransformFlags = GetNullableTransformFlags(fieldInfo.GetCustomAttributesData());
        TypeToSourceStringCore(builder, fieldInfo.FieldType, 0, nullableTransformFlags);
        return builder.ToString();
    }

    [return: Belte.Nullability([1])]
    public static string TypeToSourceString(ParameterInfo parameterInfo) {
        var builder = new StringBuilder();
        var nullableTransformFlags = GetNullableTransformFlags(parameterInfo.GetCustomAttributesData());
        TypeToSourceStringCore(builder, parameterInfo.ParameterType, 0, nullableTransformFlags);
        return builder.ToString();
    }

    [return: Belte.Nullability([1])]
    public static string TypeToSourceString(PropertyInfo propertyInfo) {
        var builder = new StringBuilder();
        var nullableTransformFlags = GetNullableTransformFlags(propertyInfo.GetCustomAttributesData());
        TypeToSourceStringCore(builder, propertyInfo.PropertyType, 0, nullableTransformFlags);
        return builder.ToString();
    }

    [return: Belte.Nullability([1])]
    public static string FieldToSourceString(FieldInfo fieldInfo) {
        var builder = new StringBuilder();
        var nullableTransformFlags = GetNullableTransformFlags(fieldInfo.GetCustomAttributesData());
        TypeToSourceStringCore(builder, fieldInfo.FieldType, 0, nullableTransformFlags);
        builder.Append(' ');
        builder.Append(fieldInfo.Name);
        return builder.ToString();
    }

    [return: Belte.Nullability([1])]
    public static string ParameterToSourceString(ParameterInfo parameterInfo) {
        var builder = new StringBuilder();
        var nullableTransformFlags = GetNullableTransformFlags(parameterInfo.GetCustomAttributesData());
        TypeToSourceStringCore(builder, parameterInfo.ParameterType, 0, nullableTransformFlags);
        builder.Append(' ');
        builder.Append(parameterInfo.Name);
        return builder.ToString();
    }

    [return: Belte.Nullability([1])]
    public static Exception WithLocation([Belte.Nullability([1])] Exception exception, SimpleTextLocation location) {
        return new Diagnostics.ExecException(exception, location);
    }

    private static byte[] GetNullableTransformFlags(IEnumerable<CustomAttributeData> customAttributes) {
        byte[] nullableTransformFlags = null;

        foreach (var attribute in customAttributes) {
            if (attribute.AttributeType.FullName == "Belte.NullabilityAttribute") {
                if (attribute.ConstructorArguments[0].Value is IList<CustomAttributeTypedArgument> value) {
                    var length = value.Count;
                    nullableTransformFlags = new byte[length];

                    for (var i = 0; i < length; i++)
                        nullableTransformFlags[i] = (byte)value[i].Value;

                    break;
                }
            }
        }

        return nullableTransformFlags;
    }

    // TODO This needs to be ref int position or else it won't advance properly if an element needs to move the position more than 1
    private static void TypeToSourceStringCore(
        StringBuilder builder,
        Type type,
        int position,
        byte[] nullableTransformFlags) {
        if (type.IsPointer) {
            Debug.Assert(type.HasElementType);
            TypeToSourceStringCore(builder, type.GetElementType(), position, nullableTransformFlags);
            builder.Append('*');
            return;
        }

        if (type.IsFunctionPointer) {
            // TODO
        }

        if (type.IsUnmanagedFunctionPointer) {
            // TODO
        }

        if (type.IsEnum) {
            builder.Append(type.Name);
            return;
        }

        if (type.IsPrimitive) {
            builder.Append(GetPrimitiveTypeName(type.Name));
            return;
        }

        if (type.IsGenericParameter) {
            builder.Append(type.Name);
            return;
        }

        Debug.Assert(!type.IsGenericMethodParameter);

        if (type.IsSZArray) {
            Debug.Assert(type.HasElementType);
            var nullable = true;

            if (nullableTransformFlags is not null && nullableTransformFlags[position++] == 1)
                nullable = false;

            TypeToSourceStringCore(builder, type.GetElementType(), position, nullableTransformFlags);
            builder.Append("[]");

            if (nullable)
                builder.Append('?');

            return;
        }

        if (type.IsArray) {
            // TODO
        }

        if (type.IsValueType) {
            if (type.Name == "Nullable") {
                TypeToSourceStringCore(builder, type.GenericTypeArguments[0], position, nullableTransformFlags);
                builder.Append('?');
                return;
            }

            builder.Append(type.Name);
            AppendGenerics();
            return;
        }

        if (type.IsClass) {
            var name = GetKnownReferenceTypeName(type.Name);
            builder.Append(name);

            var nullable = true;

            if (nullableTransformFlags is not null && nullableTransformFlags[position++] == 1)
                nullable = false;

            AppendGenerics();

            if (nullable)
                builder.Append('?');

            return;
        }

        if (type.IsInterface) {
            builder.Append(type.Name);
            var nullable = true;

            if (nullableTransformFlags is not null && nullableTransformFlags[position++] == 1)
                nullable = false;

            AppendGenerics();

            if (nullable)
                builder.Append('?');

            return;
        }

        throw ExceptionUtilities.Unreachable();

        void AppendGenerics() {
            if (!type.IsGenericType)
                return;

            builder.Append('<');

            var first = true;

            foreach (var genericArgument in type.GenericTypeArguments) {
                if (first)
                    first = false;
                else
                    builder.Append(", ");

                TypeToSourceStringCore(builder, genericArgument, position++, nullableTransformFlags);
            }

            builder.Append('>');
        }
    }

    private static string GetKnownReferenceTypeName(string name) {
        switch (name) {
            case "String":
                return "string";
            case "Type":
                return "type";
            default:
                return name;
        }
    }

    private static string GetPrimitiveTypeName(string typeName) {
        switch (typeName) {
            case "Boolean": return "bool";
            case "Byte": return "uint8";
            case "SByte": return "int8";
            case "Int16": return "int16";
            case "UInt16": return "uint16";
            case "Int32": return "int32";
            case "UInt32": return "uint32";
            case "Int64": return "int64";
            case "UInt64": return "uint64";
            case "IntPtr": return "intptr";
            case "UIntPtr": return "uintptr";
            case "Char": return "char";
            case "Double": return "float64";
            case "Single": return "float32";
            default: throw ExceptionUtilities.UnexpectedValue(typeName);
        }
    }
}
