using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace OpticaSosa.Infrastructure.Clinical;

// Recomprime exclusivamente IDAT. Conserva los bytes filtrados, dimensiones, profundidad,
// orientación, perfiles de color y demás chunks; nunca decodifica/reescala los píxeles.
public static class LosslessPngOptimizer
{
    public static byte[] Optimize(byte[] original, long maximumInflatedBytes, CancellationToken ct)
    {
        var chunks = new List<(int Offset, int Size, string Type)>();
        using var idat = new MemoryStream();
        int position = 8;
        while (position < original.Length)
        {
            if (original.Length - position < 12) throw new ArgumentException("PNG incompleto.");
            var size = BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(position, 4));
            if (size > int.MaxValue || size + 12L > original.Length - position) throw new ArgumentException("PNG incompleto.");
            var type = Encoding.ASCII.GetString(original, position + 4, 4);
            chunks.Add((position, (int)size + 12, type));
            if (type == "IDAT") idat.Write(original, position + 8, (int)size);
            position += (int)size + 12;
        }
        idat.Position = 0;
        using var compressed = new MemoryStream();
        using (var inflater = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
        using (var deflater = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var block = new byte[81920]; long count = 0; int read;
            while ((read = inflater.Read(block)) != 0)
            {
                ct.ThrowIfCancellationRequested();
                count += read;
                if (count > maximumInflatedBytes) throw new ArgumentException("La imagen supera la capacidad de optimización segura.");
                deflater.Write(block, 0, read);
            }
        }
        using var output = new MemoryStream(); output.Write(original, 0, 8);
        bool written = false;
        Span<byte> number = stackalloc byte[4];
        foreach (var chunk in chunks)
        {
            if (chunk.Type != "IDAT") output.Write(original, chunk.Offset, chunk.Size);
            else if (!written)
            {
                var payload = compressed.ToArray();
                BinaryPrimitives.WriteUInt32BigEndian(number, (uint)payload.Length); output.Write(number);
                output.Write("IDAT"u8); output.Write(payload);
                uint crc = 0xffffffff;
                foreach (var b in "IDAT"u8) crc = Update(crc, b);
                foreach (var b in payload) crc = Update(crc, b);
                BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); output.Write(number);
                written = true;
            }
        }
        return output.ToArray();
    }
    private static uint Update(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        return crc;
    }
}
