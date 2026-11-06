using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 7c: TRK's last five — abilities gained for good (Nog, Q), a name chosen as it enters and an
/// "ability activated" event (Kai Winn), an extra turn (H.M.S. Bounty), phasing (The City on the Edge of
/// Forever). Each new piece against its card's PRINTED text, through real casting.
/// </summary>
public class TrkBatch7cTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	int OnTop(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(player, ZoneType.Library)); // TEST SETUP: index 0 is the top
		return id;
	}

	int InGraveyard(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Graveyard);
		return id;
	}

	/// <summary>Answers a choice of type T with these ids; any other with its minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> On<T>(params int[] ids)
		where T : ChoiceAction =>
		c =>
			c switch
			{
				T => ids,
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	int UntappedLands(int player) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);

	void Activate(int card, int index)
	{
		s = s.Act(Activations(card).First(a => a.AbilityIndex == index));
		Resolve();
	}

	// ===== Nog: types, base P/T, a trigger and statics gained for good =====

	[Test]
	public void Nog_BecomesAScout_ThenAnOfficer_AndKeepsWhatHeGained()
	{
		var nog = Field(Trk("Nog, Ambitious Cadet"), me);
		var swamp = InGraveyard(TestGames.Card("Swamp"), me);
		var forest = InGraveyard(TestGames.Card("Forest"), me);

		Activate(nog, 1); // {3}: "If Nog is a Scout" — he is a Rogue, so nothing
		Assert.That(
			s.HasSubtype(s.GetCard(nog), "Officer"),
			Nu.False,
			"{3} does nothing to a Rogue"
		);

		Activate(nog, 0); // {1}: "he becomes a Ferengi Scout with base power and toughness 3/4"
		var scout = s.GetCard(nog);
		Assert.That(
			s.Subtypes(scout),
			Nu.EquivalentTo(new[] { "Ferengi", "Scout" }),
			"a Ferengi Scout"
		);
		Assert.That((s.Power(scout), s.Toughness(scout)), Nu.EqualTo((3, 4)));
		Assert.That(
			s.LegalActions().OfType<PlayLandAction>().Any(p => p.CardId == swamp),
			Nu.False,
			"a Scout can't play lands from the graveyard yet"
		);

		// "Whenever Nog becomes tapped, surveil 2."
		var top = OnTop(TestGames.Card("Plains"), me);
		s = s.Tap(nog);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseFromLibraryTopChoice);
		s = s.Choose([top]);
		Assert.That(
			s.IsIn(top, me, ZoneType.Graveyard),
			Nu.True,
			"surveil 2 put the top card away"
		);

		Activate(nog, 1); // {3}: now a Scout — "he becomes a Ferengi Officer and gains …"
		var officer = s.GetCard(nog);
		Assert.That(s.Subtypes(officer), Nu.EquivalentTo(new[] { "Ferengi", "Officer" }));
		Assert.That((s.Power(officer), s.Toughness(officer)), Nu.EqualTo((3, 4)), "still base 3/4");

		// "You may play lands from your graveyard" — not once a turn — "and an additional land".
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = swamp });
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Assert.That(
			Alive(swamp) && Alive(forest),
			Nu.True,
			"two lands from the graveyard in one turn"
		);
	}

	// ===== Q: an exiled card's gained ability — exile a card from your hand, cast a copy paying its cost =====

	[Test]
	public void Q_TheNonlandCardGainsTheGift_ACopyCastPayingItsCost()
	{
		foreach (var c in s.CardsIn(me, ZoneType.Library).ToList())
			s = s.MoveCard(c.Id, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: a library of two
		var plains = OnTop(TestGames.Card("Plains"), me);
		var bear = OnTop(TestGames.Creature("Bear", "2/2"), me);
		var q = Hand(Trk("Q, Stacking the Deck"), me);
		Cast(q, []);
		Assert.That(
			s.IsIn(bear, me, ZoneType.Exile),
			Nu.True,
			"exile until you exile a nonland card"
		);
		Assert.That(
			Activations(bear).Count(),
			Nu.EqualTo(HandCount(me)),
			"one activation per card in hand to exile"
		);

		var fodder = Hand(TestGames.Card("Swamp"), me);
		var untapped = UntappedLands(me);
		s = s.Act(Activations(bear).Single(a => a.ExileId == fodder));
		Resolve(On<YesNoChoice>(1));
		Assert.That(s.IsIn(fodder, me, ZoneType.Exile), Nu.True, "Exile a card from your hand");
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Bear" && c.IsToken),
			Nu.EqualTo(1),
			"the copy was cast and resolved into a token"
		);
		Assert.That(UntappedLands(me), Nu.EqualTo(untapped - 1), "the copy's {1} was paid");
		Assert.That(s.IsIn(bear, me, ZoneType.Exile), Nu.True, "the card itself stays exiled");
	}

	// ===== Kai Winn: a name chosen as she enters; casts, activations and MANA abilities lose 3 =====

	[Test]
	public void KaiWinn_TheNamedCardCast_ActivatedOrTappedForMana_CostsThreeLife()
	{
		var kai = Hand(Trk("Kai Winn, Faithless Leader"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = kai });
		Resolve(c =>
			c switch
			{
				ChooseKeywordChoice k => [k.Keywords.IndexOf("Savvy Profiteer") + 1],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.GetCard(kai).GetComponent<ChosenName>()?.Name, Nu.EqualTo("Savvy Profiteer"));

		var cast = Hand(Trk("Savvy Profiteer"), them);
		var tapper = Field(Trk("Savvy Profiteer"), them);
		var maker = Field(Trk("Savvy Profiteer"), them);
		var other = Hand(TestGames.Creature("Bear", "2/2"), them);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.PreCombatMain && x.GetGame().ActivePlayerId == them
		);

		var life = Life(them);
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = other });
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(life), "another name costs nothing");

		s = s.Act(new CastSpellAction { PlayerId = them, CardId = cast });
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(life - 3), "casts a spell with the chosen name");

		s = s.Act(
			new TapForManaAction
			{
				PlayerId = them,
				CardId = tapper,
				Color = ManaColor.Red,
			}
		);
		Resolve();
		Assert.That(
			Life(them),
			Nu.EqualTo(life - 6),
			"{T}: Add {R} — a mana ability is an ability"
		);

		s = s.Act(Activations(maker).Single());
		Resolve();
		Assert.That(Life(them), Nu.EqualTo(life - 9), "{3}, {T}: Create a Treasure token");

		// The payment taps a Profiteer for a cast once nothing else is left: a mana ability all the same.
		var payer = Field(Trk("Savvy Profiteer"), them);
		foreach (
			var land in s.CardsIn(them, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.ToList()
		)
			s = s.Tap(land.Id); // TEST SETUP: only the Profiteer can pay
		var bear = Hand(TestGames.Creature("Bear", "2/2"), them);
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = bear });
		Resolve();
		Assert.That(s.GetCard(payer).IsTapped, Nu.True);
		Assert.That(Life(them), Nu.EqualTo(life - 12), "tapped for mana by the payment");
	}

	// ===== H.M.S. Bounty: an extra turn, once a game; summoning sickness after the opponent's extra turn =====

	[Test]
	public void HmsBounty_WithAWhale_TakesOneExtraTurnAGame()
	{
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 3 && x.GetGame().Step == Step.PreCombatMain
		);
		var bounty = Field(Trk("H.M.S. Bounty, Unlikely Timeship"), me);
		s = s.UpdateObject(bounty, s.GetCard(bounty) with { ChargeCounters = 8 }); // TEST SETUP: a creature at 8+
		Field(Trk("Humpback Whales"), me);
		var theirs = Field(TestGames.Creature("Bear", "2/2"), them);
		s = s.UpdateObject(theirs, s.GetCard(theirs) with { ControlledSinceTurn = 2 }); // cast on their turn 2

		void HitThem()
		{
			s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
			while (s.GetPendingChoice() is DeclareAttackerChoice)
				s = s.Choose([them]);
			s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		}

		var hand = HandCount(me);
		HitThem();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "draw a card");
		Assert.That(
			s.GetGame().ExtraTurns,
			Nu.EqualTo(new[] { me }),
			"take an extra turn after this one"
		);

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 4 && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(me), "turn 4 is mine again");
		Assert.That(s.GetPlayer(me).BegunExtraTurn, Nu.True);
		Assert.That(
			Combat.IsSummoningSick(s, s.GetCard(theirs)),
			Nu.True,
			"their most recent turn is 2, not 3: still sick in my extra turn"
		);

		HitThem();
		Assert.That(
			s.GetGame().ExtraTurns,
			Nu.Empty,
			"haven't begun an extra turn this game — now you have"
		);
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == 5);
		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(them));
	}

	// ===== The City on the Edge of Forever: phasing, held by the Human, back in each controller's untap step =====

	[Test]
	public void TheCity_PhasesOutArtifactsAndCreatures_UntilTheHumanIsGone()
	{
		var mine = Field(TestGames.Creature("Bear", "2/2"), me);
		s = s.PutCounters(mine, CounterKinds.PlusOne, 1);
		var rock = Field(TestGames.Inline("Rock", "{0}", "Artifact"), them);
		var theirs = Field(TestGames.Creature("Bear", "2/2"), them);
		var incarnation = s.GetCard(mine).Incarnation;
		var lands = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));

		Cast(Hand(Trk("The City on the Edge of Forever"), me), []);
		Assert.That(
			Alive(mine) || Alive(theirs) || Alive(rock),
			Nu.False,
			"I — all artifacts and creatures phase out"
		);
		Assert.That(
			s.IsIn(mine, me, ZoneType.PhasedOut) && s.IsIn(theirs, them, ZoneType.PhasedOut),
			Nu.True
		);
		Assert.That(
			s.GetCard(mine).Incarnation,
			Nu.EqualTo(incarnation),
			"not a zone change: the same object"
		);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land)),
			Nu.EqualTo(lands)
		);
		var human = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Human");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 2 && x.GetGame().Step == Step.PreCombatMain
		);
		Assert.That(Alive(theirs), Nu.False, "Permanents can't phase in");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 5 && x.GetGame().Step == Step.PostCombatMain
		);
		Assert.That(Alive(human.Id), Nu.False, "III — sacrifice a creature token");
		Assert.That(Life(me), Nu.EqualTo(20 - 2 + 5), "II — each player loses 2; III — you gain 5");
		Assert.That(Life(them), Nu.EqualTo(20 - 2));
		Assert.That(Alive(mine), Nu.False, "my untap step came before the Human left");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 6 && x.GetGame().Step == Step.Upkeep
		);
		Assert.That(
			Alive(theirs) && Alive(rock),
			Nu.True,
			"theirs phase in during THEIR untap step"
		);
		Assert.That(Alive(mine), Nu.False);

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 7 && x.GetGame().Step == Step.Upkeep
		);
		Assert.That(Alive(mine), Nu.True, "mine during mine");
		Assert.That(s.GetCard(mine).PlusOneCounters, Nu.EqualTo(1), "its counters kept");
	}

	[Test]
	public void KaiWinn_TheAiNames_AnActivatedAbilityInPlay_ThenAKnownHandCard_ThenACommonInTheirColours()
	{
		// Shayne, 2026-11-06 (limited): step by step.
		Hand(Trk("Kai Winn, Faithless Leader"), me); // TRK in the game: its names are offered
		var names = CardRegistry.NonlandNames(s);
		var choice = new ChooseKeywordChoice(me, names) { NamesACard = true };
		string Named() => names[new MagicCore.Ai.GreedyAi(me).Answer(s, choice).Single() - 1];

		var profiteer = Field(Trk("Savvy Profiteer"), them); // "{T}: Add {R}." / "{3}, {T}: Create a Treasure"
		Field(Trk("Kai Winn, Faithless Leader"), them);
		Assert.That(
			Named(),
			Nu.EqualTo("Savvy Profiteer"),
			"a card with an activated ability in play"
		);

		s = s.MoveCard(profiteer, s.ZoneId(them, ZoneType.Graveyard));
		var known = Hand(Trk("Q, Stacking the Deck"), them);
		s = s.UpdateObject(known, s.GetCard(known) with { KnownTo = Knowers.Everyone }); // seen by a card's effect
		Hand(Trk("H.M.S. Bounty, Unlikely Timeship"), them); // not seen
		Assert.That(
			Named(),
			Nu.EqualTo("Q, Stacking the Deck"),
			"if none: a card known to be in their hand"
		);

		s = s.MoveCard(known, s.ZoneId(them, ZoneType.Graveyard));
		foreach (
			var land in s.CardsIn(them, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land) && c.Name != "Island")
				.ToList()
		)
			s = s.MoveCard(land.Id, s.ZoneId(them, ZoneType.Graveyard)); // TEST SETUP: they show only blue
		var common = Named();
		Assert.That(
			CardRegistry
				.Sets.SelectMany(CardRegistry.Printings)
				.Any(p => p.Card.Name == common && p.Rarity == "common" && p.Card.Colors is ["U"]),
			Nu.True,
			$"if none: a common in a colour their lands show — named {common}"
		);
	}
}
