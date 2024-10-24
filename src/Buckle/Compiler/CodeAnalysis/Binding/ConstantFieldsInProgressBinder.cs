
namespace Buckle.CodeAnalysis.Binding;

internal sealed class ConstantFieldsInProgressBinder : Binder {
    internal ConstantFieldsInProgressBinder(ConstantFieldsInProgress inProgress, Binder next)
        : base(next, BinderFlags.FieldInitializer | next.flags) {
        constantFieldsInProgress = inProgress;
    }

    internal override ConstantFieldsInProgress constantFieldsInProgress { get; }
}
