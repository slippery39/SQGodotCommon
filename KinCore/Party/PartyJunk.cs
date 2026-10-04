using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>A junk card a foe's move can add (`KinEnemiesPlan.md`, step 3).</summary>
public enum Junk
{
	None,

	/// <summary>Unplayable; clogs the hand. Shuffled into the draw pile.</summary>
	Mire,

	/// <summary>Costs 1 to clear; while held, line cards cannot be played. Into the hand.</summary>
	Web,

	/// <summary>Unplayable; drawn, your weakest monster takes 2. Into the discard pile.</summary>
	Rot,

	/// <summary>Into the hand; gone at the turn's end — and if it was not played, 1 less energy next turn.</summary>
	Doubt,
}

/// <summary>Marks a JUNK card: grey, "JUNK", never a spell; gone for the fight when played.</summary>
public record JunkCard : GameComponent;

/// <summary>ROT: drawn, your weakest monster takes this much.</summary>
public record HurtsWhenDrawn : GameComponent
{
	public int Amount { get; init; } = 2;
}

/// <summary>DOUBT: still in the hand when the turn ends, it goes — and costs this much energy next turn.</summary>
public record Fleeting : GameComponent
{
	public int Debt { get; init; } = 1;
}

/// <summary>A junk card's words — and, for Mire and Rot, the refusal that makes it unplayable.</summary>
public record JunkStep : CardStep
{
	public bool Unplayable { get; init; }

	public override bool NeedsTarget => false;

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		Unplayable ? "It cannot be played" : null;

	public override ActionResult Execute(GameState s) => new(s);
}

/// <summary>
/// **JUNK CARDS** (interview 2026-10-03, every answer as recommended): a foe's move adds them for the
/// FIGHT only — a battle's deck is rebuilt from the run deck, so none follows you out. Playing one
/// (Web, Doubt) clears it for the fight; Mire and Rot cannot be played, so they come back every
/// reshuffle. They thin nothing and test a deck that never removes cards.
/// </summary>
public static class PartyJunk
{
	private static KinCard Card(
		string name,
		int cost,
		string text,
		bool unplayable,
		params GameComponent[] more
	) =>
		new()
		{
			Name = name,
			Cost = cost,
			Exhausts = true,
			Effects =
			[
				new KinEffect
				{
					Template = new JunkStep { Unplayable = unplayable },
					Text = text,
				},
			],
			Components = [new JunkCard(), .. more],
		};

	public static KinCard CardOf(Junk junk) =>
		junk switch
		{
			Junk.Mire => Card("Mire", 0, "Unplayable.", unplayable: true),
			Junk.Web => Card("Web", 1, "Line cards can't be played while you hold it.", false),
			Junk.Rot => Card(
				"Rot",
				0,
				"Unplayable. Drawn: your weakest monster takes 2.",
				unplayable: true,
				new HurtsWhenDrawn()
			),
			_ => Card(
				"Doubt",
				1,
				"Gone at turn end. Unplayed: 1 less energy next turn.",
				false,
				new Fleeting()
			),
		};

	/// <summary>`count` of the junk, where that junk goes.</summary>
	public static GameState Add(GameState s, Junk junk, int count)
	{
		var zone = junk switch
		{
			Junk.Mire => ZoneType.Draw,
			Junk.Rot => ZoneType.Discard,
			_ => ZoneType.Hand,
		};
		for (var i = 0; i < count; i++)
			(s, _) = s.AddObject(CardOf(junk), s.ZoneId(zone));
		return zone == ZoneType.Draw ? KinRng.ShuffleZone(s, s.ZoneId(ZoneType.Draw)) : s;
	}

	/// <summary>A WEB in your hand: no line card (swap, to the front, to the back) can be played.</summary>
	public static bool Webbed(GameState s) =>
		s.CardsIn(ZoneType.Hand).Any(c => c.Name == "Web" && c.HasComponent<JunkCard>());

	/// <summary>Is this a line card a Web holds?</summary>
	public static bool MovesTheLine(KinCard card) =>
		card.Effects.Any(e => e.Template is SwapAction or RallyAction or RetreatAction);

	/// <summary>A card just drawn: ROT hurts your weakest monster.</summary>
	public static GameState Drawn(GameState s, KinCard card)
	{
		if (
			card.GetComponent<HurtsWhenDrawn>() is not { } rot
			|| s.LivingAllies().OrderBy(a => a.Hp).ThenBy(a => a.Position).FirstOrDefault()
				is not { } weakest
		)
			return s;
		(s, _) = PartyState.HitAlly(s, weakest, rot.Amount, card.Name);
		return s;
	}

	/// <summary>The turn ends: each DOUBT still in hand goes, and takes its energy from next turn.</summary>
	public static GameState FleetingGo(GameState s)
	{
		foreach (
			var card in s.CardsIn(ZoneType.Hand).Where(c => c.HasComponent<Fleeting>()).ToList()
		)
		{
			var party = s.GetParty();
			s = s.UpdateObject(
				party.Id,
				party with
				{
					EnergyDebt = party.EnergyDebt + card.GetComponent<Fleeting>()!.Debt,
				}
			);
			s = s.RemoveObject(card.Id);
		}
		return s;
	}
}
