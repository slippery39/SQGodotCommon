using System.Diagnostics;
using System.Text;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Limited;
using MagicCore.Sets;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Ai;

/// <summary>
/// AI-17.7, the blunder lint (Shayne, 2026-11-08): AI-vs-AI games in which every decision is checked for a named,
/// OBVIOUS mistake and counted, whatever the game's result. A lint, not a weight - it never changes a decision. Each
/// checker's exact rule is its comment; the self-test is running an older build (<c>beam+nolanddrop</c>) and seeing
/// the class the fix removed come back.
/// AI-19's misplay scan (Shayne, 2026-11-11) extends it: the games are TRK's (or any set's) AI sealed decks too, each
/// kept as a record and READ afterwards (<see cref="Read"/>: a rule changed is re-read in seconds, not replayed), and
/// per-card checks S1-S6 rank the cards the AI plays wrong against their peers. A move is numbered from 1, as the
/// record's transcript (<c>--record=FILE --explain=N</c>).
/// </summary>
public static class BlunderLint
{
	public const string PassedWithLand = "pass-with-land";
	public const string AttackIntoBigger = "attack-into-bigger";
	public const string WastedRemoval = "wasted-removal";
	public const string FaceBurn = "face-burn";
	public const string FlashOnOwnTurn = "flash-on-own-turn";
	public const string LeftInHand = "S1 left-in-hand";
	public const string NeverCast = "S1 never-cast";
	public const string TappedEarly = "S3a tapped-early";
	public const string NeverUsed = "S3b never-used";
	public const string StationWithAttacker = "S3c station-with-attacker";
	public const string LandcycleMissedDrop = "S3d landcycle-missed-drop";
	public const string TurnedAround = "S4 turned-around";
	public const string MayDeclined = "S5 may-declined";
	public const string NoEffect = "S6 no-effect";

	static readonly string[] Classes =
	[
		PassedWithLand,
		AttackIntoBigger,
		WastedRemoval,
		FaceBurn,
		FlashOnOwnTurn,
		LeftInHand,
		NeverCast,
		TappedEarly,
		NeverUsed,
		StationWithAttacker,
		LandcycleMissedDrop,
		TurnedAround,
		MayDeclined,
		NoEffect,
	];

	/// <summary>A card ranked only with at least this many games of the chance (S1, S5) or uses (S3a).</summary>
	public const int MinGames = 10;

	public sealed record Finding(
		string Class,
		int Seed,
		int Move,
		int Turn,
		string Text,
		string Card = ""
	);

	/// <summary>One game read: its findings, its per-card counts and the mana values of the cards counted.</summary>
	public sealed record GameResult(
		List<Finding> Found,
		Dictionary<(string Card, string Key), int> Counts,
		Dictionary<string, int> ManaValues
	);

	/// <summary>
	/// <paramref name="games"/> AI-vs-AI games, read. FRA's built decks when <paramref name="set"/> is null (the lint
	/// as it was); else game i plays seed i's two AI sealed decks of that set (<see cref="SimulatedRatings.Decks"/>,
	/// DATA-2's), stopped past <see cref="SimulatedRatings.GameBudget"/> and read as far as it went. Each game's record
	/// goes to <paramref name="recordsDir"/> as <c>seed-N.json</c> when given.
	/// </summary>
	public static (string Report, IReadOnlyList<Finding> Findings) Run(
		int games,
		int firstSeed,
		string ai,
		int? only = null,
		string? set = null,
		string? recordsDir = null
	)
	{
		var results = new (string File, GameResult Result)?[games];
		var progress = new RunProgress("lint", games);
		var (flagged, stopped, stale) = (0, 0, 0);
		Parallel.For(
			0,
			games,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, // no more games than cores: a game's budget is wall-clock
			g =>
			{
				var seed = firstSeed + g;
				if (only is { } o && seed != o)
					return;
				var (record, finished) = Play(seed, g, ai, set);
				var file = $"seed-{seed}.json";
				if (recordsDir is not null)
					File.WriteAllText(file = Path.Combine(recordsDir, file), record.ToJson());
				if (!finished)
					Interlocked.Increment(ref stopped);
				if (TryCheck(record) is { } result)
				{
					results[g] = (file, result);
					Interlocked.Add(ref flagged, result.Found.Count);
				}
				else
					Interlocked.Increment(ref stale);
				progress.Tick(() => $"{flagged} flagged, {stopped} stopped, {stale} not replayed");
			}
		);
		return Report(
			[.. results.OfType<(string, GameResult)>()],
			$"{games} games, seeds {firstSeed}.., {(set is null ? "FRA's built decks" : $"{set} AI sealed decks")}, AI `{ai}`"
				+ (recordsDir is null ? "" : $", records in `{recordsDir}`")
				+ $"; {stopped} stopped past {SimulatedRatings.GameBudget.TotalMinutes:0} min (read as far as played), {stale} not replayed (left out)"
		);
	}

	/// <summary>Every record in <paramref name="dir"/> read again (<c>--lint --records=DIR</c>) - a changed check, no games replayed.</summary>
	public static (string Report, IReadOnlyList<Finding> Findings) Read(string dir)
	{
		var files = Directory.GetFiles(dir, "*.json");
		var results = new (string, GameResult)?[files.Length];
		var progress = new RunProgress("lint read", files.Length);
		var stale = 0;
		Parallel.For(
			0,
			files.Length,
			i =>
			{
				if (TryCheck(GameRecord.FromJson(File.ReadAllText(files[i]))) is { } result)
					results[i] = (files[i], result);
				else
					Interlocked.Increment(ref stale);
				progress.Tick(() => $"{stale} not replayed");
			}
		);
		return Report(
			[.. results.OfType<(string, GameResult)>()],
			$"{files.Length} records read from `{dir}`, {stale} not replayed (left out)"
		);
	}

