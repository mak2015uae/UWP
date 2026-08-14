using System;
using System.Linq;
using DrawboardCodingExercise.ViewModel.Infrastructure;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.ViewModel.UnitTests;

/// <summary>
/// Tests for <see cref="SourceOrderedCollection{T}"/>, the placement rules that keep an incrementally-filled list
/// in the order its source published.
/// </summary>
/// <remarks>
/// Extracted from the film detail ViewModel so these rules can be exercised directly, rather than by staging a
/// page navigation and inferring the outcome from the rendered list.
/// </remarks>
public class SourceOrderedCollectionTests
{
	private static readonly string[] SourceOrder = { "a", "b", "c", "d" };

	/// <summary>
	/// Creates a collection keyed on the item itself.
	/// </summary>
	/// <returns>A collection primed with the standard source ordering.</returns>
	private static SourceOrderedCollection<string> Create()
	{
		var collection = new SourceOrderedCollection<string>(item => item);
		collection.Reset(SourceOrder);
		return collection;
	}

	/// <summary>
	/// Items arriving in reverse still read in source order, and do so at every step rather than only once
	/// complete — a list that reorders itself while filling looks broken.
	/// </summary>
	[Fact]
	public void Add_ItemsArrivingInReverse_AreOrderedAtEveryStep()
	{
		var collection = Create();

		collection.Add("d");
		collection.Items.ShouldBe(new[] { "d" });

		collection.Add("c");
		collection.Items.ShouldBe(new[] { "c", "d" });

		collection.Add("a");
		collection.Items.ShouldBe(new[] { "a", "c", "d" });

		collection.Add("b");
		collection.Items.ShouldBe(new[] { "a", "b", "c", "d" });
	}

	/// <summary>
	/// Every arrival order produces the same result.
	/// </summary>
	/// <param name="arrivals">The order the items arrive in.</param>
	[Theory]
	[InlineData("abcd")]
	[InlineData("dcba")]
	[InlineData("bdac")]
	[InlineData("cadb")]
	public void Add_AnyArrivalOrder_ProducesSourceOrder(string arrivals)
	{
		var collection = Create();

		foreach (var item in arrivals.Select(character => character.ToString()))
		{
			collection.Add(item);
		}

		collection.Items.ShouldBe(SourceOrder);
	}

	/// <summary>
	/// An item the source never listed is kept and placed last, rather than dropped or jumping to the front.
	/// </summary>
	[Fact]
	public void Add_UnlistedItem_IsPlacedLast()
	{
		var collection = Create();

		collection.Add("unlisted");
		collection.Add("b");
		collection.Add("a");

		collection.Items.ShouldBe(new[] { "a", "b", "unlisted" });
	}

	/// <summary>
	/// Several unlisted items keep their arrival order relative to each other, having nothing better to sort by.
	/// </summary>
	[Fact]
	public void Add_SeveralUnlistedItems_KeepTheirArrivalOrder()
	{
		var collection = Create();

		collection.Add("second");
		collection.Add("first");
		collection.Add("a");

		collection.Items.ShouldBe(new[] { "a", "second", "first" });
	}

	/// <summary>
	/// Keys are matched case-insensitively, since they are typically URLs.
	/// </summary>
	[Fact]
	public void Add_KeyDifferingByCase_IsStillPlacedByTheSourceOrder()
	{
		var collection = Create();

		collection.Add("D");
		collection.Add("A");

		collection.Items.ShouldBe(new[] { "A", "D" });
	}

	/// <summary>
	/// Resetting clears the items and adopts the new ordering, so revisiting a page starts clean.
	/// </summary>
	[Fact]
	public void Reset_ClearsItemsAndAdoptsTheNewOrdering()
	{
		var collection = Create();
		collection.Add("a");
		collection.Add("b");

		collection.Reset(new[] { "z", "y" });

		collection.Items.ShouldBeEmpty();
		collection.SourceCount.ShouldBe(2);

		collection.Add("y");
		collection.Add("z");
		collection.Items.ShouldBe(new[] { "z", "y" });
	}

	/// <summary>
	/// A null ordering is treated as empty, so a caller need not special-case a missing collection.
	/// </summary>
	[Fact]
	public void Reset_NullOrdering_IsTreatedAsEmpty()
	{
		var collection = Create();

		collection.Reset(null);

		collection.SourceCount.ShouldBe(0);
		collection.Items.ShouldBeEmpty();
	}

	/// <summary>
	/// The source count distinguishes "the source listed nothing" from "nothing has arrived yet", which is what
	/// lets a page tell an empty result from a load in progress.
	/// </summary>
	[Fact]
	public void SourceCount_ReportsWhatWasExpectedRatherThanWhatArrived()
	{
		var collection = Create();

		collection.SourceCount.ShouldBe(4);
		collection.Items.ShouldBeEmpty();

		collection.Add("a");

		collection.SourceCount.ShouldBe(4);
		collection.Items.Count.ShouldBe(1);
	}

	/// <summary>
	/// Before any reset the collection is simply empty, rather than throwing.
	/// </summary>
	[Fact]
	public void SourceCount_BeforeAnyReset_IsZero()
	{
		var collection = new SourceOrderedCollection<string>(item => item);

		collection.SourceCount.ShouldBe(0);
		Should.NotThrow(() => collection.Add("anything"));
		collection.Items.ShouldBe(new[] { "anything" });
	}
}
