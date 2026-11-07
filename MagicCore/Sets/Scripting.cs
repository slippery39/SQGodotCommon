using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Sets;

/// <summary>
/// The helpers every set's Cards file scripts with (<c>using static MagicCore.Sets.Scripting;</c>) — moved
/// here from FraCards as TRK needed them (MC-5.3). A helper only one set uses stays in its Cards file.
/// </summary>
public static class Scripting
{
	/// <summary>"Target [filter]" — a permanent: Target(Is.Creature &amp; Is.Yours) is "target creature you control".</summary>
	public static TargetSpec Target(Filter? filter = null) => TargetSpec.Permanent(filter);

	public static readonly TargetSpec Creature = TargetSpec.Creature;

	/// <summary>Threshold — "as long as there are seven or more cards in your graveyard".</summary>
	public static readonly AtLeast Threshold = new(Count.NumberOf(Ref.CardsInYourGraveyard), 7);

	/// <summary>"Enters tapped unless you control two or more other lands" (FRA's slow lands, TRK's enters-untapped lands).</summary>
	public static readonly Condition YouControlTwoOrMoreOtherLands = new AtLeast(
		Count.NumberOf(Ref.Permanents(Is.Land & Is.Yours & !Is.Source)),
		2
	);

	/// <summary>"Permanent card" / "nonland permanent card". No set here has battles yet.</summary>
	public const CardType NonlandPermanentTypes =
		CardType.Creature | CardType.Artifact | CardType.Enchantment | CardType.Planeswalker;
	public const CardType PermanentTypes = NonlandPermanentTypes | CardType.Land;

	/// <summary>"A nonland card": every card type but land.</summary>
	public const CardType NonlandTypes =
		NonlandPermanentTypes
		| CardType.Instant
		| CardType.Sorcery
		| CardType.Kindred
		| CardType.Battle;

	public static readonly TargetSpec CreatureOrPlaneswalker = TargetSpec.CreatureOrPlaneswalker;

	public static SpellEffectsComponent Spell(TargetSpec[] targets, params GameAction[] effects) =>
		new([.. effects]) { Targets = [.. targets] };

	public static TriggeredAbilityComponent When(
		Trigger on,
		TargetSpec[] targets,
		params GameAction[] effects
	) => new(on, [.. effects]) { Targets = [.. targets] };

	/// <summary>A Saga's chapter: "II — You gain 3 life." (Amok Time) — triggers as its lore counters reach N.</summary>
	public static TriggeredAbilityComponent Chapter(
		int n,
		TargetSpec[] targets,
		params GameAction[] effects
	) =>
		When(
			new Trigger(EventKind.CountersPut) { Subject = Filters.Is.Source, Chapter = n },
			targets,
			effects
		);

	/// <summary>"{T}: Add one mana of any color." — five abilities, one per colour, sharing a restriction.</summary>
	public static GameComponent[] AnyColorMana(ManaRestriction restriction) =>
		[
			.. ManaAbilities.AnyColor.Select(c => new ManaAbilityComponent(c)
			{
				Restriction = restriction,
			}),
		];

	/// <summary>
	/// Cycling (CR 702.29) — Decree of Pain: "Cycling {3}{B}{B} ({3}{B}{B}, Discard this card: Draw a card.)" —
	/// its keyword named, so "when you cycle" sees it (Trigger.Ability).
	/// </summary>
	public static ActivatedAbilityComponent Cycling(string cost) =>
		new(ManaCost.Parse(cost), [new DrawCards(Ref.You, 1)])
		{
			Costs = [new DiscardSelfCost()],
			From = ZoneType.Hand,
			Keyword = "Cycling",
		};

	/// <summary>
	/// Landcycling — cycling that searches instead of drawing (CR 702.29e): "Basic landcycling {2} ({2}, Discard
	/// this card: Search your library for a basic land card, reveal it, put it into your hand, then shuffle.)"
	/// is Cycling("{2}", BasicLand).
	/// </summary>
	public static ActivatedAbilityComponent Cycling(string cost, SearchLibrary instead) =>
		Cycling(cost) with
		{
			Effects = [instead],
		};

