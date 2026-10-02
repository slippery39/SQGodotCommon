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
		KinArt.Sprite(name) ?? KinArt.Drawing(name) ?? KinArt.Figure(KinArt.ColourFor(name), true);

	/// <summary>What comes after this point of the run, in a few words.</summary>
	private static string Ahead(PartyRun run) =>
		run.Phase switch
		{
			RunPhase.Town => $"the town of {run.Region.Name}",
			RunPhase.Route => "back to the route",
			_ => "",
		};

	// ===== The town's buildings (KinMapPlan.md §3) — each opened from the town map (KinTownMap)

	/// <summary>
	/// **Inside a building.** `back` returns to the town map. A change made inside (a purchase, a
	/// heal, a swap) redraws the same building.
	/// </summary>
	public void ShowBuilding(
		PartyRun run,
		BuildingKind kind,
		Action<Func<PartyRun, PartyRun>> change,
		Action back
	)
	{
		switch (kind)
		{
			case BuildingKind.Hospital:
				ShowHospital(run, change);
				break;
			default:
				ShowShop(run, change);
				break;
		}
		Row().AddChild(Button("◀ BACK TO TOWN", back));
	}

	/// <summary>
	/// **The hospital**: the team's HP, healing everyone to full for gold — and the team's ORDER, the
	/// only place it is set (2026-10-02): press a monster to send it to the front.
	/// </summary>
	private void ShowHospital(PartyRun run, Action<Func<PartyRun, PartyRun>> change)
	{
		Begin("HOSPITAL", "", "town");
		ShowTeam(run, index => change(r => r.MoveToFront(index)));
		var heal = Button(
			$"HEAL EVERYONE — {PartyRun.HospitalPrice} GOLD",
			() => change(r => r.HealAtHospital())
		);
		heal.Disabled = run.CannotHeal is not null;
		Row().AddChild(heal);
		if (run.CannotHeal is { } why)
			_column.AddChild(Label(why + ".", 22, KinPalette.Bone));
	}

	/// <summary>**The shop**: three cards (each once), and paying to take a card out.</summary>
	private void ShowShop(PartyRun run, Action<Func<PartyRun, PartyRun>> change)
	{
		Begin("SHOP", $"GOLD {run.Gold}", "town");

		var shop = Row();

		var cards = run.ShopCards();
		for (var i = 0; i < cards.Count; i++)
		{
			var offer = i;
			// The card itself, as in the hand; its price under it, or SOLD.
			var tile = CardButton(
				cards[i],
				() => change(r => r.BuyCard(offer)),
				run.Sold.Contains(offer) ? "SOLD" : $"{PartyRun.CardPrice} GOLD"
			);
			tile.Disabled = !run.CanBuyCard(offer);
			if (tile.Disabled)
				tile.Modulate = new Color(0.55f, 0.55f, 0.6f);
			shop.AddChild(tile);
		}

		var remove = Button(
			$"REMOVE A CARD — {PartyRun.RemovePrice} GOLD",
			() => ShowRemove(run, change)
		);
		remove.Disabled = !run.CanRemove;
		Row().AddChild(remove);
	}

	/// <summary>
	/// **A SPRING: heal, or upgrade a card** (round 4 — STS's campfire). One tile a card that has a +,
	/// showing what it becomes.
	/// </summary>
	public void ShowSpring(PartyRun run, Action heal, Action<int> upgrade)
	{
		Begin("A SPRING", "", "map");
		ShowTeam(run);
		Row().AddChild(Button($"HEAL {(int)(PartyRun.RestHeal * 100)}%", heal));

		var upgradable = run.Upgradable.ToList();
		if (upgradable.Count == 0)
		{
			_column.AddChild(
				Label("No card in your deck can be upgraded yet.", 20, KinPalette.Bone)
			);
			return;
		}
		_column.AddChild(Label("OR UPGRADE ONE", 24, KinPalette.Bone));
		var row = ScrollRow(370);
		// One tile a NAME: five Strikes upgrade the same way. Still more names than fit: it scrolls.
		foreach (var index in upgradable.DistinctBy(i => run.Deck[i].Name))
		{
			var better = run.Deck[index].Upgraded!;
			row.AddChild(CardButton(better, () => upgrade(index)));
		}
	}

	/// <summary>
	/// The deck as its REAL cards, in a scrolling row as the spring's (playtest, 2026-10-02: name
	/// buttons hid what each card does): the one pressed leaves the deck for good.
	/// </summary>
	private void ShowRemove(PartyRun run, Action<Func<PartyRun, PartyRun>> change)
	{
		Begin(
			"REMOVE A CARD",
			$"{PartyRun.RemovePrice} gold. A thinner deck draws its best cards more."
		);

		var row = ScrollRow(370);
		for (var i = 0; i < run.Deck.Count; i++)
		{
			var index = i;
			row.AddChild(CardButton(run.Deck[i], () => change(r => r.Remove(index))));
		}

		Row().AddChild(Button("BACK", () => change(r => r)));
	}
}
