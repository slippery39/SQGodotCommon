using System;
using System.Linq;
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
	public void ReminderTextStaysTight()
	{
		// **The fluff generator is: state the rule, then restate it as a consequence.** Rite said
		// "not a body", "goes to the discard pile" AND "never holds a lane" — three spellings of
		// one fact. Irradiated said the mark was permanent, then that it lasted the run.
		//
		// The rule is NOT "keep it short": Toughness' second sentence (damage beyond it hits the
		// face behind) is an independent rule and nothing else in the game says it. The rule is
		// **cut any clause the player can derive from the clause before it** — which no test can
		// check. So this checks the SHAPE that sprawl always shows up in, and leaves the judgement
		// to the author.
		//
		// It asserts nothing about what any keyword SAYS, so a wording pass never breaks it.
		foreach (var keyword in KeywordLibrary.All)
		{
			var words = keyword.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
			Assert.That(
				words,
				Has.Length.LessThanOrEqualTo(20),
				$"{keyword.Name}'s reminder text is {words.Length} words. Cut the clause that "
					+ "repeats the one before it; keep any that adds a rule."
			);

			var sentences = keyword.Text.Count(c => c == '.');
			Assert.That(
				sentences,
				Is.LessThanOrEqualTo(2),
				$"{keyword.Name}'s reminder text is {sentences} sentences. Two is the ceiling: "
					+ "the rule, and at most one thing that does not follow from it."
			);
		}
	}

	[Test]
	public void NothingIsFoundInEmptyText()
	{
		Assert.That(KeywordLibrary.In("", null, "   "), Is.Empty);
	}
}
