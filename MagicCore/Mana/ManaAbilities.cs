using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;

namespace MagicCore;

/// <summary>Which permanents make mana, and when they may. The one place both are answered.</summary>
public static class ManaAbilities
{
	public static IEnumerable<ManaColor> ColorsOf(GameState s, Card card)
	{
		// A basic land type's mana ability is intrinsic (CR 305.6) — an ability all the same.
		var intrinsic = card.Subtypes.Select(BasicLandColor).OfType<ManaColor>();
		if (intrinsic.Any() && s.HasLostAllAbilities(card))
			return [];
		return intrinsic
			.Concat(
				AbilitiesOf(s, card)
					.Where(m => m.IsPlain)
					.SelectMany(m => m.AnyOneColor ? AnyColor : [m.Color])
			)
			.Concat(s.AbilitiesOf<ChosenColorMana>(card).Select(m => m.Color))
			.Distinct();
	}

	/// <summary>THE list of a permanent's mana abilities: its own, then those granted to it (Rok-Tahk, Rishkar).</summary>
	public static IEnumerable<ManaAbilityComponent> AbilitiesOf(GameState s, Card card)
	{
		var own = s.AbilitiesOf<ManaAbilityComponent>(card);
		var granted = Granted(s, card);
		return granted is null ? own : own.Concat(granted);
	}

	/// <summary>The mana abilities granted to it; null (no allocation) for the usual none — payment asks per source.</summary>
	static List<ManaAbilityComponent>? Granted(GameState s, Card card)
	{
		List<ManaAbilityComponent>? granted = null;
		foreach (var (g, grant) in Grants(s))
			if (grant.Affects.Matches(s, card, new Filters.FilterContext(g.ControllerId, g.Id)))
				(granted ??= []).Add(grant.Ability);
		return granted is null || s.HasLostAllAbilities(card) ? null : granted;
	}

	[ThreadStatic]
	static GameState? grantsOf;

	[ThreadStatic]
	static List<(Card, GrantsManaAbility)>? grants;

	/// <summary>
	/// Every "grants a mana ability" on the battlefield (a gained one too), listed once per state: payment asks
	/// per source, and a scan per source made ManaPayment.Find quadratic in permanents — 72 Troubling Tribbles
	/// with a Station's one move per creature took minutes a step. A state never changes, so its list doesn't.
	/// ponytail: holds each thread's last state alive until it asks again; a ConditionalWeakTable if that matters.
	/// </summary>
	static List<(Card, GrantsManaAbility)> Grants(GameState s)
	{
		if (ReferenceEquals(grantsOf, s) && grants is not null)
			return grants;
		var found = new List<(Card, GrantsManaAbility)>();
		foreach (var player in (int[])[s.Player1Id(), s.Player2Id()])
		foreach (var g in s.CardsIn(player, ZoneType.Battlefield))
		foreach (var grant in s.AbilitiesOf<GrantsManaAbility>(g)) // cheap on none — a gained grant too
			found.Add((g, grant));
		(grantsOf, grants) = (s, found);
		return found;
	}

	/// <summary>Plains, Island, Swamp, Mountain, Forest — domain counts these.</summary>
	public static bool IsBasicLandType(string subtype) => BasicLandColor(subtype) is not null;

	static ManaColor? BasicLandColor(string subtype) =>
		subtype switch
		{
			"Plains" => ManaColor.White,
			"Island" => ManaColor.Blue,
			"Swamp" => ManaColor.Black,
			"Mountain" => ManaColor.Red,
			"Forest" => ManaColor.Green,
			_ => null,
		};

	/// <summary>White, blue, black, red, green — "any color" (never colorless).</summary>
	public static readonly ImmutableArray<ManaColor> AnyColor =
	[
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green,
	];

	/// <summary>
	/// What tapping it for mana makes: one unit per mana, each with the colours it may be.
	/// SameColor: every unit the one colour chosen (Loot, the Nexus). Restriction: what it may pay
	/// for. Sacrificed: it is sacrificed as it makes the mana (Treasure). A Mountain makes an extra
	/// {R} per Molten Tide this turn.
	/// </summary>
	public record Yield(
		ImmutableArray<ImmutableArray<ManaColor>> Units,
		bool SameColor,
		ManaRestriction Restriction,
		bool Sacrificed = false
	)
	{
		/// <summary>May this mana pay for that spell (null: an ability, or anything but a spell)?</summary>
		public bool CanPayFor(GameState s, Card? spell, Card? abilitySource = null) =>
			Allows(Restriction, s, spell, abilitySource);
	}

