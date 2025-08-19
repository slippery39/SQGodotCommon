using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreNodes;

public class RootNode : CoreNode
{
	public override string NodeName { get; set; } = "RootNode";
	private HashSet<int> AssignedNodeIds { get; set; } = new HashSet<int>();
	private int nextNodeId = 1;
	private int nextComponentId = 1;

	protected CoreNode SystemsContainer { get; set; }

	public RootNode()
	{
		Init();
	}

	private void Init()
	{
		//Rootnodes should always be their own root
		Root = this;
		SystemsContainer = new CoreNode();
		SystemsContainer.NodeName = "Systems";
		AddNode(SystemsContainer);
		AddSystem<EventsSystem>();
	}

	/// <summary>
	/// Registers an node inside the game. Should not be called by non root objects
	/// </summary>
	/// <param name="node"></param>
	public void RegisterNode(CoreNode node)
	{
		//Adding this here because we accidently did this and then spent 3 hours trying to figure out why our tests were failing.
		if (node == this)
		{
			Console.WriteLine(
				"You should not register a root node with itself.... there is a problem in your code. Check it."
			);
			return;
		}

		//Make sure we don't have any overlapping ids.
		//If it hasn't been assigned an id, it has been assigned an id but likely from somewhere else, or it has an id that already exists
		//in the state, then we assign it a new id.
		if (node.NodeId < 1 || node.NodeId > nextNodeId || AssignedNodeIds.Contains(node.NodeId))
		{
			node.NodeId = nextNodeId++;
			AssignedNodeIds.Add(node.NodeId);
		}

		node.Root = this;

		foreach (var c in node.Components)
		{
			if (c.ComponentId < -1)
			{
				c.ComponentId = nextComponentId++;
			}
		}

		if (node.GetChildren() == null)
			return;

		//Recursive function to add the children.
		foreach (var e in node.GetChildren())
		{
			RegisterNode(e);
		}
	}

	public void AddSystem(CoreSystem system)
	{
		SystemsContainer.AddNode(system);
	}

	public void AddSystem<T>()
		where T : CoreSystem, new()
	{
		var newSystem = new T();
		SystemsContainer.AddNode(newSystem);
	}

	public T GetSystem<T>()
	{
		var system = SystemsContainer.GetChildren().OfType<T>().FirstOrDefault();
		if (object.Equals(system, default(T)))
		{
			Console.WriteLine($"Could not find system {typeof(T).Name}");
		}

		return system;
	}

	public virtual void AddEvent(CoreEvent evt)
	{
		GetSystem<EventsSystem>().AddEvent(evt);
	}

	public override RootNode DeepClone()
	{
		var newState = MemberwiseClone() as RootNode;
		newState.ClearChildren();
		newState.Components = new List<CoreNodeComponent>();
		newState.AssignedNodeIds = new HashSet<int>(); //reset the assigned node ids since we are going to readd the entire tree anyways.
		newState.Root = newState;

		GetChildren()
			.ToList()
			.ForEach(x =>
			{
				var clone = x.DeepClone();
				newState.AddNode(clone);
			});

		return newState;
	}
}
