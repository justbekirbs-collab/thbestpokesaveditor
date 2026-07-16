using System;
using System.IO;
using PKHeX.Core;

namespace PokeSaveEditor.Infrastructure.Parsers.PKHeX;

public static class RomHackSaveDetector
{
    public static SaveFile? TryDetect(byte[] data)
    {
        // 1. Try native PKHeX detection
        try
        {
            var sav = SaveUtil.GetVariantSAV(data);
            if (sav != null)
                return sav;
        }
        catch
        {
            // Ignore native detection errors
        }

        // 2. Try direct constructor instantiation based on file size and structures (useful for ROM hacks)
        int len = data.Length;

        // Gen 3 GBA structure detection
        if (len >= 0x10000 && IsGen3GbaStructure(data))
        {
            try { return new SAV3E(data); } catch {}
            try { return new SAV3FRLG(data); } catch {}
            try { return new SAV3RS(data); } catch {}
        }

        // Gen 4 structure detection (Diamond/Pearl/Platinum/HGSS)
        if (len >= 0x40000 && len <= 0x100000)
        {
            try { return new SAV4Pt(data); } catch {}
            try { return new SAV4HGSS(data); } catch {}
            try { return new SAV4DP(data); } catch {}
        }

        // Gen 5 structure detection (Black/White/Black2/White2)
        if (len >= 0x20000 && len <= 0x100000)
        {
            try { return new SAV5B2W2(data); } catch {}
            try { return new SAV5BW(data); } catch {}
        }

        // Gen 6 structure detection (XY/ORAS)
        if (len >= 0x5000 && len <= 0x100000)
        {
            try { return new SAV6AO(data); } catch {}
            try { return new SAV6XY(data); } catch {}
        }

        // Gen 7 structure detection (Sun/Moon/USUM/LGPE)
        if (len >= 0x50000 && len <= 0x200000)
        {
            try { return new SAV7USUM(data); } catch {}
            try { return new SAV7SM(data); } catch {}
        }

        // Gen 8 structure detection (Sword/Shield/BDSP/Legends Arceus)
        if (len >= 0xE0000 && len <= 0x200000)
        {
            try { return new SAV8SWSH(data); } catch {}
            try { return new SAV8BS(data); } catch {}
            try { return new SAV8LA(data); } catch {}
        }

        // Gen 9 structure detection (Scarlet/Violet)
        if (len >= 0x200000 && len <= 0x500000)
        {
            try { return new SAV9SV(data); } catch {}
        }

        // Gen 1 / Gen 2 structure detection
        if (len == 0x8000 || len == 0x10000 || len == 0x20000)
        {
            try { return new SAV2(data); } catch {}
            try { return new SAV1(data); } catch {}
        }

        return null;
    }

    private static bool IsGen3GbaStructure(ReadOnlySpan<byte> data)
    {
        // GBA section signature 0x08012025 (little-endian) is at offset 0xFF8 in Gen 3 sections
        // A valid Gen 3 save file has sections containing this signature.
        // Let's search a few sections to see if we find it.
        const uint g3Signature = 0x08012025;
        
        // We check offset 0xFF8 of the first slot
        if (data.Length >= 0xFFC)
        {
            uint sig = BitConverter.ToUInt32(data.Slice(0xFF8, 4));
            if (sig == g3Signature) return true;
        }

        // We check offset 0xFF8 of the second slot if present (at 512 bytes alignment or 0)
        if (data.Length >= 512 + 0xFFC)
        {
            uint sig = BitConverter.ToUInt32(data.Slice(512 + 0xFF8, 4));
            if (sig == g3Signature) return true;
        }

        // Also check if any sector has it
        for (int i = 0; i < 14; i++)
        {
            int offset = i * 0x1000 + 0xFF8;
            if (data.Length >= offset + 4)
            {
                uint sig = BitConverter.ToUInt32(data.Slice(offset, 4));
                if (sig == g3Signature) return true;
            }
        }

        return false;
    }
}
