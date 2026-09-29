
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class MissingMetadataTypeSymbol : ErrorTypeSymbol {
    private protected readonly int _arity;
    private protected readonly bool _mangleName;

    private MissingMetadataTypeSymbol(string name, int arity, bool mangleName, TupleExtraData tupleData = null)
        : base(tupleData) {
        this.name = name;
        this.arity = arity;
        this.mangleName = mangleName && arity > 0;
    }

    public override string name { get; }

    public override int arity { get; }

    internal override bool mangleName { get; }

    // TODO Is this sufficient replacement for usesite diagnostics for now?
    internal override bool unreported => true;

    internal override BelteDiagnostic error {
        get {
            var containingAssembly = this.containingAssembly;

            if (containingAssembly?.isMissing == true) {
                return Error.NoTypeDef(this, containingAssembly);
            } else {
                var containingModule = this.containingModule;

                if (containingModule?.isMissing == true)
                    return Error.NoTypeDefFromModule(this, containingModule.name);

                throw ExceptionUtilities.Unreachable();
                // if (containingAssembly is object) {
                //     if (containingAssembly.Dangerous_IsFromSomeCompilation) {
                //         // This scenario is quite tricky and involves a circular reference. Suppose we have
                //         // assembly Alpha that has a type C. Assembly Beta refers to Alpha and uses type C.
                //         // Now we create a new source assembly that replaces Alpha, and refers to Beta.
                //         // The usage of C in Beta will be redirected to refer to the source assembly.
                //         // If C is not in that source assembly then we give the following warning:

                //         // CS7068: Reference to type 'C' claims it is defined in this assembly, but it is not defined in source or any added modules
                //         return new CSDiagnosticInfo(ErrorCode.ERR_MissingTypeInSource, this);
                //     } else {
                //         // The more straightforward scenario is that we compiled Beta against a version of Alpha
                //         // that had C, and then added a reference to a different version of Alpha that
                //         // lacks the type C:

                //         // error CS7069: Reference to type 'C' claims it is defined in 'Alpha', but it could not be found
                //         return new CSDiagnosticInfo(ErrorCode.ERR_MissingTypeInAssembly, this, containingAssembly.Name);
                //     }
                // } else if (ContainingType is ErrorTypeSymbol { ErrorInfo: { } info }) {
                //     return info;
                // } else {
                //     // This is the best we can do at this point
                //     return new CSDiagnosticInfo(ErrorCode.ERR_BogusType, string.Empty);
                // }
            }
        }
    }
}
