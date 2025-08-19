using Common;
using R3;

namespace Tests;

public class EventManagerTests
{
	[Test]
	public void Can_Fire_Event()
	{
		var eventManager = new EventManager();

		var hasFired = false;

		eventManager.Events.Where(x => x.Channel == "test-channel").Subscribe(x => hasFired = true);
		eventManager.AddEvent("test-channel");

		Assert.That(hasFired, Is.True);
	}

	[Test]
	public void Can_Fire_Generic_Event()
	{
		var eventManager = new EventManager();

		var hasFired = false;

		eventManager.Events.Where(x => x.Channel == "test-channel").Subscribe(x => hasFired = true);
		eventManager.AddEvent("test-channel", new TestEvent { TestMessage = "" });

		Assert.That(hasFired, Is.True);
	}

	private class TestEvent
	{
		public string TestMessage { get; set; } = "";
	}
}
