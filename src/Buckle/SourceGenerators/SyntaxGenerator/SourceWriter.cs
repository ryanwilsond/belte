using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SyntaxGenerator;

/// <summary>
/// Handles writing the syntax source file.
/// </summary>
internal sealed class SourceWriter {
    private readonly TextWriter _writer;
    private readonly IDictionary<string, string> _parentMap;

    private const int IndentSize = 4;
    private int _indentLevel;
    private bool _needIndent = true;

    /// <summary>
    /// Creates a new <see cref="SourceWriter" /> with a deserialized XML tree representation of the syntax XML file.
    /// </summary>
    internal SourceWriter(TextWriter writer, Tree tree) {
        _writer = writer;
        _tree = tree;
        _parentMap = tree.types.ToDictionary(n => n.Name, n => n.Base);
        _parentMap.Add(tree.Root, null);
    }

    private Tree _tree { get; }

    /// <summary>
    /// Writes the green/internal syntax.
    /// </summary>
    public static void WriteInternal(TextWriter writer, Tree tree) => new SourceWriter(writer, tree).WriteInternal();

    /// <summary>
    /// Writes the red syntax.
    /// </summary>
    public static void WriteSyntax(TextWriter writer, Tree tree) => new SourceWriter(writer, tree).WriteSyntax();

    /// <summary>
    /// If the given type name represents any type of node list.
    /// </summary>
    internal static bool IsAnyNodeList(string typeName) {
        return IsNodeList(typeName) || IsSeparatedNodeList(typeName);
    }

    private static bool IsAnyList(string typeName) {
        return IsAnyNodeList(typeName) || typeName == "SyntaxNodeOrTokenList";
    }

    private static bool IsSeparatedNodeList(string typeName) {
        return typeName.StartsWith("SeparatedSyntaxList<", StringComparison.Ordinal);
    }

    private static bool IsNodeList(string typeName) {
        return typeName.StartsWith("SyntaxList<", StringComparison.Ordinal);
    }

    private static bool IsTrue(string val) => val is not null && string.Compare(val, "true", true) == 0;

    private static bool IsOptional(Field f) => IsTrue(f.Optional);

    private static bool IsOverride(Field f) => IsTrue(f.Override);

    private static string OverrideModifier(Field f) => IsOverride(f) ? "override " : "";

    private static string GetElementType(string typeName) {
        if (!typeName.Contains('<'))
            return "";

        var iStart = typeName.IndexOf('<');
        var iEnd = typeName.IndexOf('>', iStart + 1);

        if (iEnd < iStart)
            return "";

        var sub = typeName.Substring(iStart + 1, iEnd - iStart - 1);

        return sub;
    }

    private static string StripPost(string name, string post) {
        return name.EndsWith(post, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - post.Length)
            : name;
    }

    private static string GetFieldType(Field field, bool green) {
        if (IsAnyList(field.Type))
            return green ? "GreenNode" : "SyntaxNode";

        return field.Type;
    }

    private bool IsDerivedOrListOfDerived(string baseType, string derivedType) {
        return IsDerivedType(baseType, derivedType)
            || ((IsNodeList(derivedType) || IsSeparatedNodeList(derivedType))
                && IsDerivedType(baseType, GetElementType(derivedType)));
    }

    private bool IsDerivedType(string typeName, string derivedTypeName) {
        if (typeName == derivedTypeName)
            return true;

        if (derivedTypeName is not null && _parentMap.TryGetValue(derivedTypeName, out var baseType))
            return IsDerivedType(typeName, baseType);

        return false;
    }

    private void Indent() {
        _indentLevel++;
    }

    private void Unindent() {
        if (_indentLevel <= 0)
            throw new InvalidOperationException("Cannot unindent from base level");

        _indentLevel--;
    }

    private void Write(string msg) {
        WriteIndentIfNeeded();
        _writer.Write(msg);
    }

    private void WriteLine() {
        _writer.WriteLine();
        _needIndent = true;
    }

    private void WriteLine(string msg) {
        if (msg != "")
            WriteIndentIfNeeded();

        _writer.WriteLine(msg);
        _needIndent = true;
    }

    private void WriteIndentIfNeeded() {
        if (_needIndent) {
            _writer.Write(new string(' ', _indentLevel * IndentSize));
            _needIndent = false;
        }
    }

