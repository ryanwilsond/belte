using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal static class ParameterHelpers {
    internal static bool ReportDefaultParameterErrors(
        Binder binder,
        Symbol owner,
        ParameterSyntax parameterSyntax,
        SourceParameterSymbol parameter,
        BoundExpression defaultExpression,
        BoundExpression convertedExpression,
        BelteDiagnosticQueue diagnostics) {
        // TODO
        var hasErrors = false;

        var parameterType = parameter.type;
        var conversion = binder.conversions.ClassifyImplicitConversionFromExpression(defaultExpression, parameterType);

        var refKind = GetModifiers(parameterSyntax.Modifiers, out SyntaxToken refnessKeyword, out SyntaxToken paramsKeyword, out SyntaxToken thisKeyword, out _);

        // CONSIDER: We are inconsistent here regarding where the error is reported; is it
        // CONSIDER: reported on the parameter name, or on the value of the initializer?
        // CONSIDER: Consider making this consistent.

        if (refKind == RefKind.Ref || refKind == RefKind.Out) {
            // error CS1741: A ref or out parameter cannot have a default value
            diagnostics.Add(ErrorCode.ERR_RefOutDefaultValue, refnessKeyword.GetLocation());
            hasErrors = true;
        } else if (paramsKeyword.Kind() == SyntaxKind.ParamsKeyword) {
            // error CS1751: Cannot specify a default value for a parameter collection
            diagnostics.Add(ErrorCode.ERR_DefaultValueForParamsParameter, paramsKeyword.GetLocation());
            hasErrors = true;
        } else if (thisKeyword.Kind() == SyntaxKind.ThisKeyword) {
            // Only need to report CS1743 for the first parameter. The caller will
            // have reported CS1100 if 'this' appeared on another parameter.
            if (parameter.Ordinal == 0) {
                // error CS1743: Cannot specify a default value for the 'this' parameter
                diagnostics.Add(ErrorCode.ERR_DefaultValueForExtensionParameter, thisKeyword.GetLocation());
                hasErrors = true;
            }
        } else if (!defaultExpression.HasAnyErrors &&
              !IsValidDefaultValue(defaultExpression.IsImplicitObjectCreation() ?
                  convertedExpression : defaultExpression)) {
            // error CS1736: Default parameter value for '{0}' must be a compile-time constant
            diagnostics.Add(ErrorCode.ERR_DefaultValueMustBeConstant, parameterSyntax.Default.Value.Location, parameterSyntax.Identifier.ValueText);
            hasErrors = true;
        } else if (!conversion.Exists ||
              conversion.IsUserDefined ||
              conversion.IsIdentity && parameterType.SpecialType == SpecialType.System_Object && defaultExpression.Type.IsDynamic()) {
            // If we had no implicit conversion, or a user-defined conversion, report an error.
            //
            // Even though "object x = (dynamic)null" is a legal identity conversion, we do not allow it.
            // CONSIDER: We could. Doesn't hurt anything.

            // error CS1750: A value of type '{0}' cannot be used as a default parameter because there are no standard conversions to type '{1}'
            diagnostics.Add(ErrorCode.ERR_NoConversionForDefaultParam, parameterSyntax.Identifier.GetLocation(),
                defaultExpression.Display, parameterType);

            hasErrors = true;
        } else if (conversion.IsReference &&
              (object)defaultExpression.Type != null &&
              defaultExpression.Type.SpecialType == SpecialType.System_String ||
              conversion.IsBoxing) {
            // We don't allow object x = "hello", object x = 123, dynamic x = "hello", IEnumerable<char> x = "hello", etc.
            // error CS1763: '{0}' is of type '{1}'. A default parameter value of a reference type other than string can only be initialized with null
            diagnostics.Add(ErrorCode.ERR_NotNullRefDefaultParameter, parameterSyntax.Identifier.GetLocation(),
                parameterSyntax.Identifier.ValueText, parameterType);

            hasErrors = true;
        } else if (((conversion.IsNullable && !defaultExpression.Type.IsNullableType()) ||
                    (conversion.IsObjectCreation && convertedExpression.Type.IsNullableType())) &&
              !(parameterType.GetNullableUnderlyingType().IsEnumType() || parameterType.GetNullableUnderlyingType().IsIntrinsicType())) {
            // We can do:
            // M(int? x = default(int))
            // M(int? x = default(int?))
            // M(MyEnum? e = default(enum))
            // M(MyEnum? e = default(enum?))
            // M(MyStruct? s = default(MyStruct?))
            //
            // but we cannot do:
            //
            // M(MyStruct? s = default(MyStruct))

            // error CS1770:
            // A value of type '{0}' cannot be used as default parameter for nullable parameter '{1}' because '{0}' is not a simple type
            diagnostics.Add(ErrorCode.ERR_NoConversionForNubDefaultParam, parameterSyntax.Identifier.GetLocation(),
                (defaultExpression.IsImplicitObjectCreation() ? convertedExpression.Type.StrippedType() : defaultExpression.Type), parameterSyntax.Identifier.ValueText);

            hasErrors = true;
        }

        ConstantValueUtils.CheckLangVersionForConstantValue(convertedExpression, diagnostics);

        // Certain contexts allow default parameter values syntactically but they are ignored during
        // semantic analysis. They are:

        // 1. Explicitly implemented interface methods; since the method will always be called
        //    via the interface, the defaults declared on the implementation will not
        //    be seen at the call site.
        //
        // UNDONE: 2. The "actual" side of a partial method; the default values are taken from the
        // UNDONE:    "declaring" side of the method.
        //
        // UNDONE: 3. An indexer with only one formal parameter; it is illegal to omit every argument
        // UNDONE:    to an indexer.
        //
        // 4. A user-defined operator; it is syntactically impossible to omit the argument.

        if (owner.IsExplicitInterfaceImplementation() ||
            owner.IsPartialImplementation() ||
            owner.IsOperator()) {
            // CS1066: The default value specified for parameter '{0}' will have no effect because it applies to a
            //         member that is used in contexts that do not allow optional arguments
            diagnostics.Add(ErrorCode.WRN_DefaultValueForUnconsumedLocation,
                parameterSyntax.Identifier.GetLocation(),
                parameterSyntax.Identifier.ValueText);
        }

        if (refKind == RefKind.RefReadOnlyParameter) {
            // A default value is specified for 'ref readonly' parameter '{0}', but 'ref readonly' should be used only for references. Consider declaring the parameter as 'in'.
            diagnostics.Add(ErrorCode.WRN_RefReadonlyParameterDefaultValue, parameterSyntax.Default.Value, parameterSyntax.Identifier.ValueText);
        }

        return hasErrors;
    }
}
