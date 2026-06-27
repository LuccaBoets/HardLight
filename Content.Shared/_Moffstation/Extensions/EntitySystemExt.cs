using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Extensions;

public static class EntitySystemExt
{
    // Hardlight
    // Channged these extensions to normal functions because Version 12 does not support it.

    /// Throws a debug assert and logs the given <paramref name="message"/> to the system's error logger.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AssertOrLogError(this EntitySystem entSys, string message)
    {
        DebugTools.Assert(message);
        entSys.Log.Error(message);
    }

    /// <see cref="AssertOrLogError"/>, but returns <paramref name="ret"/> instead of void.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AssertOrLogError<T>(this EntitySystem entSys, string message, T ret)
    {
        entSys.AssertOrLogError(message);
        return ret;
    }

    /// Throws an exception. For typechecking, returns <typeparamref name="T"/> for use in expressions, though it definitely never returns.
    [MethodImpl(MethodImplOptions.AggressiveInlining), DoesNotReturn]
    public static T Unreachable<T>(this EntitySystem _, string msg)
    {
        throw new Exception(msg);
    }

    /// Throws an exception.
    [MethodImpl(MethodImplOptions.AggressiveInlining), DoesNotReturn]
    public static void Unreachable(this EntitySystem _, string msg)
    {
        throw new Exception(msg);
    }
}
