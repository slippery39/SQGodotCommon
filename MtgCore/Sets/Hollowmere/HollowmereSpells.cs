using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 7 â€” Spells, and spells in the graveyard. Cantrips, prowess, and cast triggers.
///
/// The bridge theme: cheap spells fill the graveyard for theme 1, make Spirits for theme 6,
/// and turn on threshold. Prowess needs no engine support â€” it is a SpellCast trigger that
/// adds an until-end-of-turn modifier to its own source.
///
/// This theme is also where the format's interaction lives. Removal is at a premium with no
/// blockers â€” a resolved threat stays a threat â€” and most of it is naturally an instant or
/// sorcery, so it lands here rather than in the glue slot.
///
/// Batches 1 and 4: 30 cards.
/// </summary>
public static class HollowmereSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The format's smoothing cantrip, and a graveyard enabler in the same slot.
			CardFactory.Spell("Ponder", manaCost: 1).WithDraw(2).WithMill(1).Build(),
			// Young Pyromancer as a Spirit-maker: one card that serves themes 6 and 7 at once.
			CardFactory
				.Creature("Chapel Conjurer", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Conjure the Choir",
					OnYouCastSpell(),
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// Prowess. Free to model â€” a SpellCast trigger buffing its own source.
			CardFactory
				.Creature("Zealous Penitent", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithHaste()
				.WithTriggeredAbility("Prowess", OnYouCastSpell(), eb => eb.WithProwessBuff())
				.Build(),
			// Snapcaster-tier value: a body that rebuys the best spell in your graveyard.
			CardFactory
				.Creature("Mere-Watch Adept", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Echo of the Mere", eb => eb.WithAutoReturnSpell())
				.Build(),
			// The graveyard-spells payoff. Rebuys a spell now and again later.
			CardFactory
				.Spell("Past in Ashes", manaCost: 4)
				.WithGiveFlashback()
				.WithDraw(1)
				.WithFlashback(6)
				.Build(),
			// ===== BATCH 4 =====
			// A cantrip that comes back â€” two spell-cast triggers from one card.
			CardFactory
				.Spell("Mere-Gaze", manaCost: 1)
				.WithDraw(1)
				.WithMill(2)
				.WithFlashback(3)
				.Build(),
			// Cheap interaction with a second use. Removal is the format's scarcest resource.
			CardFactory
				.Spell("Ashen Bolt", manaCost: 1)
				.WithDamage(2)
				.WithTarget(Single().PlayersOrCreatures())
				.WithFlashback(4)
				.Build(),
			// A body from a spell slot, twice â€” bridges Spells into the go-wide themes.
			CardFactory
				.Spell("Whisper of the Choir", manaCost: 1)
				.WithCreateTokens(HollowmereTokens.Spirit())
				.WithFlashback(3)
				.Build(),
			// Raw card advantage at a small cost.
			CardFactory.Spell("Chapel Rites", manaCost: 2).WithDraw(2).WithLoseLife(1).Build(),
			// Fast mana, which in a spells deck means two spells in a turn instead of one.
			CardFactory.Spell("Silt Ritual", manaCost: 2).WithAddMana(3).Build(),
			// The combat trick, with a second use for the graveyard theme.
			CardFactory
				.Spell("Spectral Surge", manaCost: 2)
				.WithBoost(3, 3)
				.WithTarget(Single().YourCreatures())
				.WithFlashback(3)
				.Build(),
			// Rebuys the best spell in your graveyard and replaces itself.
			CardFactory
				.Spell("Echo of the Drowned", manaCost: 2)
				.WithReturnSpellFromGraveyard()
				.WithDraw(1)
				.Build(),
			// A second cantrip with flashback â€” the spells deck wants many of these.
			CardFactory.Spell("Second Sight", manaCost: 2).WithDraw(2).WithFlashback(4).Build(),
			// Storm. Free to model, and the payoff for a turn full of cheap spells.
			CardFactory
				.Spell("Storm of Silt", manaCost: 1)
				.WithDamage(1)
				.WithTarget(Single().PlayersOrCreatures())
				.WithStorm()
				.Build(),
			// Deep card selection that also feeds the graveyard.
			CardFactory.Spell("Chorus of Whispers", manaCost: 2).WithDraw(3).WithDiscard().Build(),
			// Exile removal â€” the format's only clean answer to the recursion themes.
			CardFactory
				.Spell("Drown the Lantern-Bearer", manaCost: 3)
				.WithExile()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// Efficient burn with a second use.
			CardFactory
				.Spell("Silt Blast", manaCost: 3)
				.WithDamage(4)
				.WithTarget(Single().PlayersOrCreatures())
				.WithFlashback(6)
				.Build(),
			// Removal that advances the graveyard plan at the same time.
			CardFactory
				.Spell("Snuff the Lantern", manaCost: 2)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.WithMill(2)
				.Build(),
			// Recursion plus a card â€” the Spells deck's way into the graveyard theme.
			CardFactory
				.Spell("Grim Improvisation", manaCost: 2)
				.WithReturnCreatureFromGraveyard()
				.WithDraw(1)
				.Build(),
			// Prowess on the cheapest possible body.
			CardFactory
				.Creature("Kindled Zealot", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithTriggeredAbility("Prowess", OnYouCastSpell(), eb => eb.WithProwessBuff())
				.Build(),
			// Every spell you cast digs the graveyard one deeper.
			CardFactory
				.Creature("Mere-Touched Adept", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility("Silt Trickle", OnYouCastSpell(), eb => eb.WithMill(2))
				.Build(),
			// Drains a point per spell â€” the payoff that lets a spells deck win without combat.
			CardFactory
				.Creature("Mere-Bound Ritualist", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Siphon the Rite",
					OnYouCastSpell(),
					eb =>
						eb.WithAction(
							new DrainLifeAction
							{
								Amount = 1,
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// A defensive prowess body â€” Taunt plus a growing power stabilises a board.
			CardFactory
				.Creature("Silt-Bound Cantor", manaCost: 3, power: 1, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTaunt()
				.WithTriggeredAbility("Prowess", OnYouCastSpell(), eb => eb.WithProwessBuff())
				.Build(),
			// Value on a body, and a Human for the tribal decks.
			CardFactory
				.Creature("Hollowmere Savant", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Study the Mere", eb => eb.WithDraw(1))
				.WithTriggeredAbility("Prowess", OnYouCastSpell(), eb => eb.WithProwessBuff())
				.Build(),
			// Big card draw for the decks that can afford to durdle.
			CardFactory.Spell("Tide of Whispers", manaCost: 3).WithDraw(4).WithDiscard().Build(),
			// Rebuys two spells on arrival â€” a four-drop that must two-for-one.
			CardFactory
				.Creature("Drowned Archivist", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Reshelve the Drowned", eb => eb.WithAutoReturnSpell().WithMill(2))
				.Build(),
			// A prowess body that keeps coming back â€” hard for a removal deck to answer.
			CardFactory
				.Creature("Spell-Sworn Revenant", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Spirit)
				.WithGraveyardRecursion(manaCost: 4)
				.WithTriggeredAbility("Prowess", OnYouCastSpell(), eb => eb.WithProwessBuff())
				.Build(),
			// The storm finisher: with a full turn of cheap spells this ends the game.
			CardFactory
				.Spell("Mere-Storm", manaCost: 5)
				.WithDamage(2)
				.WithTarget(Single().PlayersOrCreatures())
				.WithStorm()
				.Build(),
			// A token engine at the top of the curve, for the Spirits crossover.
			CardFactory
				.Creature("Archmage of the Mere", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Summon the Choir",
					OnYouCastSpell(),
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// The sweeper. Answers the go-wide decks, and comes back to do it again.
			CardFactory
				.Spell("The Drowning Chorus", manaCost: 6)
				.WithDamage(3)
				.WithTarget(AllValid().OpponentCreatures())
				.WithDraw(2)
				.WithFlashback(8)
				.Build(),
		];

	/// "Whenever you cast a spell" â€” the theme's shared trigger.
	private static EventTriggerCondition OnYouCastSpell() =>
		new()
		{
			EventTypeName = EventTypeNames.SpellCast,
			Filter = new IsControlledByYouSpecification(),
		};
}
