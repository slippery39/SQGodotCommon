using System.Collections.Generic;
using System.Linq;
using MagicCore;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;
using static MagicTable.TableChanges;

namespace SQGodotCommon.Tests;

public class TableChangesTests
{
	/// <summary>
	/// A whole AI-vs-AI game, one step per move: the diff sees what the table animates (TBL-4.5) — casts, what
	/// resolves where, land drops, draws, deaths, damage, life and abilities going on the stack — and a manager not animating keeps nothing.
	/// GreedyAi plays it: seed 2 has a creature surviving damage (15 has none; 1 had until AI-33.4's two-attack
	/// clock); a change in how it plays can move that — pick another seed that has every kind (5, 7 and 8 did).
	/// </summary>
	[Test]
	public void AGameSeenStepByStep_ShowsEveryKindOfChange()
	{
		var quiet = new MagicTableManager(
			FraDecks.All["White-Black"],
			FraDecks.All["Red-Green"],
			15,
			aiVsAi: true,
			search: false
		);
		quiet.StepAi();
		Assert.That(quiet.TakeSteps(), Is.Empty);

		var m = new MagicTableManager(
			FraDecks.All["White-Black"],
			FraDecks.All["Red-Green"],
			2,
			aiVsAi: true,
			search: false
		)
		{
			Animate = true,
		};
		var before = m.State;
		var changes = new List<Change>();
		for (var i = 0; i < 5000 && !m.IsOver; i++)
		{
			m.StepAi();
			foreach (var step in m.TakeSteps())
			{
				changes.AddRange(Between(before, step));
				before = step;
			}
		}
		Assert.That(
			before,
			Is.SameAs(m.State),
			"every move's state was handed over, the last one last"
		);

		bool Saw(ZoneType? from, ZoneType to) =>
			changes.OfType<Moved>().Any(c => c.From == from && c.To == to);
		Assert.Multiple(() =>
		{
			Assert.That(Saw(ZoneType.Hand, ZoneType.Stack), "a cast");
			Assert.That(Saw(ZoneType.Stack, ZoneType.Battlefield), "a permanent spell resolving");
			Assert.That(Saw(ZoneType.Stack, ZoneType.Graveyard), "an instant or sorcery resolving");
			Assert.That(Saw(ZoneType.Hand, ZoneType.Battlefield), "a land played");
			Assert.That(Saw(ZoneType.Library, ZoneType.Hand), "a draw");
			Assert.That(Saw(ZoneType.Battlefield, ZoneType.Graveyard), "a death");
			Assert.That(changes.OfType<Life>().Any(l => l.To < l.From), "life lost");
			Assert.That(changes.OfType<Damaged>().Any(), "damage marked on a survivor");
			Assert.That(changes.OfType<Triggered>().Any(), "an ability going on the stack");
		});
	}
}