    private string CommaJoin(params object[] values) => Join(", ", values);

    private string Join(string separator, params object[] values)
        => string.Join(separator, values.SelectMany(v => (v switch {
            string s => new[] { s },
            IEnumerable<string> ss => ss,
            _ => throw new InvalidOperationException("Join must be passed strings or collections of strings")
        }).Where(s => s != "")));

    private void OpenBlock() {
        WriteLine(" {");
        Indent();
    }

    private void CloseBlock(string extra = "") {
        Unindent();
        WriteLine("}" + extra);
    }

    private bool IsNode(string typeName) {
        return _parentMap.ContainsKey(typeName);
    }

    private bool IsNodeOrNodeList(string typeName) {
        return IsNode(typeName) ||
            IsNodeList(typeName) ||
            IsSeparatedNodeList(typeName) ||
            typeName == "SyntaxNodeOrTokenList";
    }

    private void WriteInternal() {
        WriteFileHeader();
        WriteLine("namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;");

        WriteGreenNodes();
        WriteVisitorT();
        WriteVisitor();
        WriteGreenRewriter();
        WriteGreenFactory();
    }

    private void WriteSyntax() {
        WriteFileHeader();
        WriteLine("namespace Buckle.CodeAnalysis.Syntax;");

        WriteRedNodes();
        WriteVisitorT();
        WriteVisitor();
        WriteRedFactory();
    }

    private void WriteFileHeader() {
        WriteLine("// <auto-generated/>");
        WriteLine();
        WriteLine("using System.Diagnostics;");
        WriteLine("using Buckle.CodeAnalysis.Syntax.InternalSyntax;");
        WriteLine("using Diagnostics;");
        WriteLine();
    }

    private void WriteGreenNodes() {
        var nodes = _tree.types.Where(n => n is not PredefinedNode).ToList();

        foreach (var node in nodes) {
            WriteLine();
            WriteGreenNode(node);
        }
    }

