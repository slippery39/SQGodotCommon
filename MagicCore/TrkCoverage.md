# Star Trek (TRK) coverage — MagicCore

Status, not rules: what is scripted, what waits and why (story MC-5). The rules live in
`MagicCore/CLAUDE.md`. **Scripted: the 10 Planet duals (MC-5.5, 2026-10-31); MC-5.3 batch 1, every tier
0–1 common and uncommon not waiting on STATION or ANY-COUNTER (88); batch 2, Station (MC-5.2) and the 12
STATION commons and uncommons waiting on nothing else; batch 3, the small mechanics (beam me up, strange new
worlds, dilemma, power 8, becomes tapped, look and take, Federation) and their 31 commons and uncommons** —
147/311 play, with the basics and Mugato. Batch 3 left for batch 4: Astute Terraformer (ONCE-A-TURN),
La'An Noonien-Singh (CANT-BLOCK), Beckett Mariner and Brad Boimler (ANY-COUNTER), and four re-tiered to 3
from their full text — Species 8472 (can't be blocked by artifact creatures; can't be targeted by opponents'
artifact abilities), Ensign Pavel Chekov (exile their top X, play a land from among them), Erica Ortegas (a
land from your hand, one returned at the next end step), Warp Speed ("Lands you control enter untapped"). Batch 2
re-tiered Stray from History to tier 2 (Null Summoner's exile, from a sorcery); Hemmer (TAPPED), Rutherford
(LOOK-TAKE) and Zefram Cochrane (three tier-3 one-offs: look at the top any time, cast an artifact from
the top, Spacecraft enter with two more counters — Shayne: with the one-offs) wait. Batch 1 re-tiered from the full text: Tea, Earl Grey, Hot (a modal activated ability), Daniels (a
graveyard-to-bottom cost), Culber and Stamets (mana of any one color) and V'Ger (look at a hand) were
tier 3; Red Alert!, Sickbay, Locutus, Spatial Anomaly, Munitions Enthusiast and Khaaaan! tier 2 (`python tools/magic_set.py coverage --set TRK`; every card's script beside its text:
`MagicCore/TrkCardSheet.md`). The TOK tokens are DEFINED, waiting for their cards. The tiers are the survey's (MC-5.1):
judged from each card's full text against `AbilityCatalog.md` — a batch re-checks its own cards'
tiers before building. Tier 0 keywords only · 1 existing pieces · 2 a tested piece with a new option
· 3 a new engine piece (`/card-batch`).

**By rarity (C/U/R/M):** tier 0 5/1/0/0 · tier 1 62/41/9/2 · tier 2 27/49/30/8 · tier 3 2/19/41/15.
Buildable with today's pieces 103 · + token definitions 142 · + Station 160 · + every small piece
below 234 (94/91/39/10). The rest are tier-3 one-offs, mostly rare and mythic.

## What each card waits on — and what to reuse

Rules for the new mechanics: Wizards' "Star Trek Release Notes" (magic.wizards.com) — Station's
are Edge of Eternities'. Grep MtgCore before building any of these (MagicCore/CLAUDE.md).

| Tag | The new piece | Reuse |
|---|---|---|
| TOK | DONE (MC-5.5): `TrkCards.Officer`, `Borg`, `KlingonWarrior`, `Romulan`, `ArtifactIllusion`, `CreateNewPlanet`; `Tokens.CreateClue`, `CreateFood`, `CreateTreasure` (tested: `TrkCardTests`). Not yet: Porthos, Murf, the Worm (CANT-BLOCK), City's Human — one-offs, with their card | `new CreateTokens(TrkCards.Officer, n)` |
| PLANET-DUAL | DONE (MC-5.5): the 10 Planet duals, `TrkCards.PlanetDual` | — |
| STATION | DONE (MC-5.2): `TrkCards.Station(n, keywords)` — `StationComponent` (a creature at n+, read live by `Card.Is`), the station ability (the creature tapped is a choice, `ActivateAbilityAction.TapId`), a static per "n+" keyword. Another threshold's ability: `If`/`OnlyIf` = `new AtLeast(Count.ChargeCountersOn(Ref.Source), n)` | — |
| SNW | DONE (MC-5.2, batch 3): `TrkCards.DifferentlyNamedLands` (`Count.DistinctNamesOf`) — a cost change (`.Negated`), a power, a life gain | — |
| BEAM | DONE (MC-5.2, batch 3): `TrkCards.BeamMeUp("{2}{W}")` — a flashback with `BeamMeUpCost` (`CastSpellAction.ReturnId`); Marooned's `EnchantedCantBeBeamedUp`. Not yet: Scotty granting it (as Stingcaster Mage grants flashback) | — |
| DILEMMA | DONE (MC-5.2, batch 3): `TriggerEvent.YouFaceADilemma` — raised by a cast, an activation and a modal trigger. Not yet: a count this turn (no C/U needs it) | — |
| SAGA | Lore counters, chapter triggers ("after your draw step" = first main phase), sacrifice after the last | `TriggerEvent.YourFirstMainPhase` |
| POWER8 | DONE (MC-5.2, batch 3): `TriggerEvent.YouAttackWithTotalPower8` (`Trigger.While`, judged as attackers are declared); `Count.TotalPowerOf` for a "then … if" | — |
| TAPPED | DONE (MC-5.2, batch 3): `TriggerEvent.SelfBecomesTapped` — `MagicState.Tap`, every tap path | — |
| FED | DONE (MC-5.2, batch 3): `TrkCards.Federation` (`Count.CreatureTypesAmong`, Spacecraft left out) | — |
| QUEST, ANY-COUNTER | Quest, promotion, peace counters; "a counter of any kind" | proposed: ONE general counter map for the new kinds; the existing counter fields stay |
| KICKER | An optional extra payment as it is cast, then "if kicked" | `ReplicateComponent`'s pattern; `SacrificeCost` (Eject the Warp Core) |
| SHOCK | "As this land enters, you may pay 2 life. If you don't, it enters tapped." | `EntersTappedComponent(Unless)` |
| ENTERS-UNTAPPED | "When this land enters untapped, choose one —" | `EntersTappedComponent` + a modal trigger |
| CANT-BLOCK | "Can't block", as a keyword-like grant | `Combat.CantBeBlocked`'s pattern |
| LOOK-TAKE | DONE (MC-5.2, batch 3): `LookAndTake(n, filter) { Reveal, Must, RestToGraveyard }` | — |
| DELAYED-RETURN | "Exile …, return it at the beginning of the next end step" | `Flicker`, `DelayedTrigger` |
| LEAVES-GY | "Whenever one or more cards leave your graveyard" | — |
| CONTROL | Gain control (until end of turn, or for good; exchange) | MtgCore's `GainControlAction` |
| ASSIMILATE | Onto the battlefield under your control, +1/+1 counter, a Borg artifact creature | `ReturnFromGraveyard`, the type change |
| ONCE-A-TURN, RESOLVED-TWICE, TYPE-SET, TRIGGER-TWICE, GRANT-TRIGGER | Small one-offs: activate only once each turn; "the second time this ability has resolved this turn"; "becomes a [type]"; a trigger triggering an additional time; artifact creatures "have '[triggered ability]'" | `Exhaust`'s stamp; `OncePerTurn`; `GrantsActivatedAbility` |

Also needed, no tag: H.M.S. Bounty's extra turn (MtgCore's `TakeExtraTurnAction`), Borg Cube's
affinity (a `CostChange` with a count). Genuinely new, found in neither engine: Station, dilemma,
the beam-me-up cost, Sagas, Kicker, assimilate, explore, phasing (The City on the Edge of Forever),
sunburst, trigger doubling, draw replacement (Mr. Spock).