	static (GameRecord Record, bool Finished) Play(int seed, int g, string ai, string? set)
	{
		IReadOnlyList<CardDefinition> deck1,
			deck2,
			side1 = [],
			side2 = [];
		if (set is null)
		{
			var decks = FraDecks.All.Keys.ToList();
			(deck1, deck2) = (
				FraDecks.All[decks[g % decks.Count]],
				FraDecks.All[decks[(g + 1) % decks.Count]]
			);
		}
		else
		{
			var (a, b) = SimulatedRatings.Decks(set, seed);
			(deck1, deck2) = (
				[.. a.Main.Select(n => CardRegistry.Get(n, set))],
				[.. b.Main.Select(n => CardRegistry.Get(n, set))]
			);
			(side1, side2) = (
				SealedBuildMatch.Outside(a.Sideboard),
				SealedBuildMatch.Outside(b.Sideboard)
			);
		}
		var names = ("Player1", "Player2");
		var s = MagicRules.NewGame(
			deck1,
			deck2,
			seed,
			names: names,
			sideboard1: side1,
			sideboard2: side2
		);
		var record = GameRecord.New(seed, deck1, deck2, names, side1, side2);
		var first = s.DecidingPlayerId();
		var ais = new[] { first, s.OpponentOf(first) }.ToDictionary(
			p => p,
			p => AiMatch.Create(ai, p, seed, searchThreads: 1)
		);
		var clock = Stopwatch.StartNew();
		for (var move = 0; move < 4_000 && !s.IsOver(); move++)
		{
			if (clock.Elapsed > SimulatedRatings.GameBudget)
				return (record, false);
			var decision = ais[s.DecidingPlayerId()].Decide(s);
			record = record.Add(s, MoveTokens.Token(s, decision), "ai");
			s = decision.State;
		}
		return (s.IsOver() ? record.Ended(s) : record, s.IsOver());
	}

	/// <summary>
	/// Null when the record no longer replays. ponytail: a TRK game with Kai Winn goes stale on replay - her name list
	/// reads the game's printings and a record keeps names only; left out until "name a card" answers with the name
	/// itself (Shayne, 2026-11-11).
	/// </summary>
	static GameResult? TryCheck(GameRecord record)
	{
		try
		{
			return Check(record);
		}
		catch (MoveTokens.StaleException)
		{
			return null;
		}
	}

