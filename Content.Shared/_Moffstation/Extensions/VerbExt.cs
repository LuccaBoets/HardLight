using System.Runtime.CompilerServices;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Extensions;

public static class VerbExt
{
    // Hardlight
    // Channged these extensions to normal functions because Version 12 does not support it.

    // These extensions are literally copy/pasted because I can't use generic `new()` construction because
    // sandboxing. Kill me.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(this SortedSet<UtilityVerb> verbs, VerbInfo info, Action act) =>
        verbs.Add(info, default, act);

    public static void Add(this SortedSet<UtilityVerb> verbs, VerbInfo info, int priority, Action act) =>
        verbs.Add(new UtilityVerb
        {
            Act = act,
            Priority = priority,
            Icon = info.Icon,
            Text = info.Text(),
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(this SortedSet<AlternativeVerb> verbs, VerbInfo info, Action act) =>
        verbs.Add(info, default, act);

    public static void Add(this SortedSet<AlternativeVerb> verbs, VerbInfo info, int priority, Action act) =>
        verbs.Add(new AlternativeVerb
        {
            Act = act,
            Priority = priority,
            Icon = info.Icon,
            Text = info.Text(),
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(this SortedSet<InteractionVerb> verbs, VerbInfo info, Action act) =>
        verbs.Add(info, default, act);

    public static void Add(this SortedSet<InteractionVerb> verbs, VerbInfo info, int priority, Action act) =>
        verbs.Add(new InteractionVerb
        {
            Act = act,
            Priority = priority,
            Icon = info.Icon,
            Text = info.Text(),
        });
}

[DataRecord, Serializable, NetSerializable]
public readonly partial record struct VerbInfo(
    LocId VerbTextLoc,
    LocId? PopupLoc,
    LocId? PopupOtherLoc,
    SpriteSpecifier? Icon,
    SoundSpecifier? Sound
)
{
    private static ILocalizationManager LocalizationManager { get; } = IoCManager.Resolve<ILocalizationManager>();

    private const string TextSuffix = "-verb-text";
    private const string PopupSuffix = "-popup";
    private const string PopupOtherSuffix = "-popup-other";

    public static VerbInfo Build(
        string loc,
        LocId? verbText = null,
        LocId? popup = null,
        LocId? popupOther = null,
        string? icon = null,
        SpriteSpecifier? iconSpec = null,
        string? sound = null,
        ProtoId<SoundCollectionPrototype>? sounds = null,
        SoundSpecifier? soundSpec = null
    ) => new(
        verbText ?? loc + TextSuffix,
        popup ?? loc + PopupSuffix,
        popupOther ?? loc + PopupOtherSuffix,
        iconSpec ?? (icon != null ? new SpriteSpecifier.Texture(new ResPath($"Interface/VerbIcons/{icon}.svg.192dpi.png")) : null),
        (soundSpec ?? (sound != null ? new SoundPathSpecifier(sound) : null)) ?? (sounds != null ? new SoundCollectionSpecifier(sounds) : null)
    );

    private static string? GetLocString(LocId? loc, (string, object)[]? args = null) =>
        loc != null ? LocalizationManager.GetString(loc, args ?? []) : null;

    public string Text((string, object)[]? args = null) => GetLocString(VerbTextLoc, args)!;
    public string? Popup((string, object)[]? args = null) => GetLocString(PopupLoc, args);
    public string? PopupOther((string, object)[]? args = null) => GetLocString(PopupOtherLoc, args);
}
