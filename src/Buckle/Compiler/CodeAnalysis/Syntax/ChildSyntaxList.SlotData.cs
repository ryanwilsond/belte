
namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class ChildSyntaxList {
    public readonly struct SlotData {
        public readonly int slotIndex;

        public readonly int precedingOccupantSlotCount;

        public readonly int positionAtSlotIndex;

        public SlotData(SyntaxNode node) : this(0, 0, node.position) { }

        public SlotData(int slotIndex, int precedingOccupantSlotCount, int positionAtSlotIndex) {
            this.slotIndex = slotIndex;
            this.precedingOccupantSlotCount = precedingOccupantSlotCount;
            this.positionAtSlotIndex = positionAtSlotIndex;
        }
    }
}