	/// <summary>One record, replayed and checked decision by decision (both seats).</summary>
	public static GameResult Check(GameRecord record)
	{
		var (before, last) = record.Replay();
		var seed = record.Seed;
		var found = new List<Finding>();
		var counts = new Dictionary<(string, string), int>();
		var manaValues = new Dictionary<string, int>();
		void Count(string card, string key) =>
			counts[(card, key)] = counts.GetValueOrDefault((card, key)) + 1;

		var burns =
			new List<(
				int Card,
				int Who,
				int OppLife,
				int Move,
				int Turn,
				bool Creature,
				string Text
			)>();
		// S1: (card, turn) castable at a main-phase stop, left in hand at the end of one; per card the turns castable
		var castableAt = new HashSet<(int, int)>();
		var leftAt = new HashSet<(int, int)>();
		var castableTurns = new Dictionary<int, int>();
		var names = new Dictionary<int, string>(); // a token gone by the game's end has no card left to name
		var cast = new HashSet<int>();
		var gamesCastable = new HashSet<(int, string)>();
		var heldAtEnd = new HashSet<(int, int)>();
		// S3: (permanent, its controller's cycle) usable / used; a cycle starts at its controller's turn
		var lastOwnTurn = new Dictionary<int, int>();
		var usableIn = new HashSet<(int, int)>();
		var usedIn = new HashSet<(int, int)>();
		var cyclesUsable = new Dictionary<int, int>();
		var used = new HashSet<int>();
		var early =
			new List<(int Card, int Player, int Turn, int Move, string Name, string When)>();
		var cycledAt = new HashSet<(int, int)>();
		// S4: each player's instant-speed moves left at its last own-turn stop, its hand then, and whether anything new came since
		var lastStop = new Dictionary<int, (HashSet<string> Keys, HashSet<int> Hand)>();
		var news = new Dictionary<int, bool>();
		// S6: our spell or ability alone on the stack, and the position it went on in
		(int Id, string Name, int Move, int Turn, string? Print)? resolving = null;

		for (var i = 0; i < before.Count; i++)
		{
			var s = before[i];
			var next = i + 1 < before.Count ? before[i + 1] : last;
			var (action, answer) = MoveTokens.Resolve(s, record.Moves[i].Token);
			var who = s.DecidingPlayerId();
			var game = s.GetGame();
			var (turn, step, active) = (game.TurnNumber, game.Step, game.ActivePlayerId);
			var choice = s.GetPendingChoice();
			var stackEmpty = s.StackIsEmpty();
			var move = i + 1;
			if (active != 0)
				lastOwnTurn[active] = turn;
			void Add(string c, string text, string card = "") =>
				found.Add(new(c, seed, move, turn, text, card));

			// S3a: an early tap stands once its creature is still tapped as its controller's next turn begins
			early.RemoveAll(e =>
			{
				if (active == e.Player && turn > e.Turn)
				{
					found.Add(
						new(
							TappedEarly,
							seed,
							e.Move,
							e.Turn,
							$"{e.Name} tapped for its ability in {e.When}, stack empty, and stayed tapped",
							e.Name
						)
					);
					Count(e.Name, "tapped-early");
					return true;
				}
				return !s.HasObject(e.Card) || !s.GetCard(e.Card).IsTapped;
			});

			// S6: judged once it has left the stack, no choice pending
			if (resolving is { } r && choice is null)
			{
				var stack = StackIds(s).ToList();
				if (r.Print is null)
					resolving =
						stack.Count == 1
							? r with
							{
								Id = stack[0],
								Print = Fingerprint(s, stack[0]),
							}
							: null;
				else if (!stack.Contains(r.Id))
				{
					Count(r.Name, "resolved");
					if (stack.Count == 0 && Fingerprint(s, r.Id) == r.Print)
					{
						Count(r.Name, "no-effect");
						found.Add(
							new(
								NoEffect,
								seed,
								r.Move,
								r.Turn,
								$"{r.Name} resolved and changed nothing but its cost",
								r.Name
							)
						);
					}
					resolving = null;
				}
				else if (stack.Count > 1)
					resolving = null; // a response: not judged
			}

			// face burn resolved: the damage is the life the opponent lost once the stack is empty again
			if (stackEmpty && burns.Count > 0)
			{
				foreach (var b in burns)
				{
					var lost = b.OppLife - s.GetPlayer(s.OpponentOf(b.Who)).Life;
					// 5. A damage spell sent at the opponent's face that did not kill them. A one-shot spell
					// spent on a few points of life; a creature target legal at the cast is noted.
					if (s.GetPlayer(s.OpponentOf(b.Who)).Life > 0)
						found.Add(
							new(
								FaceBurn,
								seed,
								b.Move,
								b.Turn,
								$"{b.Text}: {lost} damage, them {b.OppLife} -> {s.GetPlayer(s.OpponentOf(b.Who)).Life}"
									+ (b.Creature ? " (a creature was a legal target)" : "")
							)
						);
				}
				burns.Clear();
			}

			// S4: anything the other player does but pass is news to whoever stopped last
			if (action is not PassPriorityAction)
				news[s.OpponentOf(who)] = true;

			if (choice is null)
			{
				var legal = s.LegalActions().ToList();
				var castable = legal.OfType<CastSpellAction>().Select(c => c.CardId).ToHashSet();
				var hand = s.CardsIn(who, ZoneType.Hand).ToList();
				var passing = action is PassPriorityAction;

				switch (action)
				{
					// 1. Passing priority in its own postcombat main phase, stack empty, with a land play legal.
					case PassPriorityAction
						when step == Step.PostCombatMain
							&& active == who
							&& stackEmpty
							&& legal.Any(a => a is PlayLandAction l && l.PlayerId == who):
						Add(PassedWithLand, "passed the second main phase with a land to play");
						break;
					case CastSpellAction c when s.HasObject(c.CardId):
						CheckCast(s, who, c, move, turn, (k, t) => Add(k, t), burns);
						break;
				}

				// S1. A sorcery-speed card in hand (no land, instant or flash) castable at its holder's main-phase stop,
				// stack empty, is LEFT when the holder passes its second main phase with it still castable: the turn
				// ends with mana that could have cast it, spent on nothing. Never cast: castable on 2+ turns, never cast.
				if (
					active == who
					&& step is Step.PreCombatMain or Step.PostCombatMain
					&& stackEmpty
				)
					foreach (
						var card in hand.Where(c =>
							!c.Is(CardType.Land) && SorcerySpeed(s, c) && castable.Contains(c.Id)
						)
					)
					{
						manaValues[card.Name] = card.ManaCost.ManaValue;
						if (castableAt.Add((card.Id, turn)))
						{
							Count(card.Name, "castable-turns");
							castableTurns[card.Id] = castableTurns.GetValueOrDefault(card.Id) + 1;
							names[card.Id] = card.Name;
							if (gamesCastable.Add((who, card.Name)))
								Count(card.Name, "games-castable");
						}
						if (passing && step == Step.PostCombatMain && leftAt.Add((card.Id, turn)))
						{
							Count(card.Name, "left-turns");
							Add(
								LeftInHand,
								$"{card.Name} castable, left in hand as the turn ended ({Lands(s, who)})",
								card.Name
							);
						}
					}
				if (action is CastSpellAction cs)
				{
					cast.Add(cs.CardId);
					// S2. When an instant or flash card is cast: whose turn, which part of it, or in response.
					var card = s.GetCard(cs.CardId);
					if (!card.Is(CardType.Land) && !SorcerySpeed(s, card))
					{
						Count(card.Name, "instant-casts");
						Count(card.Name, "cast: " + When(s, who));
					}
				}
				// S2. Held through their end step: castable there, stack empty, and not cast (the mana goes unused).
				if (active != who && step == Step.End && stackEmpty)
					foreach (
						var card in hand.Where(c =>
							!c.Is(CardType.Land)
							&& !SorcerySpeed(s, c)
							&& castable.Contains(c.Id)
							&& !(action is CastSpellAction x && x.CardId == c.Id)
						)
					)
						if (heldAtEnd.Add((card.Id, turn)))
							Count(card.Name, "end-step-held");

				// S3. Activated abilities of permanents in play: usable / used in each cycle of its controller.
				var cycle = lastOwnTurn.GetValueOrDefault(who);
				foreach (
					var id in legal
						.OfType<ActivateAbilityAction>()
						.Select(a => a.CardId)
						.Distinct()
						.Where(id => s.IsIn(id, who, ZoneType.Battlefield))
				)
					if (usableIn.Add((id, cycle)))
					{
						Count(names[id] = s.GetCard(id).Name, "ability-cycles");
						cyclesUsable[id] = cyclesUsable.GetValueOrDefault(id) + 1;
					}
				if (
					action is ActivateAbilityAction act
					&& s.IsIn(act.CardId, who, ZoneType.Battlefield)
				)
				{
					var card = s.GetCard(act.CardId);
					var ability = s.ActivatedAbilitiesOf(card).ElementAtOrDefault(act.AbilityIndex);
					used.Add(card.Id);
					if (usedIn.Add((card.Id, cycle)))
						Count(card.Name, "ability-used-cycles");
					Count(card.Name, "used: " + When(s, who));
					// S3a. A creature's {T} ability used with the stack empty anywhere but the opponent's end step;
					// it stands if the creature stays tapped until its controller's next untap step (an untap coming -
					// Tal Shiar Analyst's noncreature spell - clears it). Not one "as a sorcery": it can't wait (Archer).
					if (
						ability is { SorcerySpeed: false }
						&& ability.Costs.Any(c => c is TapSelfCost)
						&& card.Is(CardType.Creature)
					)
					{
						Count(card.Name, "tap-uses");
						if (stackEmpty && !(active != who && step == Step.End))
							early.Add((card.Id, who, turn, move, card.Name, When(s, who)));
					}
					// S3c. Station (a charge ability tapping another creature) in its own first main phase or beginning
					// of combat, tapping a creature that could attack - unless the ship is a creature later that turn
					// (station, then swing: 60 of TRK's first 80; Shayne, 2026-11-11: a removal question, not this check).
					if (
						act.TapId != 0
						&& ability
							?.Effects.OfType<PutCounters>()
							.Any(p => p.Kind == CounterKinds.Charge) == true
						&& active == who
						&& step is Step.PreCombatMain or Step.BeginCombat
						&& s.GetCard(act.TapId) is var tapped
						&& Combat.CanAttack(s, tapped)
						&& !before // the costly test last: is the ship a creature later this turn?
							.Skip(i + 1)
							.TakeWhile(x => x.GetGame().TurnNumber == turn)
							.Any(x =>
								x.HasObject(act.CardId)
								&& x.GetCard(act.CardId).Is(CardType.Creature)
							)
					)
					{
						Count(card.Name, "station-attacker");
						Add(
							StationWithAttacker,
							$"{card.Name} stationed with {tapped.Name} {s.Power(tapped)}/{s.Toughness(tapped)}, which could attack",
							card.Name
						);
					}
				}
				// S3d. Its second main phase passed, stack empty, no land played this turn, a landcycling ability usable.
				if (
					passing
					&& active == who
					&& step == Step.PostCombatMain
					&& stackEmpty
					&& s.GetPlayer(who).ThisTurn.LandsPlayed == 0
				)
					foreach (
						var a in legal
							.OfType<ActivateAbilityAction>()
							.Where(a => s.IsIn(a.CardId, who, ZoneType.Hand))
					)
						if (
							Ability(s, a) is { Keyword: "Cycling" } ab
							&& ab.Effects.Any(e => e is SearchLibrary)
							&& cycledAt.Add((a.CardId, turn))
						)
						{
							var name = s.GetCard(a.CardId).Name;
							Count(name, "landcycle-missed");
							Add(
								LandcycleMissedDrop,
								$"{name} could landcycle; the turn ended with no land played ({Lands(s, who)})",
								name
							);
						}

				// S4. An instant-speed move left at the holder's last own-turn stop, then made in the opponent's upkeep
				// or draw step with nothing new: the opponent did nothing but pass, and no new card reached its hand.
				var key = action is null ? null : InstantKey(s, action);
				if (active == who)
				{
					lastStop[who] = (
						[
							.. legal
								.Select(a => InstantKey(s, a))
								.OfType<string>()
								.Where(k => k != key),
						],
						[.. hand.Select(c => c.Id)]
					);
					news[who] = false;
				}
				else if (
					step is Step.Upkeep or Step.Draw
					&& key is not null
					&& lastStop.TryGetValue(who, out var stop)
					&& stop.Keys.Contains(key)
					&& !news.GetValueOrDefault(who)
					&& hand.All(c => stop.Hand.Contains(c.Id))
				)
				{
					var name = s.GetCard(MagicRules.CardOf(action!)).Name;
					Count(name, "turned-around");
					Add(
						TurnedAround,
						$"{Describe.Action(s, action!)} in their {step}, left at its own last stop with nothing new since",
						name
					);
				}

				// S6. Our spell or ability put on an empty stack: watched until it leaves (above) - not one that surveils,
				// whose look is its effect even when the card stays (D'Vana Tendi's {3}: Surveil 1).
				if (
					stackEmpty
					&& action is CastSpellAction or ActivateAbilityAction
					&& !(
						action switch
						{
							CastSpellAction c => s.GetCard(c.CardId)
								.GetComponent<SpellEffectsComponent>()
								?.Effects,
							ActivateAbilityAction a => Ability(s, a)?.Effects,
							_ => null,
						} ?? []
					).Any(e => e is Surveil)
				)
					resolving = (
						0,
						action is CastSpellAction c2
							? s.GetCard(c2.CardId).Name
							: $"{s.GetCard(MagicRules.CardOf(action)).Name} (ability)",
						move,
						turn,
						null
					);
			}
			// S5. A "you may" (yes or no, or a pick of none or one) answered none - by its source on the stack, a
			// "pay" counted apart (often right to decline); not a surveil's or explore's keep.
			else if (
				choice is ChoiceAction { MinChoices: 0, MaxChoices: 1, Options.Count: > 0 }
				&& choice
					is not (
						DeclareAttackerChoice
						or DeclareBlockerChoice
						or ChooseFromLibraryTopChoice
					)
				&& !choice.Prompt.EndsWith("into your graveyard?") // explore's: none is a keep
			)
			{
				// a trigger's target is chosen before it is on the stack: the choice names its source
				var source =
					choice is ChooseTargetChoice { SourceId: var srcId }
					&& s.HasObject(srcId)
					&& s.GetObject(srcId) is Card sc
						? sc.Name
					: SourceName(s) is { Length: > 0 } n ? n
					: $"({choice.Prompt})";
				var pay =
					choice is PayToKeepChoice
					|| choice.Prompt.Contains("pay", StringComparison.OrdinalIgnoreCase);
				Count(source, pay ? "pay-asked" : "may-asked");
				if (answer is { Length: 0 })
				{
					Count(source, pay ? "pay-declined" : "may-declined");
					Add(MayDeclined, $"{source}: \"{choice.Prompt}\" declined", source);
				}
			}

			// 5b. The same for a permanent whose enters-trigger is only damage (Ajani's Anguish): the target is a
			// choice made as the trigger goes on the stack, the source card named in it.
			if (
				choice is ChooseTargetChoice tc
				&& answer is [var picked]
				&& picked == s.OpponentOf(who)
				&& s.HasObject(tc.SourceId)
				&& s.GetObject(tc.SourceId) is Card src
				&& BeamSearchAi.EntersBurn(s, src)
			)
				burns.Add(
					(
						src.Id,
						who,
						s.GetPlayer(picked).Life,
						move,
						turn,
						tc.Spec.Candidates(s, who, src.Id)
							.Any(id =>
								s.GetObject(id) is Card c
								&& c.ControllerId != who
								&& c.Is(CardType.Creature)
							),
						$"{src.Name} trigger at their face{Held(s, who)}"
					)
				);

			// 2. A whole attack that is a pure downside, judged once declared (see PureDownside).
			if (
				step == Step.DeclareAttackers
				&& next.GetGame().Step != Step.DeclareAttackers
				&& PureDownside(next, who) is { } why
			)
				Add(AttackIntoBigger, why);
		}

		// S1 and S3b, judged at the game's end
		foreach (var (id, turns) in castableTurns.Where(t => t.Value >= 2 && !cast.Contains(t.Key)))
		{
			var name = names[id];
			Count(name, "never-cast");
			found.Add(
				new(NeverCast, seed, 0, 0, $"{name} castable on {turns} turns, never cast", name)
			);
		}
		foreach (var (id, cycles) in cyclesUsable.Where(c => c.Value >= 3 && !used.Contains(c.Key)))
		{
			var name = names[id];
			Count(name, "never-used");
			found.Add(
				new(
					NeverUsed,
					seed,
					0,
					0,
					$"{name}'s ability usable in {cycles} cycles, never used",
					name
				)
			);
		}
		return new(found, counts, manaValues);
	}

