using ImmutableGameObjects;
using MagicCore.Cards;

namespace MagicCore.Tests;

static class TestGames
{
	static readonly IReadOnlyList<CardData> Fra = CardDataSet.Load("FRA");

	public static CardDefinition Card(string name) =>
		CardDefinition.Vanilla(Fra.Single(c => c.Name == name));

	/// <summary>A 40-card deck of Forests and FRA nonlands, played vanilla — filler for rules tests.</summary>
	public static List<CardDefinition> Deck(int lands = 17, int size = 40) =>
		[
			.. Enumerable.Repeat(Card("Forest"), lands),
			.. Fra.Where(c => !c.TypeLine.Contains("Land"))
				.Take(size - lands)
				.Select(CardDefinition.Vanilla),
		];

	/// <summary>An inline test card — tests never depend on a real card's text or balance.</summary>
	public static CardDefinition Inline(
		string name,
		string cost,
		string typeLine,
		string[]? keywords = null,
		params GameAction[] effects
	) =>
		new(
			new CardData(
				name,
				"normal",
				cost,
				0,
				typeLine,
				null,
				null,
				null,
				null,
				null,
				keywords ?? [],
				"common",
				"0",
				null
			),
			effects.Length == 0 ? [] : [new Effects.SpellEffectsComponent([.. effects])]
		);

	/// <summary>An inline test creature, "2/2" style, costing {1}.</summary>
	public static CardDefinition Creature(string name, string pt, params string[] keywords) =>
		CreatureCosting("{1}", name, pt, keywords);

	/// <summary>An inline test planeswalker costing {1}, with the given printed loyalty and loyalty abilities.</summary>
	public static CardDefinition Planeswalker(
		string name,
		int loyalty,
		params Effects.LoyaltyAbilityComponent[] abilities
	) =>
		new(
			new CardData(
				name,
				"normal",
				"{1}",
				1,
				"Planeswalker — Test",
				null,
				null,
				null,
				$"{loyalty}",
				null,
				[],
				"common",
				"0",
				null
			),
			[.. abilities]
		);

	public static CardDefinition CreatureCosting(
		string cost,
		string name,
		string pt,
		params string[] keywords
	) =>
		new(
			new CardData(
				name,
				"normal",
				cost,
				0,
				"Creature — Test",
				null,
				pt.Split('/')[0],
				pt.Split('/')[1],
				null,
				null,
				keywords,
				"common",
				"0",
				null
			),
			[]
		);

	/// <summary>TEST SETUP: puts a fresh card straight into a zone, bypassing the rules.</summary>
	public static (GameState State, int CardId) Put(
		GameState s,
		int playerId,
		CardDefinition card,
		ZoneType zone
	)
	{
		var (next, obj) = s.AddObject(CardFactory.Create(card, playerId), s.ZoneId(playerId, zone));
		return (next, obj.Id);
	}

	/// <summary>TEST SETUP: both hands go back into their libraries, so a position holds only what a test puts in it.</summary>
	public static GameState EmptyHands(GameState s)
	{
		foreach (var p in new[] { s.Player1Id(), s.Player2Id() })
		foreach (var card in s.CardsIn(p, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(p, ZoneType.Library));
		return s;
	}

	/// <summary>
	/// Two AIs play until turn <paramref name="lastTurn"/> is over or the game ends — failing the test
	/// if it never gets there, so a stuck loop can't pass as "nothing bad happened". <paramref name="onStep"/>
	/// sees the state before and after each decision (the forced passes before it included).
	/// </summary>
	public static GameState PlayAis(
		GameState s,
		int lastTurn,
		global::MagicCore.Ai.IAi first,
		global::MagicCore.Ai.IAi second,
		Action<GameState, GameState>? onStep = null
	)
	{
		var ais = new[] { first, second }.ToDictionary(a => a.PlayerId);
		for (var i = 0; i < 2_000; i++)
		{
			var before = s;
			s = s.AutoPass();
			if (!s.IsOver() && s.GetGame().TurnNumber <= lastTurn)
				s = ais[s.DecidingPlayerId()].Decide(s).State;
			onStep?.Invoke(before, s);
			if (s.IsOver() || s.GetGame().TurnNumber > lastTurn)
				return s;
		}
		Assert.Fail($"turn {lastTurn} never ended");
		return s;
	}

	/// <summary>Started, then passed to the starting player's first main phase.</summary>
	public static GameState InFirstMain(int seed = 1) =>
		PassUntil(Started(seed), x => x.GetGame().Step == Step.PreCombatMain);

	public static GameState New(int seed = 1, bool smoothing = true) =>
		MagicRules.NewGame(Deck(), Deck(), seed, smoothing);

	/// <summary>Both players keep seven: the game is at the starting player's first upkeep.</summary>
	public static GameState Started(int seed = 1)
	{
		var s = New(seed);
		s = s.Act(s.LegalActions().OfType<Actions.KeepHandAction>().Single());
		return s.Act(s.LegalActions().OfType<Actions.KeepHandAction>().Single());
	}

	/// <summary>Plays a land when it can, otherwise passes; answers choices with the first options.</summary>
	public static GameState LandGoBotStep(GameState s)
	{
		if (s.GetPendingChoice() is { } choice)
			return s.Choose(choice.Options.Take(choice.MinChoices).Select(o => o.Id));
		var actions = s.LegalActions();
		return s.Act(
			actions.FirstOrDefault(a => a is Actions.PlayLandAction or Actions.KeepHandAction)
				?? actions.First()
		);
	}

	/// <summary>Passes priority; answers a pending choice (cleanup discard) with the first options.</summary>
	public static GameState PassOrChoose(GameState s) =>
		s.GetPendingChoice() is { } choice
			? s.Choose(choice.Options.Take(choice.MinChoices).Select(o => o.Id))
			: s.Act(s.LegalActions().OfType<Actions.PassPriorityAction>().Single());

	public static GameState PassUntil(GameState s, Func<GameState, bool> done)
	{
		for (var i = 0; i < 1000 && !done(s); i++)
			s = PassOrChoose(s);
		return s;
	}
}
