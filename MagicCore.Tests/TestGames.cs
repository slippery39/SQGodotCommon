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
