using Project;

namespace Cards;

public partial class CardsExampleScene : GameScene
{
	public new CardsExampleWorld World => GetNode<CardsExampleWorld>("World");
	public new CardsExampleUI UI => GetNode<CardsExampleUI>("UI");
}