    private void WriteGreenNode(TreeType node) {
        var @base = node.Base == "SyntaxNode" ? "BelteSyntaxNode" : node.Base;

        if (node is AbstractNode abstractNode) {
            Write($"internal abstract partial class {node.Name} : {@base}");
            OpenBlock();
            WriteLine($"internal {node.Name}(SyntaxKind kind)");
            WriteLine("  : base(kind)");
            OpenBlock();

            if (node.Name == "DirectiveTriviaSyntax")
                WriteLine("this._flags |= NodeFlags.ContainsDirectives;");

            CloseBlock();
            WriteLine();
            WriteLine($"internal {node.Name}(SyntaxKind kind, Diagnostic[] diagnostics)");
            WriteLine("  : base(kind, diagnostics)");
            OpenBlock();

            if (node.Name == "DirectiveTriviaSyntax")
                WriteLine("this._flags |= NodeFlags.ContainsDirectives;");

            CloseBlock();

            var nodeFields = GetNodeOrNodeListFields(abstractNode);

            foreach (var field in nodeFields) {
                WriteLine();
                WriteLine($"internal abstract {field.Type} {field.Name} {{ get; }}");
            }

            CloseBlock();
        } else if (node is Node nd) {
            Write($"internal sealed partial class {node.Name} : {@base}");
            OpenBlock();

            var valueFields = nd.fields.Where(n => !IsNodeOrNodeList(n.Type)).ToList();
            var nodeFields = nd.fields.Where(n => IsNodeOrNodeList(n.Type)).ToList();

            foreach (var field in nodeFields) {
                var type = GetFieldType(field, green: true);
                WriteLine($"internal readonly {type} _{field.Name};");
            }

            foreach (var field in valueFields)
                WriteLine($"internal readonly {field.Type} _{field.Name};");

            WriteLine();
            Write($"internal {node.Name}(");
            WriteGreenNodeConstructorArgs(nodeFields, valueFields);
            WriteLine(")");
            Write($"  : base(SyntaxKind.{nd.kinds.Single().Name})");
            OpenBlock();
            WriteCtorBody(nodeFields, valueFields);
            CloseBlock();

            WriteLine();
            Write($"internal {node.Name}(");
            WriteGreenNodeConstructorArgs(nodeFields, valueFields);
            var secondConstructorParameterListPrefix = (nodeFields.Count > 0 || valueFields.Count > 0) ? ", " : "";
            WriteLine($"{secondConstructorParameterListPrefix}Diagnostic[] diagnostics)");
            Write($"  : base(SyntaxKind.{nd.kinds.Single().Name}, diagnostics)");
            OpenBlock();
            WriteCtorBody(nodeFields, valueFields);
            CloseBlock();

            foreach (var field in nodeFields) {
                WriteLine();

                if (IsNodeList(field.Type)) {
                    WriteLine(
                        $"internal {OverrideModifier(field)}InternalSyntax.{field.Type} {field.Name} => new " +
                        $"InternalSyntax.{field.Type}(this._{field.Name});"
                    );
                } else if (IsSeparatedNodeList(field.Type)) {
                    WriteLine(
                        $"internal {OverrideModifier(field)}InternalSyntax.{field.Type} {field.Name} => new Internal" +
                        $"Syntax.{field.Type}(new InternalSyntax.SyntaxList<BelteSyntaxNode>(this._{field.Name}));"
                    );
                } else if (field.Type == "SyntaxNodeOrTokenList") {
                    WriteLine(
                        $"internal {OverrideModifier(field)}InternalSyntax.SyntaxList<BelteSyntaxNode> {field.Name}" +
                        $" => new InternalSyntax.SyntaxList<BelteSyntaxNode>(this._{field.Name});"
                    );
                } else {
                    WriteLine(
                        $"internal {OverrideModifier(field)}{(GetFieldType(field, green: true))} {field.Name} " +
                        $"=> this._{field.Name};"
                    );
                }
            }

            foreach (var field in valueFields) {
                WriteLine();
                WriteLine($"internal {OverrideModifier(field)}{field.Type} {field.Name} => this._{field.Name};");
            }

            WriteLine();

            // WriteGetSlotMethod
            {
                Write("internal override GreenNode GetSlot(int index)");

                if (nodeFields.Count == 0) {
                    WriteLine(" => null;");
                } else if (nodeFields.Count == 1) {
                    WriteLine($" => index == 0 ? this._{nodeFields[0].Name} : null;");
                } else {
                    Write(" => index switch");
                    OpenBlock();

                    for (int i = 0, n = nodeFields.Count; i < n; i++) {
                        var field = nodeFields[i];
                        WriteLine($"{i} => this._{field.Name},");
                    }

                    WriteLine("_ => null,");
                    CloseBlock(";");
                }
            }

            WriteLine();
            WriteLine(
                $"internal override SyntaxNode CreateRed(SyntaxNode parent, int position) => new Syntax." +
                $"{node.Name}(parent, this, position);"
            );
            WriteLine();
            WriteLine(
                $"internal override void Accept(SyntaxVisitor visitor) => visitor.Visit" +
                $"{StripPost(node.Name, "Syntax")}(this);"
            );
            WriteLine();
            WriteLine(
                $"internal override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => " +
                $"visitor.Visit{StripPost(node.Name, "Syntax")}(this);"
            );

            WriteGreenUpdateMethod(nd);
            WriteGreenSetDiagnosticsMethod(nd);

            CloseBlock();
        }
    }

    private void WriteGreenNodeConstructorArgs(List<Field> nodeFields, List<Field> valueFields) {
        var first = true;

        foreach (var field in nodeFields) {
            Write($"{(first ? "" : ", ")}{(GetFieldType(field, green: true))} {field.Name}");

            if (first)
                first = false;
        }

        foreach (var field in valueFields) {
            Write($"{(first ? "" : ", ")}{field.Type} {field.Name}");

            if (first)
                first = false;
        }
    }

