using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Moffstation.Extensions;

/// This interface marks a type as having sealed inheritance, enabling access to
/// <see cref="ISealedInheritanceExt.ThrowUnknownInheritor{TSealed, TRet}(TSealed)"/>
public interface ISealedInheritance;

public static class ISealedInheritanceExt
{
    // Hardlight
    // Channged these extensions to normal functions because Version 12 does not support it.

    /// Throws, complaining that <typeparamref name="TSealed"/> is, well, sealed, and the given receiver's type is unknown.
    [DoesNotReturn]
    public static void ThrowUnknownInheritor<TSealed>(this TSealed s) where TSealed : ISealedInheritance =>
        s.ThrowUnknownInheritor<TSealed, int>();

    /// Throws, complaining that <typeparamref name="TSealed"/> is, well, sealed, and the given receiver's type is
    /// unknown. "returns" <typeparamref name="TRet"/> to appease the One True God, the typechecker.
    [DoesNotReturn]
    public static TRet ThrowUnknownInheritor<TSealed, TRet>(this TSealed s) where TSealed : ISealedInheritance =>
        throw new Exception(
            $"Unreachable: {typeof(TSealed).FullName} has sealed inheritance, but {s.GetType().FullName} is unknown."
        );
}
