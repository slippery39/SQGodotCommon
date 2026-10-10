using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore;

/// <summary>
/// A playtest shortcut, never a rule: a fresh card put anywhere mid-game, to check a card without
/// drawing it. Onto the battlefield it ENTERS (EnterBattlefield — its "enters" triggers fire,
/// state-based actions run), so what you see is what the card does. Only while someone holds
/// priority and no choice is pending; the front end decides who may.
/// </summary>
public static class Sandbox
{
	/// <summary>Where a sandboxed card can go; Library means its top.</summary>
	public static readonly ZoneType[] Zones =
	[
		ZoneType.Hand,
		ZoneType.Battlefield,
		ZoneType.Graveyard,
		ZoneType.Exile,
		ZoneType.Library,
	];

	/// <summary>Whether the sandbox may put a card now — the one test, for the action and a front end alike.</summary>
	public static bool CanPut(GameState s) =>
		!s.IsOver() && s.GetGame().PriorityPlayerId != 0 && !s.IsWaitingForChoice;

	public static GameState Put(GameState s, int playerId, CardDefinition card, ZoneType zone) =>
		s.Act(new SandboxPutAction(playerId, card, zone));
}

/// <summary>
/// An action, not a bare state change: the triggers its entry puts on the stack are spawned
/// actions, and only the action pipeline runs them.
/// </summary>
public record SandboxPutAction(int PlayerId, CardDefinition Card, ZoneType Zone) : GameAction
{
	public override ValidationResult ValidateAdd(GameState s) =>
		!Sandbox.CanPut(s)
			? ValidationResult.Invalid("The sandbox puts cards only while someone holds priority")
		: !Sandbox.Zones.Contains(Zone)
			? ValidationResult.Invalid($"The sandbox doesn't put cards into {Zone}")
		: ValidationResult.Valid;

	public override ActionResult Execute(GameState s)
	{
		var priority = s.GetGame().PriorityPlayerId;
		// Something happened: the pass count restarts, as after any action (CR 117.3c) — or a
		// trigger it put on the stack could resolve without the other player ever seeing it.
		s = s.WithGame(s.GetGame() with { PassesInSuccession = 0 });
		var arrives = Zone is ZoneType.Battlefield or ZoneType.Library ? ZoneType.Exile : Zone;
		var (next, obj) = s.AddObject(
			CardFactory.Create(Card, PlayerId),
			s.ZoneId(PlayerId, arrives)
		);
		s = next;
		if (Zone == ZoneType.Library)
			s = s.MoveCard(obj.Id, s.ZoneId(PlayerId, ZoneType.Library), toTop: true);
		// Spawned, not called: an Aura's host choice (spawned by its entry) must run before the
		// state-based check, or the check puts the unattached Aura in the graveyard.
		if (Zone == ZoneType.Battlefield)
			s = s.EnterBattlefield(obj.Id, PlayerId)
				.SpawnAction(new GivePriorityAction { PlayerId = priority });
		return new ActionResult(s);
	}
}
