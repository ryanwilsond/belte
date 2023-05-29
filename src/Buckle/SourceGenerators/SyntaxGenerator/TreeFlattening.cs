using System;
using System.Collections.Generic;

namespace SyntaxGenerator;

/// <summary>
/// Flattens the XML tree.
/// </summary>
internal static class TreeFlattening {
    /// <summary>
    /// Placeholder until the grammar generator is
    /// </summary>
    internal static void FlattenChildren(Tree tree) {
        foreach (var type in tree.Types) {
            switch (type) {
                case AbstractNode node:
                    FlattenChildren(node.Children, node.Fields, makeOptional: false);
                    break;
                case Node node:
                    FlattenChildren(node.Children, node.Fields, makeOptional: false);
                    break;
            }
        }
    }

    private static void FlattenChildren(List<TreeTypeChild> fieldsAndChoices, List<Field> fields, bool makeOptional) {
        foreach (var fieldOrChoice in fieldsAndChoices) {
            switch (fieldOrChoice) {
                case Field field:
                    if (makeOptional && !SourceWriter.IsAnyNodeList(field.Type))
                        field.Optional = "true";

                    fields.Add(field);
                    break;
                default:
                    throw new InvalidOperationException("unknown child type");
            }
        }
    }
}
