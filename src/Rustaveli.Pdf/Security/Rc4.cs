namespace Rustaveli.Pdf.Security;

/// <summary>
/// The RC4 stream cipher, as PDF's standard security handler uses it at 40 and 128 bits. Its own inverse: encrypting
/// twice with one key gives back the data.
/// </summary>
internal static class Rc4
{
    public static byte[] Transform(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        byte[] state = new byte[256];

        for (int index = 0; index < 256; index++)
            state[index] = (byte)index;

        for (int index = 0, swap = 0; index < 256; index++)
        {
            swap = (swap + state[index] + key[index % key.Length]) & 0xFF;
            (state[index], state[swap]) = (state[swap], state[index]);
        }

        byte[] output = new byte[data.Length];

        for (int index = 0, i = 0, j = 0; index < data.Length; index++)
        {
            i = (i + 1) & 0xFF;
            j = (j + state[i]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
            output[index] = (byte)(data[index] ^ state[(state[i] + state[j]) & 0xFF]);
        }

        return output;
    }
}
