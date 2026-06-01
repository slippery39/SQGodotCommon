namespace CoreNodes.Tests;

public class CoreNodesTests
{
	[Test]
	public void TestSystemsIsBeingRegistered()
	{
		var rootNode = new RootNode();

		var systemsContainer = rootNode.GetNodeByName("Systems");

		Assert.That(systemsContainer, Is.Not.Null);
		Assert.That(systemsContainer.Root, Is.EqualTo(rootNode));
	}

	[Test]
	public void CoreNode_ComponentsProperlyRegisters()
	{
		var coreNode = new CoreNode();
		coreNode.AddComponent<TestCoreComponent>();

		// Assert that one component was added
		Assert.That(
			coreNode.Components.Count,
			Is.EqualTo(1),
			$"Expected 1 component to be registered, but found {coreNode.Components.Count} components."
		);

		// Assert that the component is of type TestCoreComponent
		Assert.That(
			coreNode.Components[0] is TestCoreComponent,
			Is.EqualTo(true),
			$"Expected the component to be of type TestCoreComponent, but it was {coreNode.Components[0].GetType().Name}."
		);

		// Assert that the component is correctly associated with the coreNode
		Assert.That(
			coreNode.Components[0].Node,
			Is.EqualTo(coreNode),
			"Expected the component to be registered to the original CoreNode, but it was registered to a different node."
		);

		var clonedNode = coreNode.DeepClone();

		// Assert that the cloned node is not the same instance as the original node
		Assert.That(
			clonedNode,
			Is.Not.EqualTo(coreNode),
			"Expected the cloned node to be a different instance from the original node, but they are equal."
		);

		// Assert that the component lists are not the same instances in the original and cloned nodes
		Assert.That(
			clonedNode.Components,
			Is.Not.EqualTo(coreNode.Components),
			"Expected the component list in the cloned node to be different from the original node's list, but they are equal."
		);

		// Assert that the cloned component is not the same instance as the original component
		Assert.That(
			clonedNode.Components[0],
			Is.Not.EqualTo(coreNode.Components[0]),
			"Expected the component in the cloned node to be a different instance than in the original node, but they are equal."
		);

		// Assert that the cloned component is correctly associated with the cloned node
		Assert.That(
			clonedNode.Components[0].Node,
			Is.EqualTo(clonedNode),
			"Expected the component in the cloned node to be registered to the cloned node, but it was registered to a different node."
		);
	}

	private class TestCoreComponent : CoreNodeComponent
	{
		public bool IsTest { get; set; } = true;

		public TestCoreComponent() { }
	}
}
