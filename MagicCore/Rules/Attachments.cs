using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Rules;

/// <summary>
/// Auras and Equipment (CR 301.5, 303.4) — ONE attachment mechanism, as in MtgCore, differing in
/// two ways: an Aura attaches as it enters and dies without a legal host; Equipment attaches by its
/// equip ability and merely falls off. What they grant is a live static on the attachment
/// (scope Is.AttachedToSource) — never stamped onto the host, which is how MtgCore's equipment
/// boosts went stale. An attachment remembers its host's incarnation too: a host that leaves and
/// comes back is a new object, and nothing is attached to it (CR 400.7).
/// </summary>
public static class Attachments
{
	/// <summary>THE "is this attached to that permanent" — same id and same object.</summary>
	public static bool IsOn(Card attachment, Card host) =>
		attachment.AttachedToId == host.Id && attachment.AttachedToIncarnation == host.Incarnation;

	/// <summary>
	/// Its host, if that is still the same object on the battlefield — or, <paramref name="orGraveyard"/>, a card in a
	/// graveyard (Animate Dead, for the rules that read it: the state check, Ref.EnchantedCreature). A front end asks
	/// for permanents only: an Aura on a graveyard card is drawn on the battlefield, not under a card it can't show.
	/// </summary>
	public static Card? HostOf(GameState s, Card attachment, bool orGraveyard = false) =>
		attachment.AttachedToId != 0
		&& (
			orGraveyard
				? CanHost(s, attachment.AttachedToId)
				: s.IsOnBattlefield(attachment.AttachedToId)
		)
		&& s.GetCard(attachment.AttachedToId) is var host
		&& IsOn(attachment, host)
			? host
			: null;

	/// <summary>
	/// A permanent, or a card in its owner's graveyard — Animate Dead: "Enchant creature card in a graveyard" (CR 303.4a:
	/// an Aura enchants what its enchant says, wherever). Same id is not same object, so a card attached to in a
	/// graveyard is the host only while it stays there.
	/// </summary>
	static bool CanHost(GameState s, int id) =>
		s.IsOnBattlefield(id)
		|| s.HasObject(id)
			&& s.GetObject(id) is Card c
			&& s.IsIn(id, c.OwnerId, ZoneType.Graveyard);

	/// <summary>The enchant it has now — changed for good (Animate Dead, Necromancy: UntilEndOfTurn.Enchant), else its own; null: no Aura.</summary>
	public static Targeting.TargetSpec? EnchantOf(Card card) =>
		card.GetComponents<UntilEndOfTurn>().LastOrDefault(u => u.Enchant is not null)?.Enchant
		?? card.GetComponent<AuraComponent>()?.Enchant;

	/// <summary>Attaches it — only while it is on the battlefield and its host is (or is a graveyard card: Animate Dead),
	/// and never to a permanent with protection from it (CR 701.3b: the attach does nothing; it stays where it was).</summary>
	public static GameState Attach(GameState s, int attachmentId, int hostId) =>
		s.IsOnBattlefield(attachmentId)
		&& CanHost(s, hostId)
		&& !Protection.Stops(s, s.GetCard(hostId), s.GetCard(attachmentId))
			? s.UpdateObject(
				attachmentId,
				s.GetCard(attachmentId) with
				{
					AttachedToId = hostId,
					AttachedToIncarnation = s.GetCard(hostId).Incarnation,
				}
			)
			: s;

	/// <summary>What is attached to this permanent — the host as it is, or as it last was.</summary>
	public static IEnumerable<Card> On(GameState s, Card host) =>
		new[] { s.Player1Id(), s.Player2Id() }
			.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
			.Where(c => IsOn(c, host));

	/// <summary>Could this Aura be on that permanent? The enchant restriction — not targeting, so hexproof doesn't matter.</summary>
	public static bool CanEnchant(GameState s, Card aura, int hostId) =>
		EnchantOf(aura) is { } enchant
		&& enchant.IsLegal(s, aura.ControllerId, aura.Id, hostId, targeting: false)
		&& !(s.GetObject(hostId) is Card host && Protection.Stops(s, host, aura)); // CR 702.16c

	/// <summary>
	/// An Aura put onto the battlefield without being cast (Return to the Light Realms): its
	/// controller chooses what it enchants (CR 303.4f). With nothing legal, the state check takes it.
	/// The question is queued, and whether there is anything to ask is judged when it comes up
	/// (<see cref="AskHostAction"/>) — an Aura returned by a COST (Malfunctioning Holodeck sacrificed for
	/// Eject the Warp Core's kicker) meets the state check first and is gone by then.
	/// ponytail: it should attach as it returns and never meet that check; it goes to the graveyard instead
	/// (DesignNotes, MagicCore known ceilings).
	/// </summary>
	public static GameState ChooseHostAsItEnters(GameState s, int auraId) =>
		s.SpawnAction(new AskHostAction(auraId));

