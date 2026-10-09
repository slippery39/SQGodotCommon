using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Sets;

/// <summary>
/// A board for FRA card tests: the starting player's first main phase, four of each basic land for
/// both players (tests cast a card two or three times in one turn), and helpers that cast through
/// the real stack. Every helper replaces <c>s</c> — call each on its own line (MagicCore/CLAUDE.md).
/// </summary>
public abstract class FraBoardTest
{
	protected GameState s = null!;
	protected int me,
		them;

	[SetUp]
	public void Board()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
		foreach (var player in new[] { me, them })
		foreach (var land in new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" })
			for (var i = 0; i < 4; i++) // tests cast a card two or three times in one turn
				(s, _) = TestGames.Put(s, player, TestGames.Card(land), ZoneType.Battlefield);
	}

	// Each helper replaces s — call on its own line (see MagicCore/CLAUDE.md, the test trap).
	protected int Field(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Battlefield);
		return id;
	}

	protected int Hand(string fra, int player) => Hand(FraCards.Get(fra), player);

	protected int Hand(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Hand);
		return id;
	}

	/// <summary>Casts the spell with these modes and targets, then resolves the whole stack.</summary>
	protected void Cast(int card, int[] modes, params int[] targets)
	{
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = s.GetCard(card).OwnerId,
				CardId = card,
				Modes = [.. modes],
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	/// <summary>Casts a creature whose entering trigger is modal, answering its mode and target choices.</summary>
	protected void CastWithTrigger(int card, int[] modes, params int[] targets)
	{
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = card });
		Resolve(choice =>
			choice switch
			{
				ChooseModesChoice => modes,
				ChooseTargetChoice t => [targets[t.Index]],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
	}

	protected void Resolve(Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		for (var i = 0; i < 100; i++)
		{
			if (s.GetPendingChoice() is { } choice)
				s = s.Choose(
					answer?.Invoke(choice)
						?? choice.Options.Take(choice.MinChoices).Select(o => o.Id)
				);
			else if (!s.StackIsEmpty())
				s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
			else
				return;
		}
	}

	/// <summary>They cast it (it needs flash or to be an instant); I then hold priority with it on the stack.</summary>
	protected int TheirSpellOnTheStack(CardDefinition card)
	{
		var spell = Hand(card, them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = spell });
		s = s.Act(new PassPriorityAction { PlayerId = them });
		return spell;
	}

	protected int Life(int p) => s.GetPlayer(p).Life;

	protected int HandCount(int p) => s.CardsIn(p, ZoneType.Hand).Count();

	protected bool Alive(int card) => s.IsOnBattlefield(card);

	protected IEnumerable<ActivateAbilityAction> Activations(int card) =>
		s.LegalActions().OfType<ActivateAbilityAction>().Where(a => a.CardId == card);

	protected IEnumerable<CastSpellAction> Casts(int card) =>
		s.LegalActions().OfType<CastSpellAction>().Where(a => a.CardId == card);

	protected Card? Jace(int p) =>
		s.CardsIn(p, ZoneType.Battlefield).FirstOrDefault(EmpowerJace.IsJaceToken);
}
