using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE REGIONS — authored, and harder as the run goes on** (`KinEnemiesPlan.md`, 2026-10-03). Foes
/// are authored at their region's strength (no levels); wild fights are authored groups. Reads the
/// real content, never restates its numbers.
/// </summary>
public partial class PartyRunTests
{
	private static IEnumerable<Foe> WildFoes(Region region) =>
		region.Easy.Concat(region.Normal).SelectMany(e => e.Foes);

	/// <summary>
	/// **CRUSH is for FRAGILE foes, and small** (playtest 2026-10-02: a 22-HP Boar's CRUSH 10 ended a
	/// run on floor 3 — nothing could answer it). In region 1 a crusher has at most 12 HP and crushes
	/// for at most 4. Kill it first, or take a little — never a run-ender.
	/// </summary>
	[Test]
	public void TheFirstRegionsCrushersAreFragileAndSmall()
	{
		var crushers = WildFoes(PartyWorld.Regions[0])
			.Where(f => f.Pattern.Any(i => i.Crushes))
			.ToList();

		Assert.That(crushers, Is.Not.Empty, "the choice — kill it first or take a little — exists");
		foreach (var foe in crushers)
		{
			Assert.That(foe.MaxHp, Is.LessThanOrEqualTo(12), foe.Name);
			Assert.That(
				foe.Pattern.Where(i => i.Crushes).Select(i => i.Amount),
				Is.All.LessThanOrEqualTo(4),
				foe.Name
			);
		}
	}

	[Test]
	public void EveryRegionHasAuthoredEasyAndNormalFightsAndItsExams()
	{
		foreach (var region in PartyWorld.Regions)
		{
			Assert.That(region.Easy, Has.Count.EqualTo(3), region.Name);
			Assert.That(region.Normal, Has.Count.EqualTo(6), region.Name);
			Assert.That(region.Bosses, Is.Not.Empty, region.Name);
			Assert.That(region.Elites, Is.Not.Empty, region.Name);
		}
	}

	/// <summary>
	/// **Each region is harder than the last** (the STS-steep curve): its normal fights carry more HP
	/// and hit harder, and so do its bosses.
	/// </summary>
	[Test]
	public void EachRegionsFoesAreTougherThanTheLast()
	{
		static double Hp(IEnumerable<Encounter> fights) =>
			fights.Average(e => e.Foes.Sum(f => f.MaxHp));
		static double Hits(IEnumerable<Encounter> fights) =>
			fights.Average(e =>
				e.Foes.SelectMany(f => f.Pattern)
					.Where(i => i.Kind == IntentType.Attack)
					.Sum(i => i.Amount)
			);

		for (var r = 1; r < PartyWorld.Regions.Count; r++)
		{
			var (before, now) = (PartyWorld.Regions[r - 1], PartyWorld.Regions[r]);
			Assert.That(Hp(now.Normal), Is.GreaterThan(Hp(before.Normal)), now.Name);
			Assert.That(Hits(now.Normal), Is.GreaterThan(Hits(before.Normal)), now.Name);
			Assert.That(
				now.Bosses.Min(b => b.Foes.Max(f => f.MaxHp)),
				Is.GreaterThan(before.Bosses.Max(b => b.Foes.Max(f => f.MaxHp))),
				now.Name
			);
		}
	}

	[Test]
	public void TheRunIsThreeRegionsAndNoneFieldsYourOwnSpecies()
	{
		var yours = PartyContent
			.Families.SelectMany(PartyContent.PoolOf)
			.Select(m => m.Name)
			.ToHashSet();

		Assert.That(PartyWorld.Regions, Has.Count.EqualTo(3));
		foreach (var region in PartyWorld.Regions)
			Assert.That(WildFoes(region).Where(f => yours.Contains(f.Name)), Is.Empty, region.Name);
	}
}
