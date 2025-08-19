namespace CoreNodes.Tests;

public static class CoreNodesTestFunctions
{
	/// <summary>
	/// Helper test method to test a node and its clone for invalid references.
	/// </summary>
	/// <param name="originalNode"></param>
	/// <param name="clonedNode"></param>
	public static void TestFullClonedNode(CoreNode originalNode, CoreNode clonedNode)
	{
		if (!originalNode.GetChildren().Any())
		{
			return;
		}

		var originalChildren = originalNode.GetChildren().ToList();
		var clonedChildren = clonedNode.GetChildren().ToList();

		Assert.That(
			originalNode.NodeName,
			Is.EqualTo(clonedNode.NodeName),
			"Nodes do not have the same NodeName when cloned... something is wrong"
		);

		Assert.That(
			originalChildren.Count,
			Is.EqualTo(clonedChildren.Count),
			$"The children counts do not match for the original node {originalNode.NodeName} {originalNode.ToString()} and the cloned node {clonedNode.NodeName} {clonedNode.ToString()}"
		);

		for (var i = 0; i < clonedChildren.Count; i++)
		{
			Assert.That(
				clonedChildren[i],
				Is.Not.EqualTo(originalChildren[i]),
				$"{clonedChildren[i].NodeName} is the same in the cloned and original version. They should not be the same reference"
			);
		}

		for (var i = 0; i < originalChildren.Count; i++)
		{
			TestFullClonedNode(originalChildren[i], clonedChildren[i]);
		}
	}
}
