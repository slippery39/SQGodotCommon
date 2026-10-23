# Reality Fracture (FRA) coverage — MagicCore

Status, not rules: what is scripted, what waits and why. The rules live in `MagicCore/CLAUDE.md`.
Count scripted cards from `FraCards.cs` (`["Name"] =` keys) — never from memory; this file once ran one high.

**Playable by rarity** (scripted, keyword-only or basic — computed, 2026-10-23): common 86/86 · uncommon 109/109 · rare 64/64 · mythic 36/36 · **295/295** — with the 10 Special Guests (SPG #159–168, story MC-3), mythics in FRA.json, in 1 pack in 55. Batch 1: Consider, Austere Command, Mind Twist, Eye of Ugin, Root Maze, Splinter Twin. Batch 2: Consign to Memory (targeting an ability on the stack, replicate), Sublime Epiphany ("choose one or more"), Flesh Duplicate (enter as a copy, vanishing), Necrodominance (skip draw, pay any life, hand size, graveyard → exile).
8a (rares from existing pieces): Aerid Konstrari, Craterclaw Colossus, Ingris Stingerquill, Overwrite the
Multiverse, The Echoverse Fulcrum, Tarmogoyf, Dark Matter Manipulator, Simulacrum Shaper, Lyra Archangel
of Dawn, Master of Barbs, Solarium Sentry, Liliana the Faultless. 8b: Stinging Vitriol, Loyal Tutor, Entrust
the Spark, Codie, Karn Gilded Guardian, Roiling Canopy, Samut Tyrant of Naktamun, Jace's Machinations,
Theorist's Sanctum, Theorist's Proxy, Solitary Cell. 8c: both Vraskas, Kwia Vigorbloom, Draconic Visitor,
Hungering Puppetbeast, Tenured Tethermage, Gideon's Memorial (Treasure, restricted mana, token replacement).
8d (one-offs): Gideon the Oathless, Flickering Hound, Gardenize, Cruel Calculations, Curse-Marred Demon,
Hexhaven Invigorator, Seasoned Cryomancer, Identity Echo, Kindred Judgment, Karn Argent Defender, Sphinx's
Approach, Null Summoner, Omnipresence. 8e (copying permanents): Sphinx of False Conclusions, Face Yourself,
Vindictive Triumph, Hall of Echoes. 8f (planeswalkers): Ajani Resolute, both Chandras, both Garruks, Jace
Reality Sculptor, Tam the Possibility. 8g: Puppet Crafting, Emrakul the Exigent Doom, Uldaros Theorix (type
change, casting from exile, casting mid-resolution). **Extrapolate the Impossible, the last** (2026-10-21,
story LIM-1.6): "from outside the game" is the sideboard — sealed's unplayed pool. **Every FRA card plays.**

Keywords by card count: Surveil 43, **Empower Jace 35** (creates/loads a Jace *planeswalker token*),
**Prepared 24** (21 cards use the two-face `prepare` layout), Flying 28. Modal "choose one", flashback,
cycling/landcycling, ward, equipment and +1/+1 counters are common. Implemented so far: 250 cards scripted
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
mana, a Mountain's extra {R}). 7f-3: Way of the Cryomancer (copying a spell). **Every common and uncommon is in** (Sphinx's Approach, the last, in 8d). **Every real planeswalker is
in** (8f added emblems, "until your next turn" and −X).
Coverage plan, by cards unblocked: 7a lands ✓ · 7b-1 modal ✓ · 7b-2 sacrifice, dies triggers, exile-until-leaves ✓ · 7c-1 continuous effects ✓ · 7c-2 step triggers,
per-turn tracking, prowess ✓ · 7d-1 flashback ✓ · 7d-2 prepare ✓ · 7d-3a landcycling, graveyard abilities ✓ · 7d-3b graveyard returns, hexproof, indestructible ✓. The Bo1 hand smoother is Arena's published description; its weighting and whether
limited Bo1 uses it are unconfirmed — hence `handSmoothing`.
