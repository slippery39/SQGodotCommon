using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CoreNodes;

/// <summary>
/// Allows us to use specify a CoreNode that should be treated like a regular List.
/// Underneath the hood, it still is structured like a node and therefore we can still loop
/// through it like a regular CoreNode.
/// </summary>
/// <typeparam name="T"></typeparam>
public class CoreList<T> : CoreNode, IList<T>
	where T : CoreNode
{
	public T this[int index]
	{
		get => Items.GetChildren().OfType<T>().ToList()[index];
		set
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException(nameof(index));
			Items.RemoveNodeAt(index);
			Insert(index, value);
		}
	}

	public int Count => Items.GetChildren().Count();

	public bool IsReadOnly => false;

	private CoreNode Items => GetNodeByName("Items");

	public CoreList()
	{
		AddContainer("Items");
	}

	public void Add(T item)
	{
		Items.AddNode(item);
	}

	public void Clear()
	{
		Items.ClearChildren();
	}

	public bool Contains(T item)
	{
		return Items.GetChildren().Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		if (array == null)
			throw new ArgumentNullException(nameof(array));
		if (arrayIndex < 0 || arrayIndex + Count > array.Length)
			throw new ArgumentOutOfRangeException(nameof(arrayIndex));

		var children = Items.GetChildren().OfType<T>().ToArray();
		Array.Copy(children, 0, array, arrayIndex, children.Length);
	}

	public IEnumerator<T> GetEnumerator()
	{
		return Items.GetChildren().OfType<T>().GetEnumerator();
	}

	public int IndexOf(T item)
	{
		var children = Items.GetChildren().OfType<T>().ToList();
		return children.IndexOf(item);
	}

	public void Insert(int index, T item)
	{
		if (index < 0 || index > Count)
			throw new ArgumentOutOfRangeException(nameof(index));

		// Add a node at the end and move it to the correct position
		Items.AddNode(item);
		var children = Items.GetChildren().ToList();
		children.Remove(item);
		children.Insert(index, item);

		// Reorder all children
		Items.ClearChildren();
		foreach (var child in children)
		{
			Items.AddNode(child);
		}
	}

	public bool Remove(T item)
	{
		if (!Contains(item))
			return false;
		Items.RemoveNode(item);
		return true;
	}

	public void RemoveAt(int index)
	{
		if (index < 0 || index >= Count)
			throw new ArgumentOutOfRangeException(nameof(index));
		var item = this[index];
		Remove(item);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return Items.GetChildren().OfType<T>().GetEnumerator();
	}
}
