using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// How often a card is OFFERED, not how strong it is — though the two had better agree.
///
/// **Rarity weights the bag; it never gates a floor.** A rare can turn up on floor 1, and that is
/// the point: an early rare you get to build the rest of the run around is where a memorable run
/// comes from. Slay the Spire does exactly this, and a floor gate would trade those runs away for
/// a tidier difficulty curve.
/// </summary>
public enum DoomRarity
{
	Common = 0,
	Uncommon,
	Rare,
}

/// <summary>
/// A card as it exists in the RUN deck — the definition, not an instance in a battle.
///
/// Deliberately NOT a GameObject. The run lives outside GameState entirely, so a run card carries
/// no engine identity; <see cref="RunCardId"/> is its own, stable across every battle of the run.
/// That stability is the whole point: a doom transform names deck entries, and battle ids are
/// thrown away when the battle ends.
/// </summary>
public record RunCard
{
	public int RunCardId { get; init; }
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";
	public int Cost { get; init; }

	public bool IsUnit { get; init; }

	/// <summary>
	/// **Exhaust: this card leaves the battle when played, instead of going to Discard.**
	///
	/// Combat v3 discards your hand every turn and reshuffles Discard the moment Draw runs dry, so
	/// a small deck is seen over and over — a 1-cost "gain 12 life" came back roughly every other
	/// turn and healing stopped being a decision. Found in a playtest: *"Field Dressing needs
	/// exhaust."*
	///
	/// **Battle scope. The run deck is untouched** and the card is back next fight, because every
	/// battle is built fresh from `Run.Deck`. Only a doom transform may remove a card from a run,
	/// and that rule is not being bent for this.
	/// </summary>
	public bool Exhausts { get; init; }

	/// <summary>
	/// **Devour: the unit this replaces dies instead of leaving.**
	///
	/// Playing into a held lane already discards the occupant with no refund, and the doc is
	/// explicit that *replaced is not dead*. Devour flips exactly that one bit — and it is the
	/// whole keyword, because a death feeds Ash, Zombie and every `Loss` read in the pool while a
	/// discard feeds nothing.
	///
	/// **A sacrifice outlet that needs no targeting**: you choose what to eat by choosing where to
	/// stand, which is the only decision this game asks for anywhere.
	/// </summary>
	public bool Devours { get; init; }

	public int Power { get; init; }
	public int Toughness { get; init; }

	/// <summary>
	/// Marks left by apocalypses — "Zombie", "Irradiated". Free-form so a new scenario needs no
	/// engine change, and readable by reward weighting so the game can offer you answers to the
	/// doom you actually took.
	/// </summary>
	public ImmutableHashSet<string> Tags { get; init; } =
		ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// What the card DOES, beyond being a body. Empty for a plain unit.
	///
	/// Lives on the run card because it is part of the card's definition, and it is copied onto the
	/// battle card each battle — the same way Power and Toughness are.
	/// </summary>
	public ImmutableList<DoomEffect> Effects { get; init; } = ImmutableList<DoomEffect>.Empty;

	/// <summary>
	/// Which act this card belongs to, or null for the shared core every act draws from.
	///
	/// **Not a <see cref="Tags"/> entry, deliberately.** Tags are marks an apocalypse LEAVES, and a
	/// doom transform writes to them — a card could gain a theme by being irradiated. Identity and
	/// damage do not belong in the same field.
	/// </summary>
	public DoomTheme? Theme { get; init; }

	/// <summary>
	/// How often this card is offered. See <see cref="DoomRarity"/> — it is a WEIGHT, not a gate,
	/// and <see cref="StarterContent.WeightOf"/> is where the weights live.
	/// </summary>
	public DoomRarity Rarity { get; init; } = DoomRarity.Common;

	public bool HasTag(string tag) => Tags.Contains(tag);

	/// <summary>
	/// This card as it enters a battle.
	///
	/// **One conversion, used everywhere.** `Run.StartBattle` builds the deck with it, and the front
	/// end needs the same thing to DRAW a card outside a battle — the reward screen offers cards
	/// that are not in play yet, and the preview scene draws cards with no battle at all. Written
	/// twice, the two would eventually disagree about what a run card becomes, and the surface
	/// nobody was looking at would be the one that was wrong.
	///
	/// The returned card has no GameState id: it gets one from `AddObject` when it is really added,
	/// and display code never needs one.
	/// </summary>
	public DoomCard ToDoomCard()
	{
		var card = new DoomCard
		{
			Name = Name,
			Description = Description,
			Cost = Cost,
			RunCardId = RunCardId,
			Tags = Tags,
			Effects = Effects,
			Exhausts = Exhausts,
			Devours = Devours,
		};

		return IsUnit
			? (DoomCard)
				card.WithComponent(new UnitComponent { Power = Power, Toughness = Toughness })
			: card;
	}
}
