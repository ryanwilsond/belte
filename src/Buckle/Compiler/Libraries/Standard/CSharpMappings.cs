using System;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries.Standard;

/// <summary>
/// The compiler-used implementation of the Standard Library.
/// </summary>
internal static partial class StandardLibrary {
    /// <summary>
    /// Gets all the pre-compiled symbols defined by the library.
    /// </summary>
    internal static Symbol[] GetSymbols() {
        return [Object, Console, Math];
    }

    /// <summary>
    /// Method used to evaluate Standard Library methods with no native implementation.
    /// </summary>
    internal static object EvaluateMethod(MethodSymbol method, object[] arguments) {
        // TODO This could be optimized by using a unique name lookup instead of comparing symbols
        // TODO Could optimize this by inlining Math methods such as Clamp
        // instead of calling the external .NET library to do it for us
        if (method == Console.members[1]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.WriteLine(arguments[0]);
        } else if (method == Console.members[2]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.WriteLine(arguments[0]);
        } else if (method == Console.members[3]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.WriteLine(arguments[0]);
        } else if (method == Console.members[4]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.WriteLine();
        } else if (method == Console.members[5]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.Write(arguments[0]);
        } else if (method == Console.members[6]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.Write(arguments[0]);
        } else if (method == Console.members[7]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.Write(arguments[0]);
        } else if (method == Console.members[8]) {
            if (!System.Console.IsInputRedirected)
                return System.Console.ReadLine();
        } else if (method == Console.members[9]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.ForegroundColor = (ConsoleColor)arguments[0];
        } else if (method == Console.members[10]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.BackgroundColor = (ConsoleColor)arguments[0];
        } else if (method == Console.members[11]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.ResetColor();
        } else if (method == Console.members[12]) {
            if (!System.Console.IsOutputRedirected)
                return System.Console.WindowWidth;
        } else if (method == Console.members[13]) {
            if (!System.Console.IsOutputRedirected)
                return System.Console.WindowHeight;
        } else if (method == Console.members[14]) {
            if (!System.Console.IsOutputRedirected) {
                var left = (int?)arguments[0] ?? System.Console.CursorLeft;
                var top = (int?)arguments[1] ?? System.Console.CursorTop;
                System.Console.SetCursorPosition(left, top);
            }
        } else if (method == Math.members[2]) {
            return arguments[0] is null ? null : System.Math.Abs(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[3]) {
            return System.Math.Abs(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[4]) {
            return arguments[0] is null ? null : System.Math.Abs((int)arguments[0]);
        } else if (method == Math.members[5]) {
            return System.Math.Abs((int)arguments[0]);
        } else if (method == Math.members[6]) {
            return arguments[0] is null ? null : System.Math.Acos(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[7]) {
            return System.Math.Acos(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[8]) {
            return arguments[0] is null ? null : System.Math.Acosh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[9]) {
            return System.Math.Acosh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[10]) {
            return arguments[0] is null ? null : System.Math.Asin(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[11]) {
            return System.Math.Asin(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[12]) {
            return arguments[0] is null ? null : System.Math.Asinh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[13]) {
            return System.Math.Asinh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[14]) {
            return arguments[0] is null ? null : System.Math.Atan(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[15]) {
            return System.Math.Atan(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[16]) {
            return arguments[0] is null ? null : System.Math.Atanh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[17]) {
            return System.Math.Atanh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[18]) {
            return arguments[0] is null ? null : System.Math.Ceiling(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[19]) {
            return System.Math.Ceiling(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[20]) {
            return arguments[0] is null || arguments[0] is null || arguments[0] is null ? null :
                System.Math.Clamp(
                    Convert.ToDouble(arguments[0]),
                    Convert.ToDouble(arguments[1]),
                    Convert.ToDouble(arguments[2])
                );
        } else if (method == Math.members[21]) {
            return System.Math.Clamp(
                    Convert.ToDouble(arguments[0]),
                    Convert.ToDouble(arguments[1]),
                    Convert.ToDouble(arguments[2])
                );
        } else if (method == Math.members[22]) {
            return arguments[0] is null || arguments[0] is null || arguments[0] is null ? null :
                System.Math.Clamp(
                    (int)arguments[0],
                    (int)arguments[1],
                    (int)arguments[2]
                );
        } else if (method == Math.members[23]) {
            return System.Math.Clamp(
                    (int)arguments[0],
                    (int)arguments[1],
                    (int)arguments[2]
                );
        } else if (method == Math.members[24]) {
            return arguments[0] is null ? null : System.Math.Cos(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[25]) {
            return System.Math.Cos(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[26]) {
            return arguments[0] is null ? null : System.Math.Cosh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[27]) {
            return System.Math.Cosh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[28]) {
            return arguments[0] is null ? null : System.Math.Exp(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[29]) {
            return System.Math.Exp(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[30]) {
            return arguments[0] is null ? null : System.Math.Floor(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[31]) {
            return System.Math.Floor(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[32]) {
            if (arguments[0] is null || arguments[1] is null || arguments[2] is null)
                return null;

            var rate = Convert.ToDouble(arguments[2]);
            var start = Convert.ToDouble(arguments[0]);
            return start + rate * (Convert.ToDouble(arguments[1]) - start);
        } else if (method == Math.members[33]) {
            var rate = Convert.ToDouble(arguments[2]);
            return Convert.ToDouble(arguments[0]) * (1 - rate) + Convert.ToDouble(arguments[1]) * rate;
        } else if (method == Math.members[34]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Log(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[35]) {
            return System.Math.Log(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[36]) {
            return arguments[0] is null ? null : System.Math.Log(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[37]) {
            return System.Math.Log(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[38]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Max(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[39]) {
            return System.Math.Max(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[40]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Max((int)arguments[0], (int)arguments[1]);
        } else if (method == Math.members[41]) {
            return System.Math.Max((int)arguments[0], (int)arguments[1]);
        } else if (method == Math.members[42]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Min(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[43]) {
            return System.Math.Min(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[44]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Min((int)arguments[0], (int)arguments[1]);
        } else if (method == Math.members[45]) {
            return System.Math.Min((int)arguments[0], (int)arguments[1]);
        } else if (method == Math.members[46]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Pow(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[47]) {
            return System.Math.Pow(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[48]) {
            return arguments[0] is null ? null : System.Math.Round(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[49]) {
            return System.Math.Round(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[50]) {
            return arguments[0] is null ? null : System.Math.Sin(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[51]) {
            return System.Math.Sin(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[52]) {
            return arguments[0] is null ? null : System.Math.Sinh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[53]) {
            return System.Math.Sinh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[54]) {
            return arguments[0] is null ? null : System.Math.Sqrt(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[55]) {
            return System.Math.Sqrt(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[56]) {
            return arguments[0] is null ? null : System.Math.Tan(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[57]) {
            return System.Math.Tan(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[58]) {
            return arguments[0] is null ? null : System.Math.Tanh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[59]) {
            return System.Math.Tanh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[60]) {
            return arguments[0] is null ? null : System.Math.Truncate(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[61]) {
            return System.Math.Truncate(Convert.ToDouble(arguments[0]));
        }

        return null;
    }

    /// <summary>
    /// Converts a Standard Library method name into its C# equivalent as a string.
    /// </summary>
    internal static string CSharpEmitMethod(MethodSymbol method) {
        if (method == Console.members[1]) {
            return "global::System.Console.WriteLine";
        } else if (method == Console.members[2]) {
            return "global::System.Console.WriteLine";
        } else if (method == Console.members[3]) {
            return "global::System.Console.WriteLine";
        } else if (method == Console.members[4]) {
            return "global::System.Console.Write";
        } else if (method == Console.members[5]) {
            return "global::System.Console.Write"; ;
        } else if (method == Console.members[6]) {
            return "global::System.Console.ReadLine";
        } else if (method == Console.members[7]) {
            return "global::System.Console.ForegroundColor = ";
        } else if (method == Console.members[8]) {
            return "global::System.Console.BackgroundColor = ";
        } else if (method == Console.members[9]) {
            return "global::System.Console.ResetColor";
        } else if (method == Math.members[2] || method == Math.members[3]) {
            return "global::System.Math.Abs";
        } else if (method == Math.members[4] || method == Math.members[5]) {
            return "global::System.Math.Abs";
        } else if (method == Math.members[6] || method == Math.members[7]) {
            return "global::System.Math.Acos";
        } else if (method == Math.members[8] || method == Math.members[9]) {
            return "global::System.Math.Acosh";
        } else if (method == Math.members[10] || method == Math.members[11]) {
            return "global::System.Math.Asin";
        } else if (method == Math.members[12] || method == Math.members[13]) {
            return "global::System.Math.Asinh";
        } else if (method == Math.members[14] || method == Math.members[15]) {
            return "global::System.Math.Atan";
        } else if (method == Math.members[16] || method == Math.members[17]) {
            return "global::System.Math.Atanh";
        } else if (method == Math.members[18] || method == Math.members[19]) {
            return "global::System.Math.Ceiling";
        } else if (method == Math.members[20] || method == Math.members[21]) {
            return "global::System.Math.Clamp";
        } else if (method == Math.members[22] || method == Math.members[23]) {
            return "global::System.Math.Clamp";
        } else if (method == Math.members[24] || method == Math.members[25]) {
            return "global::System.Math.Cos";
        } else if (method == Math.members[26] || method == Math.members[27]) {
            return "global::System.Math.Cosh";
        } else if (method == Math.members[28] || method == Math.members[29]) {
            return "global::System.Math.Exp";
        } else if (method == Math.members[30] || method == Math.members[31]) {
            return "global::System.Math.Floor";
        } else if (method == Math.members[32] || method == Math.members[33]) {
            return "((Func<double, double, double, double>)((x, y, z) => { return x * (1 - z) + y * z; } ))";
        } else if (method == Math.members[34] || method == Math.members[35]) {
            return "global::System.Math.Log";
        } else if (method == Math.members[36] || method == Math.members[37]) {
            return "global::System.Math.Log";
        } else if (method == Math.members[38] || method == Math.members[39]) {
            return "global::System.Math.Max";
        } else if (method == Math.members[40] || method == Math.members[41]) {
            return "global::System.Math.Max";
        } else if (method == Math.members[42] || method == Math.members[43]) {
            return "global::System.Math.Min";
        } else if (method == Math.members[44] || method == Math.members[45]) {
            return "global::System.Math.Min";
        } else if (method == Math.members[46] || method == Math.members[47]) {
            return "global::System.Math.Pow";
        } else if (method == Math.members[48] || method == Math.members[49]) {
            return "global::System.Math.Round";
        } else if (method == Math.members[50] || method == Math.members[51]) {
            return "global::System.Math.Sin";
        } else if (method == Math.members[52] || method == Math.members[53]) {
            return "global::System.Math.Sinh";
        } else if (method == Math.members[54] || method == Math.members[55]) {
            return "global::System.Math.Sqrt";
        } else if (method == Math.members[56] || method == Math.members[57]) {
            return "global::System.Math.Tan";
        } else if (method == Math.members[58] || method == Math.members[59]) {
            return "global::System.Math.Tanh";
        } else if (method == Math.members[60] || method == Math.members[61]) {
            return "global::System.Math.Truncate";
        }

        return "?";
    }
}
