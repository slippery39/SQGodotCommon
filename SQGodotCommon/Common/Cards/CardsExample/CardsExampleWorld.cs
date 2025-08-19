using System.Collections.Generic;
using System.Linq;
using Common.Core;
using Project;

namespace Cards;

public partial class CardsExampleWorld : GameWorld
{
	public CharacterUI GetHero() =>
		GetNode<Node2D>("Heroes").GetChildrenOfType<CharacterUI>().FirstOrDefault();

	public IEnumerable<CharacterUI> GetEnemies() =>
		GetNode<Node2D>("Enemies").GetChildren().OfType<CharacterUI>();
}
