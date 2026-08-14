using System;
using System.Collections.Generic;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Services;
using Serilog;

namespace DrawboardCodingExercise.Services.EventAggregator;

/// <summary>
/// The default <see cref="IEventAggregator"/>: fans each message out to its matching subscribers in the order
/// the messages were posted.
/// </summary>
/// <remarks>
/// Delivery is ordered and synchronous, which the previous dataflow-based implementation could not guarantee.
/// Each subscription there owned an independent action block consuming on the thread pool, so two messages
/// handled by two different subscriptions could be processed in either order — a measured, reproducible race,
/// not a theoretical one.
/// <para>
/// That mattered because busy and completion notifications are handled by separate subscriptions on the shell.
/// A completion overtaking its own busy notification left the shell's progress entry permanently stuck: the
/// completion found nothing to remove, and the busy notification was then added with nothing left to clear it.
/// </para>
/// <para>
/// The consequence of fixing it this way is that handlers now run on the posting thread. Every subscriber in
/// this application uses <see cref="SubscribeOnUI{T}"/>, which hands the work straight to the dispatcher and
/// returns, so posting stays cheap. A subscriber registered with <see cref="Subscribe{T}(Action{T})"/> must
/// keep its handler quick, or move the work onto a task of its own.
/// </para>
/// </remarks>
public class EventAggregator : IEventAggregator
{
	private readonly IThreadDispatcher _threadDispatcher;
	private readonly ILogger _logger;

	/// <summary>Guards <see cref="_subscriptions"/> against concurrent registration and removal.</summary>
	private readonly object _gate = new();

	/// <summary>The live subscriptions, held in registration order so delivery order is deterministic.</summary>
	private readonly List<Subscription> _subscriptions = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="EventAggregator"/> class.
	/// </summary>
	/// <param name="threadDispatcher">Marshals <see cref="SubscribeOnUI{T}"/> handlers onto the UI thread.</param>
	/// <param name="logger">Receives failures thrown by a subscriber's handler.</param>
	public EventAggregator(IThreadDispatcher threadDispatcher, ILogger logger)
	{
		_threadDispatcher = threadDispatcher;
		_logger = logger;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Delivers to every matching subscriber before returning, in registration order. A handler that throws is
	/// logged and does not prevent the remaining subscribers from receiving the message — this method is called
	/// from completion paths where an escaping exception would mask the original failure.
	/// </remarks>
	public void Post<T>(T arg) where T : notnull
	{
		Subscription[] snapshot;

		lock (_gate)
		{
			snapshot = _subscriptions.ToArray();
		}

		foreach (var subscription in snapshot)
		{
			try
			{
				subscription.Deliver(arg);
			}
			catch (Exception exception)
			{
				_logger.Error(
					exception,
					"A subscriber to {MessageType} threw while handling a message",
					typeof(T).Name);
			}
		}
	}

	/// <inheritdoc />
	public IDisposable Subscribe<T>(Action<T> action) =>
		Add(typeof(T), message => action((T)message), dispatchToUI: false);

	/// <inheritdoc />
	public IDisposable Subscribe<T>(Action<T> action, Predicate<T> filter) =>
		Add(
			typeof(T),
			message =>
			{
				if (filter((T)message))
				{
					action((T)message);
				}
			},
			dispatchToUI: false);

	/// <inheritdoc />
	public IDisposable SubscribeOnUI<T>(Action<T> action) =>
		Add(typeof(T), message => action((T)message), dispatchToUI: true);

	/// <summary>
	/// Registers a subscription and returns the token that removes it.
	/// </summary>
	/// <param name="messageType">The message type this subscription observes, including derived types.</param>
	/// <param name="handler">The handler, already closed over the caller's typed delegate.</param>
	/// <param name="dispatchToUI">Whether the handler must be marshalled onto the UI thread.</param>
	/// <returns>A token that ends the subscription when disposed.</returns>
	private IDisposable Add(Type messageType, Action<object> handler, bool dispatchToUI)
	{
		var subscription = new Subscription(
			messageType,
			handler,
			dispatchToUI ? _threadDispatcher : null);

		lock (_gate)
		{
			_subscriptions.Add(subscription);
		}

		return new Unsubscriber(() =>
		{
			lock (_gate)
			{
				_subscriptions.Remove(subscription);
			}
		});
	}

	/// <summary>
	/// One registered handler, and the message type it cares about.
	/// </summary>
	private sealed class Subscription
	{
		private readonly Type _messageType;
		private readonly Action<object> _handler;
		private readonly IThreadDispatcher? _threadDispatcher;

		/// <summary>
		/// Initializes a new instance of the <see cref="Subscription"/> class.
		/// </summary>
		/// <param name="messageType">The message type observed.</param>
		/// <param name="handler">The handler to invoke.</param>
		/// <param name="threadDispatcher">
		/// The dispatcher to marshal through, or <see langword="null"/> to invoke the handler directly on the
		/// posting thread.
		/// </param>
		public Subscription(Type messageType, Action<object> handler, IThreadDispatcher? threadDispatcher)
		{
			_messageType = messageType;
			_handler = handler;
			_threadDispatcher = threadDispatcher;
		}

		/// <summary>
		/// Delivers a message if this subscription matches it.
		/// </summary>
		/// <param name="message">The posted message.</param>
		/// <remarks>
		/// Dispatched deliveries preserve posting order: the dispatcher runs the handler inline when already on
		/// the UI thread, and otherwise queues it at a single priority, which the platform drains in order.
		/// </remarks>
		public void Deliver(object message)
		{
			if (!_messageType.IsInstanceOfType(message))
			{
				return;
			}

			if (_threadDispatcher is null)
			{
				_handler(message);
				return;
			}

			_threadDispatcher.FireOnUIAndForget(() => _handler(message));
		}
	}

	/// <summary>
	/// Removes a subscription when disposed.
	/// </summary>
	private sealed class Unsubscriber : IDisposable
	{
		private readonly Action _remove;

		/// <summary>
		/// Initializes a new instance of the <see cref="Unsubscriber"/> class.
		/// </summary>
		/// <param name="remove">The action that removes the subscription.</param>
		public Unsubscriber(Action remove) => _remove = remove;

		/// <inheritdoc />
		/// <remarks>Disposing more than once is harmless.</remarks>
		public void Dispose() => _remove();
	}
}
