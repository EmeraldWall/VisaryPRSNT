namespace Visary.Core.Export;

/// <summary>Reads pixel dimensions from PNG, JPEG, GIF and BMP headers without decoding the image.</summary>
public static class ImageInfo
{
    public static bool TryGetSize(ReadOnlySpan<byte> d, out int width, out int height)
    {
        width = height = 0;
        if (d.Length >= 24 && d[0] == 0x89 && d[1] == 'P' && d[2] == 'N' && d[3] == 'G')
        {
            width = BigEndian(d[16..20]);
            height = BigEndian(d[20..24]);
            return width > 0 && height > 0;
        }
        if (d.Length >= 10 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F')
        {
            width = d[6] | d[7] << 8;
            height = d[8] | d[9] << 8;
            return width > 0 && height > 0;
        }
        if (d.Length >= 26 && d[0] == 'B' && d[1] == 'M')
        {
            width = BitConverter.ToInt32(d[18..22]);
            height = Math.Abs(BitConverter.ToInt32(d[22..26]));
            return width > 0 && height > 0;
        }
        if (d.Length >= 4 && d[0] == 0xFF && d[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < d.Length)
            {
                if (d[i] != 0xFF) { i++; continue; }
                var marker = d[i + 1];
                if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }
                var len = d[i + 2] << 8 | d[i + 3];
                // Start of frame markers carry the size (C4, C8 and CC are other segment types).
                if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                {
                    height = d[i + 5] << 8 | d[i + 6];
                    width = d[i + 7] << 8 | d[i + 8];
                    return width > 0 && height > 0;
                }
                i += 2 + len;
            }
        }
        return false;
    }

    static int BigEndian(ReadOnlySpan<byte> b) => b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3];
}
