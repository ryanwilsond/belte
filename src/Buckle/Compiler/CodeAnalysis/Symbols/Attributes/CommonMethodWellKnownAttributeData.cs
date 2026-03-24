
namespace Buckle.CodeAnalysis.Symbols;

internal class CommonMethodWellKnownAttributeData : WellKnownAttributeData {
    internal CommonMethodWellKnownAttributeData(bool preserveSigFirstWriteWins) {
        _preserveSigFirstWriteWins = preserveSigFirstWriteWins;
        _dllImportIndex = _methodImplIndex = _preserveSigIndex = -1;
    }

    internal CommonMethodWellKnownAttributeData()
        : this(false) {
    }

    #region DllImportAttribute, MethodImplAttribute, PreserveSigAttribute

    private readonly bool _preserveSigFirstWriteWins;
    private bool _dllImportPreserveSig;
    private int _dllImportIndex;

    private int _methodImplIndex;
    private MethodImplAttributes _attributes;

    private int _preserveSigIndex;

    internal void SetPreserveSignature(int attributeIndex) {
        _preserveSigIndex = attributeIndex;
    }

    internal void SetMethodImplementation(int attributeIndex, MethodImplAttributes attributes) {
        _attributes = attributes;
        _methodImplIndex = attributeIndex;
    }

    internal void SetDllImport(
        int attributeIndex,
        string moduleName,
        string entryPointName,
        MethodImportAttributes flags,
        bool preserveSig) {
        dllImportPlatformInvokeData = new DllImportData(moduleName, entryPointName, flags);
        _dllImportIndex = attributeIndex;
        _dllImportPreserveSig = preserveSig;
    }

    internal DllImportData? dllImportPlatformInvokeData { get; private set; }

    internal MethodImplAttributes methodImplAttributes {
        get {
            var result = _attributes;

            if (_dllImportPreserveSig || _preserveSigIndex >= 0)
                result |= MethodImplAttributes.PreserveSig;

            if (_dllImportIndex >= 0 && !_dllImportPreserveSig) {
                if (_preserveSigFirstWriteWins) {
                    if ((_preserveSigIndex == -1 || _dllImportIndex < _preserveSigIndex) &&
                        (_methodImplIndex == -1 || (_attributes & MethodImplAttributes.PreserveSig) == 0 || _dllImportIndex < _methodImplIndex)) {
                        result &= ~MethodImplAttributes.PreserveSig;
                    }
                } else {
                    if (_dllImportIndex > _preserveSigIndex && (_dllImportIndex > _methodImplIndex || (_attributes & MethodImplAttributes.PreserveSig) == 0)) {
                        result &= ~MethodImplAttributes.PreserveSig;
                    }
                }
            }

            return result;
        }
    }

    #endregion
}
