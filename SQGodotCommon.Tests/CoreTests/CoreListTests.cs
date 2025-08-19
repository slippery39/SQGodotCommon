namespace CoreNodes.Tests;

public class CoreListTests
{
	private CoreList<TestNode> CreateCoreListWith3Items()
	{
		var coreList = new CoreList<TestNode>
		{
			new TestNode() { NodeName = "Test Node 1", TestId = 1 },
			new TestNode() { NodeName = "Test Node 2", TestId = 2 },
			new TestNode() { NodeName = "Test Node 3 ", TestId = 3 },
		};

		return coreList;
	}

	[Test]
	public void CoreList_Indexer_Works()
	{
		var coreList = CreateCoreListWith3Items();

		Assert.IsNotNull(coreList[0]);
		Assert.IsNotNull(coreList[1]);
		Assert.IsNotNull(coreList[2]);

		//Getter works
		Assert.That(coreList[0].TestId, Is.EqualTo(1));
		Assert.That(coreList[1].TestId, Is.EqualTo(2));
		Assert.That(coreList[2].TestId, Is.EqualTo(3));

		//Setter works
		var testNode = new TestNode() { NodeName = "Test Node 4", TestId = 4 };
		coreList[0] = testNode;

		Assert.That(coreList[0], Is.EqualTo(testNode));
	}

	[Test]
	public void CoreList_IndexOf_Works()
	{
		var coreList = CreateCoreListWith3Items();
		var item = coreList[0];

		Assert.That(coreList.IndexOf(item), Is.EqualTo(0));
	}

	[Test]
	public void CoreList_CopyTo_Works()
	{
		var coreList = CreateCoreListWith3Items();
		var arr = new TestNode[3];

		coreList.CopyTo(arr, 0);

		Assert.That(arr.Length, Is.EqualTo(3));
		Assert.That(arr[0], Is.EqualTo(coreList[0]));
		Assert.That(arr[1], Is.EqualTo(coreList[1]));
		Assert.That(arr[2], Is.EqualTo(coreList[2]));
	}

	[Test]
	public void CoreList_Count_Works()
	{
		var coreList = CreateCoreListWith3Items();
		Assert.That(coreList.Count, Is.EqualTo(3));
	}

	[Test]
	public void CoreList_Remove_Works()
	{
		var coreList = CreateCoreListWith3Items();
		var node = coreList[1];
		coreList.Remove(node);

		Assert.That(coreList.Count, Is.EqualTo(2));
		Assert.That(coreList.Contains(node), Is.EqualTo(false));
	}

	[Test]
	public void CoreList_Enumerator_Works()
	{
		var coreList = CreateCoreListWith3Items();

		Console.WriteLine("Testing enumerator....");

		foreach (var item in coreList)
		{
			Console.WriteLine(item.Dump());
		}
	}

	private class TestNode : CoreNode
	{
		public int TestId { get; set; }
	}
}