    private void WriteCtorBody(List<Field> nodeFields, List<Field> valueFields) {
        WriteLine($"this.slotCount = {nodeFields.Count};");

        foreach (var field in nodeFields) {
            if (IsAnyList(field.Type) || IsOptional(field)) {
                Write($"if ({field.Name} is not null)");
                OpenBlock();

                // TODO Support multiple kinds
                if (field.kinds.Count == 1) {
                    WriteLine(
                        $"Debug.Assert({field.Name}.kind == SyntaxKind.{field.kinds.Single().Name}, $\"incorrect syn" +
                        $"tax kind '{{{field.Name}.kind}}', expected '{{SyntaxKind.{field.kinds.Single().Name}}}'\");"
                    );
                }

                WriteLine($"this.AdjustFlagsAndWidth({field.Name});");
                WriteLine($"this._{field.Name} = {field.Name};");
                CloseBlock();
            } else {
                if (field.kinds.Count == 1) {
                    WriteLine(
                        $"Debug.Assert({field.Name}.kind == SyntaxKind.{field.kinds.Single().Name}, $\"incorrect syn" +
                        $"tax kind '{{{field.Name}.kind}}', expected '{{SyntaxKind.{field.kinds.Single().Name}}}'\");"
                    );
                }

                WriteLine($"this.AdjustFlagsAndWidth({field.Name});");
                WriteLine($"this._{field.Name} = {field.Name};");
            }
        }

        foreach (var field in valueFields)
            WriteLine($"this._{field.Name} = {field.Name};");
    }

    private void WriteGreenUpdateMethod(Node node) {
        WriteLine();
        Write($"internal {node.Name} Update(");
        Write(CommaJoin(node.fields.Select(f => {
            var type =
                f.Type == "SyntaxNodeOrTokenList" ? "InternalSyntax.SyntaxList<BelteSyntaxNode>" :
                f.Type == "SyntaxTokenList" ? "InternalSyntax.SyntaxList<SyntaxToken>" :
                IsNodeList(f.Type) ? "InternalSyntax." + f.Type :
                IsSeparatedNodeList(f.Type) ? "InternalSyntax." + f.Type :
                f.Type;

            return $"{type} _{f.Name}";
        })));
        Write(")");
        OpenBlock();

        if (node.fields.Count > 0) {
            Write("if (");
            var nCompared = 0;

            foreach (var field in node.fields) {
                if (IsDerivedOrListOfDerived("SyntaxNode", field.Type) ||
                    IsDerivedOrListOfDerived("SyntaxToken", field.Type) ||
                    field.Type == "SyntaxNodeOrTokenList") {
                    if (nCompared > 0)
                        Write(" || ");

                    Write($"_{field.Name} != this.{field.Name}");
                    nCompared++;
                }
            }

            if (nCompared > 0) {
                Write(")");
                OpenBlock();
                Write($"var newNode = SyntaxFactory.{StripPost(node.Name, "Syntax")}(");
                Write(CommaJoin(node.fields.Select(f => $"_{f.Name}")));
                WriteLine(");");
                WriteLine("var diags = GetDiagnostics();");
                WriteLine("if (diags?.Length > 0)");
                WriteLine("    newNode = newNode.WithDiagnosticsGreen(diags);");
                WriteLine("return newNode;");
                CloseBlock();
            }

            WriteLine();
        }

        WriteLine("return this;");
        CloseBlock();
    }

    private void WriteGreenSetDiagnosticsMethod(Node node) {
        WriteLine();
        WriteLine("internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics)");
        Indent();
        Write($"=> new {node.Name}(");
        Write(CommaJoin(
            node.fields.Select(f => $"this._{f.Name}"),
            "diagnostics"));
        WriteLine(");");
        Unindent();
    }

