using System.Collections.Generic;
using System.Linq;
using Godot;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **A basic card's text as SYMBOLS** (the declutter pass, Shayne, 2026-09-30: "most games use
/// symbols to portray certain values"). Built from the card's STEPS, not its sentence, so it cannot
/// drift from the rules: a card made only of basic steps reads "[shield] 8", "[bolt] 8",
/// "[swirl] +3 [cards] 2". A card with any real rule keeps its words (`null` here). Presentation
/// only — it reads the steps' authored numbers and adds the live Spell Power the hand already shows.
/// </summary>
public static class KinCardIcons
{
	private const string Dir = "res://KinGame/Art/icons/";

	/// <summary>The BBCode line, or null when the card has a rule words must say.</summary>
	public static string Line(KinCard card, int spellBonus, int iconSize)
	{
		// A card that carries a rule on itself (Wildfire's discount, Meteor's X) says it in words.
		if (!card.Components.IsEmpty)
			return null;
		var parts = new List<string>();
		foreach (var effect in card.Effects)
		{
			var part = Part(effect.Template, spellBonus, iconSize);
			if (part is null)
				return null;
			parts.Add(part);
		}
		return parts.Count == 0 ? null : "[center]" + string.Join("   ", parts) + "[/center]";
	}

	private static readonly Color Grove = KinPalette.Family(Family.Grove).Darkened(0.1f);
	private static readonly Color Ember = Color.FromHtml("#C2621F");

	private static string Part(ImmutableGameObjects.GameAction step, int spellBonus, int size) =>
		step switch
		{
			GuardAction g => Icon("shield", KinCardKit.Ink, size) + $" {g.Amount}",
			RootAction r => Icon("shield", Grove, size) + $" {r.Amount}",
			StrikeAction
			{
				Aim: Aim.Front,
				PerX: 0,
				PerBlock: 0,
				PerThorns: 0,
				PlusCardsInHand: false,
			} s => Icon("attack", KinCardKit.Ink, size) + $" {s.Amount}",
			SpellDamageAction
			{
				Target: SpellTarget.Foe,
				PerX: 0,
				PerBurn: 0,
				PerSpellThisTurn: 0,
				PerRootedOnLine: 0,
				FromSpellDamageThisTurn: false,
				SpellPowerTimes: 1,
			} d => Icon("spell", Ember, size) + " " + Live(d.Amount, spellBonus),
			BurnAction { All: false, Double: false, RiseToHighest: false } b => Icon(
				"burn",
				Ember,
				size
			) + $" {b.Amount}",
			ThornsAction { ForFight: false, AllLine: false } t => Icon("thorns", Grove, size)
				+ $" {t.Amount}",
			DrawAction { CountKey: "" } d => Icon("draw", KinCardKit.Ink, size) + $" {d.Count}",
			GainEnergyAction e => Icon("energy", KinCardKit.Ink, size) + $" +{e.Amount}",
			SpellPowerAction { ForFight: false } p => Icon("spell_power", Ember, size)
				+ $" +{p.Amount}",
			GrowAction { AllLine: false, ByOwnPower: false } g => Icon("power", Grove, size)
				+ $" +{g.Amount}",
			_ => null,
		};

	/// <summary>A spell's damage as it plays NOW — green when Spell Power is in it, as the words were.</summary>
	private static string Live(int amount, int bonus) =>
		bonus > 0 ? $"[color=#1F7A2E]{amount + bonus}[/color]" : $"{amount}";

	private static string Icon(string name, Color tint, int size) =>
		$"[img={size}x{size} color=#{tint.ToHtml(false)}]{Dir}{name}.svg[/img]";
}
