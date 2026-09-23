using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// The API boundary for the companion game. The Godot board reads everything through here and
/// never computes a game fact itself — including where an attack will land.
/// </summary>
public static class PartyState
{
	public const string BattleKey = "PartyBattle";

	public static PartyBattle GetParty(this GameState s) =>
		(PartyBattle)s.GetObject(s.GetWellKnownId(BattleKey));

	/// <summary>Every companion, knocked out or not, in board order.</summary>
	public static IEnumerable<Ally> Allies(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey)).OfType<Ally>().OrderBy(a => a.Space);

	public static IEnumerable<Ally> LivingAllies(this GameState s) =>
		s.Allies().Where(a => !a.IsKnockedOut);

	/// <summary>Every foe still standing, left to right — the order they act in.</summary>
	public static IEnumerable<Foe> LivingFoes(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey))
			.OfType<Foe>()
			.Where(f => !f.IsDead)
			.OrderBy(f => f.Space);

	/// <summary>A knocked-out companion leaves the row, so its space is free.</summary>
	public static Ally? AllyAt(this GameState s, int space) =>
		s.LivingAllies().FirstOrDefault(a => a.Space == space);

	public static Foe? FoeAt(this GameState s, int space) =>
		s.LivingFoes().FirstOrDefault(f => f.Space == space);

	public static Ally Owner(this GameState s, KinCard card) =>
		(Ally)s.GetObject(card.GetComponent<OwnedBy>()!.AllyId);

	/// <summary>
	/// A card that must be dropped ON a space — a step, a push, a swap. Which spaces are legal is
	/// the play's own validation; the board asks `PlayPartyCardAction` space by space.
	/// </summary>
	public static bool NeedsASpace(this KinCard card) =>
		card.Effects.Any(e => e.Template is CardStep { NeedsSpace: true });

	/// <summary>
	/// **Why this companion cannot step to that space, or null if it can.** One step, into an empty
	/// space on the row. Shared by the free move and by every card that moves you, so the two can
	/// never disagree about what a legal step is.
	/// </summary>
	public static string? StepRefusal(this GameState s, Ally ally, int space)
	{
		if (space < 0 || space >= PartyBattle.Spaces)
			return $"There is no space {space}";

		if (Math.Abs(space - ally.Space) != 1)
			return $"{ally.Name} can only step to a space next to it";

		if (s.AllyAt(space) is { } there)
			return $"{there.Name} is standing there";

		return null;
	}

	/// <summary>
	/// **The spaces on YOUR row this foe's intent will hit.** The telegraph the board draws and the
	/// attack that resolves both come from here, so what you see is what happens.
	///
	/// A shape is fixed columns, so it can be empty — that is a dodged attack. Homing picks the
	/// lowest-HP companion (leftmost on a tie). Draw Fire pulls a single-target hit on a companion
	/// beside it onto itself.
	/// </summary>
	public static ImmutableList<int> IntentTargets(this GameState s, Foe foe)
	{
		var intent = foe.Current;
		if (intent.Kind != IntentType.Attack)
			return [];

		ImmutableList<int> spaces;
		if (intent.Homing)
		{
			var weakest = s.LivingAllies().OrderBy(a => a.Hp).ThenBy(a => a.Space).FirstOrDefault();
			spaces = weakest is null ? [] : [weakest.Space];
		}
		else
		{
			spaces =
			[
				.. intent
					.Offsets.Select(o => foe.Space + o)
					.Where(c => c >= 0 && c < PartyBattle.Spaces),
			];
		}

		var party = s.GetParty();
		if (
			spaces.Count == 1
			&& party.DrawFireAllyId != 0
			&& s.GetObject(party.DrawFireAllyId) is Ally { IsKnockedOut: false } decoy
			&& s.AllyAt(spaces[0]) is { } victim
			&& Math.Abs(victim.Space - decoy.Space) == 1
		)
			return [decoy.Space];

		return spaces;
	}

	/// <summary>
	/// **What each companion's HP will lose if you end the turn now**, played out on a throwaway
	/// copy by the rules themselves — never a sum the UI adds up.
	/// </summary>
	public static ImmutableDictionary<int, int> HpLostIfTurnEndsNow(this GameState s)
	{
		if (s.GetParty().IsOver)
			return ImmutableDictionary<int, int>.Empty;

		var (after, _) = s.AddAction(new EndPartyTurnAction()).ProcessAllActions();
		return s.Allies()
			.ToImmutableDictionary(a => a.Id, a => a.Hp - ((Ally)after.GetObject(a.Id)).Hp);
	}

	/// <summary>Damage to a companion: Block first, then HP.</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitAlly(
		GameState s,
		Ally ally,
		int amount,
		string by
	)
	{
		var blocked = Math.Min(amount, ally.Block);
		var hit = ally with
		{
			Block = ally.Block - blocked,
			Hp = Math.Max(0, ally.Hp - (amount - blocked)),
		};
		s = s.UpdateObject(ally.Id, hit);

		ImmutableList<GameEvent> events =
		[
			new AllyHitEvent
			{
				AllyId = ally.Id,
				Damage = amount - blocked,
				Blocked = blocked,
				By = by,
			},
		];
		if (hit.IsKnockedOut)
			events = events.Add(new AllyKnockedOutEvent { AllyId = ally.Id });

		return (s, events);
	}

	/// <summary>Damage to a foe: Block first, then HP. Ends the battle when the last one falls.</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitFoe(GameState s, Foe foe, int amount)
	{
		// Off-Balance rides on every hit, whoever lands it — Pike's strike and Bramble's Thorns alike.
		amount += foe.OffBalance;

		var blocked = Math.Min(amount, foe.Block);
		var hit = foe with
		{
			Block = foe.Block - blocked,
			Hp = Math.Max(0, foe.Hp - (amount - blocked)),
		};
		s = s.UpdateObject(foe.Id, hit);

		ImmutableList<GameEvent> events =
		[
			new FoeHitEvent
			{
				FoeId = foe.Id,
				Damage = amount - blocked,
				Blocked = blocked,
			},
		];

		if (!s.LivingFoes().Any())
		{
			var party = s.GetParty();
			s = s.UpdateObject(party.Id, party with { IsOver = true, Won = true });
			events = events.Add(new PartyBattleEndedEvent { Won = true });
		}

		return (s, events);
	}
}
