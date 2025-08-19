using Common;
using R3;

namespace Tests;

public class EventsProcessorTests
{
	[Test]
	public void Can_Process_One_Event()
	{
		var evtProcessor = new EventsProcessor();
		bool eventProcessed = false;
		evtProcessor.RegisterHandler(
			(TestEvent evt) =>
			{
				eventProcessed = true;
				evtProcessor.Next();
			}
		);

		evtProcessor.AddEvent(new TestEvent { Message = "Hello", TestMessage = "Test Hello" });

		evtProcessor.Process();

		Assert.That(eventProcessed, Is.True);
	}

	[Test]
	public void Can_Process_Two_Events()
	{
		var evtProcessor = new EventsProcessor();
		bool eventProcessed = false;
		evtProcessor.RegisterHandler(
			(TestEvent evt) =>
			{
				if (evt.Message == "Second Event")
				{
					eventProcessed = true;
				}
				evtProcessor.Next();
			}
		);

		evtProcessor.AddEvent(new TestEvent { Message = "Hello", TestMessage = "Test Hello" });
		evtProcessor.AddEvent(
			new TestEvent { Message = "Second Event", TestMessage = "Test Hello" }
		);

		evtProcessor.Process();
		evtProcessor.Process();

		Assert.That(eventProcessed, Is.True);
	}

	[Test]
	public void Can_Process_Different_Event_Types()
	{
		var evtProcessor = new EventsProcessor();
		string testMessageFound = "";
		int testIntFound = -1;

		evtProcessor.RegisterHandler(
			(TestEvent evt) =>
			{
				testMessageFound = evt.TestMessage;
				evtProcessor.Next();
			}
		);

		evtProcessor.RegisterHandler<TestEvent2>(
			(evt) =>
			{
				testIntFound = evt.TestInt;
				evtProcessor.Next();
			}
		);

		evtProcessor.AddEvent(new TestEvent { Message = "Hello", TestMessage = "Test Hello" });
		evtProcessor.AddEvent(new TestEvent2 { Message = "Second Event", TestInt = 40 });

		evtProcessor.Process();
		evtProcessor.Process();

		Assert.That(testMessageFound, Is.EqualTo("Test Hello"));
		Assert.That(testIntFound, Is.EqualTo(testIntFound));
	}

	private class TestEvent : GameEvent
	{
		public string TestMessage { get; set; } = "";
	}

	private class TestEvent2 : GameEvent
	{
		public int TestInt { get; set; } = 1;
	}
}
