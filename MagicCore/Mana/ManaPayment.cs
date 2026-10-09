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
/// It solves UNITS of mana, not permanents: tapping a permanent makes its whole yield
/// (ManaAbilities.YieldOf) — Loot, the Nexus makes several of one colour, a Mountain under Molten
/// Tide an extra {R}. A permanent is tapped once, and what the payment doesn't use floats.
///
/// ponytail: spends floating mana first, then lands with the fewest colours. Arena's tapper also
/// keeps colours the rest of your hand needs. That trigger has fired — the FRA decks play duals
/// since batch 7a, so a {1}{W} can still tap the Island a {U}{U} in hand needed. Next to build.
/// </summary>
public static class ManaPayment
{
	/// <summary>
	/// One unit of mana: from a permanent tapped for it, or floating (LandId 0). Floats: made by a
	/// tapped permanent but not spent — it goes to the pool.
	/// </summary>
	public record Payment(int LandId, ManaColor Color, bool Floats = false);

	/// <summary>
	/// A permanent (or one floating mana) and what tapping it makes. Kind: what makes two sources the
	/// same choice — two Forests, two Mountains under Molten Tide — so the search tries one of them.
	/// </summary>
	record Source(int LandId, bool IsCreature, bool SameColor, ImmutableArray<Unit> Units)
	{
		public string Kind { get; } =
			$"{LandId == 0}|{IsCreature}|{SameColor}|{string.Join(";", Units.Select(u => string.Join(",", u.Colors)))}";
	}

	/// <summary>One mana a source makes, and the colours it may be.</summary>
	record Unit(int Source, ImmutableArray<ManaColor> Colors);

	/// <summary>Allowed colours for one mana; TwoBridColor set means "this, or two generic instead".</summary>
	record Need(ImmutableArray<ManaColor> Allowed, ManaColor? TwoBridColor = null);

	static readonly ImmutableArray<ManaColor> Any = [.. Enum.GetValues<ManaColor>()];

	/// <param name="forSpellFromHand">Casting a spell from the hand — Heartwood Crafter's mana can't pay for that.</param>
	public static IReadOnlyList<Payment>? Find(
		GameState s,
		int playerId,
		ManaCost cost,
		int x,
		int excludeId = 0,
		bool convoke = false,
		bool forSpellFromHand = false
	)
	{
		var player = s.GetPlayer(playerId);
		var tappable =
			new List<(
				int Id,
				bool IsCreature,
				bool SameColor,
				ImmutableArray<ImmutableArray<ManaColor>> Units
			)>();
		foreach (var c in s.CardsIn(playerId, ZoneType.Battlefield))
		{
			if (c.Id == excludeId)
				continue;
			var yield = ManaAbilities.CanTap(s, c) ? ManaAbilities.YieldOf(s, c) : null;
			if (yield is not null && forSpellFromHand && yield.NotForSpellsFromHand)
				yield = null; // its mana can't pay — but it may still convoke
			// Convoke (CR 702.51): an untapped creature you control may pay {1} or one mana of its
			// colour — Colorless is how it pays generic only. Summoning sickness doesn't matter:
			// tapping for convoke isn't a {T} cost. A one-mana creature may pay either way.
			ImmutableArray<ManaColor>? convokes =
				convoke && c.Is(CardType.Creature) && !c.IsTapped
					? [.. c.Colors, ManaColor.Colorless]
					: null;
			if (yield is not null && convokes is { } own && yield.Units.Length == 1)
				tappable.Add(
					(
						c.Id,
						true,
						false,
						[
							[.. yield.Units[0].Union(own)],
						]
					)
				);
			else if (yield is not null)
				tappable.Add((c.Id, c.Is(CardType.Creature), yield.SameColor, yield.Units));
			else if (convokes is { } only)
				tappable.Add((c.Id, true, false, [only]));
		}
		var sources = new List<Source>();
		foreach (var color in Any)
			for (var i = 0; i < player.ManaPool[color]; i++)
				sources.Add(new(0, false, false, [new(sources.Count, [color])]));
		// Lands before creatures: a creature tapped for mana can no longer attack or block.
		foreach (
			var (id, isCreature, sameColor, units) in tappable
				.OrderBy(p => p.IsCreature)
				.ThenBy(p => p.Units.Sum(u => u.Length))
		)
			sources.Add(
				new(id, isCreature, sameColor, [.. units.Select(u => new Unit(sources.Count, u))])
			);
		var all = sources.SelectMany(src => src.Units).ToList();

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

		var search = new Search(sources, all);
		if (!search.Solve(needs))
			return null;
		// What a tapped permanent made and the payment didn't use floats (it empties with the step).
		var plan = search.Plan.ToList();
		for (var u = 0; u < all.Count; u++)
		{
			var src = sources[all[u].Source];
			if (!search.Used[u] && src.LandId != 0 && search.Tapped(all[u].Source))
				plan.Add(
					new Payment(
						src.LandId,
						search.Locked[all[u].Source] ?? all[u].Colors[0],
						Floats: true
					)
				);
		}
		return plan;
	}

