using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Power-level floors for the set. These are not balance opinions — they catch cards that are
/// mathematically worse than another card doing the same job, which is the failure mode that
/// produced an unplayable Windswept Chorus sitting next to Lingering Souls.
///
/// Deliberately narrow: only comparisons the code can make honestly. Judging whether a card is
/// *interesting* is not automatable and stays a human review job.
/// </summary>
[TestFixture]
public class HollowmereRateTests
{
	private static IReadOnlyList<Card> Cards => Hollowmere.Cards;

	private static bool HasAnyAbility(Card c) =>
		c.GetComponents<TriggeredAbilityComponent>().Any()
		|| c.GetComponents<ActivatedAbilityComponent>().Any()
		|| c.GetComponents<StaticAbilityComponent>().Any()
		|| c.GetComponents<ThresholdComponent>().Any()
		|| c.HasComponent<FlashbackComponent>()
		|| c.HasComponent<TransformComponent>()
		|| c.HasComponent<GraveyardCountComponent>();

	private static CreatureComponent? Body(Card c) => c.GetComponent<CreatureComponent>();

	private static int KeywordCount(CreatureComponent b) =>
		(b.HasFlying ? 1 : 0)
		+ (b.HasReach ? 1 : 0)
		+ (b.HasTaunt ? 1 : 0)
		+ (b.HasLifelink ? 1 : 0)
		+ (b.HasTrample ? 1 : 0)
		+ (b.HasDeathtouch ? 1 : 0)
		+ (b.HasHaste ? 1 : 0)
		+ (b.HasDoubleStrike ? 1 : 0);

	/// <summary>
	/// A creature with no abilities is pure body, so it has to pay rate. Keywords count as
	/// roughly a point of stats each, which is why the check is not simply power + toughness —
	/// a 2/2 flyer and a 3/2 ground creature are close in value.
	///
	/// The floor is deliberately low: it flags cards that are below *limited* rate, not cards
	/// that merely fall short of cube. Anything it catches is unarguably weak.
	/// </summary>
	[Test]
	public void AbilitylessCreatures_MeetTheRateFloor()
	{
		var offenders = Cards
			.Where(c => Body(c) is not null && !HasAnyAbility(c))
			.Where(c =>
				Body(c)!.Power + Body(c)!.Toughness + KeywordCount(Body(c)!) < c.ManaCost * 2 + 1
			)
			.Select(c =>
				$"{c.Name} ({c.ManaCost}cc {Body(c)!.Power}/{Body(c)!.Toughness} "
				+ $"+{KeywordCount(Body(c)!)}kw)"
			)
			.ToList();

		Assert.That(offenders, Is.Empty, $"Under-rate: {string.Join("; ", offenders)}");
	}

	/// <summary>
	/// Catches the Windswept Chorus failure directly: two cards that create the same tokens,
	/// where one costs more for the same or fewer bodies. Flashback cost counts, because that
	/// is where the two differed.
	/// </summary>
	[Test]
	public void NoTokenMaker_IsStrictlyWorseThanAnother()
	{
		var makers = Cards.Select(TokenProfile).Where(p => p != null).Cast<TokenInfo>().ToList();

		var offenders = new List<string>();
		foreach (var a in makers)
		foreach (var b in makers)
		{
			if (a.Name == b.Name || a.Token != b.Token)
				continue;

			// b is at least as cheap on both castings and makes at least as many bodies,
			// and beats a on at least one axis.
			var bNoWorse =
				b.ManaCost <= a.ManaCost && b.TotalCost <= a.TotalCost && b.Bodies >= a.Bodies;
			var bBetter =
				b.ManaCost < a.ManaCost || b.TotalCost < a.TotalCost || b.Bodies > a.Bodies;

			if (bNoWorse && bBetter)
				offenders.Add($"{a.Name} is strictly worse than {b.Name}");
		}

		Assert.That(offenders, Is.Empty, string.Join("; ", offenders.Distinct()));
	}

	private sealed record TokenInfo(
		string Name,
		string Token,
		int Bodies,
		int ManaCost,
		int TotalCost
	);

	/// Total bodies and total mana across both castings, which is how a flashback token
	/// maker is actually evaluated in a draft.
	private static TokenInfo? TokenProfile(Card card)
	{
		var spell = card.GetComponent<SpellComponent>();
		if (spell == null)
			return null;

		var actions = spell.Effects.Select(e => e.ActionTemplate).ToList();

		// Only cards whose ENTIRE text is making tokens compare cleanly. A spell that also
		// reanimates or mills is doing a second job, and calling it "strictly worse" than a
		// pure token maker would be wrong — that false positive is why this guard exists.
		if (actions.Count == 0 || actions.Any(a => a is not CreateCardAction))
			return null;

		var creates = actions.Cast<CreateCardAction>().ToList();

		// Only single-token spells compare cleanly; a mixed-token spell is its own thing.
		var tokenNames = creates.Select(c => c.CardTemplate.Name).Distinct().ToList();
		if (tokenNames.Count != 1)
			return null;

		var flashback = card.GetComponent<FlashbackComponent>();
		var castings = flashback == null ? 1 : 2;
		var bodies = creates.Sum(c => c.Count) * castings;
		var totalCost = card.ManaCost + (flashback?.FlashbackManaCost ?? 0);

		return new TokenInfo(card.Name, tokenNames[0], bodies, card.ManaCost, totalCost);
	}
}
