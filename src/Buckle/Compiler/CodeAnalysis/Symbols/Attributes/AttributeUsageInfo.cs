using System;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal readonly struct AttributeUsageInfo : IEquatable<AttributeUsageInfo> {
    [Flags]
    private enum PackedAttributeUsage {
        None = 0,
        Assembly = AttributeTargets.Assembly,
        Module = AttributeTargets.Module,
        Class = AttributeTargets.Class,
        Struct = AttributeTargets.Struct,
        Enum = AttributeTargets.Enum,
        Constructor = AttributeTargets.Constructor,
        Method = AttributeTargets.Method,
        Property = AttributeTargets.Property,
        Field = AttributeTargets.Field,
        Event = AttributeTargets.Event,
        Interface = AttributeTargets.Interface,
        Parameter = AttributeTargets.Parameter,
        Delegate = AttributeTargets.Delegate,
        ReturnValue = AttributeTargets.ReturnValue,
        GenericParameter = AttributeTargets.GenericParameter,
        All = AttributeTargets.All,
        Initialized = GenericParameter << 1,
        AllowMultiple = Initialized << 1,
        Inherited = AllowMultiple << 1
    }

    private readonly PackedAttributeUsage _flags;

    internal static readonly AttributeUsageInfo Default = new AttributeUsageInfo(
        validTargets: AttributeTargets.All,
        allowMultiple: false,
        inherited: true
    );

    internal static readonly AttributeUsageInfo Null = default;

    internal AttributeUsageInfo(AttributeTargets validTargets, bool allowMultiple, bool inherited) {
        _flags = (PackedAttributeUsage)validTargets | PackedAttributeUsage.Initialized;

        if (allowMultiple)
            _flags |= PackedAttributeUsage.AllowMultiple;

        if (inherited)
            _flags |= PackedAttributeUsage.Inherited;
    }

    internal bool isNull {
        get {
            return (_flags & PackedAttributeUsage.Initialized) == 0;
        }
    }

    internal AttributeTargets validTargets {
        get {
            return (AttributeTargets)(_flags & PackedAttributeUsage.All);
        }
    }

    internal bool allowMultiple {
        get {
            return (_flags & PackedAttributeUsage.AllowMultiple) != 0;
        }
    }

    internal bool inherited {
        get {
            return (_flags & PackedAttributeUsage.Inherited) != 0;
        }
    }

    internal bool hasValidAttributeTargets {
        get {
            var value = (int)validTargets;
            return value != 0 && (value & (int)~AttributeTargets.All) == 0;
        }
    }

    public static bool operator ==(AttributeUsageInfo left, AttributeUsageInfo right) {
        return left._flags == right._flags;
    }

    public static bool operator !=(AttributeUsageInfo left, AttributeUsageInfo right) {
        return left._flags != right._flags;
    }

    public override bool Equals(object obj) {
        if (obj is AttributeUsageInfo info)
            return Equals(info);

        return false;
    }

    public bool Equals(AttributeUsageInfo other) {
        return this == other;
    }

    public override int GetHashCode() {
        return ((int)_flags).GetHashCode();
    }

    internal object GetValidTargetsErrorArgument() {
        var validTargetsInt = (int)validTargets;

        if (!hasValidAttributeTargets)
            return string.Empty;

        var builder = ArrayBuilder<string>.GetInstance();
        var flag = 0;

        while (validTargetsInt > 0) {
            if ((validTargetsInt & 1) != 0)
                builder.Add(GetErrorDisplayNameResourceId((AttributeTargets)(1 << flag)));

            validTargetsInt >>= 1;
            flag++;
        }

        return new ValidTargetsStringLocalizableErrorArgument(builder.ToArrayAndFree());
    }

    private readonly struct ValidTargetsStringLocalizableErrorArgument : IFormattable {
        private readonly string[]? _targetResourceIds;

        internal ValidTargetsStringLocalizableErrorArgument(string[] targetResourceIds) {
            _targetResourceIds = targetResourceIds;
        }

        public override string ToString() {
            return ToString(null, null);
        }

        public string ToString(string format, IFormatProvider formatProvider) {
            var builder = PooledStringBuilder.GetInstance();
            var culture = formatProvider as System.Globalization.CultureInfo;

            if (_targetResourceIds is not null) {
                foreach (var id in _targetResourceIds) {
                    if (builder.Builder.Length > 0)
                        builder.Builder.Append(", ");

                    builder.Builder.Append(id.ToString(culture));
                }
            }

            var message = builder.Builder.ToString();
            builder.Free();

            return message;
        }
    }

    private static string GetErrorDisplayNameResourceId(AttributeTargets target) {
        switch (target) {
            case AttributeTargets.Assembly: return "assembly";
            case AttributeTargets.Class: return "class";
            case AttributeTargets.Constructor: return "constructor";
            case AttributeTargets.Enum: return "enum";
            case AttributeTargets.Field: return "field";
            case AttributeTargets.GenericParameter: return "template parameter";
            case AttributeTargets.Interface: return "interface";
            case AttributeTargets.Method: return "method";
            case AttributeTargets.Module: return "module";
            case AttributeTargets.Parameter: return "parameter";
            case AttributeTargets.Property: return "property";
            case AttributeTargets.ReturnValue: return "return value";
            case AttributeTargets.Struct: return "struct";
            default:
                throw ExceptionUtilities.UnexpectedValue(target);
        }
    }
}
