# Solution — Star Wars Films (UWP)

Submission for the Drawboard UWP coding exercise. Chosen option: **Option 1, the Star Wars films API** (`https://swapi.info`).

Companion documents: [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) (the plan this was built from), [ARCHITECTURE.md](ARCHITECTURE.md) (how the scaffold works), [AI-COLLABORATION.md](AI-COLLABORATION.md) (agent collaboration and validation), [CLAUDE.md](CLAUDE.md) (build commands).

---

## What was built

**Page 1 — film list.** Every film from `GET /films`, each row showing the title and the episode number formatted as the films themselves title it (`Episode IV`). Clicking a row opens page 2.

**Page 2 — film detail.** Title, episode number, release date, director and producer, plus the **bonus opening crawl**, and a list of the film's characters that fills in progressively as each name resolves.

Both pages report progress through the shell's indicator, offer a retry when a request fails, and have distinct empty, error and loading states.

## How to build and run

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"

# Build and deploy (x64 or ARM64)
& $msbuild DrawboardCodingExercise\DrawboardCodingExercise.csproj -t:Restore,Build,Deploy `
    -p:Configuration=Debug -p:Platform=x64 -p:AppxBundle=Never

# Tests — offline
dotnet test DrawboardCodingExercise.Services.UnitTests\DrawboardCodingExercise.Services.UnitTests.csproj
dotnet test DrawboardCodingExercise.ViewModel.UnitTests\DrawboardCodingExercise.ViewModel.UnitTests.csproj

# Tests — live API
dotnet test DrawboardCodingExercise.Services.IntegrationTests\DrawboardCodingExercise.Services.IntegrationTests.csproj
```

`dotnet build` cannot build the app: the UWP project is a legacy non-SDK project needing Visual Studio's MSBuild. `-p:AppxBundle=Never` avoids a bundling step that fails unless both platforms were built. Details in [CLAUDE.md](CLAUDE.md).

**Test results:** 73 service tests, 63 ViewModel tests, 5 live-API integration tests — 141 passing. Measured coverage: ViewModel 87.7%, Contracts 74.5%, Services 45.9%. Both x64 Debug and ARM64 Release build with zero warnings. The app has been installed and run; see [CHANGES.md](CHANGES.md) §0 for requirement-by-requirement verification.

## Structure

Dependencies point inward, and only the UWP head project references `Windows.*`. That is what lets every ViewModel be tested on plain .NET with no UI host.

| Project | Contents |
|---|---|
| `.Contracts` | Domain model (`Film`, `RelatedResource`, `RelatedResourceKind`), navigation parameter, service interfaces, `PageKey` |
| `.Services` | `FilmService`, `BusyOperationRunner`, data transfer objects, `FilmMapper`, `APIClient`, `RequestUriResolver` |
| `.ViewModel` | `FilmListViewModel`, `FilmDetailViewModel`, row ViewModels, `PageViewModelBase`, `SourceOrderedCollection`, `EpisodeFormatter` |
| `DrawboardCodingExercise` | Pages, converters, Autofac modules, platform services |
| `.TestSupport` | Test doubles shared by all three test projects |
| `.Services.UnitTests`, `.ViewModel.UnitTests`, `.Services.IntegrationTests` | Tests |

Data transfer objects never leave the services layer, and ViewModels never see JSON, HTTP or a `Frame`.

## Design decisions worth discussing

**The films endpoint returns everything page 2 needs, including the opening crawl.** So the detail page issues **no request for the film itself** — `FilmService` caches the list and serves detail views from it. Only character names need requests. The cache also compensates for the framework rebuilding page ViewModels on every navigation, which makes back navigation instant.

**Navigation carries an identifier, not an object.** `FilmDetailParameter(int FilmId)` is stored in the frame's back stack and replayed on back navigation. A value survives that round trip cleanly, and a cold cache is recoverable — the service re-fetches if the detail page is reached after a suspend.

