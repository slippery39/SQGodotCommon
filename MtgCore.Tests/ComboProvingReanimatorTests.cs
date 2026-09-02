using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// **The reanimator package, and the set-wide guards CMB's own header promises.**
///
/// The two cards worth pinning hardest are the tutor and the reanimation spell, because both belong
/// to families this project has shipped BLANK before: a library search filtered with a
/// battlefield-only spec finds nothing, and a reanimation spell whose target list resolves empty
/// does nothing. Neither failure errors.
/// </summary>
[TestFixture]
public class ComboProvingReanimatorTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	private static Card Card(string name) => ComboProving.Cards.Single(c => c.Name == name);

	/// <summary>
	/// **The whole set must not reuse a name from any other set.** `SetRegistry` resolves duplicates
	/// last-registered-wins and CMB registers last, so a shared name silently REPLACES the other
	/// set's card in every union — a behaviour swap with no error and no symptom. The set header
	/// promises this; without the test that promise is prose.
	/// </summary>
	[Test]
	public void NoComboProvingCardReusesAnExistingName()
	{
		var others = SetRegistry
			.All.Where(s => !string.Equals(s.Code, ComboProving.Code, StringComparison.Ordinal))
			.SelectMany(s => s.Cards.Select(c => c.Name))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var collisions = ComboProving.Cards.Select(c => c.Name).Where(others.Contains).ToList();

		Assert.That(
			collisions,
			Is.Empty,
			"these would replace another set's card: " + string.Join(", ", collisions)
		);
	}

	/// <summary>
	/// Names must also be unique WITHIN the set — two cards sharing one name cannot both exist,
	/// since every per-card table downstream is name-keyed.
	/// </summary>
	[Test]
	public void ComboProvingNamesAreUnique()
	{
		var dupes = ComboProving
			.Cards.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		Assert.That(dupes, Is.Empty);
	}

	/// <summary>
	/// The tutor has to find a creature IN THE LIBRARY. `IsCreatureSpecification` is battlefield-only
	/// — it enforces shroud and hexproof, which needs a permanent — so as a library filter it matches
	/// nothing and the card searches and finds nothing, silently. Four shipped cards were blank for
	/// exactly this reason; this asserts the consequence rather than the filter's type.
	/// </summary>
	[Test]
	public void TheTutorPutsACreatureFromTheLibraryIntoTheGraveyard()
	{
		var s = StockLibrary(_state, Card("Aurex, the Sevenfold"), NonCreature("Filler"));

		var after = Resolve(s, Card("Consign to Rot"));

		Assert.That(
			Graveyard(after).Select(c => c.Name),
			Does.Contain("Aurex, the Sevenfold"),
			"the fatty was not entombed — check the library filter is zone-agnostic"
		);
	}

	/// <summary>The control: it must take a CREATURE, not merely the first card it finds.</summary>
	[Test]
	public void TheTutorDoesNotTakeANonCreature()
	{
		var s = StockLibrary(_state, NonCreature("Filler A"), NonCreature("Filler B"));

		var after = Resolve(s, Card("Consign to Rot"));

		Assert.That(Graveyard(after), Is.Empty, "there was no creature to find");
	}

	/// <summary>
	/// **The combo, end to end**: entomb the eight-drop, then reanimate it for one mana. Asserts the
	/// payoff's own trigger fired too — a 7/7 arriving without drawing seven is half a card.
	/// </summary>
	[Test]
	public void EntombThenReanimatePutsTheEightDropIntoPlayAndDrawsSeven()
	{
		var s = StockLibrary(
			_state,
			Card("Aurex, the Sevenfold"),
			NonCreature("A"),
			NonCreature("B"),
			NonCreature("C"),
			NonCreature("D"),
			NonCreature("E"),
			NonCreature("F"),
			NonCreature("G")
		);

		var entombed = Resolve(s, Card("Consign to Rot"));
		var target = Graveyard(entombed).Single(c => c.Name == "Aurex, the Sevenfold");
		var handBefore = HandSize(entombed);

		var after = Resolve(entombed, Card("Raise the Sunken"), target.Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				Battlefield(after).Select(c => c.Name),
				Does.Contain("Aurex, the Sevenfold"),
				"it was not reanimated"
			);
			Assert.That(
				HandSize(after) - handBefore,
				Is.EqualTo(7),
				"the enters-the-battlefield trigger did not draw"
			);
		});
	}

	// ===== helpers =====

	private static Card NonCreature(string name) => new() { Name = name, ManaCost = 1 };

	private IEnumerable<Card> Graveyard(GameState s) => s.GetCardsInZone(_ids.Player1GraveyardId);

	private IEnumerable<Card> Battlefield(GameState s) =>
		s.GetCardsInZone(_ids.Player1BattlefieldId);

	private int HandSize(GameState s) => s.GetChildrenIds(_ids.Player1HandId).Count();

	private GameState StockLibrary(GameState state, params Card[] cards)
	{
		foreach (var card in cards)
			(state, _) = state.AddObject(
				card with
				{
					OwnerId = _ids.Player1Id,
					ControllerId = _ids.Player1Id,
				},
				parentId: _ids.Player1LibraryId
			);
		return state;
	}

	/// Resolves a spell's effects directly, the same way PoolFeatures' probe does — no casting, so
	/// mana and timing cannot mask a card that does nothing.
	private GameState Resolve(GameState state, Card card, int targetId = 0)
	{
		var spell = card.GetComponent<SpellComponent>()!;

		var action = new ResolveEffectAction
		{
			Effects = spell.Effects,
			CastingPlayerId = _ids.Player1Id,
			SourceCardId = 0,
		};

		if (targetId != 0)
			action = action with
			{
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					ImmutableList.Create(targetId)
				),
			};

		var (next, _) = state.AddAction(action).ProcessAllActions();
		return next;
	}
}
