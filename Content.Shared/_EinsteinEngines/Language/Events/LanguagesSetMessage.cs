// VRS port: Einstein Engines Language framework (via Triad).

using Robust.Shared.Serialization;

namespace Content.Shared._EinsteinEngines.Language.Events;

/// <summary>
///     Sent from the client to the server when it wants to set its current language.
/// </summary>
[Serializable, NetSerializable]
public sealed class LanguagesSetMessage(string currentLanguage) : EntityEventArgs
{
    public string CurrentLanguage = currentLanguage;
}
