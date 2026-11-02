using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;

namespace MagicCore.Actions;

/// <summary>
/// Flesh Duplicate: "a copy of any creature on the battlefield" — anyone's, not itself. ponytail:
/// "you may" is offered as must whenever there is a creature — a 0/0 that copies nothing just dies,
/// and the AI's look-ahead answers a choice with its minimum (Lookahead.QuickAnswer).
/// </summary>
public record ChooseCreatureToCopyChoice : ChoiceAction
{
	public int PlayerId { get; init; }
	public int SourceId { get; init; }

	public ChooseCreatureToCopyChoice(int playerId, int sourceId)
	{
		PlayerId = playerId;
		SourceId = sourceId;
		MinChoices = 1;
		MaxChoices = 1;
		OutputKey = ChoosePermanentChoice.Chosen;
		Prompt = "Enter as a copy of a creature?";
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		[
			.. new[] { s.Player1Id(), s.Player2Id() }
				.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
				.Where(c => c.Id != SourceId && c.Is(CardType.Creature))
				.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name }),
		];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>
/// After the choice: it becomes a copy of the chosen creature — with vanishing, if that had none.
/// ponytail: copied just AFTER entering, so the model's own "enters with counters / tapped" don't
/// apply (its "when this enters" triggers do); copy before EnterBattlefield's entry stamp if a card needs it.
/// </summary>
public record BecomeCopyOfChosenAction(int CardId, int VanishingIfNone) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		if (
			!s.IsOnBattlefield(CardId)
			|| ChoosePermanentChoice.Read(InputContext).FirstOrDefault() is not (> 0 and var id)
			|| !s.IsOnBattlefield(id)
		)
			return new(s);
		var card = s.GetCard(CardId);
		var model = s.GetCard(id);
		var copy = (Card)card.WithCopiableValuesOf(model).WithComponent(new EnteredAsCopy(card));
		if (VanishingIfNone > 0 && !s.HasAbility<Vanishing>(model))
			foreach (var component in Vanishing.Of(VanishingIfNone))
				copy = (Card)copy.WithComponent(component);
		// It entered just before it became the copy: vanishing's "enters with N time counters" here.
		return new(
			s.UpdateObject(CardId, copy with { TimeCounters = 0 })
				.PutCounters(CardId, CounterKinds.Time, copy.GetComponent<Vanishing>()?.N ?? 0)
		);
	}
}

/// <summary>
/// Pick up to one of your permanents matching a filter — "you may sacrifice a creature". Not
/// targeting: the chooser's battlefield, filtered (no filter is any permanent).
/// </summary>
public record ChoosePermanentChoice : ChoiceAction
{
	public const string Chosen = "permanent";

	public static IEnumerable<int> Read(ImmutableDictionary<string, object> context) =>
		context.GetValueOrDefault(Chosen) switch
		{
			int id => [id],
			ImmutableList<int> ids => ids,
			_ => [],
		};

	public int PlayerId { get; init; }
	public int SourceId { get; init; }
	public Filter? Filter { get; init; }

	/// <param name="required">"Sacrifices a creature" must; "you may sacrifice" need not.</param>
	public ChoosePermanentChoice(int playerId, int sourceId, Filter? filter, bool required = false)
	{
		PlayerId = playerId;
		SourceId = sourceId;
		Filter = filter;
		MinChoices = required ? 1 : 0;
		MaxChoices = 1;
		OutputKey = Chosen;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		Filter
			.Among(s, PlayerId, ZoneType.Battlefield, SourceId)
			.Select(id => new ChoiceOption { Id = id, DisplayText = s.GetObject(id).Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>"You may …": Yes is picking the one option, No is picking none. Read by IfChosenAction.</summary>
public record YesNoChoice : ChoiceAction
{
	public int PlayerId { get; init; }

	public YesNoChoice(int playerId, string prompt)
	{
		PlayerId = playerId;
		Prompt = prompt;
		MinChoices = 0;
		MaxChoices = 1;
		OutputKey = ChoosePermanentChoice.Chosen;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) => [new ChoiceOption { Id = 1, DisplayText = "Yes" }];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>"Counter it unless its controller pays {N}" — Yes pays. Its own type so the AI's look-ahead can pay.</summary>
public record PayToKeepChoice : YesNoChoice
{
	public PayToKeepChoice(int playerId, string prompt)
		: base(playerId, prompt) { }
}

/// <summary>An Aura entering without being cast: its controller chooses what it enchants (CR 303.4f).</summary>
public record ChooseHostChoice : ChoiceAction
{
	public int PlayerId { get; init; }
	public int AuraId { get; init; }

	public ChooseHostChoice(int playerId, int auraId)
	{
		PlayerId = playerId;
		AuraId = auraId;
		Prompt = "Choose what it enchants";
		OutputKey = ChoosePermanentChoice.Chosen;
		MinChoices = 1;
		MaxChoices = 1;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		!s.IsOnBattlefield(AuraId)
			? []
			: new[] { s.Player1Id(), s.Player2Id() }
				.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
				.Where(c =>
					c.Id != AuraId && Rules.Attachments.CanEnchant(s, s.GetCard(AuraId), c.Id)
				)
				.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
				.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

public record AttachChosenHostAction(int AuraId) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		new(
			ChoosePermanentChoice.Read(InputContext).FirstOrDefault() is var host && host != 0
				? Rules.Attachments.Attach(s, AuraId, host)
				: s
		);
}

/// <summary>Sacrifices what the previous choice picked (to its owner's graveyard — it dies).</summary>
public record SacrificeChosenAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var id in ChoosePermanentChoice.Read(InputContext))
			s = s.Sacrifice(id);
		return new ActionResult(s);
	}
}

/// <summary>"If you do, …": runs the effects only when the previous choice picked something.</summary>
public record IfChosenAction(ImmutableList<GameAction> Then) : GameAction
{
	/// <summary>Which choice's answer to read — a permanent or yes/no (default), or cards from a hand.</summary>
	public string Key { get; init; } = ChoosePermanentChoice.Chosen;

	/// <summary>Run instead on a "no" — Clash of Elements' "if they didn't, … the bottom".</summary>
	public ImmutableList<GameAction> Otherwise { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var chosen = InputContext.GetValueOrDefault(Key) switch
		{
			int => true,
			ImmutableList<int> ids => !ids.IsEmpty,
			_ => false,
		};
		foreach (var effect in chosen ? Then : Otherwise)
			s = (effect with { InputContext = InputContext }).Execute(s).GameState;
		return new ActionResult(s);
	}
}
