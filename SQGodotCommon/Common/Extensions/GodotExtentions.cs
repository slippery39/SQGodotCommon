using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Core;

public static class GodotExtensions
{
	public static IEnumerable<T> GetChildrenOfType<T>(this Node node)
	{
		if (node == null)
		{
			throw new ArgumentNullException(nameof(node));
		}
		return node.GetChildren().OfType<T>();
	}

	public static T GetNodeOfType<T>(this Node node)
	{
		return node.GetChildren().OfType<T>().FirstOrDefault();
	}

	public static T GetNodeWithGuard<T>(this Node node, string nodeName)
		where T : Node
	{
		var retNode =
			node.GetNodeOrNull<T>(nodeName)
			?? throw new InvalidOperationException(
				$"Could not find node {nodeName} in {node.Name}"
			);

		return retNode;
	}

	public static void Disable(this Node node)
	{
		node.SetProcess(false);
		node.SetProcessInput(false);
		node.SetProcessUnhandledInput(false);
		node.SetProcessUnhandledKeyInput(false);
		node.SetProcessInternal(false);
		node.SetProcessShortcutInput(false);
		node.SetPhysicsProcess(false);
		node.SetPhysicsProcessInternal(false);

		var control = node as Control;
		if (control != null)
		{
			control.SetProcessUnhandledInput(false);
			control.SetProcessUnhandledKeyInput(false);
			control.SetProcessShortcutInput(false);
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
		}

		node.ProcessMode = Node.ProcessModeEnum.Disabled;
		foreach (var child in node.GetChildren())
		{
			child.Disable();
		}
	}

	public static void Enable(this Node node)
	{
		node.SetProcess(true);
		node.SetProcessInput(true);
		node.SetProcessUnhandledInput(true);
		node.SetProcessUnhandledKeyInput(true);
		node.SetProcessInternal(true);
		node.SetProcessShortcutInput(true);
		node.SetPhysicsProcess(true);
		node.SetPhysicsProcessInternal(true);

		var control = node as Control;
		if (control != null)
		{
			control.SetProcessUnhandledInput(true);
			control.SetProcessUnhandledKeyInput(true);
			control.SetProcessShortcutInput(true);
			//Note - this does not reset it to whatever it was before, if that behavior is needed we may need to
			//make a more complicated system that remembers its previous state.
			control.MouseFilter = Control.MouseFilterEnum.Stop;
		}

		node.ProcessMode = Node.ProcessModeEnum.Inherit;
		foreach (var child in node.GetChildren())
		{
			child.Enable();
		}
	}

	public static void ClearChildren(this Node node)
	{
		foreach (var chlld in node.GetChildren())
		{
			chlld.QueueFree();
		}
	}

	public static Vector3 WithX(this Vector3 vector3, float amount)
	{
		return new Vector3(amount, vector3.Y, vector3.Z);
	}

	public static Vector2 WithX(this Vector2 vector2, float amount)
	{
		return new Vector2(amount, vector2.Y);
	}

	public static Vector2 AddX(this Vector2 vector2, float amount)
	{
		return new Vector2(vector2.X + amount, vector2.Y);
	}

	public static Vector3 WithY(this Vector3 vector3, float amount)
	{
		return new Vector3(vector3.X, amount, vector3.Z);
	}

	public static Vector2 WithY(this Vector2 vector2, float amount)
	{
		return new Vector2(vector2.X, amount);
	}

	public static Vector2 AddY(this Vector2 vector2, float amount)
	{
		return new Vector2(vector2.X, vector2.Y + amount);
	}

	public static Vector3 WithZ(this Vector3 vector3, float amount)
	{
		return new Vector3(vector3.X, vector3.Y, amount);
	}

	public static Vector2 ViewportMiddle(this Node node)
	{
		return new Vector2(
			node.GetViewport().GetVisibleRect().Size.X / 2,
			node.GetViewport().GetVisibleRect().Size.Y / 2
		);
	}

	public static void CallNextFrame(this Node node, Action action)
	{
		Callable.From(action).CallDeferred();
	}

	public static T GetAncestorOfType<T>(this Node node)
	{
		Node current = node;

		while (current != null)
		{
			if (current is T retNode)
				return retNode;

			current = current.GetParent();
		}

		return default;
	}

	public static Color WithAlpha(this Color color, float alpha)
	{
		return new Color(color.R, color.G, color.B, alpha);
	}
}
