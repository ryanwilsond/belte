
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// The implemented names of specific members the compiler has special awareness of.
/// </summary>
internal static class WellKnownMemberNames {
    internal const string InstanceConstructorName = ".ctor";
    internal const string PowerOperatorName = "op_Power";
    internal const string MultiplyOperatorName = "op_Multiply";
    internal const string DivideOperatorName = "op_Divide";
    internal const string ModulusOperatorName = "op_Modulus";
    internal const string AdditionOperatorName = "op_Addition";
    internal const string UnaryPlusOperatorName = "op_UnaryPlus";
    internal const string SubtractionOperatorName = "op_Subtraction";
    internal const string UnaryNegationOperatorName = "op_UnaryNegation";
    internal const string LeftShiftOperatorName = "op_LeftShift";
    internal const string RightShiftOperatorName = "op_RightShift";
    internal const string UnsignedRightShiftOperatorName = "op_UnsignedRightShift";
    internal const string BitwiseAndOperatorName = "op_BitwiseAnd";
    internal const string BitwiseExclusiveOrOperatorName = "op_BitwiseExclusiveOr";
    internal const string BitwiseOrOperatorName = "op_BitwiseOr";
    internal const string IncrementOperatorName = "op_Increment";
    internal const string DecrementOperatorName = "op_Decrement";
    internal const string LogicalNotOperatorName = "op_LogicalNot";
    internal const string BitwiseNotOperatorName = "op_BitwiseNot";
    internal const string IndexOperatorName = "op_Index";
    internal const string IndexAssignName = "op_IndexAssign";
    internal const string EqualityOperatorName = "op_Equality";
    internal const string InequalityOperatorName = "op_Inequality";
    internal const string LessThanOperatorName = "op_LessThan";
    internal const string GreaterThanOperatorName = "op_GreaterThan";
    internal const string LessThanOrEqualOperatorName = "op_LessThanOrEqual";
    internal const string GreaterThanOrEqualOperatorName = "op_GreaterThanOrEqual";
    internal const string EntryPointMethodName = "Main";
    internal new const string ToString = "ToString";
}
