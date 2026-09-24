using System;
using System.Linq;
using Godot;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE MAP's screens** (KinJam.md "THE MAP"): the town and its shop, the choice of area, a find,
/// the deeper path and the gym. Presentation only — every fact and price comes from `PartyRun`, and
/// every choice goes back to the board as a change to the run.
/// </summary>
public sealed partial class KinPartyRunScreens
{
	private static Texture2D ArtFor(string name) =>
		KinArt.Drawing(name) ?? KinArt.Figure(KinArt.ColourFor(name), true);

	/// <summary>What comes after this point of the run, in a few words.</summary>
	private static string Ahead(PartyRun run) =>
		run.Phase switch
		{
			RunPhase.Town => $"the town of {run.Region.Name}",
			RunPhase.Gym => $"the gym — {run.Region.Gym.Name}",
			RunPhase.Trail => run.CurrentStop.Kind switch
			{
				StopKind.Battle => $"a wild fight in {run.Area!.Name}",
				StopKind.Find => $"something on the path in {run.Area!.Name}",
				_ => "the deeper path — or the gym",
			},
			_ => "",
		};

	// ===== The town

	/// <summary>
	/// **The town: everyone is healed, and the shop is open** — Snares, three cards, and paying to
	/// take a card out of the deck. The team (and bench) can be rearranged here too.
	/// </summary>
	public void ShowTown(PartyRun run, Action<Func<PartyRun, PartyRun>> change)
	{
		Begin(
			$"TOWN — {run.Region.Name.ToUpperInvariant()}",
			"Everyone is rested to full — you too. Spend your gold, then set out."
		);

		ShowTeam(run, (team, bench) => change(r => r.Swap(team, bench)));

		_column.AddChild(Label("SHOP", 28, KinPalette.Bone));
		var shop = Row();

		var snare = Tile(
			KinPalette.Slate,
			null,
			"SNARE",
			["Catch a foe at a third of its HP or less.", $"{PartyRun.SnarePrice} GOLD"],
			new Vector2(240, 220),
			() => change(r => r.BuySnare())
		);
		snare.Disabled = !run.CanBuySnare;
		shop.AddChild(snare);

		var cards = run.ShopCards();
		for (var i = 0; i < cards.Count; i++)
		{
			var offer = i;
			var tile = Tile(
				KinPalette.Slate,
				null,
				run.Sold.Contains(offer)
					? "SOLD"
					: $"{cards[i].Name.ToUpperInvariant()} ({cards[i].Cost})",
				run.Sold.Contains(offer)
					? []
					:
					[
						string.Join(" ", KinRulesText.Lines(cards[i])),
						$"{PartyRun.CardPrice} GOLD",
					],
				new Vector2(240, 220),
				() => change(r => r.BuyCard(offer))
			);
			tile.Disabled = !run.CanBuyCard(offer);
			shop.AddChild(tile);
		}

		var buttons = Row();
		var remove = Button(
			$"REMOVE A CARD — {PartyRun.RemovePrice} GOLD",
			() => ShowRemove(run, change)
		);
		remove.Disabled = !run.CanRemove;
		buttons.AddChild(remove);
		buttons.AddChild(Button("SET OUT ▶", () => change(r => r.LeaveTown())));
	}

	/// <summary>The deck, one button a card: the one pressed leaves the deck for good.</summary>
	private void ShowRemove(PartyRun run, Action<Func<PartyRun, PartyRun>> change)
	{
		Begin(
			"REMOVE A CARD",
			$"{PartyRun.RemovePrice} gold. A thinner deck draws its best cards more."
		);

		HBoxContainer row = null;
		for (var i = 0; i < run.Deck.Count; i++)
		{
			if (i % 5 == 0)
				row = Row();
			var index = i;
			row!.AddChild(
				Button(
					$"{run.Deck[i].Name.ToUpperInvariant()} ({run.Deck[i].Cost})",
					() => change(r => r.Remove(index))
				)
			);
		}

		Row().AddChild(Button("BACK", () => change(r => r)));
	}

