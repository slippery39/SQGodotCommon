using System.Linq;
using MagicCore;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class MagicArtTests
{
	/// <summary>
	/// A prepared copy is named after its spell face, and "Seed Suture" is three creatures' face: by
	/// name it showed the first one's image (Shayne's sealed game, 2026-10-23). It shows its own creature.
	/// </summary>
	[Test]
	public void APreparedCopy_ShowsTheCreatureThatPreparedIt_NotAnotherWithTheSameFace()
	{
		const string vanguard = "Vigorbloom Vanguard // Seed Suture";
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5);
		m.AutoplayTo(1);
		m.Sandbox(m.HumanId, CardRegistry.Get(vanguard), ZoneType.Battlefield);
		var creature = m
			.State.CardsIn(m.HumanId, ZoneType.Battlefield)
			.Single(c => c.Name == vanguard);

		var s = Prepare.Become(m.State, creature.Id);
		var copy = s.CardsIn(m.HumanId, ZoneType.Exile).Single(c => c.IsCopy);

		Assert.That(
			copy.Name,
			Is.EqualTo("Seed Suture"),
			"precondition: named after the shared face"
		);
		Assert.That(
			MagicArt.ImageName(s, copy),
			Is.EqualTo($"{vanguard}|FRA"),
			"the creature, as its set prints it"
		);
	}
}
