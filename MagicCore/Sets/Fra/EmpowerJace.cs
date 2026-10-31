using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Sets.Fra;

/// <summary>
/// Reality Fracture's keyword action: "Put N loyalty counters on a Jace token you control. If you
/// don't control one, first create a blue Jace planeswalker token with '[−1]: Surveil 1' and
/// '[−3]: Draw a card.'" Created and loaded in one resolution, so the token never sees a
/// state-based check at 0 loyalty — unless N itself is 0.
///
/// ponytail: with two Jace tokens, the first is chosen; the rules let you pick.
/// </summary>
public record EmpowerJace(Count Amount) : Effect
{
	public static readonly CardDefinition JaceToken =
		new(
			new CardData(
				"Jace",
				"token",
				null,
				0,
				"Planeswalker — Jace",
				null,
				null,
				null,
				"0",
				["U"],
				[],
				null
			),
			[
				new LoyaltyAbilityComponent(-1, [new Surveil(1)]),
				new LoyaltyAbilityComponent(-3, [new DrawCards(Ref.You, 1)]),
			]
		);

	public static bool IsJaceToken(Card c) =>
		c.IsToken && c.Is(CardType.Planeswalker) && c.Subtypes.Contains("Jace");

	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		var jace = s.CardsIn(Controller, ZoneType.Battlefield).FirstOrDefault(IsJaceToken);
		if (jace is null)
		{
			(s, jace) = s.AddObject(
				CardFactory.Create(JaceToken, Controller) with
				{
					IsToken = true,
				}
			);
			s = s.EnterBattlefield(jace.Id, Controller);
			jace = s.GetCard(jace.Id);
		}
		return new(Actions.Loyalty.Change(s, jace.Id, n, Controller));
	}
}