	// ===== The wild

	/// <summary>
	/// **Two areas, and what lives in each** — choosing an area is choosing what you might catch.
	/// </summary>
	public void ShowAreas(PartyRun run, Action<int> choose)
	{
		Begin(
			$"{run.Region.Name.ToUpperInvariant()} — CHOOSE AN AREA",
			"Each has its own creatures. Two wild fights, a find, then a deeper path — and the gym."
		);

		var row = Row();
		for (var i = 0; i < run.Region.Areas.Count; i++)
		{
			var index = i;
			var area = run.Region.Areas[i];
			row.AddChild(
				Tile(
					KinPalette.Slate,
					ArtFor(area.Pool[0].Name),
					area.Name.ToUpperInvariant(),
					[
						area.Description,
						"Lives here: "
							+ string.Join(", ", area.Pool.Select(f => f.Name).Distinct()),
						$"Rare, down the deeper path: {area.Rare.Name}",
					],
					new Vector2(420, 520),
					() => choose(index)
				)
			);
		}
	}

	public void ShowFind(PartyRun run, Action take)
	{
		Begin(
			$"{run.Area!.Name.ToUpperInvariant()} — A FIND",
			$"Stop {run.StopIndex + 1} of {run.Trail.Count}"
		);

		_column.AddChild(
			Label(
				run.CurrentStop.Find switch
				{
					FindKind.Snare => "A Snare, dropped in the grass. +1 Snare.",
					FindKind.Gold => $"A purse on the path. +{PartyRun.FoundGold} gold.",
					_ =>
						$"A quiet spot to rest. Every monster heals {(int)(PartyRun.RestHeal * 100)}% of its max HP.",
				},
				26,
				KinPalette.Gold
			)
		);
		ShowTeam(run, null);
		Row().AddChild(Button("TAKE IT ▶", take));
	}

	/// <summary>
	/// **The deeper path: optional, harder, and the only place the rare lives.** HP carries into the
	/// gym, so this is the push-your-luck.
	/// </summary>
	public void ShowDeep(PartyRun run, Action deeper, Action turnBack)
	{
		var fight = run.CurrentStop.Encounter!;
		Begin(
			$"{run.Area!.Name.ToUpperInvariant()} — THE DEEPER PATH",
			"A harder fight, and a rare creature to catch. Or turn back and face the gym as you are."
		);

		var row = Row();
		foreach (var foe in fight.Foes)
			row.AddChild(
				Tile(
					KinPalette.Slate,
					ArtFor(foe.Name),
					(foe.Name == run.Area.Rare.Name ? "RARE · " : "") + foe.Name.ToUpperInvariant(),
					[$"HP {foe.MaxHp} · SPEED {foe.Speed}"],
					new Vector2(220, 300),
					deeper
				)
			);

		ShowTeam(run, null);
		var buttons = Row();
		buttons.AddChild(Button("GO DEEPER", deeper));
		buttons.AddChild(Button("TURN BACK TO THE GYM", turnBack));
	}

	public void ShowGym(PartyRun run, Action fight)
	{
		Begin(
			$"THE GYM — {run.Region.Gym.Name.ToUpperInvariant()}",
			$"The leader has {run.Region.Gym.LeaderHp} health: a swing into an empty column hits them. Their creatures cannot be caught."
		);

		var row = Row();
		foreach (var foe in run.Region.Gym.Foes)
			row.AddChild(
				Tile(
					KinPalette.Slate,
					ArtFor(foe.Name),
					foe.Name.ToUpperInvariant(),
					[$"HP {foe.MaxHp} · SPEED {foe.Speed}"],
					new Vector2(220, 300),
					fight
				)
			);

		ShowTeam(run, null);
		Row().AddChild(Button("FIGHT", fight));
	}
}
