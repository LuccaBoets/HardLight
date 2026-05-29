namespace Content.Shared._HL.PlayerMoods;

/// <summary>
/// Abstract base for composable erotic mood pool components.
/// Not registered — only concrete subtypes are used.
/// Each subtype is a separate selectable trait contributing its pool to the entity's moods.
/// </summary>
public abstract partial class EroticMoodPoolComponentBase : Component
{
    /// <summary>Dataset prototype ID this pool draws moods from.</summary>
    public abstract string DatasetId { get; }

    /// <summary>Locale key for the category label used in chat reminders.</summary>
    public abstract string CategoryLocKey { get; }

    /// <summary>Currently active rolled moods for this pool.</summary>
    public List<PlayerMood> ActiveMoods = new();

    /// <summary>Game time at which to next send a reminder message.</summary>
    public TimeSpan NextReminder;

    /// <summary>Game time at which to next re-roll this pool's moods.</summary>
    public TimeSpan NextReroll;

    /// <summary>Base interval between reminder nudges, before jitter.</summary>
    [DataField]
    public TimeSpan ReminderBase = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Base interval between re-rolls, before jitter.
    /// </summary>
    [DataField]
    public TimeSpan RerollBase = TimeSpan.FromMinutes(180);

    /// <summary>
    /// Full range of random jitter on reminder interval.
    /// </summary>
    [DataField]
    public TimeSpan ReminderJitter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Full range of random jitter on reroll interval.
    /// </summary>
    [DataField]
    public TimeSpan RerollJitter = TimeSpan.FromMinutes(60);

    /// <summary>How many moods to roll per cycle from this pool.</summary>
    [DataField]
    public int MoodsPerCycle = 1;
}
