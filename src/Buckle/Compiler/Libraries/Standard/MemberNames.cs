using System.Linq;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.Libraries.Standard;

internal static partial class StandardLibrary {
    internal static class ObjectMembers {
        internal static MethodSymbol Constructor => Object.members[0] as MethodSymbol;
        internal static new MethodSymbol ToString => Object.members[1] as MethodSymbol;
        internal static new MethodSymbol Equals => Object.members.ElementAtOrDefault(2) as MethodSymbol;
        internal static new MethodSymbol ReferenceEquals => Object.members.ElementAtOrDefault(3) as MethodSymbol;
        internal static MethodSymbol op_Equality => Object.members.ElementAtOrDefault(4) as MethodSymbol;
        internal static MethodSymbol op_Inequality => Object.members.ElementAtOrDefault(5) as MethodSymbol;
    }

    internal static class ConsoleMembers {
        internal static ClassSymbol Color => Console.members[0] as ClassSymbol;
        internal static MethodSymbol GetWidth => Console.members[1] as MethodSymbol;
        internal static MethodSymbol GetHeight => Console.members[2] as MethodSymbol;
        internal static MethodSymbol Input => Console.members[3] as MethodSymbol;
        internal static MethodSymbol PrintLine_String => Console.members[4] as MethodSymbol;
        internal static MethodSymbol PrintLine_Any => Console.members[5] as MethodSymbol;
        internal static MethodSymbol PrintLine_Object => Console.members[6] as MethodSymbol;
        internal static MethodSymbol PrintLine => Console.members[7] as MethodSymbol;
        internal static MethodSymbol Print_String => Console.members[8] as MethodSymbol;
        internal static MethodSymbol Print_Any => Console.members[9] as MethodSymbol;
        internal static MethodSymbol Print_Object => Console.members[10] as MethodSymbol;
        internal static MethodSymbol ResetColor => Console.members[11] as MethodSymbol;
        internal static MethodSymbol SetForegroundColor => Console.members[12] as MethodSymbol;
        internal static MethodSymbol SetBackgroundColor => Console.members[13] as MethodSymbol;
        internal static MethodSymbol SetCursorPosition => Console.members[14] as MethodSymbol;
    }

    internal static class DirectoryMembers {
        internal static MethodSymbol Create => Directory.members[0] as MethodSymbol;
        internal static MethodSymbol Delete => Directory.members[1] as MethodSymbol;
        internal static MethodSymbol Exists => Directory.members[2] as MethodSymbol;
        internal static MethodSymbol GetCurrentDirectory => Directory.members[3] as MethodSymbol;
        internal static MethodSymbol GetDirectories => Directory.members.ElementAtOrDefault(4) as MethodSymbol;
        internal static MethodSymbol GetFiles => Directory.members.ElementAtOrDefault(5) as MethodSymbol;
    }

    internal static class FileMembers {
        internal static MethodSymbol AppendText => File.members[0] as MethodSymbol;
        internal static MethodSymbol Create => File.members[1] as MethodSymbol;
        internal static MethodSymbol Copy => File.members[2] as MethodSymbol;
        internal static MethodSymbol Delete => File.members[3] as MethodSymbol;
        internal static MethodSymbol Exists => File.members[4] as MethodSymbol;
        internal static MethodSymbol ReadText => File.members[5] as MethodSymbol;
        internal static MethodSymbol WriteText => File.members[6] as MethodSymbol;
        internal static MethodSymbol AppendLines => File.members.ElementAtOrDefault(7) as MethodSymbol;
        internal static MethodSymbol ReadLines => File.members.ElementAtOrDefault(8) as MethodSymbol;
        internal static MethodSymbol WriteLines => File.members.ElementAtOrDefault(9) as MethodSymbol;
    }
}
