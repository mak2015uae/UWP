# AI Collaboration and Validation

The exercise invites the use of an AI agent and asks what challenges it faced, how they were resolved, and how the resulting code was validated. This is that account, written honestly — including the things the agent got wrong.

Agent: Claude Code (Opus). The work was planned first ([IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)), then implemented against that plan.

---

## How the work was structured

The agent was not asked to "build the app". It was asked, in order, to: document the existing scaffold, produce an implementation plan, and only then implement it. That sequence mattered more than any individual prompt — reading the scaffold first surfaced the constraints that shaped the design, and writing the plan first meant the implementation had acceptance criteria to be checked against rather than being judged on whether it compiled.

Every work package finished with a build and a test run. Nothing was reported as done on the strength of having been written.

## Challenges, and how they were resolved

### 1. Assumed API schemas were wrong

The agent's first instinct was to write data transfer objects from prior knowledge of this API — where the films endpoint returns `{count, results}`. **It does not.** `https://swapi.info/api/films` returns a bare JSON array.

Resolution: fetch the live endpoints and read the actual payloads before writing a single mapped type. Three facts came out of that which changed the design:

- The response is a bare array, not an envelope.
- Fields are snake_case, which the scaffold's camel-case contract resolver does not map.
- Related resources are **absolute URLs**, while `IAPIClient` resolves paths against a base address.

That last one is a genuine trap: the scaffold's client built URLs by string concatenation, so passing an absolute URL through produced a request to `{base}/https://swapi.info/api/people/1`, which fails looking like a server fault.

The first fix was a helper that stripped the base off before calling the client. It worked, and it was well tested — but it treated the symptom. `Uri`'s own base-relative constructor already resolves relative *and* absolute forms correctly, so the real fix was one line inside the client, not a new step in front of it. Reviewing the code afterwards surfaced that; the helper became `RequestUriResolver`, which now owns URI construction for the client rather than compensating for it, and `FilmService` stopped needing to know the API's address at all.

Worth naming the pattern: the first instinct was to *add* something, and the better answer was to fix what was already there. Both versions passed their tests.

**Lesson applied throughout:** verify the external contract, do not recall it.

### 2. The legacy project file does not glob

The UWP project is a non-SDK project. Files are not picked up automatically; each needs a `<Compile>` or `<Page>` entry. The agent knew this from the documentation pass and scheduled it as an explicit step in every affected work package — the failure mode is a confusing "type not found" or "missing InitializeComponent" error rather than anything pointing at the project file.

### 3. A layering mistake caught by the compiler

`EpisodeFormatter` was first written into the services layer. The ViewModel project does not reference services — deliberately — so it could not use it. Rather than adding the reference, the class moved to the ViewModel project, which is where a presentation-formatting concern belongs anyway. The project structure enforced the layering that a convention alone would not have.

### 4. A memory leak introduced by the agent's own design

Making `PageViewModelBase` implement `IDisposable` seemed straightforwardly correct. It was not: a disposable component resolved from an Autofac root container is retained by the container for later disposal, so every page ViewModel ever created would have become unreachable but uncollectable — a leak proportional to how much the user navigated.

Caught by reasoning about the interaction between the new base class and the existing registration, not by any test. Resolved with `ExternallyOwned()` on the ViewModel registrations, documented at the registration site.

**This is the clearest example of why review mattered.** Everything compiled and every test passed both before and after.

### 5. A real ordering bug caught by a test

Character requests run concurrently, so responses arrive out of order. Rows are inserted at their API-listed position as they resolve. The agent used `-1` as the "not listed" sentinel — and `-1` sorts *before* everything, so an unexpected resource jumped to the front instead of being appended.

The test `OnNavigatedToAsync_UnlistedCharacter_IsAppendedRatherThanDropped` failed on the first run. Fixed by using `int.MaxValue` as the ordinal for an unlisted resource. Worth noting that this was a test written for a defensive branch the agent expected never to be taken — exactly the kind of test easily argued away as unnecessary.

### 6. Three latent defects found in the scaffold

Found while documenting, not while implementing, which is an argument for reading before writing:

- `ShellViewModel.OnNotifyDone` did `RemoveAt(IndexOf(...))` with no guard. An unmatched completion notification throws on the UI thread from an event handler — unrecoverable. Now guarded, with a regression test.
- `NavigationService.BackAsync` never raised `Navigated`, so the shell's back button kept its pre-navigation state until the next forward navigation.
- `ThreadDispatcher.RunOnUIThreadAsync` handed an asynchronous callback to a void-returning platform primitive, discarding the inner task, so a cross-thread caller resumed as soon as the callback yielded rather than when the work finished.

The first led to a design decision rather than just a patch: because an unpaired notification is this damaging, the pairing was confined to `BusyOperationRunner` so no caller can get it wrong.

### 7. The defect the test suite could not have found

The application built, deployed, and every one of its 97 tests passed. Then it was run, and the shell's progress indicator stayed visible with stale text — showing "Loading film" while on the film list, and "Loading films" while on the detail page. Inverted, which was the clue: those were leftovers.

The cause was in the scaffold's `EventAggregator`. Each subscription owned an independent action block consuming on the thread pool, so two messages handled by two *different* subscriptions could arrive in either order. The shell handles busy and completion notifications through separate subscriptions, so a completion could overtake its own busy notification: the completion found nothing to remove, the busy notification was then added, and nothing remained to clear it.

Two things about this are worth being blunt about.

**The guard added in point 6 made it invisible.** Guarding `RemoveAt` against an index of `-1` stopped the crash — and converted it into a silent stuck indicator. The symptom was treated; the ordering defect underneath went unnoticed. A crash would have been found faster.

