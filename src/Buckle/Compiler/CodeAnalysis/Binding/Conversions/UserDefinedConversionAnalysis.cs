using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class UserDefinedConversionAnalysis {
    internal readonly TypeSymbol fromType;
    internal readonly TypeSymbol toType;
    internal readonly TemplateParameterSymbol constrainedToTypeOpt;
    internal readonly MethodSymbol @operator;

    internal readonly Conversion sourceConversion;
    internal readonly Conversion targetConversion;
    internal readonly UserDefinedConversionAnalysisKind kind;

    internal static UserDefinedConversionAnalysis Normal(
        TemplateParameterSymbol constrainedToTypeOpt,
        MethodSymbol op,
        Conversion sourceConversion,
        Conversion targetConversion,
        TypeSymbol fromType,
        TypeSymbol toType) {
        return new UserDefinedConversionAnalysis(
            UserDefinedConversionAnalysisKind.ApplicableInNormalForm,
            constrainedToTypeOpt,
            op,
            sourceConversion,
            targetConversion,
            fromType,
            toType);
    }

    internal static UserDefinedConversionAnalysis Lifted(
        TemplateParameterSymbol constrainedToTypeOpt,
        MethodSymbol op,
        Conversion sourceConversion,
        Conversion targetConversion,
        TypeSymbol fromType,
        TypeSymbol toType) {
        return new UserDefinedConversionAnalysis(
            UserDefinedConversionAnalysisKind.ApplicableInLiftedForm,
            constrainedToTypeOpt,
            op,
            sourceConversion,
            targetConversion,
            fromType,
            toType);
    }

    private UserDefinedConversionAnalysis(
        UserDefinedConversionAnalysisKind kind,
        TemplateParameterSymbol constrainedToTypeOpt,
        MethodSymbol op,
        Conversion sourceConversion,
        Conversion targetConversion,
        TypeSymbol fromType,
        TypeSymbol toType) {
        this.kind = kind;
        this.constrainedToTypeOpt = constrainedToTypeOpt;
        @operator = op;
        this.sourceConversion = sourceConversion;
        this.targetConversion = targetConversion;
        this.fromType = fromType;
        this.toType = toType;
    }
}
