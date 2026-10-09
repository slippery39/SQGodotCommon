# Reality Fracture (FRA) coverage — MagicCore

Status, not rules: what is scripted, what waits and why. The rules live in `MagicCore/CLAUDE.md`.
Count scripted cards from `FraCards.cs` (`["Name"] =` keys) — never from memory; this file once ran one high.

Keywords by card count: Surveil 43, **Empower Jace 35** (creates/loads a Jace *planeswalker token*),
**Prepared 24** (21 cards use the two-face `prepare` layout), Flying 28. Modal "choose one", flashback,
cycling/landcycling, ward, equipment and +1/+1 counters are common. Implemented so far: 193 cards scripted
in `FraCards` (every one played by `RandomGameTests.RandomPlay_EveryScriptedCard`), including 15 Empower Jace cards, the 15 dual lands, 14 modal cards and 11 sacrifice /
dies / exile-until cards, 25 step-trigger / per-turn / prowess / surveil-trigger cards, the 6 flashback cards, 19 prepare cards (Pyre Rhymer and Heartwood Crafter wait: a mana-tap
trigger, restricted mana), 12 landcycling / graveyard-ability cards, and 7 graveyard-return / hexproof /
indestructible / exhaust cards.
7e-1a: 11 commons (impulse draw, reflexive triggers, revealed hands, chosen colors). 7e-1b: Icy Reception, Unflinching
Hortimancer, Wrecking Gecko (counterspells and ward). 7e-1c: Medic's Kitesail, Ferocity of the Hunt,
Hunter's Axe, Warrior's Blades, Lich's Relic (auras and equipment). 7e-2a: 11 uncommons — Refute Destiny, Your Fate Ends Here,
Precise Redaction, Multiply by Zero, Essence Burn, Violent Echoes, Clash of Elements, Primal Witchstalker,
Theorix Charm, Twisted Fates, Vigorbloom Charm (removal and spells). 7e-2b: 13 — Bloombrute, Koth of the
Homestead, Rescue Girl, both Teyos, Kiora of Fire and Ashes, Pia Determined Rebuilder, Hapatra the Desert
Frost, Geist of Saint Thalia, Arni Humble Scribe, Traxos Scourge Eternal, Saheeli Jewel of Avishkar,
Hexhaven Dueling Arena (creatures). Uncommons left: 25, batched 7e-2c–e
(HANDOFF-MtgReal.md); Flourishing Grapple waits with Infinite Coursework, Heartwood Crafter on restricted
mana, Way of the Cryomancer on copying a spell. Commons left: Infinite Coursework
(waits: "loses all abilities" needs one central read of a card's abilities), Sphinx's Approach
(deferred). Puppet Crafting waits on type-changing. **Two real planeswalkers are
in** (The Theorist, Ajani Unrelenting); **the other 6 are not**: each needs an emblem, an "until your next turn" effect, impulse draw or a −X cost first.
Coverage plan, by cards unblocked: 7a lands ✓ · 7b-1 modal ✓ · 7b-2 sacrifice, dies triggers, exile-until-leaves ✓ · 7c-1 continuous effects ✓ · 7c-2 step triggers,
per-turn tracking, prowess ✓ · 7d-1 flashback ✓ · 7d-2 prepare ✓ · 7d-3a landcycling, graveyard abilities ✓ · 7d-3b graveyard returns, hexproof, indestructible ✓. The Bo1 hand smoother is Arena's published description; its weighting and whether
limited Bo1 uses it are unconfirmed — hence `handSmoothing`.
