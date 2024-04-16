using System;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
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

    internal static string EmitCSharpMethod(MethodSymbol method) {
        // TODO
        return null;
    }
}
