using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;

namespace MagicCore.Tests.Ai;

/// <summary>
/// AI-29.3 (Shayne, 2026-11-19): which land to play — curated scenarios, written up front "so that we aren't solely
/// relying on in game scenarios" to find a basic one not working. The common cases first, edge cases after. Each asks
/// the default search what it does in a position built to be the scenario.
/// </summary>
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class LandOrderTests
{
	GameState s = null!;
	int me;

	static readonly CardDefinition RedOne = TestGames.CreatureCosting("{R}", "Red One", "1/1");
	static readonly CardDefinition RedTwo = TestGames.CreatureCosting("{1}{R}", "Red Two", "2/2");
	static readonly CardDefinition WhiteTwo = TestGames.CreatureCosting(
		"{W}{W}",
		"White Two",
		"2/3"
	);
	static readonly CardDefinition GreyThree = TestGames.CreatureCosting(
		"{3}",
		"Grey Three",
		"3/3"
	);

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
	}

	int Put(CardDefinition card, ZoneType zone)
	{
		(s, var id) = TestGames.Put(s, me, card, zone);
		return id;
	}

	int Put(string name, ZoneType zone) => Put(CardRegistry.Get(name), zone);

	/// <summary>Our library becomes exactly these cards — the deck the rule reads its colours from.</summary>
	void Library(params (CardDefinition Card, int Count)[] cards)
	{
		foreach (var c in s.CardsIn(me, ZoneType.Library).ToList())
			s = s.RemoveObject(c.Id);
		foreach (var (card, n) in cards)
			for (var i = 0; i < n; i++)
				Put(card, ZoneType.Library);
	}

	/// <summary>A white-red deck: half lands, the spells of both colours.</summary>
	void WhiteRedDeck(int whiteTwos = 4, int reds = 4) =>
		Library(
			(CardRegistry.Get("Plains"), 8),
			(CardRegistry.Get("Mountain"), 8),
			(WhiteTwo, whiteTwos),
			(RedTwo, reds),
			(GreyThree, 16 - whiteTwos - reds)
		);

	/// <summary>
	/// What the default search does here: the land it plays, or what it did instead. A CONTROL passes
	/// <see cref="Evaluator.Terms.LandOrder"/> as <paramref name="without"/>: the search before the rule still misplays,
	/// so a position that stops provoking it fails loudly instead of passing for nothing.
	/// </summary>
	static string Played(GameState at, Evaluator.Terms without = Evaluator.Terms.None)
	{
		var d = new BeamSearchAi(at.DecidingPlayerId(), new(Without: without)).Decide(at);
		return d.Action is PlayLandAction l ? at.GetCard(l.CardId).Name : $"not a land: {d.What}";
	}

	[Test]
	public void TurnOne_AnUntappedLand_ToPlayTheOneDrop()
	{
		WhiteRedDeck();
		Put("Rockfall Vale", ZoneType.Hand); // enters tapped: no two other lands
		Put("Mountain", ZoneType.Hand);
		Put(RedOne, ZoneType.Hand);

		Assert.That(Played(s), Is.EqualTo("Mountain"));
	}

	[Test]
	public void TurnOne_ATappedLand_WithNoOneDrop()
	{
		WhiteRedDeck();
		Put("Mountain", ZoneType.Hand);
		Put("Rockfall Vale", ZoneType.Hand);
		Put(RedTwo, ZoneType.Hand);

		Assert.That(Played(s), Is.EqualTo("Rockfall Vale"));
	}

	[Test]
	public void TurnOne_APlains_ForTheWhiteDoublePipInHand()
	{
		WhiteRedDeck();
		Put("Mountain", ZoneType.Hand);
		Put("Plains", ZoneType.Hand);
		Put(WhiteTwo, ZoneType.Hand);

		Assert.That(Played(s, Evaluator.Terms.LandOrder), Is.EqualTo("Mountain"), "control");
		Assert.That(Played(s), Is.EqualTo("Plains"));
	}

	[Test]
	public void TurnOne_APlains_ForTheWhiteDoublePipsInTheDeck()
	{
		WhiteRedDeck(whiteTwos: 6, reds: 3);
		Put("Mountain", ZoneType.Hand);
		Put("Plains", ZoneType.Hand);
		Put(GreyThree, ZoneType.Hand);

		Assert.That(Played(s, Evaluator.Terms.LandOrder), Is.EqualTo("Mountain"), "control");
		Assert.That(Played(s), Is.EqualTo("Plains"));
	}

	// Review, 2026-11-19: a land that enters tapped and asks a choice as it enters (Room of Refuge's colour) is a tapped
	// land - the choice pending after the play is no shockland's "pay 2 life".
	[Test]
	public void TurnOne_ATappedLand_ThatAsksAColourAsItEnters()
	{
		WhiteRedDeck();
		Put("Plains", ZoneType.Hand);
		Put("Room of Refuge", ZoneType.Hand);
		Put(WhiteTwo, ZoneType.Hand);

		Assert.That(Played(s), Is.EqualTo("Room of Refuge"));
	}

	// The suite, 2026-11-19 (seed 2 move 2): an X spell for X=0 is no reason to want the mana - counted as a cast, it
	// played a Mountain on turn 1 over a land free to enter tapped. Guards the rule: the search before it was right.
	[Test]
	public void TurnOne_AnXSpell_IsNoReasonForAnUntappedLand()
	{
		WhiteRedDeck();
		Put("Konstrari Annex", ZoneType.Hand); // enters tapped: no planeswalker
		Put("Mountain", ZoneType.Hand);
		Put(TestGames.Inline("Burn X", "{X}{R}", "Sorcery"), ZoneType.Hand);

		Assert.That(Played(s), Is.EqualTo("Konstrari Annex"));
	}

	// The suite, 2026-11-19 (seed 3 move 32, seed 4 move 13): the untapped land that lets the hand spend a turn's mana -
	// counted in spells, a 2-drop tied the 5-drop the Forest makes castable, and the tapped land won. Guards the rule.
	[Test]
	public void AnUntappedLand_ThatCastsTheBigSpell_OverATappedOne()
	{
		WhiteRedDeck();
		foreach (var land in new[] { "Mountain", "Forest", "Mountain", "Forest" })
			Put(land, ZoneType.Battlefield);
		Put("Konstrari Annex", ZoneType.Hand);
		Put("Forest", ZoneType.Hand);
		Put(TestGames.CreatureCosting("{4}{G}", "Big One", "5/5"), ZoneType.Hand);
		Put(RedTwo, ZoneType.Hand);

		Assert.That(Played(s), Is.EqualTo("Forest"));
	}

	// Shayne, 2026-11-13 (ShayneNotesSeed1 #97, AI-9.9): "Nobody is discarding Rockfall Vale in this situation" - the
	// only red source, a Forest beside it. Here: a forced discard of one, Forest in play, Vale and a Forest in hand.
	[Test]
	public void AForcedDiscard_KeepsTheRedSource_BesideAForest()
	{
		WhiteRedDeck();
		Put("Forest", ZoneType.Battlefield);
		var vale = Put("Rockfall Vale", ZoneType.Hand);
		Put("Forest", ZoneType.Hand);
		Put(RedTwo, ZoneType.Hand);
		Put(RedTwo, ZoneType.Hand);
		var discarder = Put(
			TestGames.Inline("Discarder", "{0}", "Sorcery", null, new Discard(Ref.You, 1)),
			ZoneType.Hand
		);
		s = s.Act(s.LegalActions().OfType<CastSpellAction>().First(a => a.CardId == discarder));
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is not null);

		var answer = new BeamSearchAi(me).Decide(s).Answer;

		Assert.That(answer, Is.Not.Null.And.Not.Contains(vale));
	}

	// Shayne, 2026-11-13 (ShayneNotesSeed1 #24): turn 2, a Forest played over Rockfall Vale - "it would actually be
	// better to play the Rockfall Vale": tapped now for free, both colours from next turn.
	[Test]
	public void TurnTwo_RockfallVale_OverAForest_WithNothingToCast()
	{
		var x = KeptRecords.Before("ShayneNotesSeed1", 24);

		Assert.That(Played(x, Evaluator.Terms.LandOrder), Is.EqualTo("Forest"), "control");
		Assert.That(Played(x), Is.EqualTo("Rockfall Vale"));
	}

	// Shayne, 2026-11-18 (AI-9.10, FRA seed 2 #23): Konstrari Annex enters untapped only while we control a
	// planeswalker; Rockfall Vale's two other lands hold from now on. The walker at 1 dies to its own −1, so the Annex
	// goes first - before the −1, and before the Vale.
	[Test]
	public void AnUntappedLand_WhoseConditionCanBreak_IsPlayedFirst()
	{
		WhiteRedDeck();
		foreach (var land in new[] { "Mountain", "Forest", "Mountain", "Forest" })
			Put(land, ZoneType.Battlefield);
		var walker = Put(
			TestGames.Planeswalker(
				"Walker",
				1,
				new LoyaltyAbilityComponent(-1, [new DrawCards(Ref.You, 1)])
			),
			ZoneType.Hand
		);
		s = s.EnterBattlefield(walker, me);
		Put("Rockfall Vale", ZoneType.Hand);
		var annex = Put("Konstrari Annex", ZoneType.Hand);
		Put(TestGames.CreatureCosting("{3}{R}{G}", "Big One", "5/5"), ZoneType.Hand);
		var played = s.Act(new PlayLandAction { PlayerId = me, CardId = annex });

		Assert.Multiple(() =>
		{
			Assert.That(s.GetCard(walker).Loyalty, Is.EqualTo(1), "setup: the walker at 1");
			Assert.That(
				played.GetCard(annex).IsTapped,
				Is.False,
				"setup: the Annex enters untapped now"
			);
			Assert.That(
				Played(s, Evaluator.Terms.LandOrder),
				Is.EqualTo("Rockfall Vale"),
				"control"
			);
			Assert.That(Played(s), Is.EqualTo("Konstrari Annex"));
		});
	}
}
