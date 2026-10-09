using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore;

/// <summary>
/// Arena-style auto-payment: finds which floating mana and which untapped lands pay a cost.
/// The only place a cost is solved — casting validates and pays through the same call.
///
/// Backtracking, so it finds a payment whenever one exists (hybrid and {2/W} make greedy wrong).
/// Identical sources (two floating G, two Forests) are tried once per step, which keeps the
/// search tiny however many basics are out.
///
/// ponytail: spends floating mana first, then lands with the fewest colours. Arena's tapper also
/// keeps colours the rest of your hand needs. That trigger has fired — the FRA decks play duals
/// since batch 7a, so a {1}{W} can still tap the Island a {U}{U} in hand needed. Next to build.
/// </summary>
public static class ManaPayment
{
	/// <summary>One unit of mana spent: from a land tapped for it, or floating (LandId 0).</summary>
	public record Payment(int LandId, ManaColor Color);

	record Source(int LandId, ImmutableArray<ManaColor> Colors, bool IsCreature = false);

	/// <summary>Allowed colours for one mana; TwoBridColor set means "this, or two generic instead".</summary>
	record Need(ImmutableArray<ManaColor> Allowed, ManaColor? TwoBridColor = null);

	static readonly ImmutableArray<ManaColor> Any = [.. Enum.GetValues<ManaColor>()];

	public static IReadOnlyList<Payment>? Find(
		GameState s,
		int playerId,
		ManaCost cost,
		int x,
		int excludeId = 0,
		bool convoke = false
	)
	{
		var player = s.GetPlayer(playerId);
		// Convoke (CR 702.51): each untapped creature you control may pay {1} or one mana of its
		// colour — Colorless is how it pays generic only. Summoning sickness doesn't matter: tapping
		// for convoke isn't a {T} cost. Ordered after lands, like a creature's own mana ability.
		var convokers = convoke
			? s.CardsIn(playerId, ZoneType.Battlefield)
				.Where(c => c.Is(Cards.CardType.Creature) && !c.IsTapped && c.Id != excludeId)
				.ToDictionary(c => c.Id, c => (ManaColor[])[.. c.Colors, ManaColor.Colorless])
			: [];
		var sources = Any.SelectMany(c => Enumerable.Repeat(new Source(0, [c]), player.ManaPool[c]))
			.Concat(
				ManaAbilities
					.Sources(s, playerId, excludeId)
					// One source per permanent: a mana creature convoking may pay what it makes OR its own colour.
					.Select(c => new Source(
						c.Id,
						[
							.. ManaAbilities
								.ColorsOf(c)
								.Concat(convokers.GetValueOrDefault(c.Id) ?? [])
								.Distinct(),
						],
						c.Is(Cards.CardType.Creature)
					))
					.Concat(
						convokers
							.Where(kv => !ManaAbilities.CanTap(s, s.GetCard(kv.Key)))
							.Select(kv => new Source(kv.Key, [.. kv.Value], IsCreature: true))
					)
					// Lands before creatures: a creature tapped for mana can no longer attack or block.
					.OrderBy(src => src.IsCreature)
					.ThenBy(src => src.Colors.Length)
			)
			.ToList();

		// Most constrained first; generic last so it takes whatever colours are left.
		var needs = cost
			.Symbols.SelectMany(sym =>
				sym.Kind switch
				{
					SymbolKind.Colored => [new Need([sym.Color])],
					SymbolKind.Hybrid => [new Need([sym.Color, sym.Color2])],
					SymbolKind.TwoBrid => [new Need([sym.Color], sym.Color)],
					SymbolKind.Generic => Enumerable.Repeat(new Need(Any), sym.Amount),
					_ => Enumerable.Repeat(new Need(Any), x),
				}
			)
			.OrderBy(n => n.Allowed.Length + (n.TwoBridColor is null ? 0 : 1))
			.ToImmutableList();

		var plan = new List<Payment>();
		return Solve(needs, sources, new bool[sources.Count], plan) ? plan : null;
	}

	static bool Solve(
		ImmutableList<Need> needs,
		List<Source> sources,
		bool[] used,
		List<Payment> plan
	)
	{
		if (needs.IsEmpty)
			return true;
		// Fewer sources left than mana still owed: fail now. Without this, an unaffordable X
		// backtracked through every order of land kinds for its generic — 5^X paths — and
		// LegalActions asks for every X up to the land count (20 lands hung a test). Checked here,
		// not once up front, because {2/W} grows the need list mid-search.
		if (needs.Count > used.Count(u => !u))
			return false;
		var need = needs[0];
		var rest = needs.RemoveAt(0);
		var tried = new HashSet<(bool, bool, string, ManaColor)>();

		for (var i = 0; i < sources.Count; i++)
		{
			if (used[i])
				continue;
			foreach (var color in sources[i].Colors.Where(need.Allowed.Contains))
			{
				if (
					!tried.Add(
						(
							sources[i].LandId == 0,
							sources[i].IsCreature,
							string.Join(",", sources[i].Colors),
							color
						)
					)
				)
					continue;
				used[i] = true;
				plan.Add(new Payment(sources[i].LandId, color));
				if (Solve(rest, sources, used, plan))
					return true;
				used[i] = false;
				plan.RemoveAt(plan.Count - 1);
			}
		}

		// {2/W} that could not be paid with W: pay two generic instead.
		return need.TwoBridColor is not null
			&& Solve(rest.AddRange([new Need(Any), new Need(Any)]), sources, used, plan);
	}

	/// <summary>
	/// Spends a payment. A land's mana is produced and spent in the same breath, so tapping it is
	/// the whole cost; floating mana leaves the pool.
	/// </summary>
	public static GameState Pay(GameState s, int playerId, IEnumerable<Payment> payments)
	{
		foreach (var p in payments)
		{
			if (p.LandId != 0)
				s = s.UpdateObject(p.LandId, s.GetCard(p.LandId) with { IsTapped = true });
			else
			{
				var player = s.GetPlayer(playerId);
				s = s.WithPlayer(player with { ManaPool = player.ManaPool.Add(p.Color, -1) });
			}
		}
		return s;
	}
}
