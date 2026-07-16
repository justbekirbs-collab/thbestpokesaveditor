namespace PokeSaveEditor.Infrastructure.Parsers.Gen3;

using System.Buffers.Binary;

/// <summary>
/// Handles decryption and substructure reordering for Gen 3 Pokémon data.
/// 
/// The 48-byte data substructure is:
/// 1. Divided into four 12-byte blocks (G=Growth, A=Attacks, E=EVs, M=Misc)
/// 2. Shuffled based on (PersonalityValue % 24) using a fixed permutation table
/// 3. XOR-encrypted using a keystream seeded by the Pokémon's OT ID XOR PV
/// </summary>
public static class Gen3Decryptor
{
    /// <summary>
    /// The 24 possible substructure orderings indexed by (PV % 24).
    /// Each entry defines the order: [0]=G position, [1]=A position, [2]=E position, [3]=M position.
    /// Letters: G=Growth, A=Attacks, E=EVs/Condition, M=Miscellaneous
    /// </summary>
    private static readonly int[][] ShuffleOrders =
    [
        [0, 1, 2, 3], // GAEM
        [0, 1, 3, 2], // GAME
        [0, 2, 1, 3], // GEAM
        [0, 2, 3, 1], // GEMA
        [0, 3, 1, 2], // GMAE
        [0, 3, 2, 1], // GMEA
        [1, 0, 2, 3], // AGEM
        [1, 0, 3, 2], // AGME
        [1, 2, 0, 3], // AEGM
        [1, 2, 3, 0], // AEMG
        [1, 3, 0, 2], // AMGE
        [1, 3, 2, 0], // AMEG
        [2, 0, 1, 3], // EGAM
        [2, 0, 3, 1], // EGMA
        [2, 1, 0, 3], // EAGM
        [2, 1, 3, 0], // EAMG
        [2, 3, 0, 1], // EMGA
        [2, 3, 1, 0], // EMAG
        [3, 0, 1, 2], // MGAE
        [3, 0, 2, 1], // MGEA
        [3, 1, 0, 2], // MAGE
        [3, 1, 2, 0], // MAEG
        [3, 2, 0, 1], // MEGA
        [3, 2, 1, 0], // MEAG
    ];

    /// <summary>
    /// Decrypts and unshuffles the 48-byte Pokémon data substructure.
    /// Returns a 48-byte array with blocks in canonical order: [G][A][E][M].
    /// </summary>
    /// <param name="encryptedData">The 48 encrypted bytes from the Pokémon structure.</param>
    /// <param name="personalityValue">The Pokémon's PV (determines shuffle order).</param>
    /// <param name="otId">The full 32-bit OT ID (encryption key = OT ID XOR PV).</param>
    public static byte[] DecryptSubstructure(
        ReadOnlySpan<byte> encryptedData,
        uint personalityValue,
        uint otId)
    {
        if (encryptedData.Length != Gen3OffsetMap.PkmnSubstructureSize)
            throw new ArgumentException(
                $"Expected {Gen3OffsetMap.PkmnSubstructureSize} bytes, got {encryptedData.Length}.",
                nameof(encryptedData));

        // Step 1: XOR decrypt using key = OT ID XOR PV
        uint key = otId ^ personalityValue;
        byte[] decrypted = new byte[Gen3OffsetMap.PkmnSubstructureSize];

        for (int i = 0; i < Gen3OffsetMap.PkmnSubstructureSize; i += 4)
        {
            uint encrypted = BinaryPrimitives.ReadUInt32LittleEndian(encryptedData[i..]);
            BinaryPrimitives.WriteUInt32LittleEndian(decrypted.AsSpan(i), encrypted ^ key);
        }

        // Step 2: Unshuffle — move blocks to canonical order [G][A][E][M]
        int orderIndex = (int)(personalityValue % 24);
        int[] order = ShuffleOrders[orderIndex];

        byte[] unshuffled = new byte[Gen3OffsetMap.PkmnSubstructureSize];
        const int blockSize = Gen3OffsetMap.SubstructureBlockSize;

        for (int blockType = 0; blockType < 4; blockType++)
        {
            int sourcePosition = order[blockType] * blockSize;
            int destPosition = blockType * blockSize;
            decrypted.AsSpan(sourcePosition, blockSize)
                     .CopyTo(unshuffled.AsSpan(destPosition));
        }

        return unshuffled;
    }

    /// <summary>
    /// Encrypts and reshuffles a canonical-order substructure back to storage format.
    /// </summary>
    public static byte[] EncryptSubstructure(
        ReadOnlySpan<byte> canonicalData,
        uint personalityValue,
        uint otId)
    {
        if (canonicalData.Length != Gen3OffsetMap.PkmnSubstructureSize)
            throw new ArgumentException(
                $"Expected {Gen3OffsetMap.PkmnSubstructureSize} bytes, got {canonicalData.Length}.",
                nameof(canonicalData));

        // Step 1: Reshuffle to storage order
        int orderIndex = (int)(personalityValue % 24);
        int[] order = ShuffleOrders[orderIndex];
        const int blockSize = Gen3OffsetMap.SubstructureBlockSize;

        byte[] shuffled = new byte[Gen3OffsetMap.PkmnSubstructureSize];
        for (int blockType = 0; blockType < 4; blockType++)
        {
            int sourcePosition = blockType * blockSize;
            int destPosition = order[blockType] * blockSize;
            canonicalData.Slice(sourcePosition, blockSize)
                         .CopyTo(shuffled.AsSpan(destPosition));
        }

        // Step 2: XOR encrypt
        uint key = otId ^ personalityValue;
        byte[] encrypted = new byte[Gen3OffsetMap.PkmnSubstructureSize];

        for (int i = 0; i < Gen3OffsetMap.PkmnSubstructureSize; i += 4)
        {
            uint plain = BinaryPrimitives.ReadUInt32LittleEndian(shuffled.AsSpan(i));
            BinaryPrimitives.WriteUInt32LittleEndian(encrypted.AsSpan(i), plain ^ key);
        }

        return encrypted;
    }

    /// <summary>Calculates the 16-bit checksum of the decrypted canonical 48-byte substructure.</summary>
    public static ushort CalculateChecksum(ReadOnlySpan<byte> canonicalData)
    {
        if (canonicalData.Length != Gen3OffsetMap.PkmnSubstructureSize)
            throw new ArgumentException($"Expected {Gen3OffsetMap.PkmnSubstructureSize} bytes.");

        ushort sum = 0;
        for (int i = 0; i < Gen3OffsetMap.PkmnSubstructureSize; i += 2)
        {
            sum += BinaryPrimitives.ReadUInt16LittleEndian(canonicalData[i..]);
        }
        return sum;
    }
}
