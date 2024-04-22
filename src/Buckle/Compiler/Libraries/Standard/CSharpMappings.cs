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
        return [Console, Math];
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
                System.Console.WriteLine();
        } else if (method == Console.members[4]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.Write(arguments[0]);
        } else if (method == Console.members[5]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.Write(arguments[0]);
        } else if (method == Console.members[6]) {
            if (!System.Console.IsInputRedirected)
                return System.Console.ReadLine();
        } else if (method == Console.members[7]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.ForegroundColor = (ConsoleColor)arguments[0];
        } else if (method == Console.members[8]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.BackgroundColor = (ConsoleColor)arguments[0];
        } else if (method == Console.members[9]) {
            if (!System.Console.IsOutputRedirected)
                System.Console.ResetColor();
        } else if (method == Math.members[2]) {
            return arguments[0] is null ? null : System.Math.Abs(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[3]) {
            return arguments[0] is null ? null : System.Math.Abs((int)arguments[0]); ;
        } else if (method == Math.members[4]) {
            return arguments[0] is null ? null : System.Math.Acos(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[5]) {
            return arguments[0] is null ? null : System.Math.Acosh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[6]) {
            return arguments[0] is null ? null : System.Math.Asin(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[7]) {
            return arguments[0] is null ? null : System.Math.Asinh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[8]) {
            return arguments[0] is null ? null : System.Math.Atan(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[9]) {
            return arguments[0] is null ? null : System.Math.Atanh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[10]) {
            return arguments[0] is null ? null : System.Math.Ceiling(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[11]) {
            return arguments[0] is null || arguments[0] is null || arguments[0] is null ? null :
                System.Math.Clamp(
                    Convert.ToDouble(arguments[0]),
                    Convert.ToDouble(arguments[1]),
                    Convert.ToDouble(arguments[2])
                );
        } else if (method == Math.members[12]) {
            return arguments[0] is null || arguments[0] is null || arguments[0] is null ? null :
                System.Math.Clamp(
                    (int)arguments[0],
                    (int)arguments[1],
                    (int)arguments[2]
                );
        } else if (method == Math.members[13]) {
            return arguments[0] is null ? null : System.Math.Cos(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[14]) {
            return arguments[0] is null ? null : System.Math.Cosh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[15]) {
            return arguments[0] is null ? null : System.Math.Exp(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[16]) {
            return arguments[0] is null ? null : System.Math.Floor(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[17]) {
            if (arguments[0] is null || arguments[1] is null || arguments[2] is null)
                return null;

            var rate = Convert.ToDouble(arguments[2]);
            return Convert.ToDouble(arguments[0]) * (1 - rate) + Convert.ToDouble(arguments[1]) * rate;
        } else if (method == Math.members[18]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Log(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[19]) {
            return arguments[0] is null ? null : System.Math.Log(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[20]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Max(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[21]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Max((int)arguments[0], (int)arguments[1]);
        } else if (method == Math.members[22]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Min(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[23]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Min((int)arguments[0], (int)arguments[1]);
        } else if (method == Math.members[24]) {
            return arguments[0] is null || arguments[1] is null ? null :
                System.Math.Pow(Convert.ToDouble(arguments[0]), Convert.ToDouble(arguments[1]));
        } else if (method == Math.members[25]) {
            return arguments[0] is null ? null : System.Math.Round(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[26]) {
            return arguments[0] is null ? null : System.Math.Sin(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[27]) {
            return arguments[0] is null ? null : System.Math.Sinh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[28]) {
            return arguments[0] is null ? null : System.Math.Sqrt(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[29]) {
            return arguments[0] is null ? null : System.Math.Tan(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[30]) {
            return arguments[0] is null ? null : System.Math.Tanh(Convert.ToDouble(arguments[0]));
        } else if (method == Math.members[31]) {
            return arguments[0] is null ? null : System.Math.Truncate(Convert.ToDouble(arguments[0]));
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
        } else if (method == Math.members[2]) {
            return "global::System.Math.Abs";
        } else if (method == Math.members[3]) {
            return "global::System.Math.Abs";
        } else if (method == Math.members[4]) {
            return "global::System.Math.Acos";
        } else if (method == Math.members[5]) {
            return "global::System.Math.Acosh";
        } else if (method == Math.members[6]) {
            return "global::System.Math.Asin";
        } else if (method == Math.members[7]) {
            return "global::System.Math.Asinh";
        } else if (method == Math.members[8]) {
            return "global::System.Math.Atan";
        } else if (method == Math.members[9]) {
            return "global::System.Math.Atanh";
        } else if (method == Math.members[10]) {
            return "global::System.Math.Ceiling";
        } else if (method == Math.members[11]) {
            return "global::System.Math.Clamp";
        } else if (method == Math.members[12]) {
            return "global::System.Math.Clamp";
        } else if (method == Math.members[13]) {
            return "global::System.Math.Cos";
        } else if (method == Math.members[14]) {
            return "global::System.Math.Cosh";
        } else if (method == Math.members[15]) {
            return "global::System.Math.Exp";
        } else if (method == Math.members[16]) {
            return "global::System.Math.Floor";
        } else if (method == Math.members[17]) {
            return "((Func<double, double, double, double>)((x, y, z) => { return x * (1 - z) + y * z; } ))";
        } else if (method == Math.members[18]) {
            return "global::System.Math.Log";
        } else if (method == Math.members[19]) {
            return "global::System.Math.Log";
        } else if (method == Math.members[20]) {
            return "global::System.Math.Max";
        } else if (method == Math.members[21]) {
            return "global::System.Math.Max";
        } else if (method == Math.members[22]) {
            return "global::System.Math.Min";
        } else if (method == Math.members[23]) {
            return "global::System.Math.Min";
        } else if (method == Math.members[24]) {
            return "global::System.Math.Pow";
        } else if (method == Math.members[25]) {
            return "global::System.Math.Round";
        } else if (method == Math.members[26]) {
            return "global::System.Math.Sin";
        } else if (method == Math.members[27]) {
            return "global::System.Math.Sinh";
        } else if (method == Math.members[28]) {
            return "global::System.Math.Sqrt";
        } else if (method == Math.members[29]) {
            return "global::System.Math.Tan";
        } else if (method == Math.members[30]) {
            return "global::System.Math.Tanh";
        } else if (method == Math.members[31]) {
            return "global::System.Math.Truncate";
        }

        return "?";
    }
}
