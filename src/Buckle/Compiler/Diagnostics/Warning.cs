using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
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

    internal static BelteDiagnostic UnreachableCode(TextLocation location) {
        var message = "unreachable code";
        return CreateWarning(DiagnosticCode.WRN_UnreachableCode, location, message);
    }

    internal static BelteDiagnostic NeverGivenType(TextLocation location, TypeSymbol type) {
        var message = $"the given expression is never of the provided type ('{type.ToNullOrString(SymbolDisplayFormat.QualifiedNameFormat)}')";
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
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"argument {arg} should be a variable because it is passed to a 'ref const' parameter";
        // return CreateWarning(DiagnosticCode.WRN_RefConstNotVariable, location, message);
    }

    internal static BelteDiagnostic ArgExpectedRef(TextLocation location, int arg) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"argument {arg} should be passed with the 'ref' keyword";
        // return CreateWarning(DiagnosticCode.WRN_ArgExpectedRef, location, message);
    }

    internal static BelteDiagnostic TemplateParameterSameAsOuterMethod(TextLocation location, string name, Symbol symbol) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"template parameter '{name}' has the same name as the template parameter from outer method '{symbol}'";
        // return CreateWarning(DiagnosticCode.WRN_TemplateParameterSameAsOuterMethod, location, message);
    }

    internal static BelteDiagnostic TemplateParameterSameAsOuter(TextLocation location, string name, Symbol symbol) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"template parameter '{name}' has the same name as the template parameter from outer type '{symbol}'";
        // return CreateWarning(DiagnosticCode.WRN_TemplateParameterSameAsOuter, location, message);
    }

    internal static BelteDiagnostic DefaultValueNoEffect(TextLocation location, string name) {
        var message = $"the default value specified for parameter '{name}' will have no effect because it applies to a member that is used in contexts that do not allow optional arguments";
        return CreateWarning(DiagnosticCode.WRN_DefaultValueNoEffect, location, message);
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
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"reference kind modifier of parameter '{parameter}' doesn't match the corresponding parameter '{hiddenParameter}' in hidden member";
        // return CreateWarning(DiagnosticCode.WRN_HidingDifferentRefness, location, message);
    }

    internal static BelteDiagnostic OverridingDifferentRefness(TextLocation location, ParameterSymbol parameter, ParameterSymbol hiddenParameter) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"reference kind modifier of parameter '{parameter}' doesn't match the corresponding parameter '{hiddenParameter}' in overridden or implemented member";
        // return CreateWarning(DiagnosticCode.WRN_OverridingDifferentRefness, location, message);
    }

    internal static BelteDiagnostic TopLevelNullabilityMismatchInParameterTypeOnOverride(TextLocation location, Symbol symbol) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"nullability of type of parameter '{symbol}' doesn't match overridden member";
        // return CreateWarning(DiagnosticCode.WRN_TopLevelNullabilityMismatchInParameterTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic NullabilityMismatchInParameterTypeOnOverride(TextLocation location, Symbol symbol) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"nullability of reference types in type of parameter '{symbol}' doesn't match overridden member";
        // return CreateWarning(DiagnosticCode.WRN_NullabilityMismatchInParameterTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic TopLevelNullabilityMismatchInReturnTypeOnOverride(TextLocation location) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"nullability of return type doesn't match overridden member";
        // return CreateWarning(DiagnosticCode.WRN_TopLevelNullabilityMismatchInReturnTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic NullabilityMismatchInReturnTypeOnOverride(TextLocation location) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"nullability of reference types in return type doesn't match overridden member";
        // return CreateWarning(DiagnosticCode.WRN_NullabilityMismatchInReturnTypeOnOverride, location, message);
    }

    internal static BelteDiagnostic LocalUsingTypeName(TextLocation location, string name) {
        var message = $"local '{name}' shares a name with a type in this namespace";
        return CreateWarning(DiagnosticCode.WRN_LocalUsingTypeName, location, message);
    }

    internal static BelteDiagnostic ProtectedInSealed(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': new protected member declared in sealed type; no different than private";
        return CreateWarning(DiagnosticCode.WRN_ProtectedInSealed, location, message);
    }

    internal static BelteDiagnostic SealedInSealed(TextLocation location, Symbol symbol) {
        var message = $"'{symbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': sealed member declared in sealed type; no different than non-sealed override";
        return CreateWarning(DiagnosticCode.WRN_SealedInSealed, location, message);
    }

    // TODO Implement this warning
    internal static BelteDiagnostic ImpliedReference(TextLocation location) {
        throw Utilities.ExceptionUtilities.Unreachable();
        // var message = $"implicit types infer reference types making the 'ref' keyword not necessary in this context";
        // return CreateWarning(DiagnosticCode.WRN_ImpliedReference, location, message);
    }

    internal static BelteDiagnostic NamespaceNameShadowsBelte(TextLocation location, NamespaceSymbol symbol) {
        var message = $"namespace '{symbol}' potentially shadows parts of the Standard Library";
        return CreateWarning(DiagnosticCode.WRN_NamespaceNameShadowsBelte, location, message);
    }

    internal static BelteDiagnostic UnusedUsingDirective(TextLocation location) {
        var message = $"using directive is unnecessary";
        return CreateWarning(DiagnosticCode.WRN_UnusedUsingDirective, location, message);
    }

    internal static BelteDiagnostic ExitingControlFlowInWith(TextLocation location) {
        var message = $"exiting the with body early will result in the reversals not taking place; consider using a 'with (...) try'";
        return CreateWarning(DiagnosticCode.WRN_ExitingControlFlowInWith, location, message);
    }

    internal static BelteDiagnostic IgnoringReturnValue(TextLocation location, MethodSymbol method) {
        var message = $"ignoring return value of method '{method}'; consider using a discard assignment if this is intended";
        var suggestion = "_ = %";
        return CreateWarning(DiagnosticCode.WRN_IgnoringReturnValue, location, message, suggestion);
    }

    internal static BelteDiagnostic TransientForEachAssignment(TextLocation location) {
        var message = $"assignment to a for-each iterator local does not modify the element in the source collection";
        return CreateWarning(DiagnosticCode.WRN_TransientForEachAssignment, location, message);
    }

    internal static BelteDiagnostic StructInefficiencyCache(TextLocation location, TypeSymbol type, int actualSize, int optimalSize) {
        var message = $"'{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': struct crosses an unnecessary cache line; struct layout could be reduced from {actualSize} bytes to {optimalSize} bytes by reordering fields";
        return CreateWarning(DiagnosticCode.WRN_StructInefficiencyCache, location, message);
    }

    internal static BelteDiagnostic StructInefficiencyPadding(TextLocation location, TypeSymbol type, int actualSize, int optimalSize) {
        var message = $"'{type.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat)}': struct layout could be reduced from {actualSize} bytes to {optimalSize} bytes by reordering fields";
        return CreateWarning(DiagnosticCode.WRN_StructInefficiencyPadding, location, message);
    }

    internal static BelteDiagnostic LongTuple(TextLocation location, int size) {
        var message = $"long tuple ({size} elements); consider using a named struct";
        return CreateWarning(DiagnosticCode.WRN_LongTuple, location, message);
    }

    internal static BelteDiagnostic UnnecessaryLowLevelDefaultLiteral(TextLocation location, TypeSymbol type) {
        var message = $"lowlevel default literal is unnecessary because the type '{type}' has a default value; consider using a regular default literal";
        var suggestion = "default";
        return CreateWarning(DiagnosticCode.WRN_UnnecessaryLowLevelDefaultLiteral, location, message, suggestion);
    }

    internal static BelteDiagnostic UnnecessaryLowLevelDefaultExpression(TextLocation location, TypeSymbol type) {
        var message = $"lowlevel default expression is unnecessary because the type '{type}' has a default value; consider using a regular default expression";
        var suggestion = $"default({type})";
        return CreateWarning(DiagnosticCode.WRN_UnnecessaryLowLevelDefaultExpression, location, message, suggestion);
    }

    internal static BelteDiagnostic LocalFunctionUsingEntryPointName(TextLocation location) {
        var message = $"local function uses the entry point name but is not treated as the entry point because it does not have the correct signature";
        return CreateWarning(DiagnosticCode.WRN_LocalFunctionUsingEntryPointName, location, message);
    }

    internal static BelteDiagnostic DifferentConstOnOverride(TextLocation location, Symbol symbol, Symbol hiddenMember) {
        var message = $"'{symbol}': member is marked 'const' but overridden member '{hiddenMember}' is not";
        return CreateWarning(DiagnosticCode.WRN_DifferentConstOnOverride, location, message);
    }

    internal static BelteDiagnostic DifferentConstOnOverrideParameter(TextLocation location, Symbol symbol, Symbol hiddenMember, string name) {
        var message = $"'{symbol}': parameter '{name}' is marked 'const' but the corresponding parameter on overridden member '{hiddenMember}' is not";
        return CreateWarning(DiagnosticCode.WRN_DifferentConstOnOverrideParameter, location, message);
    }

    internal static BelteDiagnostic DuplicateReference(string reference) {
        var message = $"\"{reference}\": reference has already been added to the compilation";
        return CreateWarning(DiagnosticCode.WRN_DuplicateReference, null, message);
    }

    internal static BelteDiagnostic DuplicateAssembly(AssemblyIdentity assembly) {
        var message = $"\"{assembly.GetDisplayName()}\": assembly has already been added to the compilation";
        return CreateWarning(DiagnosticCode.WRN_DuplicateAssembly, null, message);
    }

    internal static BelteDiagnostic NullBinaryEquality(TextLocation location, bool isNot, BoundExpression left) {
        var message = $"null checks should use the 'is' or 'isnt' operator";
        var suggestion = $"{left} {(isNot ? "isnt" : "is")} null";
        return CreateWarning(DiagnosticCode.WRN_NullBinaryEquality, location, message, suggestion);
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
