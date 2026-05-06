namespace Content.Server.Speech;

public sealed class ListenEvent : EntityEventArgs
{
    public readonly string Message;
    public readonly string? OriginalMessage;
    public readonly EntityUid Source;

    public ListenEvent(string message, string? originalMessage, EntityUid source)
    {
        Message = message;
        OriginalMessage = originalMessage;
        Source = source;
    }
}

public sealed class ListenAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Source;

    public ListenAttemptEvent(EntityUid source)
    {
        Source = source;
    }
}
