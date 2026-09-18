using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// The CONTENT, not the mechanism. HolderEffectTests proves effects fire from an enemy; these prove
/// the enemies the game actually ships with are wired up to use them.
///
/// A definition whose effects never reach the battle looks exactly like a definition that works —
/// so these assert through a real battle rather than reading the library back.
/// </summary>
public class ContentDefinitionTests
{
	/// <summary>
	/// **No card may declare `OnTurnStart`, because it could never fire.**
	///
	/// Combat v3 empties the field at the end of every turn, so when `StartTurnAction` fires this
	/// trigger the only thing standing is the companion. A card carrying it would be silently inert
	/// — declared, never fired, and indistinguishable from one that worked, which is the exact
	/// failure this repo keeps rediscovering.
	///
	/// The companion is the one legal holder, and Ash uses it deliberately.
	///
	/// **This becomes legal for a `Persistent` card** (v3 phase 6), which really does survive into
	/// the next turn. Narrow the assertion to non-persistent cards then rather than deleting it.
	/// </summary>
	[Test]
	public void NoCardDeclaresOnTurnStart()
	{
		var cards = Enum.GetValues<DoomTheme>()
			.SelectMany(theme => StarterContent.RewardPool(theme).AsEnumerable())
			.Concat(StarterContent.NewRun().Deck)
			.ToList();

		Assert.That(cards, Is.Not.Empty, "sanity: the pools loaded");

		foreach (var card in cards)
			Assert.That(
				card.Effects.Any(e => e.Trigger == EffectTrigger.OnTurnStart),
				Is.False,
				$"{card.Name} declares OnTurnStart, and the field is empty when that fires — "
					+ "it would never happen and would look exactly like a card that worked"
			);
	}

	/// <summary>
	/// The companion's ability actually reaches the battle. A `DoomEffect` sitting on the run's
	/// companion record and never copied onto its battle card is the same silent no-op.
	/// </summary>
	[Test]
	public void TheCompanionCarriesItsAbilityIntoBattle()
	{
		var run = StarterContent.NewRun();
		Assert.That(run.Companion.Effects, Is.Not.Empty, "Ash is authored with an ability");

		var (state, _) = run.StartBattle(DoomScenario.Flood, countdown: 9, [], opponentHealth: 500);
		var ash = state.Units().Single(u => u.HasComponent<CompanionComponent>());

		Assert.That(
			ash.Effects,
			Is.EqualTo(run.Companion.Effects),
			"the ability has to be ON the battle card, or it never fires"
		);
	}

	[Test]
	public void AFloorOnlyFieldsEnemiesItIsAllowed()
	{
		foreach (var floor in new[] { 1, 2, 3, 5, 9, 20 })
		{
			var names = StarterContent.EnemiesFor(floor).Select(e => e.Name).ToList();
			var allowed = EnemyLibrary.PlayableOn(floor).Select(d => d.Name).ToList();

			// Membership, not Is.SubsetOf — that treats a repeat as an extra item, and a floor
			// fielding two Wretches is entirely legal.
			foreach (var name in names)
				Assert.That(
					allowed,
					Does.Contain(name),
					$"floor {floor} fielded {name}, which its roster does not allow"
				);
		}
	}