**Reprints:** a card is scripted ONCE (MC-5.5). 17 TRK cards are reprints (10 shocklands, Engineered
Explosives, Reckless Impulse, 5 basics); only the basics overlap FRA. Scryfall's TRK text for Jett
Reno reads "+1/1 counter" — script +1/+1. The Stardates (SDS, 30) come last (MC-5.6).

## Every card

| Card | Rarity | Tier | Waits on |
|---|---|---|---|
| Forest | C | 0 |  |
| Island | C | 0 |  |
| Mountain | C | 0 |  |
| Plains | C | 0 |  |
| Swamp | C | 0 |  |
| Mugato | U | 0 |  |
| Andoria | C | 1 | PLANET-DUAL |
| Assault Drone | C | 1 |  |
| Automated Warfare System | C | 1 |  |
| Bajor | C | 1 | PLANET-DUAL |
| Battle-Scarred Survivalist | C | 1 |  |
| Captain's Tutelage | C | 1 |  |
| Cardassia Prime | C | 1 | PLANET-DUAL |
| Cetacean Ops | C | 1 |  |
| Cha'DIch Investigator | C | 1 | TOK |
| Cloistered Telepath | C | 1 |  |
| Command Decision | C | 1 |  |
| Consider the Prime Directive | C | 1 |  |
| Cybernetic Specialist | C | 1 |  |
| Dahar Master | C | 1 |  |
| Dathon and Picard at El-Adrel | C | 1 |  |
| Dominion Assault Force | C | 1 |  |
| Dominion Supervisor | C | 1 | ANY-COUNTER |
| Earth, Sector 001 | C | 1 | PLANET-DUAL |
| Emergency Holographic Crew | C | 1 | STATION |
| Emergency Medical Hologram | C | 1 |  |
| Federation Probe | C | 1 |  |
| Ferenginar | C | 1 | PLANET-DUAL |
| Free Borg Revolutionaries | C | 1 |  |
| Fugitive Listener | C | 1 |  |
| Genesis Effect | C | 1 | TOK |
| He's Dead, Jim | C | 1 |  |
| Horta | C | 1 |  |
| Humpback Whales | C | 1 |  |
| Interstellar Effigy | C | 1 |  |
| Make It So | C | 1 | TOK |
| Malfunctioning Holodeck | C | 1 |  |
| Moopsy | C | 1 |  |
| Orion | C | 1 | PLANET-DUAL |
| Perils of the Past | C | 1 |  |
| Photosynthetic Physician | C | 1 | TOK |
| Planetary Bombardment | C | 1 |  |
| Planetary Patrol | C | 1 |  |
| Plasma Cascade | C | 1 |  |
| Qo'noS | C | 1 | PLANET-DUAL |
| Relentless Drednok | C | 1 |  |
| Risa | C | 1 | PLANET-DUAL |
| Rogue Artificial Intelligence | C | 1 |  |
| Romulus | C | 1 | PLANET-DUAL |
| Saurian Explorer | C | 1 | TOK |
| Savvy Profiteer | C | 1 | TOK |
| Set Phasers to . . . | C | 1 |  |
| Shields Up! | C | 1 |  |
| Silicate Surveyor | C | 1 |  |
| Starfleet Security | C | 1 | TOK |
| Starfleet Spanner Jockey | C | 1 |  |
| Stray from History | C | 1 | STATION |
| Tal Shiar Analyst | C | 1 |  |
| Tal Shiar Operative | C | 1 |  |
| Tea, Earl Grey, Hot | C | 1 | TOK |
| Tenacious Drone | C | 1 | TOK |
| Tricorder | C | 1 |  |
| Unexplored Territory | C | 1 |  |
| Vexing Drone | C | 1 |  |
| Vulcan | C | 1 | PLANET-DUAL |
| Warp Core | C | 1 | STATION |
| Xenobotanist | C | 1 |  |
| Xindi Surveyors | C | 1 |  |
| Academy Instructor | U | 1 | TOK |
| Artificial Anomaly | U | 1 | TOK |
| Assembly Specialist | U | 1 | TOK |
| Bat'leth | U | 1 |  |
| Collective Drone | U | 1 |  |
| Command Subadjunct | U | 1 |  |
| Commander Raffi Musiker | U | 1 | TOK |
| Constable Odo | U | 1 | TOK |
| Culber and Stamets, Star-Crossed | U | 1 |  |
| D'Ghor Bloodletter | U | 1 |  |
| D'Vana Tendi, Optimistic Ensign | U | 1 |  |
| Daniels, Temporal Agent | U | 1 |  |
| David and Carol Marcus, Creators | U | 1 | TOK |
| Gaseous Anomaly | U | 1 | TOK |
| Impossible Choice | U | 1 | TOK |
| Khaaaaaaaaaaaannn! | U | 1 |  |
| Khan, Despot by Design | U | 1 |  |
| Locutus, the One Who Speaks | U | 1 |  |
| Lore, Positronic Provocateur | U | 1 |  |
| Munitions Enthusiast | U | 1 |  |
| Neelix, Moral Support | U | 1 | TOK |
| Organic Avulsion Unit | U | 1 |  |
| Picard, Leading by Example | U | 1 |  |
| Ravenous Cosmozoan | U | 1 |  |
| Red Alert! | U | 1 |  |
| Resistance Is Futile | U | 1 |  |
| Saru, Unthreatened | U | 1 |  |
| Sickbay | U | 1 |  |
| Spatial Anomaly | U | 1 | TOK |
| Subspace Anomaly | U | 1 | TOK |
| Syndicate Liquidators | U | 1 |  |
| Targ Wrangler | U | 1 |  |
| Temporal Anomaly | U | 1 | TOK |
| Test of Conviction | U | 1 |  |
| Test of Courage | U | 1 |  |
| Test of Identity | U | 1 |  |
| Test of Loyalty | U | 1 |  |
| The Doctor, EMH | U | 1 | ANY-COUNTER |
| The Duras Sisters | U | 1 | TOK |
| Type-2 Phaser | U | 1 |  |
| V'Ger, the Intruder | U | 1 |  |
| Armus, Skin of Evil | R | 1 |  |
| Collation Trimatrix | R | 1 |  |
| General Chang, Cold Warrior | R | 1 |  |
| Grand Nagus Zek | R | 1 |  |
| Primary Unimatrix Adjunct | R | 1 |  |
| Replicator | R | 1 | TOK |
| Stable Wormhole | R | 1 | TOK |
| Subcommander T'Pol | R | 1 |  |
| Wesley Crusher, Starfleet Cadet | R | 1 |  |
| Crystalline Entity | M | 1 |  |
| Jadzia Dax, Living Legacy | M | 1 |  |
| A Good Day to Die | C | 2 |  |
| Andromedan Automatons | C | 2 |  |
| Astute Terraformer | C | 2 | SNW ONCE-A-TURN |
| Boarding Action | C | 2 | BEAM TOK |
| Cantankerous Captain | C | 2 | POWER8 |
| Class-F Shuttlecraft | C | 2 | STATION |
| Cold-Blooded Crew | C | 2 |  |
| Common Goal | C | 2 |  |
| DOT-7 Repair Squad | C | 2 | ONCE-A-TURN |
| Federation Field Medic | C | 2 | ANY-COUNTER |
| Final Frontier | C | 2 | BEAM TOK |
| First Contact | C | 2 |  |
| Guidance Failure | C | 2 | STATION |
| I'm a Doctor, Not a . . . | C | 2 | TYPE-SET |
| Marooned | C | 2 | BEAM |
| Melkotian Adjudicator | C | 2 | DILEMMA |
| Oberth-Class Starship | C | 2 | STATION |
| Obsidian Order Operative | C | 2 | DILEMMA |
| Open Communications | C | 2 | BEAM |
| Pattern Buffer Preservation | C | 2 | BEAM |
| Pure Energy | C | 2 | BEAM |
| Reckless Impulse | C | 2 |  |
| Shuttle Ace | C | 2 | TAPPED |
| Suurok-Class Science Vessel | C | 2 | STATION |
| Suus Mahna Practitioner | C | 2 | SNW |
| Tactical Officer | C | 2 | POWER8 |
| Temporal Conqueror | C | 2 | TOK |
| 70,000 Light-Years from Home | U | 2 |  |
| Adversary by Algorithm | U | 2 | BEAM TOK |
| Amok Time | U | 2 | SAGA |
| Anthropological Mission | U | 2 | QUEST |
| Captain Michael Burnham | U | 2 | RESOLVED-TWICE |
| Cleveland Booker, Empath | U | 2 | CANT-BLOCK TOK |
| Cloak and Phaser | U | 2 | SNW TOK |
| Conquest Mission | U | 2 | QUEST TOK |
| Dal R'El, Augment Orphan | U | 2 |  |
| De-evolve | U | 2 | CANT-BLOCK TYPE-SET |
| Dominion Saboteur | U | 2 |  |
| Energize | U | 2 | BEAM |
| Enterprise NX-01 | U | 2 | STATION |
| Gabriel Lorca, Mirror Menace | U | 2 | DILEMMA |
| Ghost of Illyria | U | 2 | DELAYED-RETURN |
| Gray and Adira, Forever Joined | U | 2 |  |
| Hemmer, Fixing What Is Broken | U | 2 | TAPPED STATION |
| Hologram Janeway | U | 2 | POWER8 |
| Hoshi Sato, Exolinguist | U | 2 | FED LOOK-TAKE |
| In the Pale Moonlight | U | 2 | SAGA TOK |
| Isolinear Processor | U | 2 |  |
| Jake Sisko, War Correspondent | U | 2 | RESOLVED-TWICE TOK |
| Jett Reno, Problem Solver | U | 2 | TAPPED DILEMMA |
| Kirk, Commanding Officer | U | 2 | POWER8 |
| L'Rell, Fierce Visionary | U | 2 | TOK |
| La'An Noonien-Singh, Security | U | 2 | POWER8 CANT-BLOCK |
| Phlox, Unorthodox Doctor | U | 2 | ANY-COUNTER |
| Rutherford, Upgraded Ensign | U | 2 | LOOK-TAKE STATION |
| Saavik, Stoic Student | U | 2 | FED |
| Sarcophagus, Ship of the Dead | U | 2 | STATION |
| Seven of Nine | U | 2 | DILEMMA |
| Spock, Always Your Friend | U | 2 | LOOK-TAKE |
| Support Mission | U | 2 | QUEST |
| Sylvia Tilly, Joy of Discovery | U | 2 |  |
| Tell Me What You See | U | 2 | DILEMMA |
| Test of Priorities | U | 2 | DELAYED-RETURN |
| The Measure of a Man | U | 2 | SAGA TOK |
| The Naked Time | U | 2 | SAGA |
| The Traveler | U | 2 | SNW |
| There's Coffee in that Nebula | U | 2 | LOOK-TAKE BEAM |
| Those Old Scientists | U | 2 | SAGA |
| Transporter Malfunction | U | 2 | BEAM |
| U.S.S. Cerritos, Following Up | U | 2 | STATION TOK |
| U.S.S. Excelsior, Cutting Edge | U | 2 | STATION |
| U.S.S. Voyager, Lone Wanderer | U | 2 | STATION |
| Una Chin-Riley, Number One | U | 2 | POWER8 |
| Vengeance Mission | U | 2 | QUEST |
| Warp Speed | U | 3 | SNW TOK, lands enter untapped (batch 3) |
| Worf, Honor-Bound Warrior | U | 2 | POWER8 |
| Branching Timeline | R | 2 | SNW |
| Captain Jonathan Archer | R | 2 | FED TOK |
| D-7 Battle Cruiser | R | 2 | STATION TOK |
| Deep Space Scan | R | 2 | SNW |
| Dominion Infiltration | R | 2 |  |
| Dr. Leonard "Bones" McCoy | R | 2 |  |
| Elim Garak, Simple Tailor | R | 2 | DILEMMA |
| Encounter the Unknown | R | 2 |  |
| Federation Outpost | R | 2 | ENTERS-UNTAPPED |
| Federation Resupply | R | 2 | BEAM STATION |
| Gowron, Eager for Battle | R | 2 |  |
| Hit It! | R | 2 |  |
| Holo-Engineer | R | 2 | TOK |
| Live Long and Prosper | R | 2 |  |
| Lt. Cmdr. Geordi La Forge | R | 2 | STATION |
| MACO Strike Team | R | 2 | TOK |
| Ni'Var Science Academy | R | 2 | ENTERS-UNTAPPED |
| Pah-Wraith | R | 2 | SNW |
| Q Continuum | R | 2 | ENTERS-UNTAPPED |
| Quark's Bar | R | 2 | ENTERS-UNTAPPED |
| Romulan Bird-of-Prey | R | 2 | STATION |
| Salt Vampire | R | 2 |  |
| Sarek, Federation Ambassador | R | 2 | FED |
| Sulu, Space Swashbuckler | R | 2 | TAPPED STATION |
| Tasha Yar, Out of Time | R | 2 |  |
| The Destruction of Praxis | R | 2 | SNW TOK |
| The Unicomplex | R | 2 | ENTERS-UNTAPPED |
| Troubling Tribbles | R | 2 | CANT-BLOCK |
| U.S.S. Reliant, From Hell's Heart | R | 2 | STATION |
| Year of Hell | R | 2 | SAGA |
| Captain James T. Kirk | M | 2 | TOK |
| Captain Jean-Luc Picard | M | 2 | STATION TOK |
| Excalbian Gamemaster | M | 2 | SNW TOK |
| Gul Dukat, Colonial Prefect | M | 2 |  |
| Khan, Vengeful Heart | M | 2 |  |
| Seven of Nine, Unassimilated | M | 2 | SNW |
| Starfleet Academy | M | 2 | FED |
| U.S.S. Enterprise-A, Free Spirit | M | 2 | STATION TOK |
| Eject the Warp Core | C | 3 | KICKER |
| Pet Sehlat | C | 3 |  |
| Beckett Mariner, Impetuous Ensign | U | 3 | TAPPED ANY-COUNTER |
| Blastoneuron | U | 3 | LEAVES-GY |
| Brad Boimler, Eager Ensign | U | 3 | TAPPED ANY-COUNTER |
| Combadge | U | 3 |  |
| Data, New Life | U | 3 |  |
| Dr. McCoy, Surly Surgeon | U | 3 |  |
| Ensign Pavel Chekov | U | 3 | SNW, exile their top X + play a land (batch 3) |
| Erica Ortegas, Plucky Pilot | U | 3 | TAPPED, a land from hand + DELAYED-RETURN (batch 3) |
| Exploratory Mission | U | 3 | QUEST |
| Highly Illogical | U | 3 | KICKER |
| Hive Mind Coprocessor | U | 3 | GRANT-TRIGGER |
| Hologram Stalker | U | 3 | LEAVES-GY TOK |
| Rules of Acquisition | U | 3 | CONTROL |
| Species 8472 | U | 3 | SNW, blocked-by and targeted-by restrictions (batch 3) |
| Storied Veteran | U | 3 | CANT-BLOCK |
| Vash, Freelance Archaeologist | U | 3 | LEAVES-GY |
| Vulcan Mind-Meld | U | 3 |  |
| Zefram Cochrane, Warp Pioneer | U | 3 | STATION |
| Zero, Incorporeal Sensate | U | 3 |  |
| All Good Things... | R | 3 | SAGA |
| Antagonist Incorporator | R | 3 | GRANT-TRIGGER TOK |
| Blood Crypt | R | 3 | SHOCK |
| Brazen Bluff | R | 3 |  |
| Breeding Pool | R | 3 | SHOCK |
| Captain Benjamin Sisko | R | 3 | DILEMMA TOK |
| Captain Pike, Fullness of Time | R | 3 | SNW |
| Dathon, His Heart Open | R | 3 | ANY-COUNTER |
| Deanna Troi, Psychic Counsel | R | 3 |  |
| Engineered Explosives | R | 3 |  |
| Genesis Device | R | 3 | TOK |
| Godless Shrine | R | 3 | SHOCK |
| Gwyndala, the Progeny | R | 3 |  |
| H.M.S. Bounty, Unlikely Timeship | R | 3 | STATION |
| Hallowed Fountain | R | 3 | SHOCK |
| Kai Winn, Faithless Leader | R | 3 |  |
| Kira Nerys, Firebrand | R | 3 |  |
| Lwaxana Troi, Charming Guest | R | 3 | TRIGGER-TWICE |
| Miles O'Brien, Transporter Ops | R | 3 |  |
| Mirror Universe Intrusion | R | 3 | KICKER |
| Nog, Ambitious Cadet | R | 3 |  |
| Nyota Uhura, Communications | R | 3 |  |
| Overflow Submatrix | R | 3 |  |
| Overgrown Tomb | R | 3 | SHOCK |
| Peace in Our Time | R | 3 | ANY-COUNTER |
| Sacred Foundry | R | 3 | SHOCK |
| Sela, Tal Shiar Paradox | R | 3 | LEAVES-GY TOK |
| Steam Vents | R | 3 | SHOCK |
| Stomping Ground | R | 3 | SHOCK |
| Temple Garden | R | 3 | SHOCK |
| The Caretaker | R | 3 | DILEMMA |
| The City on the Edge of Forever | R | 3 | SAGA TOK |
| The Holodeck | R | 3 |  |
| The Inner Light | R | 3 | SAGA TOK |
| The Needs of the Many | R | 3 |  |
| The Wrath of Khan | R | 3 | SAGA CONTROL |
| U.S.S. Discovery, at Black Alert | R | 3 | STATION |
| U.S.S. Enterprise-D, Galaxy-Class | R | 3 | STATION |
| Watery Grave | R | 3 | SHOCK |
| Will Riker, First in the Field | R | 3 |  |
| Worf, Intelligence Contractor | R | 3 | CONTROL |
| Borg Cube | M | 3 | STATION ASSIMILATE |
| Captain Kathryn Janeway | M | 3 |  |
| Death Is Irrelevant | M | 3 | ASSIMILATE |
| Deep Space 9 | M | 3 | STATION |
| Dr. Beverly Crusher | M | 3 | TRIGGER-TWICE |
| Dr. Joseph M'Benga | M | 3 | ANY-COUNTER |
| Georgiou, Terran Emperor | M | 3 | TOK |
| Infinite Diversity | M | 3 | FED |
| Kobayashi Maru Scenario | M | 3 | TOK |
| Lieutenant Commander Data | M | 3 | ANY-COUNTER |
| Mr. Spock, Science Officer | M | 3 |  |
| Q, Stacking the Deck | M | 3 |  |
| Quark, Humble Proprietor | M | 3 | TOK |
| Rok-Tahk, Budding Scientist | M | 3 | TOK |
| Scotty, Miracle Worker | M | 3 | BEAM |
