using System;
using Godot;
using Microsoft.Extensions.Time.Testing;

namespace Project;

public partial class GameWorld : Node2D
{
	private FakeTimeProvider fakeTimeProvider = new FakeTimeProvider();
	public TimeProvider TimeProvider => fakeTimeProvider;

	public override void _Process(double delta)
	{
		fakeTimeProvider.Advance(TimeSpan.FromSeconds(delta));
	}
}