	/// <summary>
	/// The roster IS the curve, so a floor that unlocks a tier must actually lead with it —
	/// otherwise a new enemy might not be seen for several floors and the curve would be a lie.
	/// </summary>
	/// <summary>
	/// **No ordinary fight repeats inside an act.** One Opponent crossed with its traits has to
	/// cover every ordinary battle in the act, and this is the assertion that keeps that true as
	/// either list changes — it failed the moment bosses left the ordinary roster, because seven
	/// traits could not cover ten battles.
	///
	/// **The boss floor is excluded**: a boss wears no trait, so it would collide with the untraited
	/// ordinary fight. That collision is what forced bosses out of `AllOpponents` in the first
	/// place — act 1's boss was also the Opponent for floors 10-14.
	/// </summary>
	[Test]
	public void NoOpponentIsFoughtTwiceWearingTheSameTrait()
	{
		foreach (var seed in new[] { 1, 7, 42, 1000 })
		{
			var fought = Enumerable
				.Range(1, Run.ActLength - 1)
				.Where(f => StarterContent.FloorKindFor(f) == FloorKind.Battle)
				.Select(f => StarterContent.OpponentFor(f, seed).Name)
				.ToList();

			Assert.That(
				fought,
				Is.Unique,
				$"seed {seed} fought the same Opponent twice: "
					+ string.Join(
						", ",
						fought.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key)
					)
			);
		}
	}

	/// <summary>The act has to end on something that only appears there.</summary>
	[Test]
	public void TheActEndsOnABossFoughtNowhereElse()
	{
		// **Checked for EVERY act, because every act has its own boss now.** It used to be one
		// Opponent gated by MinFloor; the boss is the act's content and lives on its theme.
		foreach (var (theme, index) in ActMap.Order.Select((t, i) => (t, i)))
		{
			var bossFloor = (index + 1) * ActMap.ActLength;
			var boss = StarterContent.OpponentFor(bossFloor, seed: 1);

			Assert.That(
				boss.Effects,
				Is.Not.Empty,
				$"{boss.Name} does nothing, which makes it just a bigger number"
			);

			// **The boss must not be fought on the way to itself.** It was: act 1's boss was also
			// the Opponent for floors 10-14, so the finale turned up five times before the finale.
			var ordinary = Enumerable
				.Range(1, ActMap.ActLength - 1)
				.Where(f => ActMap.Layout[f - 1] == FloorKind.Battle)
				.Select(f => StarterContent.OpponentFor(index * ActMap.ActLength + f, seed: 1).Name)
				.ToList();

			Assert.That(
				ordinary,
				Has.None.Contains(ThemeLibrary.Of(theme).Boss.Name),
				$"act {index + 1} fights its boss before the last floor"
			);
		}
	}

	/// <summary>
	/// Asserts the RULE, not the roster. These used to name "Herald of the End" on floor 3 and
	/// "The Choir" on floor 4, and a balance pass that moved a `MinFloor` broke them while the
	/// mechanism was working perfectly — the same reason cards are defined inline in these tests.
	/// </summary>
	[Test]
	public void AFloorLeadsWithTheHardestThingItAllows()
	{
		foreach (var floor in Enumerable.Range(1, Run.ActLength))
		{
			var lead = StarterContent.EnemiesFor(floor).First();
			var hardest = EnemyLibrary.PlayableOn(floor).Max(e => e.MinFloor);

			// EnemiesFor hands back placed Enemy objects, which carry no MinFloor — the tier has
			// to come back from the definition the name belongs to.
			var tier = EnemyLibrary.All.First(e => e.Name == lead.Name).MinFloor;

			Assert.That(
				tier,
				Is.EqualTo(hardest),
				$"floor {floor} led with {lead.Name}, which is not from the newest tier it allows"
			);
		}
	}

	[Test]
	public void AnOpponentIsChosenFromContentAndCarriesItsOwnReinforcement()
	{
		// **Boss floors are skipped: they bypass the roster entirely** and field the act's own
		// boss, which is content on the theme rather than a tier gated by MinFloor.
		foreach (var floor in Enumerable.Range(1, Run.ActLength - 1))
		{
			var opponent = StarterContent.OpponentFor(floor, seed: 1);

			Assert.That(opponent.MinFloor, Is.LessThanOrEqualTo(floor));
			Assert.That(
				EnemyLibrary.AllOpponents.Where(o => o.MinFloor <= floor).Max(o => o.MinFloor),
				Is.EqualTo(opponent.MinFloor),
				$"floor {floor} fielded {opponent.Name} while a later Opponent was already legal"
			);

			// The Opponent decides what it fields; EndTurnAction asks IT, not the content tables.
			Assert.That(opponent.Reinforcement.Name, Is.Not.Empty);
		}

		// And the boss floor does field a boss, with a reinforcement of its own.
		var last = StarterContent.OpponentFor(Run.ActLength, seed: 1);
		Assert.That(
			last.Name,
			Is.EqualTo(ThemeLibrary.Of(ActMap.ThemeFor(Run.ActLength)).Boss.Name)
		);
		Assert.That(last.Reinforcement.Name, Is.Not.Empty);
	}

	/// <summary>
	/// The whole slice in one assertion: a library enemy, placed by the content layer, firing its
	/// authored effect through a real battle with no engine change behind it.
	/// </summary>
	[Test]
	public void AHeraldFromTheLibraryHurtsYouWhenItDies()
	{
		var run = new Run { Life = 50, MaxLife = 50 }.WithCards(
			[
				new RunCard
				{
					Name = "Executioner",
					Cost = 0,
					IsUnit = true,

					// Sized off the Herald itself so it one-shots whatever the library says it is.
					// A literal 20 stopped killing it the moment its health moved, and the test
					// then failed for a reason that had nothing to do with on-death effects.
					Power = EnemyLibrary.HeraldOfTheEnd.Health,
					Toughness = EnemyLibrary.HeraldOfTheEnd.Attack * 2,
				},
			]
		);

		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 9,
			[EnemyLibrary.HeraldOfTheEnd.ToEnemy(lane: 0)],
			opponent: EnemyLibrary.TheOpponent
		);

		var executioner = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Executioner");
		(state, _) = state
			.AddAction(new PlayCardAction { CardId = executioner.Id, Lane = 0 })
			.ProcessAllActions();

		var lifeBefore = state.GetPlayer().Life;

		// Read the cost off the DEFINITION rather than restating it. This asserted a literal 4 and
		// broke on a balance pass that moved the Herald's burn to 2, while the thing under test —
		// that an authored on-death effect fires at all — was working the whole time.
		var burn = -(
			(DealDamageAction)
				EnemyLibrary
					.HeraldOfTheEnd.Effects.Single(e => e.Trigger == EffectTrigger.OnDeath)
					.Template
		).Amount;

		(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

		Assert.That(state.LivingEnemies(), Is.Empty, "the Herald should be dead");
		Assert.That(burn, Is.Not.Zero, "the Herald's on-death effect is authored to do nothing");
		Assert.That(
			state.GetPlayer().Life,
			Is.EqualTo(lifeBefore + burn),
			"its authored on-death effect should have fired — content, not a code branch"
		);
	}

	/// <summary>
	/// A reinforcement has to arrive with its behaviour. Carried through PendingSummon, because a
	/// Herald summoned mid-battle that forgot its effect would be a Herald in name only.
	/// </summary>
	[Test]
	public void AReinforcementKeepsTheEffectsOfWhatItIs()
	{
		var summon = EnemyLibrary.HeraldOfTheEnd.ToSummon(lane: 2);

		Assert.That(summon.Effects, Is.Not.Empty);
		Assert.That(summon.ToEnemy().Effects, Is.Not.Empty, "and survives becoming a real enemy");
	}
}
