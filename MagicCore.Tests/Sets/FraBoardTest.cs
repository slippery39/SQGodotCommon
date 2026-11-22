using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
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

	/// <summary>No lands at all — the base board's twenty basics answer every "unless you control".</summary>
	protected void EmptyBoard()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	/// <summary>I play this cube land from my hand (its land drop); what it triggers is left on the stack.</summary>
	protected int Play(string land)
	{
		var id = Hand(Lsv(land), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = id });
		return id;
	}

	// Each helper replaces s — call on its own line (see MagicCore/CLAUDE.md, the test trap).
	protected int Field(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Battlefield);
		return id;
	}

	protected int Hand(string fra, int player) => Hand(CardRegistry.Get(fra), player);

	protected int Hand(CardDefinition card, int player)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, ZoneType.Hand);
		return id;
	}

	/// <summary>TEST SETUP: a fresh card straight into any zone of that player's (a library: its bottom).</summary>
	protected int Put(CardDefinition card, int player, ZoneType zone)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, zone);
		return id;
	}

	/// <summary>TEST SETUP: a fresh card on TOP of that player's library (index 0), known to nobody.</summary>
	protected int OnTop(CardDefinition card, int player)
	{
		var id = Put(card, player, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(player, ZoneType.Library));
		return id;
	}

	protected int OnTop(CardDefinition card) => OnTop(card, me);

	/// <summary>The LSVPower cube's printing of a card.</summary>
	protected static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

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

	/// <summary>I cast it and the stack resolves, each choice answered as told; the choices asked, in order.</summary>
	protected List<ChoiceAction> CastAsking(int card, Func<ChoiceAction, IEnumerable<int>> answer)
	{
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = card });
		return ResolveAsking(answer);
	}

	/// <summary>The stack resolves (after an activation, say), each choice answered as told; the choices asked, in order.</summary>
	protected List<ChoiceAction> ResolveAsking(Func<ChoiceAction, IEnumerable<int>> answer)
	{
		var asked = new List<ChoiceAction>();
		Resolve(c =>
		{
			asked.Add(c);
			return answer(c);
		});
		return asked;
	}

	/// <summary>The ids the choices of type T offered, of those asked (CastAsking, ResolveAsking).</summary>
	protected static IEnumerable<int> Offered<T>(List<ChoiceAction> asked)
		where T : ChoiceAction => asked.OfType<T>().SelectMany(c => c.Options).Select(o => o.Id);

	/// <summary>The same card with "{T}: You gain 1 life."</summary>
	protected static CardDefinition WithTap(CardDefinition card) =>
		card with
		{
			Components =
			[
				.. card.Components,
				new ActivatedAbilityComponent(ManaCost.None, [new GainLife(Ref.You, 1)])
				{
					Costs = [new TapSelfCost()],
				},
			],
		};

	/// <summary>TEST SETUP: the permanent is tapped, as though it had attacked.</summary>
	protected void TapIt(int permanent)
	{
		var card = s.GetCard(permanent);
		s = s.UpdateObject(permanent, card with { IsTapped = true });
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

	/// <summary>Answers a choice of type T with these ids; any other with its minimum.</summary>
	protected static Func<ChoiceAction, IEnumerable<int>> On<T>(params int[] ids)
		where T : ChoiceAction =>
		c =>
			c switch
			{
				T => ids,
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	/// <summary>Answers a target choice with this target and a "you may" as told; any other with its minimum.</summary>
	protected static Func<ChoiceAction, IEnumerable<int>> Targeting(int target, bool yes) =>
		c =>
			c switch
			{
				ChooseTargetChoice => [target],
				YesNoChoice when yes => [1],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	/// <summary>These attack; every choice to the end of combat answered as told (blocks: none).</summary>
	protected void AttackWith(Func<ChoiceAction, IEnumerable<int>> answer, params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([attackers.Contains(d.CreatureId) ? them : 0]);
		for (var i = 0; i < 200 && s.GetGame().Step != Step.PostCombatMain; i++)
			s = s.GetPendingChoice() is { } choice
				? s.Choose(answer(choice))
				: s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
	}

	/// <summary>"{0} instant: venture into Undercity" — the room under test, one venture at a time.</summary>
	protected static CardDefinition VentureSpell =>
		new(
			TestGames.Inline("Venture", "{0}", "Instant").Data,
			[new SpellEffectsComponent([new Venture()])]
		);

	/// <summary>I venture once and it all resolves, each choice answered as told; the choices asked.</summary>
	protected List<ChoiceAction> Ventures(Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		var spell = Hand(VentureSpell, me);
		return CastAsking(spell, answer ?? (c => c.Options.Take(c.MinChoices).Select(o => o.Id)));
	}

	/// <summary>The room a player's venture marker is on (Undercity), or null with no dungeon.</summary>
	protected string? Room(int player) =>
		Undercity.Of(s, player)?.GetComponent<DungeonComponent>() is { } d
			? Undercity.Rooms[d.Room].Name
			: null;

	/// <summary>Whether I may tap it for that colour now — a land's or a rock's mana, its "activate only if" too.</summary>
	protected bool CanTapFor(int card, ManaColor color) =>
		new TapForManaAction
		{
			PlayerId = me,
			CardId = card,
			Color = color,
		}
			.ValidateAdd(s)
			.IsValid;

	protected int Life(int p) => s.GetPlayer(p).Life;

	protected int HandCount(int p) => s.CardsIn(p, ZoneType.Hand).Count();

	protected bool Alive(int card) => s.IsOnBattlefield(card);

	protected IEnumerable<ActivateAbilityAction> Activations(int card) =>
		s.LegalActions().OfType<ActivateAbilityAction>().Where(a => a.CardId == card);

	protected IEnumerable<CastSpellAction> Casts(int card) =>
		s.LegalActions().OfType<CastSpellAction>().Where(a => a.CardId == card);

	/// <summary>My untapped lands — what a cast or activation tapped is the difference.</summary>
	protected int UntappedLands() =>
		s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);

	/// <summary>A library, top first — unchanged after an effect, it was not shuffled.</summary>
	protected List<int> Library(int player) =>
		[.. s.CardsIn(player, ZoneType.Library).Select(c => c.Id)];

	/// <summary>TEST SETUP: every land of mine tapped — what pays is the rest of the board.</summary>
	protected void TapMyLands()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			TapIt(land.Id);
	}

	protected bool InExile(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Exile);

	protected bool InGraveyard(int card) =>
		s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Graveyard);

	protected Card? Jace(int p) =>
		s.CardsIn(p, ZoneType.Battlefield).FirstOrDefault(EmpowerJace.IsJaceToken);
}
