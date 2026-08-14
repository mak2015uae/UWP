using System;
using System.Collections.Generic;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// An <see cref="IEventAggregator"/> that delivers messages synchronously and records everything posted.
/// </summary>
/// <remarks>
/// The production aggregator delivers through a dataflow block and, for UI subscriptions, a dispatcher, so
/// delivery is asynchronous and a test would have to wait on it. This delivers inline, which makes assertions
/// about progress notifications deterministic, and keeps a transcript so tests can assert that busy and done
/// notifications were paired.
/// </remarks>
public sealed class RecordingEventAggregator : IEventAggregator
{
	private readonly List<(Type MessageType, Delegate Handler)> _subscriptions = new();

	/// <summary>
	/// Gets every message posted, in order.
	/// </summary>
	public List<object> PostedMessages { get; } = new();

	/// <inheritdoc />
	/// <remarks>Delivers to matching subscribers synchronously before returning.</remarks>
	public void Post<T>(T arg) where T : notnull
	{
		PostedMessages.Add(arg);

		// Copy first: a handler may subscribe or unsubscribe while being invoked.
		foreach (var (messageType, handler) in _subscriptions.ToArray())
		{
			if (messageType == typeof(T) && handler is Action<T> typedHandler)
			{
				typedHandler(arg);
			}
		}
	}

	/// <inheritdoc />
	public IDisposable Subscribe<T>(Action<T> action) => Add(action);

	/// <inheritdoc />
	public IDisposable Subscribe<T>(Action<T> action, Predicate<T> filter) =>
		Add<T>(message =>
		{
			if (filter(message))
			{
				action(message);
			}
		});

	/// <inheritdoc />
	/// <remarks>Identical to <see cref="Subscribe{T}(Action{T})"/> here; there is no UI thread in a test host.</remarks>
	public IDisposable SubscribeOnUI<T>(Action<T> action) => Add(action);

	/// <summary>
	/// Registers a handler and returns the token that removes it.
	/// </summary>
	/// <typeparam name="T">The message type observed.</typeparam>
	/// <param name="action">The handler to register.</param>
	/// <returns>A token that unsubscribes when disposed.</returns>
	private IDisposable Add<T>(Action<T> action)
	{
		var subscription = (typeof(T), (Delegate)action);
		_subscriptions.Add(subscription);

		return new Unsubscriber(() => _subscriptions.Remove(subscription));
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
		public void Dispose() => _remove();
	}
}
