using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class LambdaCapturedVariable : SynthesizedFieldSymbolBase {
    private readonly TypeWithAnnotations _type;
    private readonly bool _isThis;

    private LambdaCapturedVariable(
        SynthesizedClosureEnvironment frame,
        TypeWithAnnotations type,
        string fieldName,
        bool isThisParameter)
        : base(frame,
               fieldName,
               isPublic: true,
               isConst: false,
               isStatic: false,
               isConstExpr: false) {
        _type = type;
        _isThis = isThisParameter;
    }

    internal SynthesizedClosureEnvironment frame => (SynthesizedClosureEnvironment)containingType;

    internal static LambdaCapturedVariable Create(
        SynthesizedClosureEnvironment frame,
        Symbol captured,
        ref int uniqueId) {
        var fieldName = GetCapturedVariableFieldName(captured, ref uniqueId);
        var type = GetCapturedVariableFieldType(frame, captured);
        return new LambdaCapturedVariable(frame, new TypeWithAnnotations(type), fieldName, IsThis(captured));
    }

    private static bool IsThis(Symbol captured) {
        return captured is ParameterSymbol parameter && parameter.isThis;
    }

    private static string GetCapturedVariableFieldName(Symbol variable, ref int uniqueId) {
        if (IsThis(variable))
            return "<>4__this";

        if (variable is DataContainerSymbol local) {
            switch (local.synthesizedKind) {
                case SynthesizedLocalKind.LambdaDisplayClass:
                    return GeneratedNames.MakeLambdaDisplayLocalName(uniqueId++);
            }

            // if (local.synthesizedKind == SynthesizedLocalKind.UserDefined &&
            //     (local.ScopeDesignatorOpt?.Kind() == SyntaxKind.SwitchSection ||
            //      local.ScopeDesignatorOpt?.Kind() == SyntaxKind.SwitchExpressionArm)) {
            //     // The programmer can use the same identifier for pattern variables in different
            //     // sections of a switch statement, but they are all hoisted into
            //     // the same frame for the enclosing switch statement and must be given
            //     // unique field names.
            //     return GeneratedNames.MakeHoistedLocalFieldName(local.SynthesizedKind, uniqueId++, local.Name);
            // }
        }

        return variable.name;
    }

    private static TypeSymbol GetCapturedVariableFieldType(SynthesizedContainer frame, Symbol variable) {
        var local = variable as DataContainerSymbol;

        if (local is not null) {
            if (local.type.originalDefinition is SynthesizedClosureEnvironment lambdaFrame) {
                var typeArguments = frame.templateArguments;

                if (typeArguments.Length > lambdaFrame.arity)
                    typeArguments = ImmutableArray.Create(typeArguments, 0, lambdaFrame.arity);

                return lambdaFrame.ConstructIfGeneric(typeArguments);
            }
        }

        return frame.templateMap.SubstituteType(
            (local is not null ? local.typeWithAnnotations : ((ParameterSymbol)variable).typeWithAnnotations).type
        ).type.type;
    }

    public override RefKind refKind => RefKind.None;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return _type;
    }

    internal override bool isCapturedFrame => _isThis;
}
