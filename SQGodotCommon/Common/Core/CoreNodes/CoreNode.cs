using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeepCloneable;
using Newtonsoft.Json;

namespace CoreNodes;

public class CoreNode : IDeepCloneable<CoreNode>
{
	/// <summary>
	///  Grabs the NodeName. For ease of use purposes.
	/// </summary>
	public string Name => NodeName;
	public virtual string NodeName { get; set; }
	public int NodeId { get; set; }
	protected List<CoreNode> Children { get; set; } = new List<CoreNode>();
	public CoreNode Parent { get; set; }
	public RootNode Root { get; set; }
	public List<CoreNodeComponent> Components { get; protected set; } =
		new List<CoreNodeComponent>();

	/// <summary>
	/// Set tags on a node for filtering purposes.
	/// </summary>
	public List<string> Tags { get; set; } = new List<string>();

	/// <summary>
	/// Gets the first node of type T found inside the node's children.
	/// Option to throw an error if no node is found.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
	public T GetNode<T>(bool throwErrorIfNotFound = false)
		where T : CoreNode
	{
		var component = Children.OfType<T>().FirstOrDefault();
		if (component == null && throwErrorIfNotFound)
		{
			throw new InvalidOperationException(
				$"Children node of type {typeof(T).Name} was not found on node {NodeName}({NodeId}) {System.Environment.NewLine} {JsonConvert.SerializeObject(this, Formatting.Indented)}"
			);
		}
		return component;
	}

	public T GetNodeByName<T>(string nodeName)
		where T : CoreNode
	{
		return Children.OfType<T>().First(x => x.NodeName == nodeName);
	}

	public CoreNode GetNodeByName(string nodeName)
	{
		return Children.First(x => x.NodeName == nodeName);
	}

	/// <summary>
	/// Gets the first component of type T found inside the node's components.
	/// Option to throw an error if no component is found.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
	public T GetComponent<T>(bool throwErrorIfNotFound = false)
	{
		var component = Components.OfType<T>().FirstOrDefault();
		if (component == null && throwErrorIfNotFound)
		{
			throw new InvalidOperationException(
				$"Component of type {typeof(T).Name} was not found on node {NodeName}({NodeId})"
			);
		}
		return component;
	}

	public T AddComponent<T>()
		where T : CoreNodeComponent, new()
	{
		var component = new T();
		Components.Add(component);
		component.Node = this;
		return component;
	}

	/// <summary>
	/// Adds a regular node to this node with no additional functionality to act as a container
	/// </summary>
	/// <param name="name"></param>
	/// <returns>The container node that was added</returns>
	public CoreNode AddContainer(string name)
	{
		var newNode = new CoreNode();
		newNode.NodeName = name;
		AddNode(newNode);

		return newNode;
	}

	/// <summary>
	/// Gets all immediate children nodes
	/// </summary>
	/// <returns></returns>
	public IEnumerable<CoreNode> GetChildren()
	{
		return Children;
	}

	/// <summary>
	/// Gets all descendant nodes in a flattened list.
	/// </summary>
	/// <returns></returns>
	public List<CoreNode> GetDescendants()
	{
		List<CoreNode> descendants = new List<CoreNode>();
		foreach (CoreNode child in Children)
		{
			descendants.Add(child);
			descendants.AddRange(child.GetDescendants());
		}
		return descendants;
	}

	public CoreNode AddNode(CoreNode node)
	{
		if (node.Parent != null)
		{
			node.Parent.RemoveNode(node);
		}

		node.Parent = this;

		node.NodeName = GenerateUniqueNodeName(node.NodeName);
		Children.Add(node);

		if (Root != null)
		{
			Root.RegisterNode(node);
		}

		return node;
	}

	public T AddNode<T>()
		where T : CoreNode, new()
	{
		var node = new T();
		AddNode(node);
		return node;
	}

	public void RemoveNode(CoreNode node)
	{
		var result = Children.Remove(node);
		if (!result)
		{
			Console.WriteLine(
				$"Warning : Could not find node {node.NodeName} to remove from {NodeName}. Its possible this was a cloned node."
			);
		}

		node.Parent = null;
		node.Root = null;
	}

	/// <summary>
	/// Removes a child node that is in the specified index position
	/// </summary>
	/// <param name="nodeIndex"></param>
	public void RemoveNodeAt(int nodeIndex)
	{
		var node = Children[nodeIndex];
		RemoveNode(node);
	}

	/// <summary>
	/// Removes all child nodes.
	/// </summary>
	public void ClearChildren()
	{
		Children.ForEach(x => x.Parent = null);
		Children = new List<CoreNode>();
	}

	/// <summary>
	/// Generates a unique name for a node. This is to prevent name conflicts when multiple nodes are trying to be added with the same name.
	/// </summary>
	/// <param name="baseName"></param>
	/// <returns></returns>
	private string GenerateUniqueNodeName(string baseName)
	{
		int suffix = 1;
		string newName = baseName;

		// Check if any sibling node has the same name
		while (Children.Exists(child => child.NodeName == newName))
		{
			newName = $"{baseName}_{suffix}";
			suffix++;
		}

		return newName;
	}

	public IEnumerable<CoreNode> GetAncestors()
	{
		List<CoreNode> nodes = new();

		var currentParent = Parent;
		while (currentParent != null)
		{
			nodes.Add(currentParent);
			currentParent = currentParent.Parent;
		}

		return nodes;
	}

	/// <summary>
	/// Dumps the contents of a node in a readable structured way,
	/// </summary>
	/// <param name="level"></param>
	/// <returns></returns>
	public string Dump(int level = 0)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine(new string(' ', level * 2) + ToString());
		foreach (CoreNode child in Children)
		{
			sb.Append(child.Dump(level + 1));
		}
		return sb.ToString();
	}

	public override string ToString()
	{
		return NodeName + ": " + base.ToString();
	}

	public virtual CoreNode DeepClone()
	{
		var newObj = MemberwiseClone() as CoreNode;
		newObj.Root = null; //never copy the root,if we want to add a node into the game we should always have to re-add it into the node tree manually.

		newObj.Children = Children.DeepClone().ToList();
		newObj.Children.ForEach(child => child.Parent = this);
		newObj.Parent = null;

		newObj.Components = Components.DeepClone().ToList();
		newObj.Components.ForEach(c => c.Node = newObj);

		newObj.Tags = Tags.ToList();
		return newObj;
	}
}
