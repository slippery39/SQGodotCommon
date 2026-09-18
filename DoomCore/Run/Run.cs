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

	/// <summary>
	/// The act you are standing in. **DERIVED from the floor, not chosen and not stored.**
	///
	/// A run used to be one act, picked at the start. It is all three now, in the fixed order in
	/// <see cref="ActMap.Order"/> — so the theme is a question about where you are, and storing it
	/// alongside the floor would simply be two facts that could disagree.
	/// </summary>
	public DoomTheme Theme => ActMap.ThemeFor(Floor);

	/// <summary>
	/// Floors in an act. **Derived from <see cref="ActMap.Layout"/>** so the length and the shape
	/// can never disagree — it was a hand-maintained `const 20` beside a `floor % 4` rule, and the
	/// two had to be kept in step by hand.
	/// </summary>
	public static int ActLength => ActMap.ActLength;

	/// <summary>Floors in the whole run, all acts chained.</summary>
	public static int RunLength => ActMap.RunLength;

	/// <summary>
	/// Spent at a shop. **Survives an act break** — banking through to the next act's shop is a
	/// real decision, and clearing it would delete that.
	/// </summary>
	public int Gold { get; init; }

	/// <summary>Which act this floor is in, 0-based. Derived; never stored, so it cannot drift.</summary>
	public int ActIndex => ActMap.ActIndexFor(Floor);

	/// <summary>The act's position within itself, 1-based — what content should ask about.</summary>
	public int FloorInAct => ActMap.FloorInAct(Floor);

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

	/// <summary>
	/// **The whole RUN is done, all three acts.** Named for what it has always meant to callers —
	/// "there is no floor below this one" — rather than renamed across the console, the simulator
	/// and the front end for a structural change none of them care about.
	/// </summary>
	public bool IsActComplete => Floor > RunLength;

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
		int maxEnergy = 3,
		int opponentHealth = 40,
		OpponentDefinition? opponent = null
	)
	{
		var state = DoomBattleFactory.Create(
			scenario,
			countdown,
			life: Life,
			maxLife: MaxLife,
			maxEnergy: maxEnergy,
			rngSeed: RngSeed,
			opponentHealth: opponentHealth,
			opponent: opponent
		);

		var drawId = state.ZoneId(ZoneType.Draw);
		foreach (var runCard in Deck)
		{
			(state, _) = state.AddObject(runCard.ToDoomCard(), drawId);
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

					// The ability rides on the battle card like any other holder's. Nothing in the
					// effect system knows or cares that this one is the companion.
					Effects = Companion.Effects,
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
	/// Takes the rest and walks on. Heals, capped at <see cref="MaxLife"/>, and advances the floor.
	///
	/// The floor still advances — a rest COSTS a floor of the act, which is the whole trade. Twenty
	/// floors with four rests is sixteen battles, and that is what makes the life budget stretch.
	/// </summary>
	public Run Rest(int heal) =>
		this with
		{
			Life = Math.Min(MaxLife, Life + heal),
			Floor = Floor + 1,
		};

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

		// Paid for clearing, not for surviving — a battle you lost pays nothing, and the line above
		// has already returned by then.
		run = run with
		{
			Gold = run.Gold + StarterContent.GoldFor(Floor),
		};

		// Killing the Opponent before the first firing means no apocalypse happened. Applying a
		// transform anyway would rewrite the deck for an event the player never saw — and it would
		// read as a bug, because the doom preview would have shown it coming and then it didn't.
		if (battle.DoomsFired == 0)
			return Advance(run);

		// The mark is the whole point of TAG ALONG: it survived this, and it carries that forward.
		return Advance(
			DoomTransforms.Apply(run, finishedBattle) with
			{
				Companion = Companion.Marked(battle.Scenario),
			}
		);
	}

	/// <summary>
	/// Steps onto the next floor — and **restores you to full at an act break**.
	///
	/// A life budget tuned for fifteen battles does not stretch over twenty-four, and an act break
	/// is the natural place to give some back: you have just killed the thing the act was built
	/// around, and the world changes over.
	///
	/// **It was a FULL heal and is now partial — see `StarterContent.ActBreakHealFor`.** Restoring
	/// everything meant the life budget never bound: an act cost about 90 life, the two rests gave
	/// back 72 of it, and the break returned whatever was left. Run 20 measured one death in roughly
	/// fourteen hundred ordinary battles as a result.
	///
	/// Gold is deliberately NOT cleared — banking through to the next act's shop is a decision, and
	/// wiping it would delete one.
	/// </summary>
	private Run Advance(Run run) =>
		ActMap.IsActBreak(run.Floor)
			? run with
			{
				Floor = run.Floor + 1,
				Life = Math.Min(
					run.MaxLife,
					run.Life + StarterContent.ActBreakHealFor(run.MaxLife)
				),
			}
			: run with
			{
				Floor = run.Floor + 1,
			};
}
