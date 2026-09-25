using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// A card. Behaviour comes from components — <see cref="UnitComponent"/> makes it a unit that can
/// be played to the Field and fight; without one it is a Rite that resolves and goes to Discard.
/// </summary>
public record KinCard : GameObject
{
	/// <summary>Energy to play. There is no mana, no lands and no colours — see KinJam.md.</summary>
	public int Cost { get; init; }

	/// <summary>
	/// Identity of this card in the RUN deck, stable across battles.
	///
	/// **The thing that forced this — a doom transform rewriting the run deck from what happened in
	/// a battle — is deleted, and the field is still load-bearing.** Battle objects get fresh
	/// GameState ids every battle, so anything that has to name a card ACROSS battles names it by
	/// this instead: `KinBattle.DiedLastTurnRunCardIds` and `DiedThisTurnRunCardIds` are what the
	/// attrition reads count, and they count run card ids.
	/// </summary>
	public int RunCardId { get; init; }

	/// <summary>
	/// What this card does when it is played or dies. Copied from the run card.
	///
	/// A card with no <see cref="UnitComponent"/> and no effects does NOTHING — it costs energy and
	/// goes to Discard. That is an authoring mistake, and `PlayCardAction` refuses it rather than
	/// letting it look like a card that worked.
	/// </summary>
	public ImmutableList<KinEffect> Effects { get; init; } = ImmutableList<KinEffect>.Empty;

	/// <summary>Leaves the battle when played rather than going to Discard. See <see cref="RunCard.Exhausts"/>.</summary>
	public bool Exhausts { get; init; }

	/// <summary>**Devour** — the unit this replaces dies instead of leaving. See <see cref="RunCard.Devours"/>.</summary>
	public bool Devours { get; init; }

	/// <summary>
	/// **THE COMPANION GAME: the monster whose deck this card came from** (its `Ally` id). The card
	/// is in play only while that monster fights. 0 = the trainer's own card.
	/// </summary>
	public int OwnerId { get; init; }

	/// <summary>Its owner's name, for the card face — which sees the card and nothing else. "" = none.</summary>
	public string OwnerName { get; init; } = "";
}
