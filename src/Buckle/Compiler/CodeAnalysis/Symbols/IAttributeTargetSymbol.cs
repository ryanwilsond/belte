
namespace Buckle.CodeAnalysis.Symbols;

internal interface IAttributeTargetSymbol {
    IAttributeTargetSymbol attributesOwner { get; }

    AttributeLocation allowedAttributeLocations { get; }

    AttributeLocation defaultAttributeLocation { get; }
}
