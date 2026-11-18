using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>
/// Shayne's pricing principles (AI-27, 2026-11-17) as RELATIONS that must hold whatever a card, a body or a point of
/// life is priced at - so a rescale or the three ledgers can be checked against them. "A card" here is the
/// evaluator's own price of drawing an unseen card now (<see cref="Unseen"/>: the search's land-or-not blend), so no
/// number is restated. Those that fail today are <c>[Explicit]</c>: the target of the pricing pass, not a regression.
/// </summary>
public class PricingPrincipleTests
{
	static readonly HeldValues Held = BeamSearchAi.DefaultRollout.Held!;

	GameState s = null!;
	int me,
		them;

	[SetUp]
	public void Position()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	double Eval(GameState x, HeldValues? held = null) => Evaluator.Evaluate(x, me, held ?? Held);

	/// <summary>Our score after drawing <paramref name="n"/> unseen cards, as the search scores it: lands or not, by our land odds.</summary>
	double Unseen(GameState x, int n = 1)
	{
		var odds = Hidden.LandOdds(x, me);
		return odds * Eval(Hidden.Unknown(x, me, lands: true).Draw(me, n))
			+ (1 - odds) * Eval(Hidden.Unknown(x, me, lands: false).Draw(me, n));
	}

	/// <summary>The evaluator's price of one unseen card drawn now, from <paramref name="x"/>.</summary>
	double ACard(GameState x) => Unseen(x) - Eval(x);

	GameState With(GameState x, int player, CardDefinition card, ZoneType zone, out int id)
	{
		(x, id) = TestGames.Put(x, player, card, zone);
		return x;
	}

	GameState Lands(GameState x, int player, int n)
	{
		for (var i = 0; i < n; i++)
			x = With(x, player, TestGames.Card("Forest"), ZoneType.Battlefield, out _);
		return x;
	}

	// "drawing a random card is not punished (since drawing a random card will always get you 1 card deeper in your deck)"
	[Test]
	public void DrawingAnyCard_NeverScoresBelowNotDrawing()
	{
		var late = Lands(s, me, 8);
		foreach (var x in new[] { s, late })
		{
			Assert.That(Unseen(x), Is.GreaterThan(Eval(x)), "an unseen card");
			var top = x.CardsIn(me, ZoneType.Library).First(c => c.Is(CardType.Land));
			var land = x.MoveCard(top.Id, x.ZoneId(me, ZoneType.Hand));
			Assert.That(Eval(land), Is.GreaterThan(Eval(x)), "a land, seen");
		}
	}

	// "if I play a draw 2 and I draw 2 lands, even if it is the late game and the lands are not that valuable, I am
	// still up 2 cards ... It shouldn't matter that my draws happened to be lands"
	[Test, Explicit("AI-27: fails today - two seen lands late are a sliver of two unseen cards")]
	public void ADrawTwoThatHitsTwoLands_IsStillTwoCards()
	{
		var late = Lands(s, me, 8);
		var lands = late.CardsIn(me, ZoneType.Library).Where(c => c.Is(CardType.Land)).Take(2);
		var drew = lands.Aggregate(late, (x, c) => x.MoveCard(c.Id, x.ZoneId(me, ZoneType.Hand)));
		var two = Unseen(late, 2) - Eval(late);
		Assert.That(
			Eval(drew) - Eval(late),
			Is.EqualTo(two).Within(25).Percent,
			$"two unseen cards {two:0.00}; a card {ACard(late):0.00}"
		);
	}

	// "a random card should always be worth a card, but cards are properly evaluated once they have been drawn": what
	// a card is worth once seen averages what it was worth unseen - seeing it neither gains nor loses on average
	[Test, Explicit("AI-27: fails today - seen and unseen cards are priced on different scales")]
	public void OurSeenCard_IsWorthAnUnseenOne_OnAverage()
	{
		var x = Lands(s, me, 3);
		var library = x.CardsIn(me, ZoneType.Library).ToList();
		var seen = library.Average(c => Eval(x.MoveCard(c.Id, x.ZoneId(me, ZoneType.Hand))));
		Assert.That(
			seen - Eval(x),
			Is.EqualTo(ACard(x)).Within(10).Percent,
			$"seen {seen - Eval(x):0.00} against unseen {ACard(x):0.00}, over {library.Count}"
		);
	}

