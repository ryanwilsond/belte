using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

/// <summary>
/// All predefined warning messages that can be used by the compiler.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the warning messages to be more dynamic and represent the warning
/// more accurately.
/// </summary>
internal static class Warning {
    internal static BelteDiagnostic AlwaysValue(TextLocation location, object value) {
        var valueString = value is null ? "null" : value.ToString();

        if (value is bool)
            // False -> false
            valueString = valueString.ToLower();

        var message = $"expression will always result to '{valueString}'";
        return CreateWarning(DiagnosticCode.WRN_AlwaysValue, location, message);
    }

    internal static BelteDiagnostic NullDereference(TextLocation location) {
        var message = "dereference of a possibly null value";
        return CreateWarning(DiagnosticCode.WRN_NullDereference, location, message);
    }

    internal static BelteDiagnostic UnreachableCode(TextLocation location) {
        var message = "unreachable code";
        return CreateWarning(DiagnosticCode.WRN_UnreachableCode, location, message);
    }

    internal static BelteDiagnostic NeverGivenType(TextLocation location, TypeSymbol type) {
        var message = $"the given expression is never of the provided type ('{type.ToNullOrString()}')";
        return CreateWarning(DiagnosticCode.WRN_NeverGivenType, location, message);
    }

    internal static BelteDiagnostic PossibleMistakenEmptyStatement(TextLocation location) {
        var message = "possible mistaken empty statement";
        return CreateWarning(DiagnosticCode.WRN_PossibleMistakenEmptyStatement, location, message);
    }

    internal static BelteDiagnostic IncorrectBooleanAssignment(TextLocation location) {
        var message = "assignment in conditional expression is always constant; did you mean to use '==' instead of '=' ?";
        return CreateWarning(DiagnosticCode.WRN_IncorrectBooleanAssignment, location, message);
    }

    internal static BelteDiagnostic RefConstNotVariable(TextLocation location, int arg) {
        var message = $"argument {arg} should be a variable because it is passed to a 'ref const' parameter";
        return CreateWarning(DiagnosticCode.WRN_RefConstNotVariable, location, message);
    }

    internal static BelteDiagnostic ArgExpectedRef(TextLocation location, int arg) {
        var message = $"argument {arg} should be passed with the 'ref' keyword";
        return CreateWarning(DiagnosticCode.WRN_ArgExpectedRef, location, message);
    }

    internal static BelteDiagnostic TemplateParameterSameAsOuterMethod(TextLocation location, string name, Symbol symbol) {
        var message = $"template parameter '{name}' has the same name as the template parameter from outer method '{symbol}'";
        return CreateWarning(DiagnosticCode.WRN_TemplateParameterSameAsOuterMethod, location, message);
    }

    internal static BelteDiagnostic TemplateParameterSameAsOuter(TextLocation location, string name, Symbol symbol) {
        var message = $"template parameter '{name}' has the same name as the template parameter from outer type '{symbol}'";
        return CreateWarning(DiagnosticCode.WRN_TemplateParameterSameAsOuter, location, message);
    }

    internal static BelteDiagnostic DefaultValueNoEffect(TextLocation location, string name) {
        var message = $"the default value specified for parameter '{name}' will have no effect because it applies to a member that is used in contexts that do not allow optional arguments";
        return CreateWarning(DiagnosticCode.WRN_DefaultValueNoEffect, location, message);
    }

    internal static BelteDiagnostic RefConstParameterDefaultValue(TextLocation location, string name) {
        var message = $"a default value is specified for 'ref const' parameter '{name}', but 'ref const' should be used only for references";
        return CreateWarning(DiagnosticCode.WRN_RefConstParameterDefaultValue, location, message);
    }

    internal static BelteDiagnostic EqualsWithoutGetHashCode(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' overrides 'Object.Equals(Object)' but does not override 'Object.GetHashCode()'";
        return CreateWarning(DiagnosticCode.WRN_EqualsWithoutGetHashCode, location, message);
    }

    internal static BelteDiagnostic EqualityOpWithoutEquals(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' defines operator == or operator != but does not override 'Object.Equals(Object)'";
        return CreateWarning(DiagnosticCode.WRN_EqualityOpWithoutEquals, location, message);
    }

