using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;
using static MagicCore.Sets.Tokens;

namespace MagicCore.Sets.Trk;

/// <summary>
/// Star Trek's hand-written abilities, keyed by card name (story MC-5) — as <see cref="Fra.FraCards"/>:
/// names, costs, types, P/T and keywords come from TRK.json; only what the rules text DOES is written
/// here. A card another set already scripts (a reprint) is NOT scripted again — <see cref="CardRegistry"/>
/// refuses it. Survey, tiers and what each card waits on: <c>MagicCore/TrkCoverage.md</c>.
/// </summary>
public static class TrkCards
{
	// ===== Tokens (TRK's own; Treasure, Clue and Food are Tokens') =====

	/// <summary>"A 1/1 red Officer creature token."</summary>
	public static readonly CardData Officer = Token(
		"Officer",
		"Creature — Officer",
		"1",
		"1",
		["R"]
	);

	/// <summary>"A 1/1 colorless Borg artifact creature token."</summary>
	public static readonly CardData Borg = Token("Borg", "Artifact Creature — Borg", "1", "1");

	/// <summary>"A 2/1 red Klingon Warrior creature token with haste."</summary>
	public static readonly CardData KlingonWarrior = Token(
		"Klingon Warrior",
		"Creature — Klingon Warrior",
		"2",
		"1",
		["R"],
		["Haste"]
	);

	/// <summary>"A 2/2 black Romulan creature token."</summary>
	public static readonly CardData Romulan = Token(
		"Romulan",
		"Creature — Romulan",
		"2",
		"2",
		["B"]
	);

	/// <summary>"A 3/3 colorless Illusion artifact creature token."</summary>
	public static readonly CardData ArtifactIllusion = Token(
		"Illusion",
		"Artifact Creature — Illusion",
		"3",
		"3"
	);

	/// <summary>"A tapped Planet land token named New Planet with '{T}: Add one mana of any color.'"</summary>
	public static readonly CardData NewPlanet = Token("New Planet", "Land — Planet");

	/// <summary>"Create a tapped Planet land token named New Planet …" (Genesis Effect, Stable Wormhole).</summary>
	public static CreateTokens CreateNewPlanet =>
		new(NewPlanet, 1)
		{
			Abilities = [.. ManaAbilities.AnyColor.Select(c => new ManaAbilityComponent(c))],
			Tapped = true,
		};

	/// <summary>
	/// The Planet duals: "This land enters tapped. When this land enters, you gain 1 life.
	/// {T}: Add {A} or {B}."
	/// </summary>
	static ImmutableArray<GameComponent> PlanetDual(ManaColor a, ManaColor b) =>
		[
			new EntersTappedComponent(),
			new TriggeredAbilityComponent(TriggerEvent.SelfEnters, [new GainLife(Ref.You, 1)]),
			new ManaAbilityComponent(a),
			new ManaAbilityComponent(b),
		];

	internal static readonly Dictionary<string, ImmutableArray<GameComponent>> Scripts =
		new()
		{
			// ===== MC-5.5: the Planet duals =====
			["Andoria"] = PlanetDual(ManaColor.Blue, ManaColor.Red),
			["Bajor"] = PlanetDual(ManaColor.Red, ManaColor.White),
			["Cardassia Prime"] = PlanetDual(ManaColor.White, ManaColor.Black),
			["Earth, Sector 001"] = PlanetDual(ManaColor.Green, ManaColor.Blue),
			["Ferenginar"] = PlanetDual(ManaColor.Black, ManaColor.Green),
			["Orion"] = PlanetDual(ManaColor.Red, ManaColor.Green),
			["Qo'noS"] = PlanetDual(ManaColor.Black, ManaColor.Red),
			["Risa"] = PlanetDual(ManaColor.Green, ManaColor.White),
			["Romulus"] = PlanetDual(ManaColor.Blue, ManaColor.Black),
			["Vulcan"] = PlanetDual(ManaColor.White, ManaColor.Blue),
		};
}
