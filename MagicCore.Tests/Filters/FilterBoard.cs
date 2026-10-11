using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;

namespace MagicCore.Tests.Filters;

/// <summary>
/// One of everything the filter vocabulary asks about, on both sides, on the stack and in both
/// graveyards. The state need not be reachable (their creatures attack on a turn that isn't theirs):
/// filters and the old code both only read it. No hexproof — that's a targeting rule, not a filter.
/// "Me" is Player2, so Ref's order (you first) and TargetSpec's (player 1 first) differ and both show.
/// </summary>
sealed class FilterBoard
{
	public GameState S = null!;
	public int Me,
		Them;
	readonly Dictionary<string, int> ids = [];

	public int this[string label] => ids[label];

	public Card Card(string label) => S.GetCard(ids[label]);

	public string LabelOf(int id) =>
		id == Me ? "me"
		: id == Them ? "them"
		: ids.FirstOrDefault(kv => kv.Value == id).Key ?? $"#{id}"; // the random hand isn't labelled

	public FilterContext Context(string source = "Src", string? eventCard = null) =>
		new(Me, ids[source], eventCard is null ? 0 : ids[eventCard]);

	/// <summary>Every object a filter can be asked about here: players, permanents, stack, graveyards (not hands/libraries — random).</summary>
	public IEnumerable<GameObject> Everything() =>
		new[] { S.Player1Id(), S.Player2Id() }
			.Select(S.GetObject)
			.Concat(ids.Values.Select(S.GetObject));

	public static FilterBoard Build()
	{
		var b = new FilterBoard { S = TestGames.InFirstMain() };
		b.Me = b.S.Player2Id();
		b.Them = b.S.Player1Id();
		var turn = b.S.GetGame().TurnNumber;
		var bf = ZoneType.Battlefield;

		b.Put("Src", b.Me, Def("Creature — Test", "2/2"), bf);
		b.Put("Sage", b.Me, Def("Legendary Creature — Human Wizard", "4/5", ["U"], ["Flying"]), bf);
		b.Put("Thopter", b.Me, Def("Artifact Creature — Thopter Wizard", "1/1", ["R"]), bf);
		b.Put("Jace", b.Me, Def("Legendary Planeswalker — Jace", loyalty: "3"), bf);
		b.Put("Grove", b.Me, Def("Land — Gate"), bf);
		b.Put("Grove 2", b.Me, Def("Land — Gate"), bf);
		b.Put("Meadow", b.Me, Def("Basic Land — Plains"), bf);
		b.Put("Ward", b.Me, Def("Enchantment — Aura"), bf);
		b.Put("Shrine", b.Me, Def("Enchantment"), bf);
		b.Put("Wall", b.Them, Def("Creature — Wall", "1/4"), bf);
		b.Put("Brute", b.Them, Def("Creature — Ogre", "5/5", ["G"]), bf);
		b.Put("Raider", b.Them, Def("Creature — Goblin", "3/1", ["R"]), bf);
		b.Put("Spirit", b.Them, Def("Creature — Spirit", "1/1", ["W"], ["Flying"]), bf);
		b.Put("Isle", b.Them, Def("Basic Land — Island"), bf);
		b.Put("Vow", b.Them, Def("Planeswalker — Gideon", loyalty: "4"), bf);
		b.Put("Bear", b.Me, Def("Creature — Bear", "2/2", ["B"]), bf);
		b.Put("Golem", b.Me, Def("Artifact Creature — Golem", "3/3"), bf);
		b.Put("Sword", b.Me, Def("Artifact — Equipment"), bf);
		b.Put("Stalker", b.Them, Def("Creature — Cat", "2/2"), bf);

		b.Update("Sage", c => c with { PlusOneCounters = 1 });
		b.Update("Thopter", c => c with { IsToken = true });
		b.Update("Spirit", c => c with { IsToken = true });
		b.Update("Brute", c => c with { IsCopy = true });
		b.Update("Raider", c => c with { IsPrepared = true, AttackingId = b.Me });
		b.Update("Wall", c => c with { BlockingId = b["Bear"] });
		b.Update(
			"Bear",
			c => c with { EnteredOnTurn = turn, AttackedOnTurn = turn, AttackingId = b.Them }
		);
		b.Update("Golem", c => c with { AttackedOnTurn = turn, AttackingId = b["Vow"] });
		b.Update("Stalker", c => c with { AttackingId = b["Jace"] });
		var bear = b.Card("Bear");
		b.Update(
			"Sword",
			c => c with { AttachedToId = bear.Id, AttachedToIncarnation = bear.Incarnation }
		);

		var stack = b.S.GetWellKnownId(MagicKeys.Stack);
		b.Put("Shock", b.Me, Def("Instant", cost: "{1}{R}", colors: ["R"]), stack);
		b.Put("Golem Spell", b.Me, Def("Artifact Creature — Golem", "3/3", cost: "{3}"), stack);
		b.Put("Echo", b.Me, Def("Sorcery", cost: "{2}{U}", colors: ["U"]), stack);
		b.Update("Echo", c => (Card)c.WithComponent(new PreparedCopyOf(0, 0)));
		b.Put("Duress", b.Them, Def("Legendary Sorcery", cost: "{B}", colors: ["B"]), stack);
		b.Put("Titan", b.Them, Def("Legendary Creature — Giant", "6/6", cost: "{4}"), stack);

		var gy = ZoneType.Graveyard;
		b.Put("Bones", b.Me, Def("Creature — Skeleton", "1/1", ["B"], cost: "{1}{B}"), gy);
		b.Put("Marsh", b.Me, Def("Basic Land — Swamp"), gy);
		b.Put("Lore", b.Me, Def("Legendary Sorcery", cost: "{4}"), gy);
		b.Put("Elf", b.Them, Def("Creature — Elf", "1/1", ["G"], cost: "{G}"), gy);
		b.Put("Blast", b.Them, Def("Sorcery", cost: "{5}", colors: ["R"]), gy);
		return b;
	}

	void Put(string label, int owner, CardDefinition def, ZoneType zone) =>
		Put(label, owner, def, S.ZoneId(owner, zone));

	void Put(string label, int owner, CardDefinition def, int zoneId)
	{
		var (next, obj) = S.AddObject(
			CardFactory.Create(def with { Data = def.Data with { Name = label } }, owner),
			zoneId
		);
		S = next;
		ids[label] = obj.Id;
	}

	void Update(string label, Func<Card, Card> change) =>
		S = S.UpdateObject(ids[label], change(Card(label)));

	static CardDefinition Def(
		string typeLine,
		string? pt = null,
		string[]? colors = null,
		string[]? keywords = null,
		string cost = "{1}",
		string? loyalty = null
	) =>
		new(
			new CardData(
				"",
				"normal",
				cost,
				0,
				typeLine,
				null,
				pt?.Split('/')[0],
				pt?.Split('/')[1],
				loyalty,
				colors,
				keywords ?? [],
				"common",
				"0",
				null
			),
			ImmutableArray<GameComponent>.Empty
		);
}