    private void WriteVisitorT() {
        var nodes = _tree.types.Where(n => n is Node).ToList();

        WriteLine();
        Write("internal partial class SyntaxVisitor<TResult>");
        OpenBlock();

        foreach (var node in nodes) {
            WriteLine(
                $"internal virtual TResult Visit{StripPost(node.Name, "Syntax")}({node.Name} node) " +
                $"=> DefaultVisit(node);"
            );
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteVisitor() {
        var nodes = _tree.types.Where(n => n is Node).ToList();

        WriteLine();
        Write("internal partial class SyntaxVisitor");
        OpenBlock();

        foreach (var node in nodes) {
            WriteLine(
                $"internal virtual void Visit{StripPost(node.Name, "Syntax")}({node.Name} node) => DefaultVisit(node);"
            );
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteGreenRewriter() {
        var nodes = _tree.types.Where(n => n is Node).ToList();

        WriteLine();
        Write("internal partial class SyntaxRewriter : SyntaxVisitor<BelteSyntaxNode>");
        OpenBlock();

        foreach (var node in nodes) {
            var nd = node as Node;
            WriteLine($"internal override BelteSyntaxNode Visit{StripPost(node.Name, "Syntax")}({node.Name} node)");
            Indent();
            Write("=> node.Update(");
            Write(CommaJoin(nd.fields.Select(f => IsAnyNodeList(f.Type)
                    ? $"VisitList(node.{f.Name})"
                    : $"({f.Type})Visit(node.{f.Name})"
                )));
            WriteLine(");");
            Unindent();
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteGreenFactory() {
        var nodes = _tree.types.Where(n => n is Node).ToList();

        WriteLine();
        Write("internal static partial class SyntaxFactory");
        OpenBlock();

        foreach (var node in nodes) {
            WriteGreenFactoryMethods(node as Node);
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteGreenFactoryMethods(Node node) {
        var allArguments = CommaJoin(node.fields.Select(f => $"{f.Type} {f.Name}"));
        var requiredArguments = CommaJoin(node.fields.Where(f => !IsOptional(f)).Select(f => $"{f.Type} {f.Name}"));
        var allParameters = CommaJoin(node.fields.Select(f => IsAnyNodeList(f.Type) ? $"{f.Name}?.node" : f.Name));
        var requiredParameters = CommaJoin(
            node.fields.Select(f => IsOptional(f) ? "null" : IsAnyNodeList(f.Type) ? $"{f.Name}?.node" : f.Name)
        );

        WriteLine($"internal static {node.Name} {StripPost(node.Name, "Syntax")}({allArguments})");
        Indent();
        WriteLine($"=> new {node.Name}({allParameters});");
        Unindent();

        if (allArguments != requiredArguments) {
            WriteLine();
            WriteLine($"internal static {node.Name} {StripPost(node.Name, "Syntax")}({requiredArguments})");
            Indent();
            WriteLine($"=> new {node.Name}({requiredParameters});");
            Unindent();
        }
    }

    private void WriteRedNodes() {
        var nodes = _tree.types.Where(n => n is not PredefinedNode).ToList();

        foreach (var node in nodes) {
            WriteLine();
            WriteRedNode(node);
        }
    }

    private void WriteRedNode(TreeType node) {
        var @base = node.Base == "SyntaxNode" ? "BelteSyntaxNode" : node.Base;

        if (node is AbstractNode abstractNode) {
            Write($"public abstract partial class {node.Name} : {@base}");
            OpenBlock();
            WriteLine($"internal {node.Name}(SyntaxNode parent, GreenNode green, int position)");
            WriteLine("  : base(parent, green, position) { }");

            var nodeFields = GetNodeOrNodeListFields(abstractNode);

            foreach (var field in nodeFields) {
                var fieldType = GetRedFieldType(field);
                WriteLine();
                WriteLine($"public abstract {fieldType} {field.Name} {{ get; }}");
            }

            CloseBlock();
        } else if (node is Node nd) {
            Write($"public sealed partial class {node.Name} : {@base}");
            OpenBlock();

            var valueFields = nd.fields.Where(n => !IsNodeOrNodeList(n.Type)).ToList();
            var nodeFields = GetNodeOrNodeListFields(nd);

            foreach (var field in nodeFields) {
                if (field.Type is not "SyntaxToken" and not "SyntaxList<SyntaxToken>") {
                    if (IsSeparatedNodeList(field.Type) || field.Type == "SyntaxNodeOrTokenList")
                        WriteLine($"private SyntaxNode _{field.Name};");
                    else
                        WriteLine($"private {GetFieldType(field, green: false)} _{field.Name};");
                }
            }

            WriteLine();
            WriteLine($"internal {node.Name}(SyntaxNode parent, InternalSyntax.BelteSyntaxNode green, int position)");
            WriteLine("  : base(parent, green, position) { }");
            WriteLine();

            for (int i = 0, n = nodeFields.Count; i < n; i++) {
                var field = nodeFields[i];

                if (field.Type == "SyntaxToken") {
                    Write($"public {OverrideModifier(field)}{GetRedFieldType(field)} {field.Name}");

                    if (IsOptional(field)) {
                        OpenBlock();
                        Write("get");
                        OpenBlock();
                        WriteLine($"var slot = ((Syntax.InternalSyntax.{node.Name})this.green)._{field.Name};");
                        WriteLine(
                            $"return slot is not null ? new SyntaxToken(this, slot," +
                            $" {GetChildPosition(i)}, {GetChildIndex(i)}) : null;"
                        );
                        CloseBlock();
                        CloseBlock();
                    } else {
                        WriteLine(
                            $" => new SyntaxToken(this, ((Syntax.InternalSyntax.{node.Name})this.green)._{field.Name}" +
                            $", {GetChildPosition(i)}, {GetChildIndex(i)});"
                        );
                    }
                } else if (field.Type == "SyntaxList<SyntaxToken>") {
                    Write($"public {OverrideModifier(field)}SyntaxTokenList {field.Name}");
                    OpenBlock();
                    Write("get");
                    OpenBlock();
                    WriteLine($"var slot = this.green.GetSlot({i});");
                    WriteLine(
                        $"return slot is not null ? new SyntaxTokenList(this, slot, {GetChildPosition(i)}, " +
                        $"{GetChildIndex(i)}) : null;"
                    );
                    CloseBlock();
                    CloseBlock();
                } else {
                    Write($"public {OverrideModifier(field)}{GetRedFieldType(field)} {field.Name}");

                    if (IsNodeList(field.Type)) {
                        WriteLine($" => new {field.Type}(GetRed(ref this._{field.Name}, {i}));");
                    } else if (IsSeparatedNodeList(field.Type)) {
                        WriteLine($" => new {field.Type}(GetRed(ref this._{field.Name}, {i}), {GetChildIndex(i)});");
                    } else if (field.Type == "SyntaxNodeOrTokenList") {
                        throw new InvalidOperationException("field cannot be a random SyntaxNodeOrTokenList");
                    } else {
                        if (i == 0)
                            WriteLine($" => GetRedAtZero(ref this._{field.Name});");
                        else
                            WriteLine($" => GetRed(ref this._{field.Name}, {i});");
                    }
                }

                WriteLine();
            }

            foreach (var field in valueFields) {
                WriteLine(
                    $"public {OverrideModifier(field)}{field.Type} {field.Name} => " +
                    $"((Syntax.InternalSyntax.{node.Name})this.green).{field.Name};"
                );
                WriteLine();
            }

            WriteLine(
                $"internal override void Accept(SyntaxVisitor visitor) => visitor.Visit" +
                $"{StripPost(node.Name, "Syntax")}(this);"
            );
            WriteLine();
            WriteLine(
                $"internal override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => " +
                $"visitor.Visit{StripPost(node.Name, "Syntax")}(this);"
            );
            WriteLine();

            // WriteGetNodeSlotMethod
            {
                Write("internal override SyntaxNode GetNodeSlot(int index) ");

                var relevantNodes = nodeFields.Select((field, index) => (field, index))
                    .Where(t => t.field.Type is not "SyntaxToken" and not "SyntaxList<SyntaxToken>");

                if (!relevantNodes.Any()) {
                    WriteLine("=> null;");
                } else if (relevantNodes.Count() == 1) {
                    var (field, index) = relevantNodes.Single();
                    var whenTrue = index == 0
                        ? $"GetRedAtZero(ref this._{field.Name})"
                        : $"GetRed(ref this._{field.Name}, {index})";

                    WriteLine($"=> index == {index} ? {whenTrue} : null;");
                } else {
                    Write("=> index switch");
                    OpenBlock();

                    foreach (var (field, index) in relevantNodes) {
                        if (index == 0)
                            WriteLine($"{index} => GetRedAtZero(ref this._{field.Name}),");
                        else
                            WriteLine($"{index} => GetRed(ref this._{field.Name}, {index}),");
                    }

                    WriteLine("_ => null,");
                    CloseBlock(";");
                }
            }

            WriteLine();

            // WriteGetCachedSlotMethod
            {
                Write($"internal override SyntaxNode GetCachedSlot(int index) ");

                var relevantNodes = nodeFields.Select((field, index) => (field, index))
                    .Where(t => t.field.Type is not "SyntaxToken" and not "SyntaxList<SyntaxToken>");

                if (!relevantNodes.Any()) {
                    WriteLine("=> null;");
                } else if (relevantNodes.Count() == 1) {
                    var (field, index) = relevantNodes.Single();
                    WriteLine($"=> index == {index} ? this._{field.Name} : null;");
                } else {
                    Write("=> index switch");
                    OpenBlock();

                    foreach (var (field, index) in relevantNodes)
                        WriteLine($"{index} => this._{field.Name},");

                    WriteLine("_ => null,");
                    CloseBlock(";");
                }
            }

            CloseBlock();
        }
    }

    private string GetRedFieldType(Field field)
        => field.Type == "SyntaxList<SyntaxToken>" ? "SyntaxTokenList" : field.Type;

    private List<Field> GetNodeOrNodeListFields(TreeType node)
        => node is AbstractNode an
            ? an.fields.Where(n => IsNodeOrNodeList(n.Type)).ToList()

            : node is Node nd
                ? nd.fields.Where(n => IsNodeOrNodeList(n.Type)).ToList()

                : new List<Field>();

    private string GetChildPosition(int i) => i == 0 ? "this.position" : $"GetChildPosition({i})";

    private string GetChildIndex(int i) => i == 0 ? "0" : $"GetChildIndex({i})";

    private void WriteRedFactory() {
        WriteLine();
        Write("public static partial class SyntaxFactory");
        OpenBlock();

        var nodes = _tree.types.Where(n => n is Node).ToList();

        foreach (var node in nodes) {
            WriteRedFactoryMethods(node as Node);
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteRedFactoryMethods(Node node) {
        var allArguments = CommaJoin(node.fields.Select(f => $"{GetRedFieldType(f)} {f.Name}"));
        var requiredArguments = CommaJoin(
            node.fields.Where(f => !IsOptional(f)).Select(f => $"{GetRedFieldType(f)} {f.Name}")
        );
        var allParameters = CommaJoin(node.fields.Select(f =>
            IsNodeList(f.Type)
                ? $"{f.Name}?.node?.ToGreenList<Syntax.InternalSyntax.{GetElementType(f.Type)}>()" :
            IsSeparatedNodeList(f.Type)
                ? $"{f.Name}.node.ToGreenSeparatedList<Syntax.InternalSyntax.{GetElementType(f.Type)}>()" :
            (!IsNode(f.Type) || f.Type == "SyntaxToken")
                ? $"(Syntax.InternalSyntax.{f.Type}){f.Name}.node" :
            $"(Syntax.InternalSyntax.{f.Type}){f.Name}.green"
        ));
        var requiredParameters = CommaJoin(node.fields.Select(f => IsOptional(f) ? "null" :
            IsNodeList(f.Type)
                ? $"{f.Name}?.node?.ToGreenList<Syntax.InternalSyntax.{GetElementType(f.Type)}>()" :
            IsSeparatedNodeList(f.Type)
                ? $"{f.Name}.node.ToGreenSeparatedList<Syntax.InternalSyntax.{GetElementType(f.Type)}>()" :
            (!IsNode(f.Type) || f.Type == "SyntaxToken")
                ? $"(Syntax.InternalSyntax.{f.Type}){f.Name}.node" :
            $"(Syntax.InternalSyntax.{f.Type}){f.Name}.green"
        ));

        var fullDeclaration = $"public static {node.Name} {StripPost(node.Name, "Syntax")}({allArguments}";
        var fullBody = $"=> ({node.Name})Syntax.InternalSyntax.SyntaxFactory." +
            $"{StripPost(node.Name, "Syntax")}({allParameters}).CreateRed(";

        WriteLine($"{fullDeclaration})");
        Indent();
        WriteLine($"{fullBody});");
        Unindent();
        WriteLine();

        if (allArguments == "")
            WriteLine($"{fullDeclaration}SyntaxNode parent, int position)");
        else
            WriteLine($"{fullDeclaration}, SyntaxNode parent, int position)");

        Indent();
        WriteLine($"{fullBody}parent, position);");
        Unindent();

        if (allArguments != requiredArguments) {
            var requiredDeclaration =
                $"public static {node.Name} {StripPost(node.Name, "Syntax")}({requiredArguments}";
            var requiredBody = $"=> ({node.Name})Syntax.InternalSyntax.SyntaxFactory." +
                $"{StripPost(node.Name, "Syntax")}({requiredParameters}).CreateRed(";

            WriteLine();
            WriteLine($"{requiredDeclaration})");
            Indent();
            WriteLine($"{requiredBody});");
            Unindent();
            WriteLine();

            if (requiredArguments == "")
                WriteLine($"{requiredDeclaration}SyntaxNode parent, int position)");
            else
                WriteLine($"{requiredDeclaration}, SyntaxNode parent, int position)");

            Indent();
            WriteLine($"{requiredBody}parent, position);");
            Unindent();
        }
    }
}