	/// <summary>THE restriction check — a permanent's mana and floating mana alike.</summary>
	/// <param name="abilitySource">An activated ability's source, when the mana pays for one — Isolinear Processor asks.</param>
	public static bool Allows(
		ManaRestriction restriction,
		GameState s,
		Card? spell,
		Card? abilitySource = null
	) =>
		restriction switch
		{
			ManaRestriction.OnlyArtifacts => (spell ?? abilitySource)?.Is(CardType.Artifact)
				== true,
			ManaRestriction.NotForSpellsFromHand => spell is null
				|| !s.IsIn(spell.Id, spell.OwnerId, ZoneType.Hand),
			ManaRestriction.OnlyPlaneswalkerSpells => spell is not null
				&& spell.Is(CardType.Planeswalker),
			ManaRestriction.OnlyNoncreatureSpells => spell is not null
				&& !spell.Is(CardType.Creature),
			_ => true,
		};

	/// <summary>THE answer to "what does tapping this make" — payment, manual tapping and the X bound all ask.</summary>
	public static Yield YieldOf(GameState s, Card card)
	{
		// Emrakul's grant: "{T}: Add {C}{C}" while that card is still the one in exile. ponytail: it
		// replaces the land's own ability instead of being a second choice — the yield is one per
		// permanent; make it a choice if the colour ever matters while Emrakul waits.
		if (
			card.GetComponent<TapsForTwoColorlessWhileExiled>() is { } grant
			&& s.HasObject(grant.CardId)
			&& s.GetObject(grant.CardId) is Card waiting
			&& waiting.Incarnation == grant.Incarnation
			&& s.IsIn(waiting.Id, waiting.OwnerId, ZoneType.Exile)
		)
			return new(
				[
					[ManaColor.Colorless],
					[ManaColor.Colorless],
				],
				false,
				ManaRestriction.None
			);
		var colors = ColorsOf(s, card).ToImmutableArray();
		if (colors.IsEmpty)
			return new([], false, ManaRestriction.None);
		var context = Conditions.Of(card);
		// ponytail: one yield per permanent — the plain ability making the most (Loot's N, Rok-Tahk's
		// {G}{G}, a Treasure's colour), else one unit of every colour its one-mana abilities make; so a
		// creature with its own {G} granted {G}{G} makes only the {G}{G}. Make it a choice if that ever matters.
		ManaAbilityComponent? first = null;
		ManaAbilityComponent? oneColor = null;
		var most = 0;
		foreach (var m in AbilitiesOf(s, card)) // a loop, not LINQ: payment asks per source
		{
			if (!m.IsPlain)
				continue;
			first ??= m;
			if (!m.AnyOneColor && m.Amount is { Kind: CountOf.Fixed, Fixed: 1 })
				continue;
			var n = Math.Max(0, m.Amount.Value(s, context));
			if (oneColor is null || n > most)
				(oneColor, most) = (m, n);
		}
		ImmutableArray<ImmutableArray<ManaColor>> units = oneColor is null
			? [colors]
			:
			[
				.. Enumerable.Repeat<ImmutableArray<ManaColor>>(
					oneColor.AnyOneColor ? AnyColor : [oneColor.Color],
					most
				),
			];
		// Molten Tide: "whenever you tap a Mountain for mana, add an additional {R}" — a mana
		// ability triggering on a mana ability, so it resolves at once (CR 605.1b), as more mana.
		if (card.Subtypes.Contains("Mountain"))
			units =
			[
				.. units,
				.. Enumerable.Repeat<ImmutableArray<ManaColor>>(
					[ManaColor.Red],
					s.ThisTurnEffects<Effects.ExtraRedFromMountains>(card.ControllerId).Count()
				),
			];
		// ponytail: one restriction per permanent — all its mana abilities share it (Heartwood
		// Crafter's {C}, Gideon's Memorial's five colours). Track it per unit if a card mixes them.
		var restriction =
			first is not null && !card.Subtypes.Any(IsBasicLandType)
				? (oneColor ?? first).Restriction
				: ManaRestriction.None;
		return new(
			units,
			oneColor?.AnyOneColor == true,
			restriction,
			oneColor?.SacrificesItself == true
		);
	}

	/// <summary>{T} in ANY cost: untapped, and a creature must not be summoning sick (CR 302.6).</summary>
	public static bool CanPayTapCost(GameState s, Card card) =>
		!card.IsTapped
		&& (
			!card.Is(CardType.Creature) || s.Has(card, "Haste") || !Combat.IsSummoningSick(s, card)
		);

	public static bool CanTap(GameState s, Card card) =>
		ColorsOf(s, card).Any()
		&& CanPayTapCost(s, card)
		&& (!card.IsToken || s.CanSacrifice(card.Id)); // Quark: a Treasure's only mana is sacrificing it

	/// <summary>Permanents that can make mana for this player right now, optionally leaving one out
	/// (the permanent whose {T} is itself the cost being paid).</summary>
	public static IEnumerable<Card> Sources(GameState s, int playerId, int excludeId = 0) =>
		s.CardsIn(playerId, ZoneType.Battlefield).Where(c => c.Id != excludeId && CanTap(s, c));
}
