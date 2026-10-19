using MagicCore.Cards;

namespace MagicCore.Tests;

/// <summary>
/// The reads under every simulated step allocate nothing (story AI-7.3c: LINQ over a card's
/// components boxed the ImmutableArray — 736 B a keyword question, and the search allocated 36 GB a
/// position suite). Written back as LINQ they still answer right, so only this test notices.
/// </summary>
public class HotPathAllocationTests
{
	[Test]
	public void ReadingACreature_AllocatesNothing()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		int id;
		(s, id) = TestGames.Put(
			s,
			me,
			TestGames.Creature("Bear", "2/2", "Flying"),
			ZoneType.Battlefield
		);
		var bear = s.GetCard(id);
		void Read()
		{
			s.Power(bear);
			s.Toughness(bear);
			s.IsIndestructible(bear);
			s.Has(bear, "Flying");
			bear.HasOwn("Trample");
		}
		Read(); // the board is built once per state; that may allocate

		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < 100; i++)
			Read();

		Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
	}
}
