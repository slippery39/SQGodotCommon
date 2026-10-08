# Reality Fracture (FRA) coverage — MagicCore

Status, not rules: what is scripted, what waits and why. The rules live in `MagicCore/CLAUDE.md`.
Count scripted cards from `FraCards.cs` (`["Name"] =` keys) — never from memory; this file once ran one high.

Keywords by card count: Surveil 43, **Empower Jace 35** (creates/loads a Jace *planeswalker token*),
**Prepared 24** (21 cards use the two-face `prepare` layout), Flying 28. Modal "choose one", flashback,
cycling/landcycling, ward, equipment and +1/+1 counters are common. Implemented so far: 150 cards scripted
in `FraCards` (every one played by `RandomGameTests.RandomPlay_EveryScriptedCard`), including 15 Empower Jace cards, the 15 dual lands, 14 modal cards and 11 sacrifice /
dies / exile-until cards, 25 step-trigger / per-turn / prowess / surveil-trigger cards, the 6 flashback cards, 19 prepare cards (Pyre Rhymer and Heartwood Crafter wait: a mana-tap
trigger, restricted mana), 12 landcycling / graveyard-ability cards, and 7 graveyard-return / hexproof /
indestructible / exhaust cards. **Two real planeswalkers are
in** (The Theorist, Ajani Unrelenting); **the other 6 are not**: each needs an emblem, an "until your next turn" effect, impulse draw or a −X cost first.
Coverage plan, by cards unblocked: 7a lands ✓ · 7b-1 modal ✓ · 7b-2 sacrifice, dies triggers, exile-until-leaves ✓ · 7c-1 continuous effects ✓ · 7c-2 step triggers,
per-turn tracking, prowess ✓ · 7d-1 flashback ✓ · 7d-2 prepare ✓ · 7d-3a landcycling, graveyard abilities ✓ · 7d-3b graveyard returns, hexproof, indestructible ✓. The Bo1 hand smoother is Arena's published description; its weighting and whether
limited Bo1 uses it are unconfirmed — hence `handSmoothing`.
