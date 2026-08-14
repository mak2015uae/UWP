using System;

namespace DrawboardCodingExercise.Contracts.Services;

/// <summary>
/// Decoupled in-app messaging. Publishers post messages without knowing who, if anyone, is listening.
/// </summary>
/// <remarks>
/// Registered as a single instance. Subscriptions are held until the returned token is disposed, so
/// subscribers with a shorter lifetime than the aggregator must dispose their tokens or they will leak.
/// </remarks>
public interface IEventAggregator
{
	/// <summary>
	/// Publishes a message to every subscriber whose message type matches.
	/// </summary>
	/// <typeparam name="T">The message type, used as the subscription filter.</typeparam>
	/// <param name="arg">The message to publish.</param>
	void Post<T>(T arg) where T : notnull;

	/// <summary>
	/// Subscribes to every message of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The message type to observe.</typeparam>
	/// <param name="action">
	/// The handler, invoked on an arbitrary thread. Use <see cref="SubscribeOnUI{T}"/> when the handler
	/// touches bound state.
	/// </param>
	/// <returns>A token that ends the subscription when disposed.</returns>
	IDisposable Subscribe<T>(Action<T> action);

	/// <summary>
	/// Subscribes to messages of type <typeparamref name="T"/> that satisfy a predicate.
	/// </summary>
	/// <typeparam name="T">The message type to observe.</typeparam>
	/// <param name="action">The handler, invoked on an arbitrary thread.</param>
	/// <param name="filter">
	/// Evaluated for each candidate message; the handler runs only when it returns
	/// <see langword="true"/>.
	/// </param>
	/// <returns>A token that ends the subscription when disposed.</returns>
	IDisposable Subscribe<T>(Action<T> action, Predicate<T> filter);

	/// <summary>
	/// Subscribes to every message of type <typeparamref name="T"/>, marshalling the handler onto the UI thread.
	/// </summary>
	/// <typeparam name="T">The message type to observe.</typeparam>
	/// <param name="action">
	/// The handler, invoked on the UI thread. Dispatch is fire-and-forget, so the handler runs after
	/// <see cref="Post{T}"/> has already returned.
	/// </param>
	/// <returns>A token that ends the subscription when disposed.</returns>
	IDisposable SubscribeOnUI<T>(Action<T> action);
}
