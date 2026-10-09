# Reality Fracture (FRA) coverage — MagicCore

Status, not rules: what is scripted, what waits and why. The rules live in `MagicCore/CLAUDE.md`.
Count scripted cards from `FraCards.cs` (`["Name"] =` keys) — never from memory; this file once ran one high.

**Playable by rarity** (scripted, keyword-only or basic — computed, 2026-10-09): common 85/86 · uncommon 109/109 · rare 30/64 · mythic 15/26 · **239/285**.
8a (rares from existing pieces): Aerid Konstrari, Craterclaw Colossus, Ingris Stingerquill, Overwrite the
Multiverse, The Echoverse Fulcrum, Tarmogoyf, Dark Matter Manipulator, Simulacrum Shaper, Lyra Archangel
of Dawn, Master of Barbs, Solarium Sentry, Liliana the Faultless. Remaining rare batches 8b–8g: HANDOFF-MtgReal.md.

Keywords by card count: Surveil 43, **Empower Jace 35** (creates/loads a Jace *planeswalker token*),
**Prepared 24** (21 cards use the two-face `prepare` layout), Flying 28. Modal "choose one", flashback,
cycling/landcycling, ward, equipment and +1/+1 counters are common. Implemented so far: 232 cards scripted
in `FraCards` (every one played by `RandomGameTests.RandomPlay_EveryScriptedCard`), including 15 Empower Jace cards, the 15 dual lands, 14 modal cards and 11 sacrifice /
dies / exile-until cards, 25 step-trigger / per-turn / prowess / surveil-trigger cards, the 6 flashback cards, 21 prepare cards, 12 landcycling / graveyard-ability cards, and 7 graveyard-return / hexproof /
indestructible / exhaust cards.
7e-1a: 11 commons (impulse draw, reflexive triggers, revealed hands, chosen colors). 7e-1b: Icy Reception, Unflinching
Hortimancer, Wrecking Gecko (counterspells and ward). 7e-1c: Medic's Kitesail, Ferocity of the Hunt,
Hunter's Axe, Warrior's Blades, Lich's Relic (auras and equipment). 7e-2a: 11 uncommons — Refute Destiny, Your Fate Ends Here,
Precise Redaction, Multiply by Zero, Essence Burn, Violent Echoes, Clash of Elements, Primal Witchstalker,
Theorix Charm, Twisted Fates, Vigorbloom Charm (removal and spells). 7e-2b: 13 — Bloombrute, Koth of the
Homestead, Rescue Girl, both Teyos, Kiora of Fire and Ashes, Pia Determined Rebuilder, Hapatra the Desert
Frost, Geist of Saint Thalia, Arni Humble Scribe, Traxos Scourge Eternal, Saheeli Jewel of Avishkar,
Hexhaven Dueling Arena (creatures). 7e-2c: 9 — Tinybones, Titanbones, Jiang Yanggu Alone, Pia Aether
Ascetic, both Danithas, Plan for All Outcomes, Proft Sinister Mastermind, Countersculpt (discard and cast
triggers). 7e-2d: 6 — Tomik Izzet Sparkmage, Yoshimaru Beloved Companion, Mabel Bitter Recluse,
Hapatra the Desert Fang, Loot the Anomaly, Ghalta the Immovable (replacements, -1/-1 counters, combat
damage). 7e-2e: 6 — Yuriko Blade of the Mighty, Tomik Orzhov Lawmage, Kiora of Salt and Sand, Winter
Team Player, both Fblthps (combat, convoke, domain, "you win the game"). 7f-1: Flourishing Grapple, Infinite Coursework ("loses all abilities", read through `s.AbilitiesOf`).
7f-2: Loot the Nexus, Heartwood Crafter, Pyre Rhymer (mana: several of one colour, restricted
mana, a Mountain's extra {R}). 7f-3: Way of the Cryomancer (copying a spell). **Every common and uncommon is in but one:** Sphinx's Approach (deferred). Puppet Crafting waits on type-changing. **Two real planeswalkers are
in** (The Theorist, Ajani Unrelenting); **the other 6 are not**: each needs an emblem, an "until your next turn" effect, impulse draw or a −X cost first.
Coverage plan, by cards unblocked: 7a lands ✓ · 7b-1 modal ✓ · 7b-2 sacrifice, dies triggers, exile-until-leaves ✓ · 7c-1 continuous effects ✓ · 7c-2 step triggers,
per-turn tracking, prowess ✓ · 7d-1 flashback ✓ · 7d-2 prepare ✓ · 7d-3a landcycling, graveyard abilities ✓ · 7d-3b graveyard returns, hexproof, indestructible ✓. The Bo1 hand smoother is Arena's published description; its weighting and whether
limited Bo1 uses it are unconfirmed — hence `handSmoothing`.
