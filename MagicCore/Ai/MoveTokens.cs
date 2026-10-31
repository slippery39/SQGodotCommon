using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Sets;

namespace MagicCore.Ai;

/// <summary>
/// A move as a short token, and the fingerprints that say a recording still replays — one format for
/// the position suite (AI-6.4) and the table's game records (TBL-5). A move is its index among the
/// position's distinct moves (<c>a3</c>), the ids answering its choice (<c>c12,15</c>; <c>c</c> for
/// none), a manual tap (<c>t41:2</c> — card, colour; mana abilities are not legal actions), or a
/// sandbox placement (<c>s5:Hand:Last Gasp</c> — the playtest tool).
/// </summary>
public static class MoveTokens
{
	/// <summary>A token that no longer applies, or a position that no longer offers what was recorded.</summary>
	public class StaleException(string message) : Exception(message);

	public static string Token(GameState s, AiDecision decision) =>
		Token(s, decision.Action, decision.Answer);

	public static string Token(GameState s, GameAction? action, IReadOnlyList<int>? answer)
	{
		if (answer is not null)
			return "c" + string.Join(",", answer);
		if (action is TapForManaAction tap)
			return $"t{tap.CardId}:{(int)tap.Color}";
		if (action is SandboxPutAction put)
			return Sandbox(put.PlayerId, put.Card.Data.Name, put.Zone);
		var key = s.MoveKey(action!);
		var index = s.DistinctMoves(s.LegalActions()).FindIndex(a => Equals(s.MoveKey(a), key));
		return index >= 0
			? $"a{index}"
			: throw new InvalidOperationException($"{action}: not among the distinct moves");
	}

	/// <summary>The playtest tool's placement (MagicCore.Sandbox): by card NAME, so it reads in a record.</summary>
	public static string Sandbox(int playerId, string card, ZoneType zone) =>
		$"s{playerId}:{zone}:{card}";

	/// <summary>A token against the state it applies to: the action, or the choice's ids — the one parser.</summary>
	static (GameAction? Action, int[]? Ids) Resolve(GameState s, string token)
	{
		try
		{
			return token switch
			{
				"c" => (null, []),
				['c', .. var ids] => (null, [.. ids.Split(',').Select(int.Parse)]),
				['a', .. var index] => (s.DistinctMoves(s.LegalActions())[int.Parse(index)], null),
				['t', .. var tap] => (Tap(s, tap), null), // a card id that is no card now: InvalidCast — stale
				['s', .. var put] => (Put(put), null),
				_ => throw new FormatException($"not a move token: '{token}'"),
			};
		}
		catch (Exception e) when (e is not StaleException) // any token that won't parse here is stale
		{
			throw new StaleException($"move '{token}' no longer applies ({e.Message})");
		}
	}

	static TapForManaAction Tap(GameState s, string tap)
	{
		var (card, color) = (int.Parse(tap.Split(':')[0]), int.Parse(tap.Split(':')[1]));
		return new TapForManaAction
		{
			PlayerId = s.GetCard(card).ControllerId,
			CardId = card,
			Color = (ManaColor)color,
		};
	}

	static SandboxPutAction Put(string put)
	{
		var parts = put.Split(':', 3);
		return new SandboxPutAction(
			int.Parse(parts[0]),
			CardRegistry.Get(parts[2]),
			Enum.Parse<ZoneType>(parts[1])
		);
	}

	public static GameState Apply(GameState s, string token)
	{
		var (action, ids) = Resolve(s, token);
		try
		{
			return action is not null ? s.Act(action) : s.Choose(ids!);
		}
		catch (InvalidOperationException e)
		{
			throw new StaleException($"move '{token}' no longer applies ({e.Message})");
		}
	}

	public static string Describe(GameState s, string token) =>
		Resolve(s, token) switch
		{
			// whose zone: the playtest tool puts into either seat, whoever holds priority
			(SandboxPutAction put, _) =>
				$"Put {put.Card.Data.Name} into {put.Zone} ({s.GetPlayer(put.PlayerId).Name})",
			({ } action, _) => MagicCore.Describe.Action(s, action),
			(_, var ids) => MagicCore.Describe.Answer(s, s.GetPendingChoice()!, ids!),
		};

	/// <summary>Two answers are the same if they pick the same move, or the same ids in any order.</summary>
	public static bool Same(string a, string b) =>
		a == b
		|| (
			a[0] == 'c'
			&& b[0] == 'c'
			&& a[1..].Split(',').Order().SequenceEqual(b[1..].Split(',').Order())
		);

	/// <summary>What a position offers, as a stable hash: its distinct moves, or its choice's options.</summary>
	public static string Offers(GameState s) =>
		Fnv(
			s.GetPendingChoice() is { } choice
				? choice.Options.Select(o => $"{o.Id}:{o.DisplayText}:{o.IsEnabled}")
				: s.DistinctMoves(s.LegalActions()).Select(a => s.MoveKey(a).ToString()!)
		);

	/// <summary>How a game ended, as a stable hash: the turn, and each player's life and zones.</summary>
	public static string Ending(GameState s) =>
		Fnv(
			new[] { s.Player1Id(), s.Player2Id() }.Select(p =>
				$"{s.GetGame().TurnNumber}:{s.GetPlayer(p).Life}:{string.Join(",", PlayerZones.Select(z => s.CardsIn(p, z).Count()))}"
			)
		);

	static readonly ZoneType[] PlayerZones =
	[
		ZoneType.Library,
		ZoneType.Hand,
		ZoneType.Battlefield,
		ZoneType.Graveyard,
		ZoneType.Exile,
	];

	/// <summary>FNV-1a — stable across processes, where string.GetHashCode is not.</summary>
	static string Fnv(IEnumerable<string> parts)
	{
		var hash = 2166136261u;
		foreach (var c in string.Join("|", parts))
			hash = unchecked((hash ^ c) * 16777619u);
		return hash.ToString("x8");
	}
}
