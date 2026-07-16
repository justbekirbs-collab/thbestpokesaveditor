namespace PokeSaveEditor.Core.Models;

/// <summary>Represents a single move slot on a Pokémon.</summary>
/// <param name="MoveId">The move's internal ID (0 = empty slot).</param>
/// <param name="CurrentPp">Current Power Points remaining.</param>
/// <param name="PpUps">Number of PP Ups applied (0–3).</param>
public readonly record struct Move(ushort MoveId, byte CurrentPp, byte PpUps)
{
    /// <summary>Whether this slot contains a valid move.</summary>
    public bool IsEmpty => MoveId == 0;
}