**One place owns progress and retry.** `BusyOperationRunner` posts the busy notification, runs the operation, prompts for retry on a recoverable failure, and posts the matching completion notification in a `finally`. The shell clears its progress entry by matching message text, so an unpaired notification would leave the indicator spinning forever; confining the pairing to one class makes that impossible by construction and testable in one place. It returns an outcome rather than throwing, so ViewModels branch instead of catching.

**Failures are explained specifically.** Offline, not-found, throttled, server fault and timeout each map to their own localized message, so the user can tell whether retrying is worth their time. Exceptions the runner does not recognise propagate deliberately, rather than hiding a defect behind a retry dialog.

**Partial failure is not total failure.** The film load and the character load are separate guarded operations, so failing to resolve characters still leaves the film's details on screen with a scoped message.

**Character requests are bounded and ordered.** One film needs up to eighteen requests and the API has no batch endpoint, so concurrency is capped at six with per-URL caching. Responses arrive out of order, but each row is inserted at its API-listed position as it resolves — so the list is correctly ordered from the first row rather than shuffling as it fills or jumping once at the end.

**Loads cancel when the user navigates away.** The framework has a navigated-to hook but no navigated-from hook, so `PageViewModelBase` infers it: a page subscribes to the navigation event from inside its own hook, by which point its own event has been raised, so the next event is a navigation away. The handler removes itself, which matters because ViewModels are created per navigation and never disposed by the framework.

## Changes made to the scaffold

Five defects, each with a regression test where testable:

1. **`ShellViewModel` could crash the application.** An unmatched completion notification did `RemoveAt(IndexOf(...))`, and `IndexOf` returns `-1` — throwing on the UI thread from an event handler, which is unrecoverable. Now guarded and logged.
2. **The back button's state went stale.** `NavigationService.BackAsync` never raised `Navigated`, and the shell refreshes `CanGoBack` from that event, so the button stayed enabled at the root of the stack until the next forward navigation. Now raised in both directions.
3. **`ThreadDispatcher.RunOnUIThreadAsync` did not await its callback.** The platform dispatch primitive takes a void-returning handler, so passing it an asynchronous callback discarded the inner task and the caller resumed as soon as the callback yielded — meaning a cross-thread navigation did not actually await the page load. Now bridged through a completion source.
4. **`APIClient` built request URLs by string concatenation.** Correct for relative paths, but the payload links related resources by absolute URL, so concatenating produced `{base}/https://host/path`. Replaced with `Uri`'s base-relative resolution, which handles both forms, plus an explicit guard rejecting anything outside the configured origin.
5. **`EventAggregator` delivered messages out of order.** Each subscription owned an independent action block consuming on the thread pool, so messages handled by two different subscriptions could arrive in either order. Because the shell handles busy and completion notifications through separate subscriptions, a completion could overtake its own busy notification — leaving the progress indicator visible with stale text for the rest of the session. Rewritten for ordered, synchronous fan-out, with a test that reproduced the race before the fix. **Found by running the application**, not by the suite: the synchronous test double was more reliable than production, so every test passed against ordering guarantees the real implementation never made.

Also changed: `Shell.xaml` bound the boolean `CanGoBack` straight to `Visibility` without the converter sitting in the same resource dictionary, so the back button never appeared; `IAPIClient` gained cancellation tokens; page ViewModels are registered `ExternallyOwned` so the container does not retain one disposable instance per navigation; `.Services.IntegrationTests` had no xUnit runner adapter, so its tests were never actually discovered or run; and the `Welcome`/`PageA` demo pages were removed.

XML documentation is enforced as a build gate — `GenerateDocumentationFile` with `CS1591` promoted to an error in every project — which also meant backfilling the scaffold's undocumented public surface.

## Limitations

