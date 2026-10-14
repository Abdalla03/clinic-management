using System.Text;

namespace ClinicManagement.Application.Auth.Totp;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                builder.Append(Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return builder.ToString();
    }

    public static byte[] Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        var builder = new StringBuilder(encoded.Length);
        foreach (var ch in encoded)
        {
            if (ch is ' ' or '-' or '_' or '=')
            {
                continue;
            }

            var upper = char.ToUpperInvariant(ch);
            if (Alphabet.IndexOf(upper) < 0)
            {
                throw new FormatException($"'{ch}' is not a valid Base32 character.");
            }

            builder.Append(upper);
        }

        var output = new byte[builder.Length * 5 / 8];
        var outputIndex = 0;
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var ch in builder.ToString())
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(ch);
            bitsLeft += 5;

            if (bitsLeft < 8)
            {
                continue;
            }

            bitsLeft -= 8;
            output[outputIndex++] = (byte)((buffer >> bitsLeft) & 0xFF);
        }

        return output;
    }
}