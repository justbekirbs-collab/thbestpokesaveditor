namespace PokeSaveEditor.Infrastructure.Parsers.Gen3;

/// <summary>
/// Byte offset constants for Generation III (GBA) save files.
/// Covers Ruby/Sapphire, Emerald, and FireRed/LeafGreen.
/// References: Bulbapedia Gen III save structure documentation.
/// </summary>
public static class Gen3OffsetMap
{
    // --- Save file structure ---
    /// <summary>Size of each save section (4 KB).</summary>
    public const int SectionSize = 0x1000;

    /// <summary>Number of sections per save slot.</summary>
    public const int SectionCount = 14;

    /// <summary>Total save slot size (14 sections × 4KB).</summary>
    public const int SaveSlotSize = SectionSize * SectionCount;

    /// <summary>Section footer offset (relative to section start). Contains section ID, checksum, save index.</summary>
    public const int SectionFooterOffset = 0xFF4;

    /// <summary>Offset of the section ID within the footer (2 bytes).</summary>
    public const int SectionIdOffset = SectionFooterOffset + 0;

    /// <summary>Offset of the checksum within the footer (2 bytes).</summary>
    public const int SectionChecksumOffset = SectionFooterOffset + 2;

    /// <summary>Offset of the save index within the footer (4 bytes).</summary>
    public const int SectionSaveIndexOffset = SectionFooterOffset + 8;

    /// <summary>Expected file sizes for Gen 3 save files.</summary>
    public const int SaveFileSize_128KB = 0x20000;  // 128 KB (standard)
    public const int SaveFileSize_64KB = 0x10000;   // 64 KB (some emulators)

    // --- Section 1 (Trainer Data) ---
    /// <summary>Section ID for trainer info.</summary>
    public const int TrainerSection = 0;

    /// <summary>Trainer name offset within section 0 (7 bytes, Gen 3 encoded).</summary>
    public const int TrainerNameOffset = 0x00;
    public const int TrainerNameLength = 7;

    /// <summary>Trainer ID offset (4 bytes: low 16 = public, high 16 = secret).</summary>
    public const int TrainerIdOffset = 0x0A;

    /// <summary>Play time offset (hours: 2 bytes, minutes: 1, seconds: 1).</summary>
    public const int PlayTimeOffset = 0x0E;

    // --- Party Pokémon ---
    /// <summary>Section containing party data.</summary>
    public const int PartySectionId = 1;

    /// <summary>Party count offset within the party section.</summary>
    public const int PartyCountOffset_RS = 0x0234;
    public const int PartyCountOffset_E = 0x0234;
    public const int PartyCountOffset_FRLG = 0x0034;

    /// <summary>Party data start offset within the party section.</summary>
    public const int PartyDataOffset_RS = 0x0238;
    public const int PartyDataOffset_E = 0x0238;
    public const int PartyDataOffset_FRLG = 0x0038;

    // --- Pokémon data structure ---
    /// <summary>Size of a Pokémon in the party (100 bytes: 80 core + 20 battle stats).</summary>
    public const int PartyPokemonSize = 100;

    /// <summary>Size of a Pokémon in the PC (80 bytes: core data only).</summary>
    public const int PcPokemonSize = 80;

    /// <summary>Offset of Personality Value within Pokémon structure.</summary>
    public const int PkmnPersonalityOffset = 0x00;

    /// <summary>Offset of OT ID within Pokémon structure.</summary>
    public const int PkmnOtIdOffset = 0x04;

    /// <summary>Offset of nickname within Pokémon structure (10 bytes).</summary>
    public const int PkmnNicknameOffset = 0x08;
    public const int PkmnNicknameLength = 10;

    /// <summary>Offset of OT name within Pokémon structure (7 bytes).</summary>
    public const int PkmnOtNameOffset = 0x14;

    /// <summary>Offset of the encrypted 48-byte data substructure.</summary>
    public const int PkmnSubstructureOffset = 0x20;

    /// <summary>Size of the encrypted data substructure (48 bytes = 4 blocks × 12 bytes).</summary>
    public const int PkmnSubstructureSize = 48;

    /// <summary>Size of each substructure block (12 bytes).</summary>
    public const int SubstructureBlockSize = 12;

    /// <summary>Offset of the Pokémon checksum (within the unencrypted header, 2 bytes).</summary>
    public const int PkmnChecksumOffset = 0x1C;

    // --- Substructure block internal offsets (relative to block start after decryption) ---
    // Block G (Growth): species, item, experience, PP bonuses, friendship
    public const int SubG_Species = 0x00;       // 2 bytes
    public const int SubG_HeldItem = 0x02;      // 2 bytes
    public const int SubG_Experience = 0x04;    // 4 bytes
    public const int SubG_PpBonuses = 0x08;     // 1 byte
    public const int SubG_Friendship = 0x09;    // 1 byte

    // Block A (Attacks): 4 moves + 4 PPs
    public const int SubA_Move1 = 0x00;         // 2 bytes each
    public const int SubA_Move2 = 0x02;
    public const int SubA_Move3 = 0x04;
    public const int SubA_Move4 = 0x06;
    public const int SubA_Pp1 = 0x08;           // 1 byte each
    public const int SubA_Pp2 = 0x09;
    public const int SubA_Pp3 = 0x0A;
    public const int SubA_Pp4 = 0x0B;

    // Block E (EVs & Condition): 6 EVs + condition values
    public const int SubE_HpEv = 0x00;          // 1 byte each
    public const int SubE_AtkEv = 0x01;
    public const int SubE_DefEv = 0x02;
    public const int SubE_SpeEv = 0x03;
    public const int SubE_SpaEv = 0x04;
    public const int SubE_SpdEv = 0x05;

    // Block M (Miscellaneous): Pokérus, met location, origins, IVs, ability
    public const int SubM_PokerusStatus = 0x00; // 1 byte
    public const int SubM_MetLocation = 0x01;   // 1 byte
    public const int SubM_Origins = 0x02;       // 2 bytes (contains met level, ball, OT gender)
    public const int SubM_IvsAbility = 0x04;    // 4 bytes (packed IVs + egg/ability flags)

    /// <summary>Maximum party size.</summary>
    public const int MaxPartySize = 6;

    /// <summary>Number of PC boxes in Gen 3.</summary>
    public const int PcBoxCount = 14;

    /// <summary>Pokémon per PC box.</summary>
    public const int PcBoxSlotCount = 30;
}
