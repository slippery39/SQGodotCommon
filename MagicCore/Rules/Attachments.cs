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

	/// <summary>Its host, if that is still the same object on the battlefield.</summary>
	public static Card? HostOf(GameState s, Card attachment) =>
		attachment.AttachedToId != 0
		&& s.IsOnBattlefield(attachment.AttachedToId)
		&& s.GetCard(attachment.AttachedToId) is var host
		&& IsOn(attachment, host)
			? host
			: null;

	/// <summary>Attaches it — only while both are on the battlefield.</summary>
	public static GameState Attach(GameState s, int attachmentId, int hostId) =>
		s.IsOnBattlefield(attachmentId) && s.IsOnBattlefield(hostId)
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
		aura.GetComponent<AuraComponent>() is { } enchant
		&& enchant.Enchant.IsLegal(s, aura.ControllerId, aura.Id, hostId, targeting: false);

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
	/// Then every type change (<see cref="BecomesType"/>: Puppet Crafting, Mogis) is put right: gone from
	/// what its source no longer changes, on what it does. ponytail: stamped, not a live layer-4 effect —
	/// every rule reads card.Is(Creature); one stamp per permanent; put right before anyone gets priority.
	/// </summary>
	public static GameState Check(GameState s, int playerId)
	{
		// AI-22: each pass only when the board has something it can act on (the board is already built for the check)
		if (Characteristics.AttachmentWork(s).Attachments)
			s = Detach(s, playerId);
		if (Characteristics.AttachmentWork(s).TypeChanges)
			s = Animations(s, playerId);
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

	/// <summary>An Aura with no legal host dies; an Equipment off a non-creature falls off.</summary>
	static GameState Detach(GameState s, int playerId)
	{
		foreach (var card in s.CardsIn(playerId, ZoneType.Battlefield).ToList())
		{
			var host = HostOf(s, card);
			if (card.HasComponent<AuraComponent>())
			{
				if (host is null || !CanEnchant(s, card, host.Id))
					s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
			}
			else if (
				card.AttachedToId != 0
				&& (host is null || !host.Is(CardType.Creature) || card.Is(CardType.Creature))
			)
				s = s.UpdateObject(
					card.Id,
					card with
					{
						AttachedToId = 0,
						AttachedToIncarnation = 0,
					}
				);
		}
		return s;
	}
}
