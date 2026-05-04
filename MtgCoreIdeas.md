Things to implement next

Lands
-Opens up Primeval Titan Decks, easier to balance cards as you dont get mana automatically every turn. Special lands that allow for extra effects.
Flashback
-Used for Delver and other things
-Cards like Lingering Souls, Unburial Rites, Think Twice
X Spells
-Hearthstone does this by using all your current mana for the spell. It can work the same way here.
Hexproof / Shroud
-How do we get the targeting system to properly handle thes effects? Note that it should only apply for effects where the person selects a target (i.e. Lightning Bolt), and not effects where the targets are selected by the game itself (i.e. pyroclasm wrath of god)
Extra Turns
-I assume we would use some sort of flag in MtgGame which would either have an ExtraTurns list, or an override of who will get the next turn. In MTG you can stack extra turns, so it makes more sense to have an extra turns list.
-Some cards do the opposite, where it skips the opponents next turn. This has the same overall outcome in most cases, but would technically be slightly different under the hood. For simplicity we can just ignore this, and only implement extra turns.

Other Static Keywords
-LifeLink
-Trample
Value Over Time Simulator Evaluator
-Right now the simulator only evaluates the immediate board state.
-We need a way to allow the simulator to dynamically evaluate cards by understanding a "value over time" value of a card.
-This is mainly for cards which have abilities that can be used or triggered, but might not be able to be used right away
-Dark Confidant could be an example of a card
-For example ideally, instead of the AI just looking at Dark Confidant as a 2/1 creature, they could view it as a 2/1 creature which would potentially draw 3 cards over the next 3 turns, and lose X life over the next 3 turns
-Then the AI could use that score in its evaluation heuristic and use it to determine, 1. If they should play the card (if they have it) and 2. If they should destroy or attack the card if the opponent has it.
-This is in theory, but the question is, how do we implement this?
-Much harder to implement than to think about in theory. Can we do this in a nice clean generic way that would work for most cards?
-Is this something that could be used for normal damage as well? For example a 5/5 creature represents 15 potential damage to the face over 3 turns, is this something we could use to evaluate any creature as well.
-Ideas
-Sanxbox simulation
-We drop the creature (or permanent) into a game with nothing else in it, simulate the value of its triggered ability by ending the turn X times, and then generating a score on the resuling board state.
-Could actually work, and since there is no cascading action effect it actually might not be too computationally expensive.
-We would just need to have an already empty board state created so that we aren't making a brand new state every single time.
-Does not work for abilities that effect the baord. What if we have a creature which says, when this attacks deal 1 damage to every creature an opponent controls
-To offset that, we could have a board full of creatures for the opponent, the creature which deals damage to all creatures when it attacks would effect those creatures
-We then do a diff of the first board state from the second board state, and that is the score, not the raw score at the end.
-Since this is theoretical value, we don't need to calculate this every time. We could even pre-calculate it, or even just lazy calculate it (calculate it once when the specific card is supposed to be played)
-But this would not account for cards that get modified at runtime, like what if we have an enchantment or equipment which allows a creature to draw you a card when it attacks.
-Not sure how to handle this.. We could calculate the theoretical value again, by running a simulation with a dummy creature that has the equipment or enchantment attached and score the value after X runs.
-Anyways, this should allow us to dynamically "Score" cards based off of theoretical value, rather than just raw combat stats.
-We could also combine this with a manually populated "DANGER" score for us to manually flag which cards are more dangerous than others if in our testing the above has edge cases that don't work.

"Scripted" Keyworded Abilities
-These abilities can be scripted in via our current trigger / activated ability system, but we just display them as a Keyworded
-Need some way to do this.
-Stuff like Cycling
-just gives cards an activated ability of Discard this card : Draw a card
-can have additional effects (like deal 2 damage when you cycle this card)
-Cascade
-a triggered ability that fires when the card is cast
-plays the next card in your deck that has a mana cost of less than the source cards mana cost.
-Storm could go in here as well, but currently we did not implement it this way.

Note that this means we also need to introduce the concept of "where' triggered abilities occur. I.e. right now we are only checking them in the battlefield, but they could occur from the hand, or even the graveyard as well.

For us the more extra mechanics we make, the more confident I will be in making a different type of game later on.

Lands -> Permanent Cards, do not use the stack (so they don't need to resolve like spells or creatures) once played they are automaitcally put into play. No extra steps
-> Has a natural option reducing capability for the simulator (lands cannot be played after a certain point, so it reduces the amount of options at once)

MTG Simulator - 10 draws happened out of 100,000 games. But we didn't flag those games. Why are they drawing? Is there some combination of cards that can cause a draw to happen? (Maybe something with blood artist)
