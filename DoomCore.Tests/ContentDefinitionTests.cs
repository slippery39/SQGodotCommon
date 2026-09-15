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
	[Test]
	public void AFloorLeadsWithTheHardestThingItAllows()
	{
		Assert.That(StarterContent.EnemiesFor(1).First().Name, Is.EqualTo("Wretch"));
		Assert.That(StarterContent.EnemiesFor(3).First().Name, Is.EqualTo("Herald of the End"));
		Assert.That(StarterContent.EnemiesFor(6).First().Name, Is.EqualTo("Siege Hulk"));
	}

	[Test]
	public void AnOpponentIsChosenFromContentAndCarriesItsOwnReinforcement()
	{
		Assert.That(StarterContent.OpponentFor(1).Name, Is.EqualTo("The Opponent"));
		Assert.That(StarterContent.OpponentFor(4).Name, Is.EqualTo("The Choir"));
		Assert.That(StarterContent.OpponentFor(9).Name, Is.EqualTo("The Last Warden"));

		Assert.That(
			StarterContent.OpponentFor(9).Reinforcement.Name,
			Is.EqualTo("Siege Hulk"),
			"the Opponent decides what it fields; EndTurnAction asks IT, not the content tables"
		);
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
					Power = 20,
					Toughness = 20,
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
		(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

		Assert.That(state.LivingEnemies(), Is.Empty, "the Herald should be dead");
		Assert.That(
			state.GetPlayer().Life,
			Is.EqualTo(lifeBefore - 4),
			"and its authored on-death effect should have cost 4 — content, not a code branch"
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