	// their side of the same: a card of theirs we come to know (a reveal, a bounce) is, on average, what it was unseen
	[Test, Explicit("AI-27: fails today - seen and unseen cards are priced on different scales")]
	public void TheirSeenCard_IsWorthAnUnseenOne_OnAverage()
	{
		var library = s.CardsIn(them, ZoneType.Library).ToList();
		double Known(Card c)
		{
			var x = s.MoveCard(c.Id, s.ZoneId(them, ZoneType.Hand));
			x = x.UpdateObject(c.Id, x.GetCard(c.Id) with { KnownTo = Knowers.Everyone }); // TEST SETUP
			return Eval(x);
		}
		var seen = Eval(s) - library.Average(Known);
		Assert.That(
			seen,
			Is.EqualTo(Held.Unknown).Within(10).Percent,
			$"seen {seen:0.00} against unseen {Held.Unknown:0.00}"
		);
	}

	// "They draw a card" costs on the same scale as our own cards are worth (HeldValues' own rule)
	[Test]
	public void TheirDraw_CostsWhatOurDrawGains()
	{
		var x = Lands(Lands(s, me, 3), them, 3);
		var theirs = Eval(x) - Eval(x.Draw(them));
		Assert.That(theirs, Is.EqualTo(ACard(x)).Within(5).Percent, $"theirs {theirs:0.00}");
	}

	// "drawing a card gets 1 card closer in your deck. Waiting to draw a card doesn't really give you any additional value"
	[Test]
	public void AJaceTokenDrawingNow_BeatsBankingTheLoyalty()
	{
		var x = With(s, me, EmpowerJace.JaceToken, ZoneType.Battlefield, out var jace);
		var banked = x.UpdateObject(jace, x.GetCard(jace) with { Loyalty = 6, IsToken = true }); // TEST SETUP
		var drew = x.UpdateObject(
			jace,
			x.GetCard(jace) with
			{
				Loyalty = 3,
				IsToken = true,
				LoyaltyActivatedOnTurn = x.GetGame().TurnNumber,
			}
		); // TEST SETUP
		Assert.That(Unseen(drew), Is.GreaterThan(Eval(banked)), $"a card {ACard(s):0.00}");
	}

	// "even if you draw a card, the value of drawing a card should not just be based on the card you draw, but by the
	// fact that you are technically still 'digging' through your deck even if you have not found a good card yet ...
	// causing some misevluation from the AI on wehn to draw a card (expecially with jace tokens)". The search scores the
	// Jace token's −3 twice - the draw a land, or an unseen spell - so the land version must still see a card dug.
	// A Jace token ("−1: Surveil 1", "−3: Draw a card") at 6, three Forests in play, empty hands, 20 life each.
	// AI-27.7's switch (beam+drawncard): a card the look-ahead draws counts as a drawn card, whatever it guessed.
	[Test]
	public void AJaceTokenDrawingALand_StillDrawsACard_WithTheDrawnCardSwitch() =>
		JaceDrawsALand(drawnCard: true);

	[
		Test,
		Explicit("AI-27: fails today (the switch off) - a land drawn is priced as the land alone")
	]
	public void AJaceTokenDrawingALand_StillDrawsACard() => JaceDrawsALand(drawnCard: false);

	void JaceDrawsALand(bool drawnCard)
	{
		var x = With(
			Lands(s, me, 3),
			me,
			EmpowerJace.JaceToken,
			ZoneType.Battlefield,
			out var jace
		);
		Card Token(int loyalty, bool used) =>
			x.GetCard(jace) with
			{
				Loyalty = loyalty,
				IsToken = true,
				LoyaltyActivatedOnTurn = used ? x.GetGame().TurnNumber : 0,
			};
		var card = ACard(x.UpdateObject(jace, Token(6, used: false))); // TEST SETUP
		// the land version of the look-ahead: our unseen draws are lands
		var lands = Hidden.Unknown(x, me, lands: true);
		var banked = lands.UpdateObject(jace, Token(6, used: false)); // TEST SETUP
		var spent = lands.UpdateObject(jace, Token(3, used: true)); // TEST SETUP
		var drew = spent.Draw(me);
		var played = drew.Act(drew.LegalActions().OfType<Actions.PlayLandAction>().First());
		Evaluator.WithRootImpulse(
			x, // the search's root: the token's library unseen
			() =>
			{
				Assert.Multiple(() =>
				{
					Assert.That(
						Eval(drew) - Eval(spent),
						Is.EqualTo(card).Within(25).Percent,
						$"the land drawn, against a card {card:0.00} today"
					);
					Assert.That(
						Eval(drew),
						Is.GreaterThan(Eval(banked)),
						"the −3 against banking it"
					);
					// "You should never get an immediate loss for deploying a resource in hand"
					Assert.That(Eval(played), Is.GreaterThan(Eval(drew)), "the drawn land played");
				});
				return 0;
			},
			drawnCard
		);
	}