	/// <summary>Neither an instant nor flash: cast only at sorcery speed (<see cref="Priority.AtInstantSpeed"/>, the engine's one read).</summary>
	static bool SorcerySpeed(GameState s, Card c) => !Priority.AtInstantSpeed(s, c);

	static ActivatedAbilityComponent? Ability(GameState s, ActivateAbilityAction a) =>
		s.ActivatedAbilitiesOf(s.GetCard(a.CardId)).ElementAtOrDefault(a.AbilityIndex);

	/// <summary>A move that can be made at instant speed - an instant or flash card cast, or an ability not "as a sorcery" - as a key, else null.</summary>
	static string? InstantKey(GameState s, GameAction a) =>
		a switch
		{
			CastSpellAction c
				when s.GetCard(c.CardId) is var card
					&& !card.Is(CardType.Land)
					&& !SorcerySpeed(s, card) => $"c{c.CardId}",
			ActivateAbilityAction x when Ability(s, x) is { SorcerySpeed: false } =>
				$"a{x.CardId}:{x.AbilityIndex}",
			_ => null,
		};

	/// <summary>When a move is made: in response, or whose turn and which part of it.</summary>
	static string When(GameState s, int who) =>
		!s.StackIsEmpty()
			? "response"
			: (s.GetGame().ActivePlayerId == who ? "own " : "their ")
				+ s.GetGame().Step switch
				{
					Step.Untap or Step.Upkeep or Step.Draw => "upkeep/draw",
					Step.PreCombatMain => "main 1",
					Step.PostCombatMain => "main 2",
					Step.End or Step.Cleanup => "end step",
					_ => "combat",
				};

