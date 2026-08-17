using System.Collections.Immutable;
using System.Reflection.Emit;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// White Cards from Core Set Cube
/// https://cubecobra.com/cube/list/magiccoreset20xx
/// </summary>
public static class CoresetCubeWhite
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Anointer of Champions
			CardFactory
				.Creature("Anointer of Champions", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Cleric")
				.WithActivatedAbility(
					"",
					manaCost: 0,
					effect: eb => eb.WithBoost(1, 1).WithTarget(Single().YourCreatures())
				)
				.Build(),
			// Gideons Lawkeeper
			//TODO - Needs a "Creature can't attack until your next turn ability to mimic "tapping""
			CardFactory
				.Creature("Gideons Lawkeeper", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				.WithActivatedAbility(
					"",
					manaCost: 0,
					effect: eb => eb.WithBoost(1, 1).WithTarget(Single().YourCreatures())
				)
				.Build(),
			//Kytehon, Hero of Akros
			//Need indestructable, and flipping trigger.
			CardFactory
				.Creature("Kytheon, Hero of Akros", manaCost: 1, power: 2, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				.WithActivatedAbility(
					"",
					manaCost: 3,
					effect: eb =>
						eb.WithGrantKeyword(flying: true).WithTarget(TargetingStrategy.NoTarget())
				)
				.Build(),
			//Soul Warden
			//Need trigger condition for when a creature enters the battlefield.
			CardFactory
				.Creature("Soul Warden", manaCost: 1, power: 2, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Cleric")
				// .WithTriggeredAbility(
				//     "",
				//     TriggerConditions.

				// )
				.Build(),
			///Speaker of the Heavens
			/// Need conditional activated ability (create 4/4 angel, only if you have 7 more life than your starting life total)
			/// something to simulate vigilance?
			CardFactory
				.Creature("Speaker of the Heavens", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Cleric")
				.WithLifelink()
				// .WithTriggeredAbility(
				//     "",
				//     TriggerConditions.

				// )
				.Build(),
			//Ajani's Pridemate
			CardFactory
				.Creature("Ajani's Pridemate", manaCost: 1, power: 2, toughness: 2)
				.WithSubtype("Cat")
				.WithSubtype("Soldier")
				// .WithTriggeredAbility(
				//     "",
				//     TriggerConditions.OnGainLife(),
				//     eb => eb.WithAction(

				// )
				.Build(),
			//Fencing Ace
			CardFactory
				.Creature("Fencing Ace", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				.WithDoubleStrike()
				.Build(),
			//Grand Abolisher -doens't do anything right now, but maybe could be a way to stop traps
			CardFactory
				.Creature("Grand Abolisher", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				//TODO - Need to implement "During your turn, your opponents can't cast spells or activate abilities of artifacts, creatures, or enchantments."
				.Build(),
			//Imposing Sovereign
			//Creatures your opponents control enter the battlefield tapped, how can we simulate this in our game?
			CardFactory
				.Creature("Imposing Sovereign", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				//TODO - Need to implement "Creatures your opponents control enter the battlefield tapped."
				.Build(),
			//Knight of Glory
			//TODO - need to implement first strike
			//TODO - need to implement exalted some how since its not exactly the same (first creature you attack with every turn gets +1/+1?)
			CardFactory
				.Creature("Knight of Glory", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Human")
				.WithSubtype("Knight")
				// .WithFirstStrike()
				// .WithExalted()
				.Build(),
			//Knight of the White Orchid
			//TODO - need to implement "If an opponent controls more lands than you, you may put a land card from your hand onto the battlefield."
			CardFactory
				.Creature("Knight of the White Orchid", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Human")
				.WithSubtype("Knight")
				//Ability to get an extra land if opponent has less lands than you.
				.Build(),
			//Seasoned Hallowblade
			//  3/1 - Activated Ability - Discard a card, get indestructable
			CardFactory
				.Creature("Seasoned Hallowblade", manaCost: 2, power: 3, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Warrior")
				//TODO - Discard a card to get indestructible for a turn cycle.
				.WithActivatedAbility(
					"",
					manaCost: 0,
					//TODO additional cost - discard
					//TODO - indestructible
					effect: eb => eb.WithGrantKeyword().WithTarget(TargetingStrategy.NoTarget())
				)
				.Build(),
			//Serra Avenger
			// 2 mana 3/3 flyer - can't be cast on the first, second, or third turns of the game
			//TODO - cast restriction based on turn count
			CardFactory
				.Creature("Serra Avenger", manaCost: 2, power: 3, toughness: 3)
				.WithSubtype("Angel")
				.WithFlying()
				.Build(),
			//Stormfront Pegasus
			//2 mana 2/1 flyer
			CardFactory
				.Creature("Stormfront Pegasus", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype("Pegasus")
				.WithFlying()
				.Build(),
			//Topan Freeblade
			//TODO - Renown, when it deals damage to a player, put a +1/+1 counter, can only happen once.
			//Has vigilance, but we don't really have an equivalent yet? Maybe taunt overlaps with vigilance? Maybe we need some new combat keywords?
			CardFactory
				.Creature("Topan Freeblade", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				.Build(),
			//Angel of Vitality
			// 3 mana, 2/2 flyer, whenever you gain life, gain +1 life more.\
			// gets +2/+2 as long as you have 25 or more life.
			//TODO - conditional effects (+2/+2 as long as you have 25 or more life)
			//TODO - replacement/modifier effects (gain +1 life), need some way of modifying or tapping into existing systems. We should discuss
			CardFactory
				.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype("Angel")
				.WithFlying()
				.Build(),
			//Attended Knight
			//3 mana 2/2 first strike, etb create a 1/1 soldier
			CardFactory
				.Creature("Attended Knight", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype("Human")
				.WithSubtype("Knight")
				.WithDoubleStrike() //replace with first strike when implemented.
				// .WithEtbTrigger(
				// //how do we make etb triggers? Maybe a helper method to easily create etb tokens? or tokens in general?
				// )
				.Build(),
			//Crusader of Odric
			//Power and toughness are equal to the number of creatures you control
			//TODO - power and toughness are only integers right now, this card uses the */* templating.
			//Can we use that too?
			CardFactory
				.Creature("Crusader of Odric", manaCost: 3, power: 0, toughness: 0)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				.Build(),
			//Gideon's Avenger
			//3 mana 2/2, whenever an opponent's creature becomes tapped put a +1/+1 counter on it.
			//TODO - We don't have the idea of being tapped. Maybe we could introduce the idea of being exhausted? Exhausted happens when a creature attacks, or certain activated abilities, or "tappers" as they are called in mtg can also exhaust creatures for a turn.
			//
			CardFactory
				.Creature("Gideon's Avenger", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				.Build(),
			//Hanged Executioner
			// 3 mana 1/1 flyer, etb create a 1/1 flyer, 4 mana activated ability, exile it, exile target creature
			CardFactory
				.Creature("Hanged Executioner", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype("Spirit")
				.WithFlying()
				//TODO - ETB - create a 1/1 spirit flyer
				//TODO - exile this, exile target creature
				//.WithActivatedAbility()
				.Build(),
			//Interpid Hero
			//3 mana 1/1 - activated ability - destory a creature with power 4 or greater.
			CardFactory
				.Creature("Intrepid Hero", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype("Human")
				.WithSubtype("Soldier")
				//TODO - 0 cc, exahust activated ability, destroy a creature with power 4 or greater
				//.WithActivatedAbility()
				.Build(),
			//Master of Diversion
			//3 mana, 2/2, on attack - exhaust (our equivalent to mtg's tap) a creature the opponent controls
			CardFactory
				.Creature("Master of Diversion", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype("Human")
				.WithSubtype("Scout")
				//TODO - on attack - exhaust creature opponent controls
				.Build(),
			//Pegasus Courser
			//3 mana, flying 1/3, on attack - another creature you control gets flying for a turn cycle.
			CardFactory
				.Creature("Pegasus Courser", manaCost: 3, power: 1, toughness: 3)
				.WithSubtype("Pegasus")
				.WithFlying()
				//TODO - on attack - another creature you contorl gets flying
				.Build(),
		];
}
