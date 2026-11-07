using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Sets;

/// <summary>
/// The tokens more than one set makes — Treasure, Clue, Food — and the helper every set's token
/// definitions are written with. A set's own tokens (FRA's Cadet, TRK's Officer) stay in its Cards file.
/// </summary>
public static class Tokens
{
	/// <summary>A token's printed data: a name, a type line and what the text gives it.</summary>
	public static CardData Token(
		string name,
		string typeLine,
		string? power = null,
		string? toughness = null,
		string[]? colors = null,
		string[]? keywords = null
	) =>
		new(
			name,
			"token",
			null,
			0,
			typeLine,
			null,
			power,
			toughness,
			null,
			colors ?? [],
			keywords ?? [],
			null
		);

	/// <summary>"A Treasure token. (It's an artifact with '{T}, Sacrifice this token: Add one mana of any color.')"</summary>
	public static readonly CardData Treasure = Token("Treasure", "Artifact — Treasure");

	/// <summary>"{T}, Sacrifice this token: Add [N] mana of any (one) color." — Treasure's and Lotus's.</summary>
	public static ManaAbilityComponent SacrificeForMana(int amount) =>
		ManaAbilityComponent.OfAnyOneColor(amount) with
		{
			Costs = [new TapSelfCost(), new SacrificeSelfCost()],
		};

	/// <summary>"Create a Treasure token."</summary>
	public static CreateTokens CreateTreasure(Count count) =>
		new(Treasure, count) { Abilities = [SacrificeForMana(1)] };

	/// <summary>"A Clue token. (It's an artifact with '{2}, Sacrifice this token: Draw a card.')"</summary>
	public static readonly CardData Clue = Token("Clue", "Artifact — Clue");

	/// <summary>"Create a Clue token" (investigate: TRK's Cha'DIch Investigator, The Measure of a Man).</summary>
	public static CreateTokens CreateClue(Count count) =>
		new(Clue, count)
		{
			Abilities =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new DrawCards(Ref.You, 1)])
				{
					Costs = [new SacrificeSelfCost()],
				},
			],
		};

	/// <summary>"A Food token. (It's an artifact with '{2}, {T}, Sacrifice this token: You gain 3 life.')"</summary>
	public static readonly CardData Food = Token("Food", "Artifact — Food");

	/// <summary>"Create a Food token" (TRK's Neelix, Moral Support; Replicator).</summary>
	public static CreateTokens CreateFood(Count count) =>
		new(Food, count)
		{
			Abilities =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new GainLife(Ref.You, 3)])
				{
					Costs = [new TapSelfCost(), new SacrificeSelfCost()],
				},
			],
		};
}