	sealed class Search(List<Source> sources, List<Unit> units)
	{
		public readonly bool[] Used = new bool[units.Count];
		public readonly ManaColor?[] Locked = new ManaColor?[sources.Count];
		public readonly List<Payment> Plan = [];
		readonly int[] spent = new int[sources.Count];
		int unused = units.Count;

		public bool Tapped(int source) => spent[source] > 0;

		public bool Solve(ImmutableList<Need> needs)
		{
			if (needs.IsEmpty)
				return true;
			// Fewer units left than mana still owed: fail now. Without this, an unaffordable X
			// backtracked through every order of land kinds for its generic — 5^X paths — and
			// LegalActions asks for every X up to the land count (20 lands hung a test). Checked
			// here, not once up front, because {2/W} grows the need list mid-search.
			if (needs.Count > unused)
				return false;
			var need = needs[0];
			var rest = needs.RemoveAt(0);
			var tried = new HashSet<(string, int, ManaColor?, string, ManaColor)>();

			// A permanent already tapped first: its other mana is free, a new one costs a tap.
			foreach (var alreadyTapped in new[] { true, false })
				for (var i = 0; i < units.Count; i++)
				{
					var source = units[i].Source;
					if (Used[i] || Tapped(source) != alreadyTapped)
						continue;
					var src = sources[source];
					var colors = Locked[source] is { } locked ? [locked] : units[i].Colors;
					foreach (var color in colors.Where(need.Allowed.Contains))
					{
						// The same kind of source in the same state is the same choice.
						var key = (
							src.Kind,
							spent[source],
							Locked[source],
							string.Join(",", units[i].Colors),
							color
						);
						if (!tried.Add(key))
							continue;
						var lockedBefore = Locked[source];
						if (src.SameColor)
							Locked[source] = color;
						Used[i] = true;
						unused--;
						spent[source]++;
						Plan.Add(new Payment(src.LandId, color));
						if (Solve(rest))
							return true;
						Used[i] = false;
						unused++;
						spent[source]--;
						Locked[source] = lockedBefore;
						Plan.RemoveAt(Plan.Count - 1);
					}
				}

			// {2/W} that could not be paid with W: pay two generic instead.
			return need.TwoBridColor is not null
				&& Solve(rest.AddRange([new Need(Any), new Need(Any)]));
		}
	}

	/// <summary>
	/// Spends a payment. A permanent's mana is produced and spent in the same breath, so tapping it
	/// is the cost; floating mana leaves the pool; what a tapped permanent made beyond the cost floats.
	/// </summary>
	public static GameState Pay(GameState s, int playerId, IEnumerable<Payment> payments)
	{
		foreach (var p in payments)
		{
			if (p.LandId != 0)
				s = s.UpdateObject(p.LandId, s.GetCard(p.LandId) with { IsTapped = true });
			if (p.LandId == 0 || p.Floats)
			{
				var player = s.GetPlayer(playerId);
				s = s.WithPlayer(
					player with
					{
						ManaPool = player.ManaPool.Add(p.Color, p.Floats ? 1 : -1),
					}
				);
			}
		}
		return s;
	}
}
