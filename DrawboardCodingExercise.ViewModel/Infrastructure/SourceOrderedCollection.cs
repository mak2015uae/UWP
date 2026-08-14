using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DrawboardCodingExercise.ViewModel.Infrastructure;

/// <summary>
/// A bindable collection that keeps its items in the order a source listed them, even though they arrive in
/// whatever order concurrent work completes.
/// </summary>
/// <remarks>
/// Exists because the two obvious alternatives both look wrong on screen. Appending on arrival lets a list
/// visibly shuffle as it fills; sorting once at the end makes it jump. Placing each item against its listed
/// position keeps the order stable from the first row onwards.
/// <para>
/// Separated from the ViewModel that needed it so the placement rules can be tested directly, without staging a
/// page navigation to exercise them.
/// </para>
/// </remarks>
/// <typeparam name="T">The item type held in the collection.</typeparam>
public sealed class SourceOrderedCollection<T>
{
	private readonly Func<T, string> _keySelector;

	private IReadOnlyList<string> _sourceOrder = Array.Empty<string>();

	/// <summary>
	/// Initializes a new instance of the <see cref="SourceOrderedCollection{T}"/> class.
	/// </summary>
	/// <param name="keySelector">
	/// Extracts the key identifying an item within the source ordering. Compared case-insensitively, since the
	/// keys are typically URLs.
	/// </param>
	public SourceOrderedCollection(Func<T, string> keySelector)
	{
		_keySelector = keySelector;
	}

	/// <summary>
	/// Gets the items, in source order, for binding.
	/// </summary>
	/// <remarks>
	/// A bindable collection may only be modified on the UI thread, so callers must marshal before calling
	/// <see cref="Add"/> or <see cref="Reset"/>.
	/// </remarks>
	public ObservableCollection<T> Items { get; } = new();

	/// <summary>
	/// Gets the number of items the source said to expect.
	/// </summary>
	/// <value>
	/// The length of the ordering given to <see cref="Reset"/>. Lets a caller distinguish "the source listed
	/// nothing" from "nothing has arrived yet" without keeping a second field.
	/// </value>
	public int SourceCount => _sourceOrder.Count;

	/// <summary>
	/// Discards the current items and adopts a new source ordering.
	/// </summary>
	/// <param name="sourceOrder">
	/// The keys in the order the source listed them. An item whose key is absent from this list is still
	/// accepted, and placed after everything that is listed.
	/// </param>
	public void Reset(IReadOnlyList<string>? sourceOrder)
	{
		_sourceOrder = sourceOrder ?? Array.Empty<string>();
		Items.Clear();
	}

	/// <summary>
	/// Inserts an item at the position the source listed it.
	/// </summary>
	/// <param name="item">The item to place.</param>
	public void Add(T item)
	{
		var ordinal = OrdinalOf(_keySelector(item));

		for (var position = 0; position < Items.Count; position++)
		{
			if (OrdinalOf(_keySelector(Items[position])) > ordinal)
			{
				Items.Insert(position, item);
				return;
			}
		}

		Items.Add(item);
	}

	/// <summary>
	/// Finds a key's position in the source ordering.
	/// </summary>
	/// <param name="key">The key to locate.</param>
	/// <returns>
	/// The zero-based position, or <see cref="int.MaxValue"/> when the key was not listed — which sorts it after
	/// everything that was, rather than before it as a negative sentinel would.
	/// </returns>
	private int OrdinalOf(string key)
	{
		for (var index = 0; index < _sourceOrder.Count; index++)
		{
			if (string.Equals(_sourceOrder[index], key, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}

		return int.MaxValue;
	}
}
