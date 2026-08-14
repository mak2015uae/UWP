using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.TestSupport;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests;

/// <summary>
/// Tests for <see cref="EventAggregator.EventAggregator"/>, the messaging the shell's progress indicator depends on.
/// </summary>
/// <remarks>
/// These exist because of a defect found by running the application: the progress indicator stayed visible with
/// stale text after navigating. The cause was message loss in the aggregator, not in the code posting the
/// messages — so every test that used a synchronous test double passed while the real application misbehaved.
/// </remarks>
public class EventAggregatorTests
{
	private readonly ImmediateThreadDispatcher _threadDispatcher = new();

	/// <summary>
	/// Creates the aggregator under test.
	/// </summary>
	/// <returns>An aggregator delivering UI subscriptions inline.</returns>
	private EventAggregator.EventAggregator CreateAggregator() =>
		new(_threadDispatcher, SilentLogger.Create());

	/// <summary>
	/// Every message must reach its subscriber. Progress notifications are posted in tightly-spaced pairs — a
	/// cached load completes without yielding — and a dropped completion leaves the shell's indicator running for
	/// the rest of the session.
	/// </summary>
	[Fact]
	public async Task Post_ManyMessagesInRapidSuccession_DeliversEveryOne()
	{
		const int pairs = 200;
		var aggregator = CreateAggregator();
		var busy = new List<string>();
		var done = new List<string>();

		using (aggregator.SubscribeOnUI<NotifyBusyEvent>(message => busy.Add(message.Event)))
		using (aggregator.SubscribeOnUI<NotifyDoneEvent>(message => done.Add(message.Event)))
		{
			for (var index = 0; index < pairs; index++)
			{
				aggregator.Post(new NotifyBusyEvent($"operation {index}"));
				aggregator.Post(new NotifyDoneEvent($"operation {index}"));
			}

			await WaitForAsync(() => busy.Count >= pairs && done.Count >= pairs);
		}

		busy.Count.ShouldBe(pairs, "no busy notification may be dropped");
		done.Count.ShouldBe(pairs, "a dropped completion leaves the progress indicator stuck");
	}

	/// <summary>
	/// A subscriber only receives the message type it asked for.
	/// </summary>
	[Fact]
	public async Task Post_MessageOfAnotherType_IsNotDelivered()
	{
		var aggregator = CreateAggregator();
		var received = new List<string>();

		using (aggregator.SubscribeOnUI<NotifyDoneEvent>(message => received.Add(message.Event)))
		{
			aggregator.Post(new NotifyBusyEvent("busy"));
			aggregator.Post(new NotifyDoneEvent("done"));

			await WaitForAsync(() => received.Count >= 1);
		}

		received.ShouldHaveSingleItem().ShouldBe("done");
	}

	/// <summary>
	/// Busy and completion notifications are handled by separate subscriptions, so their relative order must be
	/// preserved across them — a completion arriving before its own busy notification would leave a stale entry
	/// behind just as surely as a dropped one.
	/// </summary>
	[Fact]
	public async Task Post_AcrossSeparateSubscriptions_PreservesOrder()
	{
		const int pairs = 50;
		var aggregator = CreateAggregator();
		var sequence = new List<string>();

		using (aggregator.SubscribeOnUI<NotifyBusyEvent>(message => sequence.Add("busy:" + message.Event)))
		using (aggregator.SubscribeOnUI<NotifyDoneEvent>(message => sequence.Add("done:" + message.Event)))
		{
			for (var index = 0; index < pairs; index++)
			{
				aggregator.Post(new NotifyBusyEvent($"{index}"));
				aggregator.Post(new NotifyDoneEvent($"{index}"));
			}

			await WaitForAsync(() => sequence.Count >= pairs * 2);
		}

		for (var index = 0; index < pairs; index++)
		{
			sequence[index * 2].ShouldBe($"busy:{index}");
			sequence[(index * 2) + 1].ShouldBe($"done:{index}");
		}
	}

	/// <summary>
	/// A predicate subscription receives only the messages that satisfy it.
	/// </summary>
	[Fact]
	public async Task Subscribe_WithFilter_DeliversOnlyMatchingMessages()
	{
		var aggregator = CreateAggregator();
		var received = new List<string>();

		using (aggregator.Subscribe<NotifyBusyEvent>(
				   message => received.Add(message.Event),
				   message => message.Event.StartsWith("keep", StringComparison.Ordinal)))
		{
			aggregator.Post(new NotifyBusyEvent("keep me"));
			aggregator.Post(new NotifyBusyEvent("drop me"));
			aggregator.Post(new NotifyBusyEvent("keep me too"));

			await WaitForAsync(() => received.Count >= 2);
		}

		received.ShouldBe(new[] { "keep me", "keep me too" });
	}

	/// <summary>
	/// Disposing a subscription stops delivery, so a subscriber with a shorter life than the aggregator can
	/// detach.
	/// </summary>
	[Fact]
	public async Task Dispose_Subscription_StopsDelivery()
	{
		var aggregator = CreateAggregator();
		var received = new List<string>();

		var subscription = aggregator.SubscribeOnUI<NotifyBusyEvent>(message => received.Add(message.Event));
		aggregator.Post(new NotifyBusyEvent("before"));
		await WaitForAsync(() => received.Count >= 1);

		subscription.Dispose();
		aggregator.Post(new NotifyBusyEvent("after"));
		await Task.Delay(50);

		received.ShouldHaveSingleItem().ShouldBe("before");
	}

	/// <summary>
	/// Waits briefly for an expected condition, so a delivery mechanism that happens to be asynchronous is given
	/// a fair chance rather than being failed on a race.
	/// </summary>
	/// <param name="condition">The condition to wait for.</param>
	/// <returns>A task that completes when the condition holds or the timeout elapses.</returns>
	private static async Task WaitForAsync(Func<bool> condition)
	{
		var timeout = Stopwatch.StartNew();

		while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(3))
		{
			await Task.Delay(10);
		}
	}
}
