using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.CodeAnalysis.Binding.BoundFactory;
using static Buckle.Utilities.LibraryUtilities;

namespace Buckle.Libraries.Graphics;

internal static partial class GraphicsLibrary {
    internal static (Symbol, BoundBlockStatement)[] InstanceMethods = {
        (Vec2.members[2], Block(
            FieldInitializer(
                Vec2,
                Vec2.members[0] as FieldSymbol,
                (Vec2.members[2] as MethodSymbol).parameters[0]
            ),
            FieldInitializer(
                Vec2,
                Vec2.members[1] as FieldSymbol,
                (Vec2.members[2] as MethodSymbol).parameters[1]
            )
        ))
    };

    internal static object EvaluateMethod(MethodSymbol method, object[] arguments) {
        // TODO
        return null;
    }

    internal static string EmitCSharpMethod(MethodSymbol method) {
        // TODO
        return null;
    }
}
