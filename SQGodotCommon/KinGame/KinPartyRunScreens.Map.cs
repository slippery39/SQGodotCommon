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

	/// <summary>**The hospital**: the team's HP, and healing everyone to full for gold.</summary>
	private void ShowHospital(PartyRun run, Action<Func<PartyRun, PartyRun>> change)
	{
		Begin(
			"HOSPITAL",
			$"The team healed to full — {PartyRun.HospitalPrice} gold. Nowhere else heals fully.",
			"town"
		);
		ShowTeam(run);
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
		Begin("SHOP", $"You have {run.Gold} gold.", "town");

		var shop = Row();

		var cards = run.ShopCards();
		for (var i = 0; i < cards.Count; i++)
		{
			var offer = i;
			// The card's action picture (style D), as the reward screen shows it.
			var tile = Tile(
				run.Sold.Contains(offer) ? KinPalette.Slate : KinPalette.Family(cards[i].Family),
				run.Sold.Contains(offer) ? null : CardArt(cards[i].Name),
				run.Sold.Contains(offer)
					? "SOLD"
					: $"{cards[i].Name.ToUpperInvariant()} ({cards[i].Cost})",
				run.Sold.Contains(offer)
					? []
					:
					[
						KinCardFace.Tag(cards[i]),
						string.Join(" ", KinRulesText.Lines(cards[i])),
						$"{PartyRun.CardPrice} GOLD",
					],
				new Vector2(260, 400),
				() => change(r => r.BuyCard(offer))
			);
			tile.Disabled = !run.CanBuyCard(offer);
			shop.AddChild(tile);
		}

		var remove = Button(
			$"REMOVE A CARD — {PartyRun.RemovePrice} GOLD",
			() => ShowRemove(run, change)
		);
		remove.Disabled = !run.CanRemove;
		Row().AddChild(remove);
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
}
