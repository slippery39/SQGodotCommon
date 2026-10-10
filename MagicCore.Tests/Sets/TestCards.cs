using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;

namespace MagicCore.Tests.Sets;

/// <summary>
/// Inline test cards a card test keeps needing — use these in new test files instead of writing them
/// again (the FRA batch files each grew their own Kill, Shock, Typed …). Tests never depend on a real
/// card's text, so these are inline, and their numbers are the test's own.
/// </summary>
public static class TestCards
{
	/// <summary>"{1} instant: destroy target [kind]" — a creature by default.</summary>
	public static CardDefinition Kill(TargetKind kind = TargetKind.Creature) =>
		Targeted("Kill", kind, new Destroy(Ref.Target()));

	/// <summary>"{1} instant: N damage to target [kind]".</summary>
	public static CardDefinition Shock(int n, TargetKind kind = TargetKind.Creature) =>
		Targeted("Shock", kind, new DealDamage(Ref.Target(), n));

	/// <summary>A {1} instant with one target word and these effects.</summary>
	public static CardDefinition Targeted(
		string name,
		TargetKind kind,
		params GameAction[] effects
	) =>
		new(
			TestGames.Inline(name, "{1}", "Instant").Data,
			[new SpellEffectsComponent([.. effects]) { Targets = [new TargetSpec(kind)] }]
		);

	/// <summary>A {1} sorcery that does nothing — a card to discard, exile, count.</summary>
	public static CardDefinition Fodder => TestGames.Inline("Fodder", "{1}", "Sorcery");

	/// <summary>A {1} noncreature artifact.</summary>
	public static CardDefinition Trinket => TestGames.Inline("Trinket", "{1}", "Artifact");

	/// <summary>The same card with another type line — "Legendary Creature — Elf", "Artifact Creature — Golem".</summary>
	public static CardDefinition Typed(CardDefinition card, string typeLine) =>
		card with
		{
			Data = card.Data with { TypeLine = typeLine },
		};

	/// <summary>The same card with one more triggered ability — "when this enters, gain 3".</summary>
	public static CardDefinition WithTrigger(
		CardDefinition card,
		TriggerEvent on,
		params GameAction[] effects
	) =>
		card with
		{
			Components = [.. card.Components, new TriggeredAbilityComponent(on, [.. effects])],
		};
}