    internal static BelteDiagnostic EqualityOpWithoutGetHashCode(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}' defines operator == or operator != but does not override 'Object.GetHashCode()'";
        return CreateWarning(DiagnosticCode.WRN_EqualityOpWithoutGetHashCode, location, message);
    }

    internal static BelteDiagnostic NewRequired(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}' hides inherited member '{hiddenMember}'; use the new keyword if hiding was intended";
        return CreateWarning(DiagnosticCode.WRN_NewRequired, location, message);
    }

    internal static BelteDiagnostic NewNotRequired(TextLocation location, Symbol symbol) {
        var message = $"the member '{symbol}' does not hide an accessible member; the new keyword is not required";
        return CreateWarning(DiagnosticCode.WRN_NewNotRequired, location, message);
    }

    internal static BelteDiagnostic NewOrOverrideExpected(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}' hides inherited member '{hiddenMember}'; to make the current member override that implementation, add the override keyword; otherwise add the new keyword";
        return CreateWarning(DiagnosticCode.WRN_NewOrOverrideExpected, location, message);
    }

    internal static BelteDiagnostic HidingDifferentRefness(TextLocation location, ParameterSymbol parameter, ParameterSymbol hiddenParameter) {
        var message = $"reference kind modifier of parameter '{parameter}' doesn't match the corresponding parameter '{hiddenParameter}' in hidden member";
        return CreateWarning(DiagnosticCode.WRN_HidingDifferentRefness, location, message);
    }

    internal static BelteDiagnostic OverridingDifferentRefness(TextLocation location, ParameterSymbol parameter, ParameterSymbol hiddenParameter) {
        var message = $"reference kind modifier of parameter '{parameter}' doesn't match the corresponding parameter '{hiddenParameter}' in overridden or implemented member";
        return CreateWarning(DiagnosticCode.WRN_OverridingDifferentRefness, location, message);
    }

    internal static BelteDiagnostic TopLevelNullabilityMismatchInParameterTypeOnOverride(TextLocation location, Symbol symbol) {
        var message = $"nullability of type of parameter '{symbol}' doesn't match overridden member";
        return CreateWarning(DiagnosticCode.WRN_TopLevelNullabilityMismatchInParameterTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic NullabilityMismatchInParameterTypeOnOverride(TextLocation location, Symbol symbol) {
        var message = $"nullability of reference types in type of parameter '{symbol}' doesn't match overridden member";
        return CreateWarning(DiagnosticCode.WRN_NullabilityMismatchInParameterTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic TopLevelNullabilityMismatchInReturnTypeOnOverride(TextLocation location) {
        var message = $"nullability of return type doesn't match overridden member";
        return CreateWarning(DiagnosticCode.WRN_TopLevelNullabilityMismatchInReturnTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic NullabilityMismatchInReturnTypeOnOverride(TextLocation location) {
        var message = $"nullability of reference types in return type doesn't match overridden member";
        return CreateWarning(DiagnosticCode.WRN_NullabilityMismatchInReturnTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic LocalUsingTypeName(TextLocation location, string name) {
        var message = $"local '{name}' shares a name with a type in this namespace";
        return CreateWarning(DiagnosticCode.WRN_LocalUsingTypeName, location, message);
    }

    internal static BelteDiagnostic ProtectedInSealed(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': new protected member declared in sealed type; no different than private";
        return CreateWarning(DiagnosticCode.WRN_ProtectedInSealed, location, message);
    }

    // TODO Implement this warning
    internal static BelteDiagnostic ImpliedReference(TextLocation location) {
        var message = $"implicit types infer reference types making the 'ref' keyword not necessary in this context";
        return CreateWarning(DiagnosticCode.WRN_ImpliedReference, location, message);
    }

    private static BelteDiagnostic CreateWarning(DiagnosticCode code, TextLocation location, string message) {
        return CreateWarning(code, location, message, []);
    }

    private static BelteDiagnostic CreateWarning(
        DiagnosticCode code,
        TextLocation location,
        string message,
        params string[] suggestions) {
        return new BelteDiagnostic(WarningInfo(code), location, message, suggestions);
    }

    private static DiagnosticInfo WarningInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticSeverity.Warning);
    }
}