- **No persistence.** The cache is in-memory and dies with the process, as the exercise allows. A cold start always re-fetches.
- **One related category.** Characters, as the requirement asks for one. `RelatedResourceKind` already covers all five and `IFilmService` takes it as a parameter, so a selector is a UI change only.
- **N+1 requests for characters.** Unavoidable — the API publishes no batch endpoint. Mitigated by bounded concurrency, per-URL caching and incremental rendering, not eliminated.
- **A resource URL the client cannot reach is skipped, not surfaced.** It is logged, and the row is simply absent. That keeps one malformed entry from costing the whole list, but a user has no way to tell a short list from a complete one.
- **The base address is hardcoded** in `ApplicationConfiguration`, standing in for real configuration storage.
- **English only.** All strings are externalized to `Resources.resw`, but only `en` exists.
- **`StreamedImage` is unused.** This API has no imagery. See the note on the MET option below.
- **No retry backoff.** Retry is user-driven; there is no automatic exponential backoff.
- **Integration tests need the network** and a healthy third-party service. Excluded with `--filter "Category!=Integration"`.
- **Views, converters and the platform services are not unit-tested.** They are unreachable from a .NET test host; a UWP test app would be needed. Covered by the manual checklist instead.
- **The listed related category is a constant**, not a bound property, so switching from characters to planets means editing the ViewModel. The service already takes the category as a parameter, so exposing a selector is a UI change only.
- **Parts of the manual checklist remain unrun.** The app has been installed and exercised — both pages, navigation, the progress indicator and the opening crawl are confirmed working on a real device — but the accessibility and layout items in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) §11 (Narrator announcements, keyboard traversal, light/dark themes, ~320 epx narrow window) and the forced-failure retry loop have not been walked through.

## How I would extend it

- **A category selector on page 2**, turning the `ListedResourceKind` constant into a bound property.
- **Incremental loading** via `ISupportIncrementalLoading` for a collection large enough to need it — the MET option's bonus requirement.
- **Automatic retry with backoff** inside `BusyOperationRunner`, prompting only after transient attempts are exhausted.
- **Persistence** behind `IFilmService` as a second cache layer, with no change above it.
- **A second API source.** Because the ViewModels depend on `IFilmService` and never on the wire format, adding the MET collection means one new service plus its data transfer objects.
- **Images**, which need care: see below.

## Important considerations

**The API's related resources are absolute URLs, while calling code naturally writes short relative paths, so the client has to accept both.** The scaffold built URLs by string concatenation, which turns an absolute URL into `{base}/https://host/path` — a request that fails looking like a server fault. `Uri`'s base-relative constructor resolves both forms correctly, so that is what the client now uses.

Resolution alone is not enough: it will happily accept an absolute URL pointing anywhere, which would let a payload send the client to a host of its choosing. Every resolved URI is therefore checked to sit beneath the configured base address and rejected loudly otherwise. Keeping both concerns in `RequestUriResolver`, behind the client, means `FilmService` never needs to know the API's address at all.

**A camel-case contract resolver does not map snake_case.** This API uses `episode_id` and `opening_crawl`, so the data transfer objects carry explicit `[JsonProperty]` names. The service tests deserialize real captured payloads through the production serializer settings precisely so a naming mistake fails a test rather than silently yielding default values.

**If the MET option were chosen, images would need a second client.** Thumbnails are served from `images.metmuseum.org`, a different host from `collectionapi.metmuseum.org`, and every call this client makes is bound to its configured origin — so it cannot fetch them at all. That is why a foreign origin is rejected loudly rather than attempted.

**UI-thread affinity is explicit.** Bound collections are only mutated through `IThreadDispatcher`, which is also what makes incremental updates deterministic in tests.

**The UWP project file does not glob.** Every added or removed file needs its `<Compile>` or `<Page>` entry maintained by hand.

**Every request carries a correlation identifier** (`X-Correlation-Id`) from `ActionContext`, shared across a logical operation including the character fan-out, so a client log line can be matched against server logs.
