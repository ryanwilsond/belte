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
    private readonly Tree _tree;
    private readonly IDictionary<string, string> _parentMap;
    private readonly ILookup<string, string> _childMap;

    private const int IndentSize = 4;
    private int _indentLevel;
    private bool _needIndent = true;

    /// <summary>
    /// Creates a new <see cref="SourceWriter" /> with a deserialized XML tree representation of the syntax XML file.
    /// </summary>
    internal SourceWriter(TextWriter writer, Tree tree) {
        _writer = writer;
        _tree = tree;
        _parentMap = tree.types.ToDictionary(n => n.name, n => n.@base);
        _parentMap.Add(tree.root, null);
        _childMap = tree.types.ToLookup(n => n.@base, n => n.name);
    }

    private Tree tree { get { return _tree; } }

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

    private static bool IsTrue(string val) => val != null && string.Compare(val, "true", true) == 0;

    private static bool IsOptional(Field f) => IsTrue(f.optional);

    private static bool IsOverride(Field f) => IsTrue(f.@override);

    private static string OverrideModifier(Field f) => IsOverride(f) ? "override " : "";

    private static string GetElementType(string typeName) {
        if (!typeName.Contains('<'))
            return string.Empty;

        var iStart = typeName.IndexOf('<');
        var iEnd = typeName.IndexOf('>', iStart + 1);

        if (iEnd < iStart)
            return string.Empty;

        var sub = typeName.Substring(iStart + 1, iEnd - iStart - 1);

        return sub;
    }

    private static string StripPost(string name, string post) {
        return name.EndsWith(post, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - post.Length)
            : name;
    }

    private static string GetFieldType(Field field, bool green) {
        if (IsAnyList(field.type))
            return green ? "GreenNode" : "SyntaxNode";

        return field.type;
    }

    private bool IsDerivedOrListOfDerived(string baseType, string derivedType) {
        return IsDerivedType(baseType, derivedType)
            || ((IsNodeList(derivedType) || IsSeparatedNodeList(derivedType))
                && IsDerivedType(baseType, GetElementType(derivedType)));
    }

    private bool IsDerivedType(string typeName, string derivedTypeName) {
        if (typeName == derivedTypeName)
            return true;

        if (derivedTypeName != null && _parentMap.TryGetValue(derivedTypeName, out var baseType))
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
        WriteLine("");
    }

    private void WriteLine(string msg) {
        if (msg != "")
            WriteIndentIfNeeded();

        _writer.WriteLine(msg);
        _needIndent = true;
    }

    private void WriteLineWithoutIndent(string msg) {
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
        WriteGreenVisitorT();
        WriteGreenVisitor();
        WriteGreenRewriter();
        WriteGreenFactory();
    }

    private void WriteSyntax() {
        WriteFileHeader();
        WriteLine("namespace Buckle.CodeAnalysis.Syntax;");

        WriteRedNodes();
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
        var nodes = tree.types.Where(n => n is not PredefinedNode).ToList();

        foreach (var node in nodes) {
            WriteLine();
            WriteGreenNode(node);
        }
    }

    private void WriteGreenNode(TreeType node) {
        var @base = node.@base == "SyntaxNode" ? "BelteSyntaxNode" : node.@base;

        if (node is AbstractNode abstractNode) {
            Write($"internal abstract class {node.name} : {@base}");
            OpenBlock();
            WriteLine($"internal {node.name}(SyntaxKind kind)");
            WriteLine("  : base(kind) { }");
            WriteLine();
            WriteLine($"internal {node.name}(SyntaxKind kind, Diagnostic[] diagnostics)");
            WriteLine("  : base(kind, diagnostics) { }");

            var nodeFields = GetNodeOrNodeListFields(abstractNode);

            foreach (var field in nodeFields) {
                WriteLine();
                WriteLine($"internal abstract {field.type} {field.name} {{ get; }}");
            }

            CloseBlock();
        } else if (node is Node nd) {
            Write($"internal sealed class {node.name} : {@base}");
            OpenBlock();

            var valueFields = nd.fields.Where(n => !IsNodeOrNodeList(n.type)).ToList();
            var nodeFields = nd.fields.Where(n => IsNodeOrNodeList(n.type)).ToList();

            foreach (var field in nodeFields) {
                var type = GetFieldType(field, green: true);
                WriteLine($"internal readonly {type} _{field.name};");
            }

            foreach (var field in valueFields)
                WriteLine($"internal readonly {field.type} _{field.name};");

            WriteLine();
            Write($"internal {node.name}(");
            WriteGreenNodeConstructorArgs(nodeFields, valueFields);
            WriteLine(")");
            Write($"  : base(SyntaxKind.{nd.kinds.Single().name})");
            OpenBlock();
            WriteCtorBody(nodeFields, valueFields);
            CloseBlock();

            WriteLine();
            Write($"internal {node.name}(");
            WriteGreenNodeConstructorArgs(nodeFields, valueFields);
            WriteLine(", Diagnostic[] diagnostics)");
            Write($"  : base(SyntaxKind.{nd.kinds.Single().name}, diagnostics)");
            OpenBlock();
            WriteCtorBody(nodeFields, valueFields);
            CloseBlock();

            foreach (var field in nodeFields) {
                WriteLine();

                if (IsNodeList(field.type)) {
                    WriteLine(
                        $"internal {OverrideModifier(field)}InternalSyntax.{field.type} {field.name} => new " +
                        $"InternalSyntax.{field.type}(this._{field.name});"
                    );
                } else if (IsSeparatedNodeList(field.type)) {
                    WriteLine(
                        $"internal {OverrideModifier(field)}InternalSyntax.{field.type} {field.name} => new Internal" +
                        $"Syntax.{field.type}(new InternalSyntax.SyntaxList<BelteSyntaxNode>(this._{field.name}));"
                    );
                } else if (field.type == "SyntaxNodeOrTokenList") {
                    WriteLine(
                        $"internal {OverrideModifier(field)}InternalSyntax.SyntaxList<BelteSyntaxNode> {field.name}" +
                        $" => new InternalSyntax.SyntaxList<BelteSyntaxNode>(this._{field.name});"
                    );
                } else {
                    WriteLine(
                        $"internal {OverrideModifier(field)}{(GetFieldType(field, green: true))} {field.name} " +
                        $"=> this._{field.name};"
                    );
                }
            }

            foreach (var field in valueFields) {
                WriteLine();
                WriteLine($"internal {OverrideModifier(field)}{field.type} {field.name} => this._{field.name};");
            }

            WriteLine();

            // WriteGetSlotMethod
            {
                Write("internal override GreenNode GetSlot(int index)");

                if (nodeFields.Count == 0) {
                    WriteLine(" => null;");
                } else if (nodeFields.Count == 1) {
                    WriteLine($" => index == 0 ? this._{nodeFields[0].name} : null;");
                } else {
                    Write(" => index switch");
                    OpenBlock();

                    for (int i = 0, n = nodeFields.Count; i < n; i++) {
                        var field = nodeFields[i];
                        WriteLine($"{i} => this._{field.name},");
                    }

                    WriteLine("_ => null,");
                    CloseBlock(";");
                }
            }

            WriteLine();
            WriteLine(
                $"internal override SyntaxNode CreateRed(SyntaxNode parent, int position) => new Syntax." +
                $"{node.name}(parent, this, position);"
            );
            WriteLine();
            WriteLine(
                $"internal override void Accept(SyntaxVisitor visitor) => visitor.Visit" +
                $"{StripPost(node.name, "Syntax")}(this);"
            );
            WriteLine();
            WriteLine(
                $"internal override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => " +
                $"visitor.Visit{StripPost(node.name, "Syntax")}(this);"
            );

            WriteGreenUpdateMethod(nd);
            WriteGreenSetDiagnosticsMethod(nd);

            CloseBlock();
        }
    }

    private void WriteGreenNodeConstructorArgs(List<Field> nodeFields, List<Field> valueFields) {
        var first = true;

        foreach (var field in nodeFields) {
            Write($"{(first ? "" : ", ")}{(GetFieldType(field, green: true))} {field.name}");

            if (first)
                first = false;
        }

        foreach (var field in valueFields) {
            Write($"{(first ? "" : ", ")}{field.type} {field.name}");

            if (first)
                first = false;
        }
    }

    private void WriteCtorBody(List<Field> nodeFields, List<Field> valueFields) {
        WriteLine($"this.slotCount = {nodeFields.Count};");

        foreach (var field in nodeFields) {
            if (IsAnyList(field.type) || IsOptional(field)) {
                Write($"if ({field.name} != null)");
                OpenBlock();

                // TODO Support multiple kinds
                if (field.kinds.Count == 1) {
                    WriteLine(
                        $"Debug.Assert({field.name}.kind == SyntaxKind.{field.kinds.Single().name}, $\"incorrect syn" +
                        $"tax kind '{{{field.name}.kind}}', expected '{{SyntaxKind.{field.kinds.Single().name}}}'\");"
                    );
                }

                WriteLine($"this.AdjustFlagsAndWidth({field.name});");
                WriteLine($"this._{field.name} = {field.name};");
                CloseBlock();
            } else {
                if (field.kinds.Count == 1) {
                    WriteLine(
                        $"Debug.Assert({field.name}.kind == SyntaxKind.{field.kinds.Single().name}, $\"incorrect syn" +
                        $"tax kind '{{{field.name}.kind}}', expected '{{SyntaxKind.{field.kinds.Single().name}}}'\");"
                    );
                }

                WriteLine($"this.AdjustFlagsAndWidth({field.name});");
                WriteLine($"this._{field.name} = {field.name};");
            }
        }

        foreach (var field in valueFields)
            WriteLine($"this._{field.name} = {field.name};");
    }

    private void WriteGreenUpdateMethod(Node node) {
        WriteLine();
        Write($"internal {node.name} Update(");
        Write(CommaJoin(node.fields.Select(f => {
            var type =
                f.type == "SyntaxNodeOrTokenList" ? "InternalSyntax.SyntaxList<BelteSyntaxNode>" :
                f.type == "SyntaxTokenList" ? "InternalSyntax.SyntaxList<SyntaxToken>" :
                IsNodeList(f.type) ? "InternalSyntax." + f.type :
                IsSeparatedNodeList(f.type) ? "InternalSyntax." + f.type :
                f.type;

            return $"{type} _{f.name}";
        })));
        Write(")");
        OpenBlock();

        Write("if (");
        var nCompared = 0;

        foreach (var field in node.fields) {
            if (IsDerivedOrListOfDerived("SyntaxNode", field.type) || IsDerivedOrListOfDerived("SyntaxToken", field.type) || field.type == "SyntaxNodeOrTokenList") {
                if (nCompared > 0)
                    Write(" || ");

                Write($"_{field.name} != this.{field.name}");
                nCompared++;
            }
        }

        if (nCompared > 0) {
            Write(")");
            OpenBlock();
            Write($"var newNode = SyntaxFactory.{StripPost(node.name, "Syntax")}(");
            Write(CommaJoin(node.fields.Select(f => $"_{f.name}")));
            WriteLine(");");
            WriteLine("var diags = GetDiagnostics();");
            WriteLine("if (diags?.Length > 0)");
            WriteLine("    newNode = newNode.WithDiagnosticsGreen(diags);");
            WriteLine("return newNode;");
            CloseBlock();
        }

        WriteLine();
        WriteLine("return this;");
        CloseBlock();
    }

    private void WriteGreenSetDiagnosticsMethod(Node node) {
        WriteLine();
        WriteLine("internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics)");
        Indent();
        Write($"=> new {node.name}(");
        Write(CommaJoin(
            node.fields.Select(f => $"this._{f.name}"),
            "diagnostics"));
        WriteLine(");");
        Unindent();
    }

    private void WriteGreenVisitorT() {
        var nodes = tree.types.Where(n => n is Node).ToList();

        WriteLine();
        Write("internal partial class SyntaxVisitor<TResult>");
        OpenBlock();

        foreach (var node in nodes) {
            WriteLine(
                $"internal virtual TResult Visit{StripPost(node.name, "Syntax")}({node.name} node) " +
                $"=> DefaultVisit(node);"
            );
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteGreenVisitor() {
        var nodes = tree.types.Where(n => n is Node).ToList();

        WriteLine();
        Write("internal partial class SyntaxVisitor");
        OpenBlock();

        foreach (var node in nodes) {
            WriteLine(
                $"internal virtual void Visit{StripPost(node.name, "Syntax")}({node.name} node) => DefaultVisit(node);"
            );
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteGreenRewriter() {
        var nodes = tree.types.Where(n => n is Node).ToList();

        WriteLine();
        Write("internal partial class SyntaxRewriter : SyntaxVisitor<BelteSyntaxNode>");
        OpenBlock();

        foreach (var node in nodes) {
            var nd = node as Node;
            WriteLine($"internal override BelteSyntaxNode Visit{StripPost(node.name, "Syntax")}({node.name} node)");
            Indent();
            Write("=> node.Update(");
            Write(CommaJoin(nd.fields.Select(f => IsAnyNodeList(f.type)
                    ? $"VisitList(node.{f.name})"
                    : $"({f.type})Visit(node.{f.name})"
                )));
            WriteLine(");");
            Unindent();
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteGreenFactory() {
        var nodes = tree.types.Where(n => n is Node).ToList();

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
        var allArguments = CommaJoin(node.fields.Select(f => $"{f.type} {f.name}"));
        var requiredArguments = CommaJoin(node.fields.Where(f => !IsOptional(f)).Select(f => $"{f.type} {f.name}"));
        var allParameters = CommaJoin(node.fields.Select(f => IsAnyNodeList(f.type) ? $"{f.name}?.node" : f.name));
        var requiredParameters = CommaJoin(
            node.fields.Select(f => IsOptional(f) ? "null" : IsAnyNodeList(f.type) ? $"{f.name}?.node" : f.name)
        );

        WriteLine($"internal static {node.name} {StripPost(node.name, "Syntax")}({allArguments})");
        Indent();
        WriteLine($"=> new {node.name}({allParameters});");
        Unindent();

        if (allArguments != requiredArguments) {
            WriteLine();
            WriteLine($"internal static {node.name} {StripPost(node.name, "Syntax")}({requiredArguments})");
            Indent();
            WriteLine($"=> new {node.name}({requiredParameters});");
            Unindent();
        }
    }

    private void WriteRedNodes() {
        var nodes = tree.types.Where(n => n is not PredefinedNode).ToList();

        foreach (var node in nodes) {
            WriteLine();
            WriteRedNode(node);
        }
    }

    private void WriteRedNode(TreeType node) {
        var @base = node.@base == "SyntaxNode" ? "BelteSyntaxNode" : node.@base;

        if (node is AbstractNode abstractNode) {
            Write($"public abstract class {node.name} : {@base}");
            OpenBlock();
            WriteLine($"internal {node.name}(SyntaxNode parent, GreenNode green, int position)");
            WriteLine("  : base(parent, green, position) { }");

            var nodeFields = GetNodeOrNodeListFields(abstractNode);

            foreach (var field in nodeFields) {
                var fieldType = GetRedFieldType(field);
                WriteLine();
                WriteLine($"public abstract {fieldType} {field.name} {{ get; }}");
            }

            CloseBlock();
        } else if (node is Node nd) {
            Write($"public sealed class {node.name} : {@base}");
            OpenBlock();

            var valueFields = nd.fields.Where(n => !IsNodeOrNodeList(n.type)).ToList();
            var nodeFields = GetNodeOrNodeListFields(nd);

            foreach (var field in nodeFields) {
                if (field.type is not "SyntaxToken" and not "SyntaxList<SyntaxToken>") {
                    if (IsSeparatedNodeList(field.type) || field.type == "SyntaxNodeOrTokenList")
                        WriteLine($"private SyntaxNode _{field.name};");
                    else
                        WriteLine($"private {GetFieldType(field, green: false)} _{field.name};");
                }
            }

            WriteLine();
            WriteLine($"internal {node.name}(SyntaxNode parent, InternalSyntax.BelteSyntaxNode green, int position)");
            WriteLine("  : base(parent, green, position) { }");
            WriteLine();

            for (int i = 0, n = nodeFields.Count; i < n; i++) {
                var field = nodeFields[i];

                if (field.type == "SyntaxToken") {
                    Write($"public {OverrideModifier(field)}{GetRedFieldType(field)} {field.name}");

                    if (IsOptional(field)) {
                        OpenBlock();
                        Write("get");
                        OpenBlock();
                        WriteLine($"var slot = ((Syntax.InternalSyntax.{node.name})this.green)._{field.name};");
                        WriteLine(
                            $"return slot != null ? new SyntaxToken(this, slot," +
                            $" {GetChildPosition(i)}, {GetChildIndex(i)}) : null;"
                        );
                        CloseBlock();
                        CloseBlock();
                    } else {
                        WriteLine(
                            $" => new SyntaxToken(this, ((Syntax.InternalSyntax.{node.name})this.green)._{field.name}," +
                            $" {GetChildPosition(i)}, {GetChildIndex(i)});"
                        );
                    }
                } else if (field.type == "SyntaxList<SyntaxToken>") {
                    Write($"public {OverrideModifier(field)}SyntaxTokenList {field.name}");
                    OpenBlock();
                    Write("get");
                    OpenBlock();
                    WriteLine($"var slot = this.green.GetSlot({i});");
                    WriteLine(
                        $"return slot != null ? new SyntaxTokenList(this, slot, {GetChildPosition(i)}, " +
                        $"{GetChildIndex(i)}) : null;"
                    );
                    CloseBlock();
                    CloseBlock();
                } else {
                    Write($"public {OverrideModifier(field)}{GetRedFieldType(field)} {field.name}");

                    if (IsNodeList(field.type)) {
                        WriteLine($" => new {field.type}(GetRed(ref this._{field.name}, {i}));");
                    } else if (IsSeparatedNodeList(field.type)) {
                        WriteLine($" => new {field.type}(GetRed(ref this._{field.name}, {i}), {GetChildIndex(i)});");
                    } else if (field.type == "SyntaxNodeOrTokenList") {
                        throw new InvalidOperationException("field cannot be a random SyntaxNodeOrTokenList");
                    } else {
                        if (i == 0)
                            WriteLine($" => GetRedAtZero(ref this._{field.name});");
                        else
                            WriteLine($" => GetRed(ref this._{field.name}, {i});");
                    }
                }

                WriteLine();
            }

            foreach (var field in valueFields) {
                WriteLine(
                    $"public {OverrideModifier(field)}{field.type} {field.name} => " +
                    $"((Syntax.InternalSyntax.{node.name})this.green).{field.name};"
                );
                WriteLine();
            }

            // WriteGetNodeSlotMethod
            {
                Write("internal override SyntaxNode GetNodeSlot(int index) ");

                var relevantNodes = nodeFields.Select((field, index) => (field, index))
                    .Where(t => t.field.type is not "SyntaxToken" and not "SyntaxList<SyntaxToken>");

                if (!relevantNodes.Any()) {
                    WriteLine("=> null;");
                } else if (relevantNodes.Count() == 1) {
                    var (field, index) = relevantNodes.Single();
                    var whenTrue = index == 0
                        ? $"GetRedAtZero(ref this._{field.name})"
                        : $"GetRed(ref this._{field.name}, {index})";

                    WriteLine($"=> index == {index} ? {whenTrue} : null;");
                } else {
                    Write("=> index switch");
                    OpenBlock();

                    foreach (var (field, index) in relevantNodes) {
                        if (index == 0)
                            WriteLine($"{index} => GetRedAtZero(ref this._{field.name}),");
                        else
                            WriteLine($"{index} => GetRed(ref this._{field.name}, {index}),");
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
                    .Where(t => t.field.type is not "SyntaxToken" and not "SyntaxList<SyntaxToken>");

                if (!relevantNodes.Any()) {
                    WriteLine("=> null;");
                } else if (relevantNodes.Count() == 1) {
                    var (field, index) = relevantNodes.Single();
                    WriteLine($"=> index == {index} ? this._{field.name} : null;");
                } else {
                    Write("=> index switch");
                    OpenBlock();

                    foreach (var (field, index) in relevantNodes)
                        WriteLine($"{index} => this._{field.name},");

                    WriteLine("_ => null,");
                    CloseBlock(";");
                }
            }

            CloseBlock();
        }
    }

    private string GetRedFieldType(Field field)
        => field.type == "SyntaxList<SyntaxToken>" ? "SyntaxTokenList" : field.type;

    private List<Field> GetNodeOrNodeListFields(TreeType node)
        => node is AbstractNode an
            ? an.fields.Where(n => IsNodeOrNodeList(n.type)).ToList()

            : node is Node nd
                ? nd.fields.Where(n => IsNodeOrNodeList(n.type)).ToList()

                : new List<Field>();

    private string GetChildPosition(int i) => i == 0 ? "this.position" : $"GetChildPosition({i})";

    private string GetChildIndex(int i) => i == 0 ? "0" : $"GetChildIndex({i})";

    private void WriteRedFactory() {
        WriteLine();
        Write("internal static partial class SyntaxFactory");
        OpenBlock();

        var nodes = tree.types.Where(n => n is Node).ToList();

        foreach (var node in nodes) {
            WriteRedFactoryMethods(node as Node);
            WriteLine();
        }

        CloseBlock();
    }

    private void WriteRedFactoryMethods(Node node) {
        var allArguments = CommaJoin(node.fields.Select(f => $"{GetRedFieldType(f)} {f.name}"));
        var requiredArguments = CommaJoin(
            node.fields.Where(f => !IsOptional(f)).Select(f => $"{GetRedFieldType(f)} {f.name}")
        );
        var allParameters = CommaJoin(node.fields.Select(f =>
            IsNodeList(f.type)
                ? $"{f.name}.node.ToGreenList<Syntax.InternalSyntax.{GetElementType(f.type)}>()" :
            IsSeparatedNodeList(f.type)
                ? $"{f.name}.node.ToGreenSeparatedList<Syntax.InternalSyntax.{GetElementType(f.type)}>()" :
            (!IsNode(f.type) || f.type == "SyntaxToken")
                ? $"(Syntax.InternalSyntax.{f.type}){f.name}.node" :
            $"(Syntax.InternalSyntax.{f.type}){f.name}.green"
        ));
        var requiredParameters = CommaJoin(node.fields.Select(f => IsOptional(f) ? "null" :
            IsNodeList(f.type)
                ? $"{f.name}.node.ToGreenList<Syntax.InternalSyntax.{GetElementType(f.type)}>()" :
            IsSeparatedNodeList(f.type)
                ? $"{f.name}.node.ToGreenSeparatedList<Syntax.InternalSyntax.{GetElementType(f.type)}>()" :
            (!IsNode(f.type) || f.type == "SyntaxToken")
                ? $"(Syntax.InternalSyntax.{f.type}){f.name}.node" :
            $"(Syntax.InternalSyntax.{f.type}){f.name}.green"
        ));

        var fullDeclaration = $"internal static {node.name} {StripPost(node.name, "Syntax")}({allArguments}";
        var fullBody = $"=> ({node.name})Syntax.InternalSyntax.SyntaxFactory." +
            $"{StripPost(node.name, "Syntax")}({allParameters}).CreateRed(";

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
                $"internal static {node.name} {StripPost(node.name, "Syntax")}({requiredArguments}";
            var requiredBody = $"=> ({node.name})Syntax.InternalSyntax.SyntaxFactory." +
                $"{StripPost(node.name, "Syntax")}({requiredParameters}).CreateRed(";

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