**The test double was more reliable than production.** `RecordingEventAggregator` delivers synchronously. Every ViewModel and runner test therefore passed against ordering guarantees the real aggregator never made. The suite was not weak in coverage — it was wrong in its assumptions, which is harder to notice. The fix was to write tests against the *real* aggregator first: one of them measured `done:1` arriving before `busy:1`, confirming the diagnosis before a line of the implementation changed. Then the aggregator was rewritten for ordered synchronous fan-out, and the double's behaviour finally matches production.

This is the single strongest argument in this whole account for running the thing you built.

### 8. Small mechanical failures

Each caught by the build or a test run, each fixed in one step: a dropped `using System;` broke `await` on a platform async operation; a Shouldly assertion with a custom message hit the `IEnumerable<char>` overload instead of the string one; an NSubstitute assertion was left unawaited, producing a warning.

Notably, `.Services.IntegrationTests` had no xUnit runner adapter — so those tests had never been discovered or run at all, and the project reported success regardless. That is a good reminder that a green suite is only evidence if the tests actually execute.

## How the code was validated

**No claim here rests on the code having been written. Each rests on something that was run.**

1. **The external contract was verified against the live service** before any mapped type existed — three endpoints fetched and their real field names, casing and structure read.

2. **Build gates were tightened before implementation, not after.** `GenerateDocumentationFile` with `CS1591` promoted to an **error** in every project means an undocumented public member fails the build. This also forced backfilling the scaffold's undocumented surface. Every project builds with **zero warnings**.

3. **Tests were built to catch mapping mistakes, not to confirm them.** `StubApiClient` deserializes canned payloads through the *production* serializer settings, so the tests cover the data transfer objects' property names too — a substitute returning ready-made objects would pass even if every wire name were wrong. `ApiSerializerSettings` was extracted so test and application cannot drift.

4. **Fixtures keep the awkward parts.** The sample payloads retain the underscores, the absolute URLs, the carriage returns, an unparsable date, a null title and a blank array entry. A tidy fixture is how a mapping defect survives a green suite.

5. **Concurrency and caching are asserted, not assumed.** The stub client records maximum concurrent requests and per-path counts, so "bounded to six", "one request for two calls" and "one request under eight concurrent callers" are measured rather than described.

6. **Integration tests re-verify the assumptions the unit tests are built on** — bare array, snake_case mapping, same-origin absolute URLs — because canned payloads would keep passing if the real service changed shape.

7. **Both platforms build.** x64 Debug and ARM64 Release, the latter exercising the stricter .NET Native toolchain.

8. **A full read-through review pass** over every file, which is what caught the Autofac ownership leak.

9. **Coverage was measured, not claimed.** An audit against the README's evaluation criteria produced a number rather than an assurance, and the number exposed something an assurance would have hidden: `APIClient` had **0%** offline coverage, because its `HttpClient` is static with no seam. Its behaviour was only ever exercised against the live service. Adding a handler-accepting constructor took it to 43.5% and pinned the absolute-URL regression offline. The lesson is narrow but useful — "we have 109 tests" and "the important paths are tested" are different claims, and only one of them can be checked.

10. **The application was run.** Signed with a self-signed certificate, the certificate trusted, the package installed, and both pages exercised on a real device. This is what found the defect in point 7, and nothing else would have.

**Current results:** 73 service tests, 63 ViewModel tests, 5 live-API integration tests — 141 passing. Measured coverage: ViewModel 87.7%, Contracts 74.5%, Services 45.9%. Zero warnings on x64 Debug and ARM64 Release. Documentation coverage is machine-checked: 633 documented members, zero missing a summary, with `CS1591` promoted to an error.

## What has not been validated

Stated plainly, because an agent claiming otherwise is the thing worth distrusting:

- **The character rows have not been seen rendering.** The heading appears, the live-API test resolves real names, and the ViewModel tests cover insertion order — but the rows sit below the fold in the screenshots taken so far, so the final rendering step is inferred rather than observed.
- **The accessibility and layout checklist has not been run** — Narrator announcements, keyboard traversal, light and dark themes, and the ~320 epx narrow-window layout ([IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) §11).
- **Failure paths have not been exercised end-to-end in the running app.** The retry dialog is unit-tested, but the loop of pointing the base address at an unreachable host, seeing the prompt, and recovering has not been performed.
- **Views, converters and platform services have no automated coverage.** They are unreachable from a .NET test host.

All of these are verifiable in minutes with the app deployed. They are listed as outstanding rather than quietly assumed.

## What worked, and what I would do differently

**Worked:** verifying the external contract first; writing the plan before the code so implementation had acceptance criteria; treating documentation as a build gate rather than a cleanup pass; building fixtures deliberately hostile.

**Would do differently:** run the app interactively at the first work package that produces something visible, instead of leaving all runtime verification to the end. Build-and-test-green is a weaker signal for a UI application than it feels like — the leak in point 4 is proof that green says nothing about lifetime behaviour, and the ordering defect in point 7 is proof that it says nothing about concurrency either. Both survived a 97-test suite. Running the app took minutes and found the second one immediately.

Related: when a test double is *stronger* than the thing it stands in for, the suite stops being evidence. Doubles should be checked against the real implementation's actual guarantees, not against the ones it would be convenient for it to have.

**Where the agent needed the most supervision:** not syntax, and not the individual classes — those were mostly fine first time. It was the *interactions*. The Autofac ownership leak, the `Navigated` event the shell silently depended on, and the dispatcher discarding its inner task were all cases where each piece was locally reasonable and the seam between them was wrong. Those needed someone to hold two files in mind at once and ask what the combination actually does.