	/// <summary>
	/// Landwalk (CR 702.14) — Thada Adel's "Islandwalk (This creature can't be blocked as long as defending
	/// player controls an Island.)" is Landwalk("Island"): the plain "can't be blocked" under a condition, no
	/// combat rule of its own. The defending player is the opponent (two players).
	/// </summary>
	public static StaticEffectComponent Landwalk(string landType) =>
		new(Is.Source, Combat.CantBeBlocked)
		{
			If = new AtLeast(
				Count.NumberOf(Ref.Permanents(Is.Land & Is.Opponents & Is.Subtype(landType))),
				1
			),
		};

	/// <summary>
	/// Fear (CR 702.36) — Shizo: "gains fear until end of turn. (It can't be blocked except by artifact creatures
	/// and/or black creatures.)" — CantBeBlockedBy the rest; granted with ModifyUntilEndOfTurn { Gains = [Fear] }.
	/// </summary>
	public static CantBeBlockedBy Fear =>
		new(Is.Creature & !Is.Artifact & !Is.Color(ManaColor.Black));

	/// <summary>Basic landcycling's search: "a basic land card, reveal it, put it into your hand".</summary>
	public static SearchLibrary BasicLand =>
		new(CardType.Land, ZoneType.Hand) { Supertype = Supertype.Basic, Reveal = true };

	/// <summary>
	/// "Ward {N} (Whenever this creature becomes the target of a spell or ability an opponent controls,
	/// counter it unless that player pays {N}.)" "It" is Ref.EventObject — the targeting spell or ability.
	/// </summary>
	public static TriggeredAbilityComponent Ward(string cost) =>
		When(
			TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
			[],
			new CounterUnlessPays(Ref.EventObject, ManaCost.Parse(cost))
		);

	/// <summary>"Equip {N} ({N}: Attach to target creature you control. Equip only as a sorcery.)"</summary>
	public static ActivatedAbilityComponent Equip(string cost) =>
		new(ManaCost.Parse(cost), [new AttachSourceTo(Ref.Target())])
		{
			SorcerySpeed = true,
			Targets = [Target(Is.Creature & Is.Yours)],
		};

	/// <summary>"Equipped/enchanted creature gets +P/+T."</summary>
	public static StaticEffectComponent AttachedGets(int power, int toughness) =>
		new(Is.AttachedToSource) { Power = power, Toughness = toughness };

	/// <summary>One bullet: its target words and effects. Its text is filled in from the card.</summary>
	public static Mode M(TargetSpec[] targets, params GameAction[] effects) =>
		new("", [.. targets], [.. effects]);

	/// <summary>"Choose one/two —" spell. The modes must match the printed bullets one for one.</summary>
	public static SpellEffectsComponent ModalSpell(
		string name,
		ModeChoice choose,
		params Mode[] modes
	) => new([]) { Modes = Printed(name, modes), Choose = choose };

	/// <summary>"When …, choose one —" — Ertai Resurrected's "choose up to one —" is new() { AtLeast = 0 }.</summary>
	public static TriggeredAbilityComponent ModalWhen(
		string name,
		Trigger on,
		ModeChoice choose,
		params Mode[] modes
	) => new(on, []) { Modes = Printed(name, modes), Choose = choose };

	/// <summary>Names each mode with its printed bullet — and refuses a script whose count differs.</summary>
	public static ImmutableList<Mode> Printed(string name, Mode[] modes)
	{
		var bullets = (CardRegistry.DataOf(name).OracleText ?? "")
			.Split('\n')
			.Where(l => l.StartsWith('•'))
			.ToList();
		if (bullets.Count != modes.Length)
			throw new InvalidOperationException(
				$"{name}: {modes.Length} modes scripted, {bullets.Count} printed"
			);
		return [.. modes.Select((m, i) => m with { Text = bullets[i] })];
	}
}