	/// <summary>The host question of <see cref="ChooseHostAsItEnters"/>, asked only while there is a host to choose.</summary>
	public record AskHostAction(int AuraId) : GameAction
	{
		public override ActionResult Execute(GameState s)
		{
			var choice = new ChooseHostChoice(s.GetCard(AuraId).ControllerId, AuraId);
			return new(
				choice
					.GetOptions(
						s,
						System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
					)
					.IsEmpty
					? s
					: s.SpawnAction(
						new PipelineAction { Steps = [choice, new AttachChosenHostAction(AuraId)] }
					)
			);
		}
	}

	/// <summary>
	/// State-based (CR 704.5m/n): an Aura attached to nothing legal goes to its owner's graveyard;
	/// an Equipment attached to something that is no longer a creature on the battlefield falls off.
	/// Every type change (<see cref="BecomesType"/>: Puppet Crafting, Mogis) is put right FIRST: gone from
	/// what its source no longer changes, on what it does — so Lion Sash, attached by its reconfigure while still a
	/// creature, is no creature by the time the fall-off is judged (CR 702.151b, 301.5c). ponytail: stamped, not a live
	/// layer-4 effect — every rule reads card.Is(Creature); one stamp per permanent; put right before anyone gets
	/// priority. A reconfigure Equipment that loses its abilities while attached is a creature again and falls off (the
	/// rulings keep it on, not a creature, until unattached) — a stamp at the attach if a card needs it.
	/// </summary>
	public static GameState Check(GameState s, int playerId)
	{
		// AI-22: each pass only when the board has something it can act on (the board is already built for the check)
		if (Characteristics.AttachmentWork(s).TypeChanges)
			s = Animations(s, playerId);
		if (Characteristics.AttachmentWork(s).Attachments)
			s = Detach(s, playerId);
		return s;
	}

	/// <summary>
	/// The entering permanent's type change put on as it enters (MagicState.EnterBattlefield), so Mogis never
	/// enters a creature — the rest waits for the state-based check.
	/// </summary>
	public static GameState Retype(GameState s, int cardId)
	{
		foreach (var player in (int[])[s.Player1Id(), s.Player2Id()])
		foreach (var source in s.CardsIn(player, ZoneType.Battlefield).ToList())
		foreach (var becomes in s.AbilitiesOf<BecomesType>(source))
			if (
				s.GetCard(cardId) is var card
				&& !card.HasComponent<Animated>()
				&& Changes(s, source, becomes, card)
			)
				s = s.UpdateObject(cardId, card.AnimatedBy(source, becomes));
		return s;
	}

	static bool Changes(GameState s, Card source, BecomesType becomes, Card host) =>
		becomes.Scope.Matches(s, host, new Filters.FilterContext(source.ControllerId, source.Id))
		&& (becomes.If is null || becomes.If.Holds(s, Conditions.Of(source)));

	static GameState Animations(GameState s, int playerId)
	{
		foreach (var host in s.CardsIn(playerId, ZoneType.Battlefield).ToList())
			if (
				host.GetComponent<Animated>() is { SourceId: not 0 } a // 0: assimilated — until it leaves
				&& !(
					s.IsOnBattlefield(a.SourceId)
					&& s.GetCard(a.SourceId) is var source
					&& source.Incarnation == a.SourceIncarnation
					&& s.AbilitiesOf<BecomesType>(source).Any(b => Changes(s, source, b, host))
				)
			)
				s = s.UpdateObject(host.Id, host.WithoutAnimation());
		foreach (var source in s.CardsIn(playerId, ZoneType.Battlefield).ToList())
		foreach (var becomes in s.AbilitiesOf<BecomesType>(source))
		foreach (
			var id in new[] { s.Player1Id(), s.Player2Id() }
				.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
				.Select(c => c.Id)
				.ToList()
		)
			if (
				s.GetCard(id) is var host
				&& !host.HasComponent<Animated>()
				&& Changes(s, source, becomes, host)
			)
				s = s.UpdateObject(id, host.AnimatedBy(source, becomes));
		return s;
	}

	/// <summary>
	/// An Aura with no legal host dies; an Equipment off a non-creature, one that is a creature itself (CR 301.5c), or one
	/// its host has protection from (CR 702.16d), falls off.
	/// </summary>
	static GameState Detach(GameState s, int playerId)
	{
		foreach (var card in s.CardsIn(playerId, ZoneType.Battlefield).ToList())
		{
			var host = HostOf(s, card, orGraveyard: true);
			if (EnchantOf(card) is not null)
			{
				if (host is null || !CanEnchant(s, card, host.Id))
					s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
			}
			else if (
				card.AttachedToId != 0
				&& (
					host is null
					|| !host.Is(CardType.Creature)
					|| card.Is(CardType.Creature)
					|| Protection.Stops(s, host, card)
				)
			)
				s = Unattach(s, card.Id);
		}
		return s;
	}

	/// <summary>
	/// THE unattach: attached to nothing, still on the battlefield — an Equipment falling off, and Lion Sash's reconfigure
	/// "or unattach from a creature" (the Unattach effect).
	/// </summary>
	public static GameState Unattach(GameState s, int attachmentId) =>
		s.UpdateObject(
			attachmentId,
			s.GetCard(attachmentId) with
			{
				AttachedToId = 0,
				AttachedToIncarnation = 0,
			}
		);
}
