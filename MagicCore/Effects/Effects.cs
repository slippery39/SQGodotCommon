using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MagicCore.Effects;

/// <summary>Pipeline context keys a resolving spell provides to its effects.</summary>
public static class ContextKeys
{
	public const string Controller = "Controller";
	public const string X = "X";
}

/// <summary>
/// An instant's or sorcery's effects, run in order when it resolves. Effects are plain data
/// GameActions (no delegates) that read the controller and X from the pipeline context.
/// </summary>
public record SpellEffectsComponent(ImmutableList<GameAction> Effects) : GameComponent;

public abstract record Effect : GameAction
{
	protected int Controller => GetInput<int>(ContextKeys.Controller);
}

public record DrawCardsEffect(int Count) : Effect
{
	public override ActionResult Execute(GameState s) => new(s.Draw(Controller, Count));
}

public record GainLifeEffect(int Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var player = s.GetPlayer(Controller);
		return new(s.WithPlayer(player with { Life = player.Life + Amount }));
	}
}

public record EachOpponentLosesLifeEffect(int Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var opponent = s.GetPlayer(s.OpponentOf(Controller));
		return new(s.WithPlayer(opponent with { Life = opponent.Life - Amount }));
	}
}
