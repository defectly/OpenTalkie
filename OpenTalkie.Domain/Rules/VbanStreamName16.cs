namespace OpenTalkie.Domain.Rules;

public static class VbanStreamName16
{
    public const int MaxBytes = 16;

    public static byte[] Create(string? name)
    {
        var bytes = new byte[MaxBytes];
        Fill(bytes, name);
        return bytes;
    }

    public static void Fill(Span<byte> destination, string? name)
    {
        if (destination.Length < MaxBytes)
        {
            throw new ArgumentException($"Destination must be at least {MaxBytes} bytes.", nameof(destination));
        }

        destination[..MaxBytes].Clear();

        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var len = Math.Min(name.Length, MaxBytes);
        for (var i = 0; i < len; i++)
        {
            destination[i] = (byte)name[i];
        }
    }

    public static bool EqualsPacketName(ReadOnlySpan<byte> packetName16, string? streamName)
    {
        if (packetName16.Length < MaxBytes)
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[MaxBytes];
        Fill(expected, streamName);
        return packetName16[..MaxBytes].SequenceEqual(expected);
    }

    public static bool EqualsName(string? left, string? right)
    {
        Span<byte> leftBytes = stackalloc byte[MaxBytes];
        Span<byte> rightBytes = stackalloc byte[MaxBytes];
        Fill(leftBytes, left);
        Fill(rightBytes, right);
        return leftBytes.SequenceEqual(rightBytes);
    }
}