	// a card in hand when the search began is SEEN: a hand of lands that unlock nothing is priced as what they are,
	// so drawing lands that turned out useless still reads as behind (Shayne, 2026-11-18)
	[Test]
	public void ASeenHandOfUselessLands_IsStillPricedAsWhatItIs_WithTheDrawnCardSwitch()
	{
		var x = Lands(s, me, 8);
		var lands = x.CardsIn(me, ZoneType.Library)
			.Where(c => c.Is(CardType.Land))
			.Take(3)
			.ToList();
		var root = lands.Aggregate(x, (y, c) => y.MoveCard(c.Id, y.ZoneId(me, ZoneType.Hand)));
		var off = Eval(root);
		var on = Evaluator.WithRootImpulse(root, () => Eval(root), drawnCards: true);
		Assert.That(on, Is.EqualTo(off).Within(1e-9));
	}

	[Test]
	public void AClueCrackedNow_BeatsHoldingIt()
	{
		var clue = new CardDefinition(Tokens.Clue, []);
		var held = With(Lands(s, me, 2), me, clue, ZoneType.Battlefield, out _);
		var cracked = Lands(s, me, 2);
		Assert.That(Unseen(cracked), Is.GreaterThan(Eval(held)), $"a card {ACard(cracked):0.00}");
	}

	// "the value is in the opponent having to spend mana to deploy their card again. It does not 'give a card back'":
	// a bounce gains less than a kill (a token's is a kill: the state-based check removes it), and the bounce's gain is the mana - so two creatures
	// of one mana value gain the same bounced, whatever their bodies
	[Test]
	public void ABounce_IsTheRecast_NeverACard()
	{
		double Gain(CardDefinition card, ZoneType to)
		{
			var x = With(s, them, card, ZoneType.Battlefield, out var id);
			var after = x.MoveCard(id, x.ZoneId(them, to));
			return Eval(after) - Eval(x);
		}
		var small = TestGames.CreatureCosting("{5}", "Small", "1/1");
		var big = TestGames.CreatureCosting("{5}", "Big", "5/5");
		Assert.That(Gain(big, ZoneType.Hand), Is.LessThan(Gain(big, ZoneType.Graveyard)));
		// the clock aside: a bounced 5/5 also misses an attack the 1/1 would barely make - tempo, not a card
		Assert.That(
			Evaluator.Without(Evaluator.Terms.Threat, () => Gain(small, ZoneType.Hand)),
			Is.EqualTo(Evaluator.Without(Evaluator.Terms.Threat, () => Gain(big, ZoneType.Hand)))
				.Within(1e-9)
		);
	}

	// "if I play a 3/2 and it trades with the opposing 3/2, both of us are neutral on cards ... we should not choose to
	// hold our 3/2 in our hand because that might happen" - at every held fraction tried
	[TestCase(0.5)]
	[TestCase(0.75)]
	[TestCase(1.0)]
	public void ACreatureCast_NeverScoresBelowHoldingIt(double fraction)
	{
		var held = new HeldValues(CardValues.Fra, fraction);
		var body = TestGames.Card("Inspired Tethermage"); // a vanilla 3/2 for {2}{G}
		var x = With(Lands(Lands(s, me, 3), them, 3), them, body, ZoneType.Battlefield, out _);
		var holding = With(x, me, body, ZoneType.Hand, out _);
		var cast = With(x, me, body, ZoneType.Battlefield, out _);
		var traded = With(Lands(Lands(s, me, 3), them, 3), me, body, ZoneType.Graveyard, out _);
		traded = With(traded, them, body, ZoneType.Graveyard, out _);
		Assert.That(Eval(cast, held), Is.GreaterThanOrEqualTo(Eval(holding, held)), "cast");
		Assert.That(Eval(traded, held), Is.GreaterThanOrEqualTo(Eval(holding, held)), "traded");
		Assert.That(
			Eval(traded, held),
			Is.EqualTo(Eval(cast, held)).Within(1e-9),
			"a trade is neutral"
		);
	}

	// "At 20 life, I would say 5 life is worth it to pay to draw a card as a baseline ... spending a card to gain life
	// is priced much higher, usually needing a lot of life (8+)"
	[Test, Explicit("AI-27: fails today - life is priced about twice a card")]
	public void FiveLifePaidAtTwenty_IsAboutACard_AndACardGainsEightOrMore()
	{
		var card = ACard(s);
		double Life(int change) =>
			Eval(s.UpdateObject(me, s.GetPlayer(me) with { Life = 20 + change })) - Eval(s);
		Assert.Multiple(() =>
		{
			Assert.That(-Life(-5) / card, Is.InRange(0.75, 1.33), "5 life paid, in cards");
			Assert.That(Life(+7) / card, Is.LessThan(1), "7 life gained, in cards");
			Assert.That(Life(+10) / card, Is.GreaterThanOrEqualTo(1), "10 life gained, in cards");
		});
	}
}
