using DoomCore;
using NUnit.Framework;

namespace DoomCore.Tests;

/// <summary>
/// The glossary is data, so what is worth testing is the MATCHING — the part with a rule in it.
/// </summary>
[TestFixture]
public class KeywordTests
{
	[Test]
	public void APhraseOnRealCardTextIsRecognised()
	{
		// Authored exactly as a card carries it — see StarterContent's doom-triggered units.
		var found = KeywordLibrary.In("when the doom fires: 6 to every enemy");

		Assert.That(found.Select(k => k.Name), Does.Contain("Doom"));
	}

	[Test]
	public void AWordInsideALongerWordIsNotAKeyword()
	{
		// **The reason this rule exists.** A substring match reports Rite for "favourite" and Power
		// for "powerful", and reminder text that appears for no reason teaches a player to stop
		// reading the panel.
		var found = KeywordLibrary.In("A favourite of the powerful, and not irradiating anything.");

		Assert.That(found, Is.Empty);
	}

	[Test]
	public void EveryKeywordFindsItselfByItsOwnName()
	{
		foreach (var keyword in KeywordLibrary.All)
			Assert.That(
				KeywordLibrary.In(keyword.Name).Select(k => k.Name),
				Does.Contain(keyword.Name),
				$"{keyword.Name} does not match its own name — check its aliases."
			);
	}

	[Test]
	public void EveryKeywordHasReminderText()
	{
		// A keyword with no text is a panel entry that explains nothing, and it looks like a bug in
		// the panel rather than a gap in the content.
		foreach (var keyword in KeywordLibrary.All)
			Assert.That(keyword.Text, Is.Not.Empty, $"{keyword.Name} has no reminder text.");
	}

	[Test]
	public void NothingIsFoundInEmptyText()
	{
		Assert.That(KeywordLibrary.In("", null, "   "), Is.Empty);
	}
}
