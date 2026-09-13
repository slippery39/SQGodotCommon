using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// The run: life and deck carried across battles, plus where you are on the map.
///
/// **This is the layer MtgCore has no equivalent of.** MtgCore has exactly one GameState per game
/// and nothing above it. Here a GameState is one BATTLE, and the run outlives it — so the run is a
/// plain immutable record, not a GameObject, and each battle is built fresh from it.
///
/// That split is what makes every apocalypse expressible as data: a scenario reads the finished
/// battle and returns a new Run. Nothing mutates in place.
/// </summary>
public record Run
{
	public int Life { get; init; } = 60;
	public int MaxLife { get; init; } = 60;

	/// <summary>1-based position on the act's 20-floor map. Not every floor is a battle.</summary>
	public int Floor { get; init; } = 1;

	public ImmutableList<RunCard> Deck { get; init; } = ImmutableList<RunCard>.Empty;

	/// <summary>
	/// TAG ALONG. Deliberately NOT part of the deck — that is what makes it untouchable by every
	/// doom transform, since all of them operate on <see cref="Deck"/>.
	/// </summary>
	public Companion Companion { get; init; } = new();

	/// <summary>Hands out RunCardIds. Doom transforms mint new cards and must never reuse an id.</summary>
	public int NextRunCardId { get; init; } = 1;

	public int RngSeed { get; init; } = 1;

	/// <summary>Floors in an act. Not every floor is a battle — rests and events fill the rest.</summary>
	public const int ActLength = 20;

	public bool IsDead => Life <= 0;

	/// <summary>
	/// No cards left. Reachable today: Flood removes every unit you did not commit, so committing
	/// nothing to a Flood can empty a starter deck outright.
	///
	/// It is a LOSS, not a stuck state. With no deck there are no blockers and no attackers, so the
	/// run cannot be won and continuing would only be a slow walk to the same place.
	/// Whether Flood should be ABLE to do this is a balance question, not a rules one.
	/// </summary>
	public bool HasNoCards => Deck.IsEmpty;

	public bool IsActComplete => Floor > ActLength;

	public bool IsOver => IsDead || HasNoCards || IsActComplete;

	public string OverReason =>
		IsDead ? "You ran out of life."
		: HasNoCards ? "You ran out of deck — nothing of yours survived."
		: IsActComplete ? "You walked out the other side of the act."
		: "";

	/// <summary>Adds a card, assigning it the next free RunCardId.</summary>
	public Run WithCard(RunCard card) =>
		this with
		{
			Deck = Deck.Add(card with { RunCardId = NextRunCardId }),
			NextRunCardId = NextRunCardId + 1,
		};

	public Run WithCards(IEnumerable<RunCard> cards) =>
		cards.Aggregate(this, (run, card) => run.WithCard(card));

	/// <summary>
	/// Builds a fresh battle from this run AND begins it: the whole deck into Draw, the given
	/// enemies into the enemy zone, shuffled, turn 1 started and the opening hand drawn. Life comes
	/// from the run, so damage taken in the last battle is still on the player — there is no
	/// automatic healing between battles.
	///
	/// Building and beginning are deliberately ONE call. A built-but-unbegun battle looks ready and
	/// has an empty hand, and nothing ever wants one — two steps only bought a silent trap.
	///
	/// Returns the opening events because they can already matter: an Irradiated card costs a life
	/// the moment it is drawn, so the first hand can damage you before you act.
	/// </summary>
	public (GameState State, ImmutableList<GameEvent> Events) StartBattle(
		DoomScenario scenario,
		int countdown,
		IEnumerable<Enemy> enemies,
		int maxEnergy = 3
	)
	{
		var state = DoomBattleFactory.Create(
			scenario,
			countdown,
			life: Life,
			maxLife: MaxLife,
			maxEnergy: maxEnergy,
			rngSeed: RngSeed
		);

		var drawId = state.ZoneId(ZoneType.Draw);
		foreach (var runCard in Deck)
		{
			var card = new DoomCard
			{
				Name = runCard.Name,
				Description = runCard.Description,
				Cost = runCard.Cost,
				RunCardId = runCard.RunCardId,
				Tags = runCard.Tags,
			};

			if (runCard.IsUnit)
				card = (DoomCard)
					card.WithComponent(
						new UnitComponent { Power = runCard.Power, Toughness = runCard.Toughness }
					);

			(state, _) = state.AddObject(card, drawId);
		}

		// The companion is on the field before the first card is drawn, free, every battle. It is
		// given RunCardId 0, which no deck card can hold (ids start at 1), so nothing that maps a
		// battle unit back to a deck entry can ever find it.
		var (withCompanion, _) = state.AddObject(
			(DoomCard)
				new DoomCard
				{
					Name = Companion.FullName,
					Description = Companion.Description,
					Cost = 0,
					RunCardId = 0,
				}
					.WithComponent(
						new UnitComponent
						{
							Power = Companion.Power,
							Toughness = Companion.Toughness,

							// The centre lane. It holds one of the five for free every battle,
							// which is the "the board is never empty" promise made concrete.
							Lane = DoomBattle.LaneCount / 2,
						}
					)
					.WithComponent(new CompanionComponent()),
			state.ZoneId(ZoneType.Field)
		);
		state = withCompanion;

		var enemyZoneId = state.ZoneId(ZoneType.Enemies);
		foreach (var enemy in enemies)
			(state, _) = state.AddObject(enemy, enemyZoneId);

		return state.BeginBattle();
	}

	/// <summary>
	/// Carries the finished battle back into the run: life as it ended, then the apocalypse's
	/// transform, then the floor advances.
	///
	/// Call this once, on a battle whose doom has resolved. A run whose player died is returned
	/// with the death intact and no transform applied — the apocalypse does not tidy up after you.
	/// </summary>
	public Run AfterBattle(GameState finishedBattle)
	{
		var battle = finishedBattle.GetBattle();
		var run = this with
		{
			Life = finishedBattle.GetPlayer().Life,
			RngSeed = finishedBattle.RngSeed,
		};

		if (battle.PlayerIsDead)
			return run;

		// The mark is the whole point of TAG ALONG: it survived this, and it carries that forward.
		return DoomTransforms.Apply(run, finishedBattle) with
		{
			Floor = Floor + 1,
			Companion = Companion.Marked(battle.Scenario),
		};
	}
}
