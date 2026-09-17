using System.Diagnostics;
using System.Text;

namespace Buckle.CodeAnalysis;

[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal sealed class MetadataImageReference : PortableExecutableReference {
    private readonly string _display;
    private readonly Metadata _metadata;

    internal MetadataImageReference(
        Metadata metadata,
        MetadataReferenceProperties properties,
        string filePath,
        string display)
        : base(properties, filePath) {
        _display = display;
        _metadata = metadata;
    }

    internal override string display {
        get {
            return _display ??
                filePath ??
                (properties.kind == MetadataImageKind.Assembly
                    ? "InMemoryAssembly"
                    : "InMemoryModule"
                );
        }
    }

    private protected override Metadata GetMetadataImpl() {
        return _metadata;
    }

    private protected override PortableExecutableReference WithPropertiesImpl(MetadataReferenceProperties properties) {
        return new MetadataImageReference(
            _metadata,
            properties,
            filePath,
            _display
        );
    }

    private string GetDebuggerDisplay() {
        var sb = new StringBuilder();
        sb.Append(properties.kind == MetadataImageKind.Module ? "Module" : "Assembly");

        if (!properties.aliases.IsEmpty) {
            sb.Append(" Aliases={");
            sb.Append(string.Join(", ", properties.aliases));
            sb.Append('}');
        }

        if (properties.embedInteropTypes)
            sb.Append(" Embed");

        if (filePath is not null) {
            sb.Append(" Path='");
            sb.Append(filePath);
            sb.Append('\'');
        }

        if (_display is not null) {
            sb.Append(" Display='");
            sb.Append(_display);
            sb.Append('\'');
        }

        return sb.ToString();
    }
}
