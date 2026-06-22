using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared.Traits.Events;

public sealed partial class PrizeballLayingActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed partial class PrizeballLayingDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class PrizeballLayingInsideDoAfterEvent : SimpleDoAfterEvent { }