	static IEnumerable<int> StackIds(GameState s) =>
		s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack));

	/// <summary>The spell or ability on top of the stack, by its card's name - what a "you may" belongs to.</summary>
	static string SourceName(GameState s) =>
		StackIds(s).FirstOrDefault() is var top and not 0
			? s.GetObject(top) switch
			{
				Card c => c.Name,
				StackAbility a => a.Source.Name,
				_ => "",
			}
			: "";

	static string Lands(GameState s, int who) =>
		$"{s.CardsIn(who, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped)} of {s.CardsIn(who, ZoneType.Battlefield).Count(c => c.Is(CardType.Land))} lands untapped";

	static readonly ZoneType[] Zones =
	[
		ZoneType.Hand,
		ZoneType.Library,
		ZoneType.Graveyard,
		ZoneType.Exile,
		ZoneType.Battlefield,
		ZoneType.Command,
	];

	/// <summary>
	/// S6's "changed nothing": both players' life and floating mana, every zone's objects (the library's in order),
	/// each permanent's tapped state, damage, counters, power and toughness, attachment and until-end-of-turn effects -
	/// all but <paramref name="except"/>, the spell itself, which moves as it resolves.
	/// </summary>
	static string Fingerprint(GameState s, int except)
	{
		var sb = new StringBuilder();
		foreach (var p in new[] { s.Player1Id(), s.Player2Id() })
		{
			var player = s.GetPlayer(p);
			sb.Append($"|{player.Life}:{player.ManaPool.Total}");
			foreach (var z in Zones)
			{
				var ids = s.GetChildrenIds(s.ZoneId(p, z)).Where(id => id != except);
				sb.Append($"|{z}:{string.Join(",", z == ZoneType.Library ? ids : ids.Order())}");
			}
			foreach (var c in s.CardsIn(p, ZoneType.Battlefield))
				sb.Append(
					$"|{c.Id}:{c.IsTapped}:{c.Damage}:{string.Join(",", c.AllCounters())}:{s.Power(c)}/{s.Toughness(c)}:{c.AttachedToId}:{c.GetComponents<UntilEndOfTurn>().Count()}"
				);
		}
		return sb.ToString();
	}

	static (string Report, IReadOnlyList<Finding> Findings) Report(
		IReadOnlyList<(string File, GameResult Result)> games,
		string title
	)
	{
		var all = games.SelectMany(g => g.Result.Found).ToList();
		var counts = new Dictionary<(string Card, string Key), int>();
		var mv = new Dictionary<string, int>();
		foreach (var (_, r) in games)
		{
			foreach (var (k, n) in r.Counts)
				counts[k] = counts.GetValueOrDefault(k) + n;
			foreach (var (k, v) in r.ManaValues)
				mv[k] = v;
		}
		int C(string card, string key) => counts.GetValueOrDefault((card, key));
		var cards = counts.Keys.Select(k => k.Card).Distinct().ToList();
		string Examples(string card, params string[] classes) =>
			string.Join(
				", ",
				all.Where(f => f.Card == card && classes.Contains(f.Class))
					.Take(3)
					.Select(f => f.Move > 0 ? $"seed {f.Seed} #{f.Move}" : $"seed {f.Seed}")
			);
		static string Pct(double x) => $"{100 * x:0}%";

		var md = new StringBuilder($"# Blunder lint: {title}\n\n");
		if (title.Contains("records"))
			md.Append(
				"Records: `seed-N.json` in the folder named above; `--record=FILE --explain=N` shows what the search imagined before move N.\n\n"
			);
		md.Append("| class | count | games with one |\n|---|---|---|\n");
		foreach (var c in Classes)
			md.Append(
				$"| {c} | {all.Count(f => f.Class == c)} | {all.Where(f => f.Class == c).Select(f => f.Seed).Distinct().Count()} |\n"
			);

		// S1, ranked by its share of castable turns left, against the median of the same mana value
		var s1 = cards
			.Where(c => C(c, "games-castable") >= MinGames)
			.Select(c => (Card: c, Share: (double)C(c, "left-turns") / C(c, "castable-turns")))
			.ToList();
		var peers = s1.GroupBy(x => mv.GetValueOrDefault(x.Card))
			.ToDictionary(g => g.Key, g => Median(g.Select(x => x.Share)));
		md.Append(
			$"\n## S1 castable and left in hand (sorcery-speed; at least {MinGames} games castable)\n\n"
				+ "Share: castable turns that ended with it still castable. Peers: the median share at its mana value.\n\n"
				+ "| card | mv | games castable | turns castable | left | share | peers | never cast (games) | examples |\n|---|---|---|---|---|---|---|---|---|\n"
		);
		foreach (
			var (card, share) in s1.OrderByDescending(x =>
					x.Share - peers[mv.GetValueOrDefault(x.Card)]
				)
				.Take(30)
		)
			md.Append(
				$"| {card} | {mv.GetValueOrDefault(card)} | {C(card, "games-castable")} | {C(card, "castable-turns")} | {C(card, "left-turns")} | {Pct(share)} | {Pct(peers[mv.GetValueOrDefault(card)])} | {C(card, "never-cast")} | {Examples(card, NeverCast, LeftInHand)} |\n"
			);

		// S2, instants and flash: when they are cast, and how often held through an end step
		md.Append(
			"\n## S2 instants and flash: when cast (reported, not flagged)\n\n| card | casts | when | held through their end step |\n|---|---|---|---|\n"
		);
		foreach (
			var card in cards
				.Where(c => C(c, "instant-casts") + C(c, "end-step-held") > 0)
				.OrderByDescending(c => C(c, "instant-casts"))
		)
			md.Append(
				$"| {card} | {C(card, "instant-casts")} | {Whens(counts, card, "cast: ")} | {C(card, "end-step-held")} |\n"
			);

		// S3, abilities: early taps first, then never used
		md.Append(
			"\n## S3 activated abilities of permanents in play\n\n"
				+ "Cycles: from its controller's untap step to the next. Tapped early: a creature's {T} ability used with the stack empty, not in the opponent's end step, and still tapped as its controller's next turn began.\n\n"
				+ "| card | cycles usable | cycles used | {T} uses | tapped early | never used (games) | when used | examples |\n|---|---|---|---|---|---|---|---|\n"
		);
		foreach (
			var card in cards
				.Where(c => C(c, "ability-cycles") > 0)
				.OrderByDescending(c =>
					C(c, "tap-uses") >= MinGames
						? (double)C(c, "tapped-early") / C(c, "tap-uses")
						: 0
				)
				.ThenByDescending(c => C(c, "never-used"))
				.Take(40)
		)
			md.Append(
				$"| {card} | {C(card, "ability-cycles")} | {C(card, "ability-used-cycles")} | {C(card, "tap-uses")} | {C(card, "tapped-early")}{(C(card, "tap-uses") > 0 ? $" ({Pct((double)C(card, "tapped-early") / C(card, "tap-uses"))})" : "")} | {C(card, "never-used")} | {Whens(counts, card, "used: ")} | {Examples(card, TappedEarly, NeverUsed)} |\n"
			);

		// S3c, S3d, S4, S6: by count
		foreach (
			var (heading, key, cls) in new[]
			{
				(
					"S3c stationed with a creature that could attack",
					"station-attacker",
					StationWithAttacker
				),
				(
					"S3d landcycler held through a missed land drop",
					"landcycle-missed",
					LandcycleMissedDrop
				),
				("S4 turned around without news", "turned-around", TurnedAround),
				("S6 resolved for no effect", "no-effect", NoEffect),
			}
		)
		{
			md.Append($"\n## {heading}\n\n| card | count | examples |\n|---|---|---|\n");
			foreach (var card in cards.Where(c => C(c, key) > 0).OrderByDescending(c => C(c, key)))
				md.Append($"| {card} | {C(card, key)} | {Examples(card, cls)} |\n");
		}

		// S5, ranked by the share declined; "pay" questions apart
		md.Append(
			$"\n## S5 \"you may\" declined (at least {MinGames} asked)\n\n| source | asked | declined | share | pay asked | pay declined | examples |\n|---|---|---|---|---|---|---|\n"
		);
		foreach (
			var card in cards
				.Where(c => C(c, "may-asked") + C(c, "pay-asked") >= MinGames)
				.OrderByDescending(c =>
					C(c, "may-asked") > 0 ? (double)C(c, "may-declined") / C(c, "may-asked") : 0
				)
		)
			md.Append(
				$"| {card} | {C(card, "may-asked")} | {C(card, "may-declined")} | {(C(card, "may-asked") > 0 ? Pct((double)C(card, "may-declined") / C(card, "may-asked")) : "-")} | {C(card, "pay-asked")} | {C(card, "pay-declined")} | {Examples(card, MayDeclined)} |\n"
			);

		// the lint's own classes, every finding
		md.Append("\n## The lint's classes, every finding\n\n");
		foreach (
			var f in all.Where(f =>
					f.Class
						is PassedWithLand
							or AttackIntoBigger
							or WastedRemoval
							or FaceBurn
							or FlashOnOwnTurn
				)
				.OrderBy(f => f.Class)
				.ThenBy(f => f.Seed)
				.ThenBy(f => f.Move)
		)
			md.Append($"- {f.Class}: seed {f.Seed}, move #{f.Move}, turn {f.Turn}: {f.Text}\n");
		return (md.ToString(), all);
	}

	static string Whens(
		Dictionary<(string Card, string Key), int> counts,
		string card,
		string prefix
	) =>
		string.Join(
			", ",
			counts
				.Where(k => k.Key.Card == card && k.Key.Key.StartsWith(prefix))
				.OrderByDescending(k => k.Value)
				.Select(k => $"{k.Key.Key[prefix.Length..]} {k.Value}")
		);

	static double Median(IEnumerable<double> xs)
	{
		var sorted = xs.Order().ToList();
		return sorted.Count == 0 ? 0
			: sorted.Count % 2 == 1 ? sorted[sorted.Count / 2]
			: (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
	}

	/// <summary>What the caster held, and how many lands are left in its library - did it have a follow-up?</summary>
	static string Held(GameState s, int who) =>
		$" [hand: {string.Join(", ", s.CardsIn(who, ZoneType.Hand).Select(c => c.Name))}; library {s.CardsIn(who, ZoneType.Library).Count()} cards, {s.CardsIn(who, ZoneType.Library).Count(c => c.Is(CardType.Land))} lands; their board {s.CardsIn(s.OpponentOf(who), ZoneType.Battlefield).Count(c => c.Is(CardType.Creature))} creatures]";

	static void CheckCast(
		GameState s,
		int who,
		CastSpellAction cast,
		int move,
		int turn,
		Action<string, string> add,
		List<(int, int, int, int, int, bool, string)> burns
	)
	{
		var card = s.GetCard(cast.CardId);
		// 6. A flash creature cast in its caster's own main phase with the stack empty - it could have waited
		// for the opponent's turn (end step, a block, in response). Cast from the hand only.
		// No exemption for "wants it to attack": cast at the end of their turn it attacks just the same (Shayne, 2026-11-09).
		if (
			card.Is(CardType.Creature)
			&& s.Has(card, "Flash")
			&& s.GetGame().ActivePlayerId == who
			&& s.GetGame().Step is Step.PreCombatMain or Step.PostCombatMain
			&& s.StackIsEmpty()
			&& s.IsIn(card.Id, who, ZoneType.Hand)
		)
			add(FlashOnOwnTurn, $"{card.Name} (flash) cast in its own main phase");
		var effects = card.GetComponent<SpellEffectsComponent>()?.Effects ?? [];
		var removal = effects.Any(e => e is DealDamage or Destroy or Exile);
		var targets = cast.Targets.Where(s.HasObject).Select(s.GetObject).ToList();
		// 3a. A removal spell aimed only at its own caster's permanents (the target of a cast is chosen, so
		// this is the AI pointing a kill at itself).
		if (
			removal
			&& targets.Count > 0
			&& targets.All(t => t is Card c && c.ControllerId == who)
			&& !card.Is(CardType.Creature)
		)
			add(
				WastedRemoval,
				$"{card.Name} aimed at its own {string.Join(", ", targets.Select(t => t.Name))}"
			);
		// 5. Damage at the opponent's face: judged once it resolves (see Check).
		if (
			(card.Is(CardType.Instant) || card.Is(CardType.Sorcery))
			&& effects.Any(e => e is DealDamage)
			&& effects.All(e => e is DealDamage) // No Admittance also empowers Jace: not pure burn
			&& targets.Any(t => t is MagicPlayer p && p.Id != who)
		)
		{
			var creature = s.LegalActions()
				.OfType<CastSpellAction>()
				.Any(a =>
					a.CardId == cast.CardId
					&& a.Targets.Any(id =>
						s.HasObject(id)
						&& s.GetObject(id) is Card t
						&& t.ControllerId != who
						&& t.Is(CardType.Creature)
					)
				);
			burns.Add(
				(
					cast.CardId,
					who,
					s.GetPlayer(s.OpponentOf(who)).Life,
					move,
					turn,
					creature,
					$"{card.Name}{(cast.X > 0 ? $" (X={cast.X})" : "")} at their face{Held(s, who)}"
				)
			);
		}
	}

	/// <summary>
	/// 2. A declared attack that gives a creature away for little (Shayne, 2026-11-09: alpha strikes that kill or deal
	/// decent damage are fine; the mistake is a pure downside). Fires when some attacker would be eaten - an untapped
	/// creature of the defender could block it, kill it and survive, and it has no first/double strike, deathtouch,
	/// indestructible or attack trigger - AND the defender can hold the whole attack to at most <see cref="Minimal"/>
	/// damage by blocking the biggest attackers it can (menace attackers counted as unblocked), AND the attacker's
	/// controller has no instant in hand with an untapped land to back it.
	/// </summary>
	const int Minimal = 2;

	static string? PureDownside(GameState s, int who)
	{
		var attackers = Combat.Attackers(s).ToList();
		var blockers = s.CardsIn(s.OpponentOf(who), ZoneType.Battlefield)
			.Where(b => !b.IsTapped)
			.ToList();
		var trick =
			s.CardsIn(who, ZoneType.Hand).Any(c => c.Is(CardType.Instant) || s.Has(c, "Flash"))
			&& s.CardsIn(who, ZoneType.Battlefield).Any(c => c.Is(CardType.Land) && !c.IsTapped);
		if (attackers.Count == 0 || trick)
			return null;
		var eaten = attackers
			.Where(a =>
				!new[]
				{
					"Menace",
					"First strike",
					"Double strike",
					"Deathtouch",
					"Indestructible",
				}.Any(k => s.Has(a, k))
				&& !s.AbilitiesOf<TriggeredAbilityComponent>(a).Any()
				&& blockers.FirstOrDefault(b =>
					Combat.CanBlock(s, b, a)
					&& s.Power(b) >= s.Toughness(a) - a.Damage
					&& s.Toughness(b) - b.Damage > s.Power(a)
				)
					is not null
			)
			.ToList();
		if (eaten.Count == 0)
			return null;
		// the defender's best hold: each blocker stops the biggest attacker it can block (one blocker per attacker)
		var open = attackers.Where(a => !s.Has(a, "Menace")).OrderByDescending(s.Power).ToList();
		var free = blockers.ToList();
		var through = attackers.Where(a => s.Has(a, "Menace")).Sum(s.Power);
		foreach (var a in open)
		{
			var b = free.FirstOrDefault(b => Combat.CanBlock(s, b, a));
			if (b is null)
				through += s.Power(a);
			else
				free.Remove(b);
		}
		return through <= Minimal
			? $"{string.Join(", ", eaten.Select(a => $"{a.Name} {s.Power(a)}/{s.Toughness(a)}"))} eaten; the attack of {attackers.Count} can be held to {through} damage"
			: null;
	}
}
