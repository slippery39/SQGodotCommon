using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Effects;

namespace MagicCore.Ai.Notes;

/// <summary>
/// AI note (AI-29.1's shape: a mechanic, matched by what the card has; this one DECIDES) — a free life gain at instant
/// speed: Zuran Orb's "Sacrifice a land: You gain 2 life." Shayne, 2026-11-23: "once zuran orb is in play, its cost is 0,
/// and its instant speed. The player effectively already has that life, there is no bonus to using it all at once. In
/// fact its actually a detriment to use zuran orb if you don't need to." Watched (MC-9.6, six lands): at 6 life against
/// 4 power the search sacrificed all six lands in its own turn, at 5 against 6 power the same, and at 3 with a Lightning
/// Bolt on the stack it answered the Bolt, then kept sacrificing to one land — every 2 life outscored a land below ~10
/// life, and no price can say "later is as good". So it is offered only when it is NEEDED: passing now would lose the
/// game (what is on the stack, this combat's damage), or the sacrifice triggers something of ours (Titania, Protector of
/// Argoth; Baloth Prime). One sacrifice at a time, the need judged again after each: per needs. Off: `beam+nofreelifegain`.
/// </summary>
public static class FreeLifeGain
{
	/// <summary>
	/// The shape: an ability of a permanent at instant speed, no mana and no {T}, no target, whose only costs are
	/// sacrifices and whose only effects gain life — Zuran Orb's, and a "Sacrifice this: you gain 3 life" alike.
	/// </summary>
	public static bool Is(ActivatedAbilityComponent ability) =>
		ability is { SorcerySpeed: false, From: ZoneType.Battlefield }
		&& ability.Targets.IsEmpty
		&& ability.Modes.IsEmpty
		&& ability.Cost.ManaValue == 0
		&& !ability.Cost.HasX
		&& !ability.Costs.IsEmpty
		&& ability.Costs.All(c => c is SacrificeCost or SacrificeSelfCost)
		&& ability.Effects.All(e => e is GainLife);

	public static bool IsMove(GameState s, GameAction a) =>
		a is ActivateAbilityAction act
		&& s.ActivatedAbilitiesOf(s.GetCard(act.CardId)).ElementAtOrDefault(act.AbilityIndex)
			is { } ability
		&& Is(ability);

	/// <summary>
	/// Whether the search is offered <paramref name="a"/> (one of <see cref="IsMove"/>'s): passing now loses, or it
	/// triggers something of ours. Asked once per list of moves (<see cref="Evaluator.SearchMoves"/>).
	/// ponytail: the trigger is judged on one sacrifice for all — a land-type trigger (one Forest of several) would be
	/// misread; judge each if a card needs it.
	/// </summary>
	public static bool Needed(GameState s, GameAction a) =>
		Evaluator.IsOff(Evaluator.Terms.FreeLifeGain)
		|| PassingLoses(s, s.DecidingPlayerId())
		|| TriggersOurs(s, s.DecidingPlayerId(), a);

	/// <summary>
	/// The rollout's use (<see cref="Rollout"/>, which plays our side only at sorcery speed): passing now would lose,
	/// so one sacrifice — a tapped permanent first (it makes no mana this turn) — then the need is judged again.
	/// </summary>
	public static GameAction? ToSurvive(GameState s, int me)
	{
		if (
			Evaluator.IsOff(Evaluator.Terms.FreeLifeGain)
			|| !s.CardsIn(me, ZoneType.Battlefield).Any(c => s.ActivatedAbilitiesOf(c).Any(Is))
			|| !PassingLoses(s, me)
		)
			return null;
		return s.LegalActions(a => IsMove(s, a))
			.OfType<ActivateAbilityAction>()
			.OrderBy(a => s.HasObject(a.SacrificeId) && s.GetCard(a.SacrificeId).IsTapped ? 0 : 1)
			.FirstOrDefault();
	}

	/// <summary>Everyone passes from here — the stack resolved, this combat's damage dealt — and <paramref name="me"/> has lost.</summary>
	static bool PassingLoses(GameState s, int me) =>
		s.GetGame().PriorityPlayerId == me
		&& !s.IsWaitingForChoice
		&& Lookahead.Settle(s.Act(new PassPriorityAction { PlayerId = me })) is var after
		&& after.IsOver()
		&& after.GetPlayer(me).HasLost;

	/// <summary>Activated, it puts a trigger of ours on the stack beside it (or asks a trigger's target).</summary>
	static bool TriggersOurs(GameState s, int me, GameAction a)
	{
		if (!a.ValidateAdd(s).IsValid)
			return false;
		var after = s.Act(a);
		return after.IsWaitingForChoice || Ours(after, me) > Ours(s, me) + 1;
	}

	static int Ours(GameState s, int me) =>
		s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack))
			.Count(id => s.StackObjectControllerId(id) == me);
}
