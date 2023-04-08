using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;
using static Buckle.Utilities.MethodUtilities;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// Extensions on the <see cref="Compilation" /> class, adding the ability to emit the tree to a display.
/// </summary>
public static class CompilationExtensions {
    /// <summary>
    /// Emits the parse tree of the compilation.
    /// </summary>
    /// <param name="text">Out.</param>
    public static void EmitTree(this Compilation self, DisplayText text) {
        if (self.globalScope.entryPoint != null) {
            EmitTree(self, self.globalScope.entryPoint, text);
        } else {
            var program = self.GetProgram();

            foreach (var pair in program.methodBodies.OrderBy(p => p.Key.name))
                EmitTree(self, pair.Key, text);
        }
    }

    /// <summary>
    /// Emits the parse tree of a single <see cref="MethodSymbol" /> after attempting to find it based on name.
    /// Note: this only searches for methods, so if the name of another type of <see cref="Symbol" /> is passed it
    /// will not be found.
    /// </summary>
    /// <param name="name">
    /// The name of the <see cref="MethodSymbol" /> to search for and then print. If not found, throws.
    /// </param>
    /// <param name="text">Out.</param>
    public static void EmitTree(this Compilation self, string name, DisplayText text) {
        var program = self.GetProgram();
        var pair = LookupMethodFromParentsFromName(program, name);
        SymbolDisplay.DisplaySymbol(text, pair.Item1);
        text.Write(CreateSpace());
        DisplayText.DisplayNode(text, pair.Item2);
    }

    /// <summary>
    /// Emits the parse tree of a single <see cref="Symbol" />.
    /// </summary>
    /// <param name="symbol"><see cref="Symbol" /> to be the root of the <see cref="SyntaxTree" /> displayed.</param>
    /// <param name="text">Out.</param>
    public static void EmitTree(this Compilation self, ISymbol symbol, DisplayText text) {
        var program = self.GetProgram();

        void WriteTypeMembers(ITypeSymbolWithMembers type, bool writeEnding = true) {
            try {
                var members = ImmutableList<Symbol>.Empty;

                if (type is StructSymbol s)
                    members = program.structMembers[s];
                else if (type is ClassSymbol c)
                    members = program.classMembers[c];

                text.Write(CreateSpace());
                text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
                text.Write(CreateLine());
                text.indent++;

                foreach (var field in members.OfType<FieldSymbol>()) {
                    SymbolDisplay.DisplaySymbol(text, field);
                    text.Write(CreateLine());
                }

                text.Write(CreateLine());

                foreach (var method in members.OfType<MethodSymbol>()) {
                    SymbolDisplay.DisplaySymbol(text, method);
                    text.Write(CreateLine());
                }

                text.Write(CreateLine());

                foreach (var typeMember in members.OfType<TypeSymbol>()) {
                    SymbolDisplay.DisplaySymbol(text, typeMember);
                    text.Write(CreateLine());
                }

                text.indent--;
                text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
                text.Write(CreateLine());
            } catch (BelteInternalException) {
                if (writeEnding) {
                    text.Write(CreatePunctuation(SyntaxKind.SemicolonToken));
                    text.Write(CreateLine());
                }
            }
        }

        if (symbol is MethodSymbol f) {
            SymbolDisplay.DisplaySymbol(text, f);

            try {
                var body = LookupMethodFromParents(program, f);
                text.Write(CreateSpace());
                DisplayText.DisplayNode(text, body);
            } catch (BelteInternalException) {
                // If the body could not be found, it probably means it is a builtin
                // In that case only showing the signature is what we want
                text.Write(CreatePunctuation(SyntaxKind.SemicolonToken));
                text.Write(CreateLine());
            }
        } else if (symbol is ITypeSymbolWithMembers t) {
            SymbolDisplay.DisplaySymbol(text, symbol);
            WriteTypeMembers(t);
        } else if (symbol is VariableSymbol v) {
            SymbolDisplay.DisplaySymbol(text, v);

            if (v.type.typeSymbol is ITypeSymbolWithMembers s && v.type.dimensions == 0)
                WriteTypeMembers(s);
            else
                text.Write(CreateLine());
        }
    }
}
