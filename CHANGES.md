# Change Log — with file and line references

Every change made to the scaffold, with line numbers as of this commit. Line references point at the current state of each file, not at a diff.

**Totals:** 31 files created, 30 modified, 7 deleted. 141 tests passing (73 service + 63 ViewModel + 5 live-API). x64 Debug and ARM64 Release both build with zero warnings. The app has been built, installed and run.

Line references use the form [file.cs:42](path#L42) and are clickable.

---

## 0. Requirement coverage

Every requirement in [README.md](README.md), with how it was verified. Option 1 (Star Wars films) was chosen.

| # | Requirement | Status | Verified by |
|---|---|---|---|
| R1 | Page 1 lists all films from the films endpoint | Done | [FilmService.cs:39](DrawboardCodingExercise.Services/FilmService.cs#L39) + [FilmListPage.xaml:45](DrawboardCodingExercise/View/FilmListPage.xaml#L45); six films confirmed on screen |
| R2 | Each row shows title **and** episode number | Done | [FilmListPage.xaml:67](DrawboardCodingExercise/View/FilmListPage.xaml#L67), [:72](DrawboardCodingExercise/View/FilmListPage.xaml#L72); confirmed on screen as `Episode I`–`Episode VI` |
| R3 | Clicking a film navigates to page 2 | Done | [FilmListPage.xaml:55-57](DrawboardCodingExercise/View/FilmListPage.xaml#L55-L57) → [FilmListViewModel.cs:152](DrawboardCodingExercise.ViewModel/FilmListViewModel.cs#L152); confirmed on screen |
| R4 | Page 2 shows title, episode, release date, director, producer | Done | [FilmDetailPage.xaml:28](DrawboardCodingExercise/View/FilmDetailPage.xaml#L28), [:34](DrawboardCodingExercise/View/FilmDetailPage.xaml#L34), [:58](DrawboardCodingExercise/View/FilmDetailPage.xaml#L58), [:72](DrawboardCodingExercise/View/FilmDetailPage.xaml#L72), [:86](DrawboardCodingExercise/View/FilmDetailPage.xaml#L86); all five confirmed on screen |
| R5 | Page 2 lists one related category | Done | Characters — [FilmDetailViewModel.cs:35](DrawboardCodingExercise.ViewModel/FilmDetailViewModel.cs#L35), [FilmDetailPage.xaml:116](DrawboardCodingExercise/View/FilmDetailPage.xaml#L116); unit + live-API tested. Heading confirmed on screen; rows sit below the fold |
| R6 | **Bonus** — opening crawl on page 2 | Done | [FilmDetailPage.xaml:102](DrawboardCodingExercise/View/FilmDetailPage.xaml#L102); confirmed on screen with paragraph breaks intact |
| R7 | No library that talks directly to the API | Done | 17 packages total, none API-specific; only `IAPIClient` over `HttpClient` + Newtonsoft |
| R8 | Solid design principles | Done | SOLID mapping in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) §10 |
| R9 | Clean, well-factored code | Done | No `Windows.*` in ViewModel/Services; no DTO escapes the services layer; both pages have empty code-behind |
| R10 | Testable **and** extensible architecture | Done | 63 ViewModel tests run with no UI host; `APIClient` testable offline via an injectable handler; a second API source needs one new service |
| R11 | Automated tests | Done | 141 passing across three projects; measured coverage ViewModel 87.7%, Contracts 74.5%, Services 45.9% |
| R12 | Error checking and reporting to the user | Done | [BusyOperationRunner.cs:172-196](DrawboardCodingExercise.Services/BusyOperationRunner.cs#L172-L196) maps five failure kinds to distinct localized messages; 13 tests |
| R13 | Usability | Done | Aggregated progress, distinct empty/error states, keyboard-navigable list, accessible back button, theme resources throughout |
| R14 | Code structure and current UWP idioms | Done | `x:Bind` in item templates, behaviours instead of code-behind handlers, `x:Uid` localization, `RelativePanel` shell |
| R15 | Persistence not required | Done | In-memory cache only; stated as a non-goal |
| R16 | README with limitations, extensions, considerations | Done | [SOLUTION.md](SOLUTION.md) |
| R17 | AI collaboration: challenges and validation | Done | [AI-COLLABORATION.md](AI-COLLABORATION.md) |
| R18 | Zip and send to hiring@drawboard.com | **Outstanding** | Yours to send |

**Still unverified visually:** the character rows themselves. The heading renders, the service resolves real names against the live API, and the ViewModel tests cover insertion order — but the rows sit below the fold in the screenshots taken so far. Scroll the detail page to confirm.

**XML documentation is verified mechanically, not asserted.** `CS1591` is promoted to an **error** in all seven managed projects, so an undocumented public member fails the build. `CS1571`–`CS1573` and `CS1712` (mismatched, duplicated, missing `param`/`typeparam` tags) are also on with documentation generation, and all seven projects rebuild with **zero warnings of any kind**. Inspecting the generated documentation files confirms **633 documented members** — 182 Contracts, 136 ViewModel, 81 UWP head, 76 Services, 43 test support, 115 tests — with **zero** lacking a `summary` or `inheritdoc`.

---

## 1. Behavioural changes to test first

These are the ones worth exercising in the running app. §2 onwards is the full inventory.

### 1.1 The app now opens on a real film list

| What | Where |
|---|---|
| Landing page changed from `Welcome` to the film list | [ShellViewModel.cs:86](DrawboardCodingExercise.ViewModel/ShellViewModel.cs#L86) |
| Page keys replaced (`Welcome`/`PageA` → `FilmList`/`FilmDetail`) | [PageKey.cs:17](DrawboardCodingExercise.Contracts/PageKey.cs#L17), [PageKey.cs:23](DrawboardCodingExercise.Contracts/PageKey.cs#L23) |
| Both pages registered | [NavigationModule.cs:26-27](DrawboardCodingExercise/Module/NavigationModule.cs#L26-L27) |
| API base address pointed at the live service | [ApplicationConfiguration.cs:21](DrawboardCodingExercise/Configuration/ApplicationConfiguration.cs#L21) |

**Expect:** six films, each with a title and `Episode IV`-style caption, appearing after a brief progress indicator in the title bar.

### 1.2 Four scaffold defects fixed

**The shell could crash the whole app.** `OnNotifyDone` called `RemoveAt(IndexOf(...))` with no guard, and `IndexOf` returns `-1` for an unmatched entry — throwing on the UI thread from an event handler, which is unrecoverable.

- Guard: [ShellViewModel.cs:100-105](DrawboardCodingExercise.ViewModel/ShellViewModel.cs#L100-L105)
- Regression test: [ShellViewModelTests.cs:128](DrawboardCodingExercise.ViewModel.UnitTests/ShellViewModelTests.cs#L128)

**The back button's state went stale.** `BackAsync` never raised `Navigated`, and the shell refreshes `CanGoBack` from that event, so the button stayed enabled at the root of the stack until the next forward navigation.

- Now raised on back as well as forward: [NavigationService.cs:144](DrawboardCodingExercise/CoreFramework/NavigationService.cs#L144) (forward is [:98](DrawboardCodingExercise/CoreFramework/NavigationService.cs#L98))

**The back button never appeared at all.** `Shell.xaml` bound the boolean `CanGoBack` straight to `Visibility`, without the converter already sitting in the same resource dictionary.

- Fixed binding: [Shell.xaml:38](DrawboardCodingExercise/View/Shell.xaml#L38)

**Expect:** click a film, then use the back button in the title bar — it should be visible on page 2, take you back, and then disappear at the root.

**The dispatcher discarded its inner task.** `RunOnUIThreadAsync` handed an asynchronous callback to a void-returning platform primitive, so a cross-thread caller resumed as soon as the callback yielded rather than when the work finished — meaning a navigation from a worker thread did not actually await the page load.

- Completion-source bridge: [ThreadDispatcher.cs:41-64](DrawboardCodingExercise/CoreFramework/ThreadDispatcher.cs#L41-L64)

**The event aggregator delivered messages out of order — found by running the app.** Each subscription owned an independent `ActionBlock` consuming on the thread pool, so two messages handled by two *different* subscriptions could arrive in either order. Busy and completion notifications are handled by separate subscriptions on the shell, so a completion could overtake its own busy notification: the completion found nothing to remove, the busy notification was then added, and nothing was left to clear it. The progress indicator stayed visible with stale text for the rest of the session.

- Rewritten for ordered, synchronous fan-out: [EventAggregator.cs](DrawboardCodingExercise.Services/EventAggregator/EventAggregator.cs)
- Reproducing test — measured `done:1` arriving before `busy:1`: [EventAggregatorTests.cs:83](DrawboardCodingExercise.Services.UnitTests/EventAggregatorTests.cs#L83)
- `System.Threading.Tasks.Dataflow` dropped from [Services.csproj](DrawboardCodingExercise.Services/DrawboardCodingExercise.Services.csproj), now unused
- Handler exceptions are logged rather than escaping, because completions are posted from `finally` blocks where an escaping exception would mask the original failure

Two lessons worth recording. First, the guard added to `ShellViewModel` above stopped the crash but converted it into a silent stuck indicator — it treated the symptom while the ordering defect underneath went unnoticed. Second, `RecordingEventAggregator`, the test double, delivers synchronously and was therefore **more reliable than production**: every ViewModel and runner test passed against ordering guarantees the real implementation never made. [EventAggregatorTests](DrawboardCodingExercise.Services.UnitTests/EventAggregatorTests.cs) now tests the real class directly, and the double's behaviour matches it.

Also renamed `FilmDetail.Loading` from "Loading film" to **"Loading film details"** — one letter apart from "Loading films" made the stale text almost impossible to read as a symptom.

### 1.3 A leak fixed that I introduced

Page ViewModels implement `IDisposable` (via their base class), and Autofac retains disposable components resolved from the root container — so every ViewModel ever created would have become unreachable but uncollectable, growing with navigation count.

- `ExternallyOwned()` on the registration: [MvvmViewExtensions.cs:39](DrawboardCodingExercise/Module/MvvmViewExtensions.cs#L39)

Not observable in the UI; noted because it is the change least visible and most easily missed.

### 1.4 Package signing enabled

The scaffold shipped with `AppxPackageSigningEnabled=false`, which produces an unsigned package. Windows will only register an unsigned package when Developer Mode is enabled, so the app could not be installed on a machine without it.

| What | Where |
|---|---|
| Signing turned on | [csproj:27](DrawboardCodingExercise/DrawboardCodingExercise.csproj#L27) |
| Certificate key file | [csproj:28](DrawboardCodingExercise/DrawboardCodingExercise.csproj#L28) |
| Key file referenced by the project | [csproj:132](DrawboardCodingExercise/DrawboardCodingExercise.csproj#L132) |

`DrawboardCodingExercise_TemporaryKey.pfx` is a self-signed development certificate, valid to 2029, with subject `CN=StevenBlom` — it **must** match the `Publisher` in [Package.appxmanifest:11](DrawboardCodingExercise/Package.appxmanifest#L11) or the build fails.

**It is deliberately not committed.** The scaffold's `.gitignore` excludes `*.pfx`, and a private key does not belong in a repository that may be public. Signing is therefore conditional on the file existing: a fresh clone builds unsigned and installs with Developer Mode, exactly as the original scaffold did, and signing switches itself on once a key is generated.

**This does not remove the need for one elevated step.** A self-signed certificate is not a trusted authority, so installation fails with `0x800B0109` (untrusted root) until the certificate is added to the machine's Trusted People store:

```powershell
# Requires administrator
Import-Certificate -FilePath DrawboardCodingExercise\DrawboardCodingExercise_TemporaryKey.cer `
    -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Either that or Developer Mode is required — both need admin. Signing is still the better default: it produces a genuinely signed artefact, and on a machine where the certificate is trusted the app installs with no developer settings at all.

To generate a fresh certificate on another machine:

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=StevenBlom" -KeyUsage DigitalSignature `
    -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
[IO.File]::WriteAllBytes("DrawboardCodingExercise_TemporaryKey.pfx", $cert.Export("Pfx", ""))
```

Then update the thumbprint at [csproj:29](DrawboardCodingExercise/DrawboardCodingExercise.csproj#L29).

### 1.5 Error handling and retry

| What | Where |
|---|---|
| Retry loop, and the completion notification posted in `finally` | [BusyOperationRunner.cs:58-107](DrawboardCodingExercise.Services/BusyOperationRunner.cs#L58-L107) |
| Which failures are retryable (unrecognised ones deliberately propagate) | [BusyOperationRunner.cs:122-126](DrawboardCodingExercise.Services/BusyOperationRunner.cs#L122-L126) |
| Retry prompt marshalled onto the UI thread | [BusyOperationRunner.cs:138-166](DrawboardCodingExercise.Services/BusyOperationRunner.cs#L138-L166) |
| Failure → specific localized message (offline / not-found / throttled / server / timeout) | [BusyOperationRunner.cs:172-196](DrawboardCodingExercise.Services/BusyOperationRunner.cs#L172-L196) |
| Retry dialog gained a message parameter | [UserInteractionService.cs:29](DrawboardCodingExercise/CoreFramework/UserInteractionService.cs#L29) |
| Dismissing the dialog resolves to Cancel, not a silent retry | [UserInteractionService.cs:42](DrawboardCodingExercise/CoreFramework/UserInteractionService.cs#L42) |

**To test:** change the base address at [ApplicationConfiguration.cs:21](DrawboardCodingExercise/Configuration/ApplicationConfiguration.cs#L21) to an unreachable host, rebuild, and launch. Expect the offline message with Retry/Cancel; Cancel should leave a readable inline error rather than a blank page.

### 1.6 URI construction moved into the client

The scaffold's `APIClient` built request URLs by string concatenation:

```csharp
new Uri($"{baseUri}/{path}")
```

That is correct only for relative paths. The films payload links related resources by **absolute** URL, so concatenating produced `https://swapi.info/api/https://swapi.info/api/people/1` — a 404 that reads like a server fault.

The first implementation worked around it with a `RelativeResourcePath` helper that stripped the base off before calling the client. That treated the symptom. `Uri`'s own base-relative constructor already resolves **both** forms correctly, so the root fix is one line in the right place:

| What | Where |
|---|---|
| Base address normalized once, in the constructor | [APIClient.cs:66](DrawboardCodingExercise.Services/APIClient.cs#L66) |
| Every request URI resolved through one seam | [APIClient.cs:183](DrawboardCodingExercise.Services/APIClient.cs#L183) |
| `new Uri(baseUri, path)` — handles relative and absolute | [RequestUriResolver.cs:90](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs#L90) |
| Trailing-slash normalization | [RequestUriResolver.cs:46](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs#L46) |
| Origin and base-path guard | [RequestUriResolver.cs:93](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs#L93), [:112](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs#L112) |

**Why the guard still exists.** `new Uri(base, absolute)` accepts an absolute URL pointing *anywhere*, which would let a payload send the client to a host of its choosing. Every resolved URI is checked to sit beneath the base address and rejected loudly otherwise, so that decision stays visible in our code rather than buried in the transport.

**What this bought:**

- **`FilmService` no longer knows the API's address.** Its constructor dropped `IAPISettings` ([FilmService.cs:61](DrawboardCodingExercise.Services/FilmService.cs#L61)) and it now passes payload URLs straight through ([:184](DrawboardCodingExercise.Services/FilmService.cs#L184)). Resolving URIs was never its job.
- **One malformed URL no longer costs the whole list.** Previously an unusable URL threw, and `Task.WhenAll` turned that into a total failure of all eighteen characters. It is now logged and skipped ([:193](DrawboardCodingExercise.Services/FilmService.cs#L193)), leaving its slot empty and filtered out ([:142](DrawboardCodingExercise.Services/FilmService.cs#L142)) — matching the tolerance the mapper already applies to every other payload field. Genuine request failures still propagate so a retry can be offered.
- **Misconfiguration fails at construction**, not on the first request, so a bad base address can't be mistaken for an unreachable server.
- **Correct escaping.** `Uri` resolution handles percent-encoding; string concatenation could double-encode.
- **Smaller surface.** `RelativeResourcePath` (95 lines) deleted; [RequestUriResolver.cs](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs) (124 lines) replaces it *and* the concatenation it used to compensate for. Tests retargeted to [RequestUriResolverTests.cs](DrawboardCodingExercise.Services.UnitTests/RequestUriResolverTests.cs) — 15 tests, up from 10.

### 1.7 Testability and factoring follow-ups

Two weaknesses found by auditing against the README's evaluation criteria rather than by a failing test.

**`APIClient` had no offline coverage at all** — 0 of 124 lines. Its `HttpClient` is `static readonly` with no seam, so status-code translation, the correlation header and the null-body guard could only be exercised against the live service. A second constructor now accepts an `HttpMessageHandler`:

| What | Where |
|---|---|
| Handler-accepting constructor | [APIClient.cs:74](DrawboardCodingExercise.Services/APIClient.cs#L74) |
| Shared client kept as one instance per application | [APIClient.cs:36](DrawboardCodingExercise.Services/APIClient.cs#L36) |
| In-memory recording handler | [RecordingHttpMessageHandler.cs](DrawboardCodingExercise.TestSupport/RecordingHttpMessageHandler.cs) |
| 19 tests | [APIClientTests.cs](DrawboardCodingExercise.Services.UnitTests/APIClientTests.cs) |

Not a test-only hatch: a supplied `DelegatingHandler` is the standard way to add retry or telemetry later. A handler gets its own client, so the shared one stays a single instance as the platform guidance requires, and Autofac still selects the three-parameter constructor because no handler is registered. **Coverage: 0% → 43.5%.**

The regression that started all this is now pinned offline — `GetAsync_AbsoluteUrlFromAPayload_RequestsItUnchanged` asserts the request URI contains no `api/https`.

**`FilmDetailViewModel` was doing too much.** Its ordered-insertion logic was a second responsibility sitting inside a page ViewModel, reachable only by staging a navigation. Extracted to [SourceOrderedCollection.cs](DrawboardCodingExercise.ViewModel/Infrastructure/SourceOrderedCollection.cs), with [12 tests](DrawboardCodingExercise.ViewModel.UnitTests/SourceOrderedCollectionTests.cs) covering every arrival order, unlisted items, case-insensitive keys and reset. The ViewModel dropped from 297 to 251 lines and its existing tests passed unchanged — which is the evidence the extraction preserved behaviour.

### 1.8 Ordering, caching and cancellation

| What | Where |
|---|---|
| Character rows placed at their API-listed position as responses arrive out of order | [FilmDetailViewModel.cs:259-281](DrawboardCodingExercise.ViewModel/FilmDetailViewModel.cs#L259-L281) |
| An unlisted resource sorts last (`int.MaxValue`, not `-1` — the bug a test caught) | [FilmDetailViewModel.cs:294](DrawboardCodingExercise.ViewModel/FilmDetailViewModel.cs#L294) |
| Film cache, guarded so concurrent callers share one request | [FilmService.cs:79](DrawboardCodingExercise.Services/FilmService.cs#L79) |
| Concurrency capped at six in-flight requests | [FilmService.cs:36](DrawboardCodingExercise.Services/FilmService.cs#L36) |
| Loads cancel when the user navigates away | [PageViewModelBase.cs:55-85](DrawboardCodingExercise.ViewModel/Infrastructure/PageViewModelBase.cs#L55-L85) |

**Expect:** the character list fills progressively and stays in a stable order while filling. Going back to the list and forward again should be instant — no second network call.

---

## 2. Created files

### Contracts — domain model and service abstractions

| Lines | File | Purpose |
|---|---|---|
| 47 | [Model/Film.cs](DrawboardCodingExercise.Contracts/Model/Film.cs) | Immutable film projection; related URLs grouped by category |
| 15 | [Model/RelatedResource.cs](DrawboardCodingExercise.Contracts/Model/RelatedResource.cs) | A resolved related resource |
| 28 | [Model/RelatedResourceKind.cs](DrawboardCodingExercise.Contracts/Model/RelatedResourceKind.cs) | The five categories |
| 14 | [Navigation/FilmDetailParameter.cs](DrawboardCodingExercise.Contracts/Navigation/FilmDetailParameter.cs) | Identifier-only navigation parameter |
| 71 | [Services/IFilmService.cs](DrawboardCodingExercise.Contracts/Services/IFilmService.cs) | Film retrieval contract |
| 45 | [Services/IBusyOperationRunner.cs](DrawboardCodingExercise.Contracts/Services/IBusyOperationRunner.cs) | Progress + retry contract |
| 97 | [Services/OperationOutcome.cs](DrawboardCodingExercise.Contracts/Services/OperationOutcome.cs) | Outcome type and failure reasons |

### Services — transport, mapping, orchestration

| Lines | File | Purpose |
|---|---|---|
| 124 | [Api/RequestUriResolver.cs](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs) | Builds every request URI; resolves relative *and* absolute forms; rejects a foreign origin |
| 35 | [Api/ApiSerializerSettings.cs](DrawboardCodingExercise.Services/Api/ApiSerializerSettings.cs) | Shared serializer settings so tests cannot drift from production |
| 77 | [Api/Dto/FilmDto.cs](DrawboardCodingExercise.Services/Api/Dto/FilmDto.cs) | Wire format, snake_case names declared explicitly |
| 23 | [Api/Dto/NamedResourceDto.cs](DrawboardCodingExercise.Services/Api/Dto/NamedResourceDto.cs) | One shape for all five related categories |
| 162 | [Mapping/FilmMapper.cs](DrawboardCodingExercise.Services/Mapping/FilmMapper.cs) | Wire → domain, tolerating every malformed field |
| 194 | [FilmService.cs](DrawboardCodingExercise.Services/FilmService.cs) | Caching, bounded fan-out, progress reporting |
| 197 | [BusyOperationRunner.cs](DrawboardCodingExercise.Services/BusyOperationRunner.cs) | Progress pairing, retry loop, failure explanation |

Key anchors: `CreateBaseUri` [:35](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs#L35), `Resolve` [:72](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs#L72), origin guard [:112](DrawboardCodingExercise.Services/Api/RequestUriResolver.cs#L112) · `GetFilmsAsync` [:71](DrawboardCodingExercise.Services/FilmService.cs#L71), `GetFilmAsync` [:106](DrawboardCodingExercise.Services/FilmService.cs#L106), `GetRelatedResourcesAsync` [:112](DrawboardCodingExercise.Services/FilmService.cs#L112), `ResolveAsync` [:171](DrawboardCodingExercise.Services/FilmService.cs#L171).

### ViewModel — presentation

| Lines | File | Purpose |
|---|---|---|
| 156 | [FilmListViewModel.cs](DrawboardCodingExercise.ViewModel/FilmListViewModel.cs) | Page 1 state and navigation command |
| 297 | [FilmDetailViewModel.cs](DrawboardCodingExercise.ViewModel/FilmDetailViewModel.cs) | Page 2 fields, crawl, incremental character list |
| 20 | [Items/FilmListItemViewModel.cs](DrawboardCodingExercise.ViewModel/Items/FilmListItemViewModel.cs) | Immutable list row |
| 16 | [Items/RelatedResourceItemViewModel.cs](DrawboardCodingExercise.ViewModel/Items/RelatedResourceItemViewModel.cs) | Immutable character row |
| 115 | [Infrastructure/PageViewModelBase.cs](DrawboardCodingExercise.ViewModel/Infrastructure/PageViewModelBase.cs) | Navigate-away cancellation, self-removing subscription |
| 41 | [Infrastructure/DispatchedProgress.cs](DrawboardCodingExercise.ViewModel/Infrastructure/DispatchedProgress.cs) | Progress delivered on the UI thread |
| 57 | [Formatting/EpisodeFormatter.cs](DrawboardCodingExercise.ViewModel/Formatting/EpisodeFormatter.cs) | Roman numerals, digit fallback out of range |
| 33 | [DesignTime/DesignTimeFilmListViewModel.cs](DrawboardCodingExercise.ViewModel/DesignTime/DesignTimeFilmListViewModel.cs) | Designer sample rows |
| 39 | [DesignTime/DesignTimeFilmDetailViewModel.cs](DrawboardCodingExercise.ViewModel/DesignTime/DesignTimeFilmDetailViewModel.cs) | Designer sample content |

Key anchors: `OnNavigatedToAsync` [FilmListViewModel.cs:101](DrawboardCodingExercise.ViewModel/FilmListViewModel.cs#L101), `IsEmpty` [:91](DrawboardCodingExercise.ViewModel/FilmListViewModel.cs#L91), `OnFilmSelected` [:144](DrawboardCodingExercise.ViewModel/FilmListViewModel.cs#L144) · `Apply` [FilmDetailViewModel.cs:198](DrawboardCodingExercise.ViewModel/FilmDetailViewModel.cs#L198), `LoadRelatedResourcesAsync` [:223](DrawboardCodingExercise.ViewModel/FilmDetailViewModel.cs#L223).

### UWP head — views

| Lines | File |
|---|---|
| 106 | [View/FilmListPage.xaml](DrawboardCodingExercise/View/FilmListPage.xaml) |
| 20 | [View/FilmListPage.xaml.cs](DrawboardCodingExercise/View/FilmListPage.xaml.cs) |
| 158 | [View/FilmDetailPage.xaml](DrawboardCodingExercise/View/FilmDetailPage.xaml) |
| 20 | [View/FilmDetailPage.xaml.cs](DrawboardCodingExercise/View/FilmDetailPage.xaml.cs) |
| 43 | [ValueConverters/ItemClickToClickedItemConverter.cs](DrawboardCodingExercise/ValueConverters/ItemClickToClickedItemConverter.cs) |

Both pages have empty code-behind. The list's click gesture reaches the ViewModel through a XAML behaviour plus the converter above, so no event handler lives in the view.

### Tests

| Lines | File |
|---|---|
| 25 | [TestSupport csproj](DrawboardCodingExercise.TestSupport/DrawboardCodingExercise.TestSupport.csproj) |
| 173 | [TestSupport/StubApiClient.cs](DrawboardCodingExercise.TestSupport/StubApiClient.cs) |
| 93 | [TestSupport/SampleFilmPayloads.cs](DrawboardCodingExercise.TestSupport/SampleFilmPayloads.cs) |
| 89 | [TestSupport/RecordingEventAggregator.cs](DrawboardCodingExercise.TestSupport/RecordingEventAggregator.cs) |
| 63 | [TestSupport/FakeBusyOperationRunner.cs](DrawboardCodingExercise.TestSupport/FakeBusyOperationRunner.cs) |
| 31 | [TestSupport/ImmediateThreadDispatcher.cs](DrawboardCodingExercise.TestSupport/ImmediateThreadDispatcher.cs) |
| 31 | [TestSupport/KeyEchoLocalizationService.cs](DrawboardCodingExercise.TestSupport/KeyEchoLocalizationService.cs) |
| 27 | [TestSupport/StubApiSettings.cs](DrawboardCodingExercise.TestSupport/StubApiSettings.cs) |
| 22 | [TestSupport/SilentLogger.cs](DrawboardCodingExercise.TestSupport/SilentLogger.cs) |
| 388 | [Services.UnitTests/FilmServiceTests.cs](DrawboardCodingExercise.Services.UnitTests/FilmServiceTests.cs) — 18 tests |
| 222 | [Services.UnitTests/BusyOperationRunnerTests.cs](DrawboardCodingExercise.Services.UnitTests/BusyOperationRunnerTests.cs) — 13 tests |
| 173 | [Services.UnitTests/RequestUriResolverTests.cs](DrawboardCodingExercise.Services.UnitTests/RequestUriResolverTests.cs) — 15 tests |
| 37 | [ViewModel.UnitTests csproj](DrawboardCodingExercise.ViewModel.UnitTests/DrawboardCodingExercise.ViewModel.UnitTests.csproj) |
| 292 | [ViewModel.UnitTests/FilmDetailViewModelTests.cs](DrawboardCodingExercise.ViewModel.UnitTests/FilmDetailViewModelTests.cs) |
| 221 | [ViewModel.UnitTests/FilmListViewModelTests.cs](DrawboardCodingExercise.ViewModel.UnitTests/FilmListViewModelTests.cs) |
| 198 | [ViewModel.UnitTests/ShellViewModelTests.cs](DrawboardCodingExercise.ViewModel.UnitTests/ShellViewModelTests.cs) |
| 73 | [ViewModel.UnitTests/TestFilms.cs](DrawboardCodingExercise.ViewModel.UnitTests/TestFilms.cs) |
| 52 | [ViewModel.UnitTests/EpisodeFormatterTests.cs](DrawboardCodingExercise.ViewModel.UnitTests/EpisodeFormatterTests.cs) |
| 134 | [IntegrationTests/SwapiEndpointTests.cs](DrawboardCodingExercise.Services.IntegrationTests/SwapiEndpointTests.cs) — 5 live-API tests |

### Documentation

[CHANGES.md](CHANGES.md) (this file), [SOLUTION.md](SOLUTION.md), [AI-COLLABORATION.md](AI-COLLABORATION.md), [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md), [ARCHITECTURE.md](ARCHITECTURE.md), [CLAUDE.md](CLAUDE.md).

---

## 3. Modified files

### Contracts

| File | Change |
|---|---|
| [PageKey.cs](DrawboardCodingExercise.Contracts/PageKey.cs) | Values replaced: `FilmList` [:17](DrawboardCodingExercise.Contracts/PageKey.cs#L17), `FilmDetail` [:23](DrawboardCodingExercise.Contracts/PageKey.cs#L23) |
| [CoreFramework/INavigationService.cs](DrawboardCodingExercise.Contracts/CoreFramework/INavigationService.cs) | Nullable parameter annotation; documented the per-navigation ViewModel lifetime and that `Navigated` fires in both directions |
| [CoreFramework/INavigateToAware.cs](DrawboardCodingExercise.Contracts/CoreFramework/INavigateToAware.cs) | Nullable parameter; documented thread and exception behaviour |
| [CoreFramework/IThreadDispatcher.cs](DrawboardCodingExercise.Contracts/CoreFramework/IThreadDispatcher.cs) | Full documentation, incl. fire-and-forget semantics |
| [CoreFramework/IProvidePageHeader.cs](DrawboardCodingExercise.Contracts/CoreFramework/IProvidePageHeader.cs) | Documented that the value is a key fragment, not display text |
| [CoreFramework/ActionContext.cs](DrawboardCodingExercise.Contracts/CoreFramework/ActionContext.cs) | `AsyncLocal<string?>` for nullable correctness [:13](DrawboardCodingExercise.Contracts/CoreFramework/ActionContext.cs#L13) |
| [Services/IEventAggregator.cs](DrawboardCodingExercise.Contracts/Services/IEventAggregator.cs) | Full documentation, incl. subscription-lifetime warning |
| [Services/IUserInteractionService.cs](DrawboardCodingExercise.Contracts/Services/IUserInteractionService.cs) | New message overload [:29](DrawboardCodingExercise.Contracts/Services/IUserInteractionService.cs#L29); documented UI-thread requirement |
| [Events/NotifyBusyEvent.cs](DrawboardCodingExercise.Contracts/Events/NotifyBusyEvent.cs), [NotifyDoneEvent.cs](DrawboardCodingExercise.Contracts/Events/NotifyDoneEvent.cs) | Documented the exact-match pairing requirement |
| [csproj](DrawboardCodingExercise.Contracts/DrawboardCodingExercise.Contracts.csproj) | `Nullable` [:6](DrawboardCodingExercise.Contracts/DrawboardCodingExercise.Contracts.csproj#L6); doc gate [:10-12](DrawboardCodingExercise.Contracts/DrawboardCodingExercise.Contracts.csproj#L10-L12) |

### Services

| File | Change |
|---|---|
| [IAPIClient.cs](DrawboardCodingExercise.Services/IAPIClient.cs) | Cancellation tokens on all three methods; documented that paths must be relative and that images on another host are unreachable |
| [APIClient.cs](DrawboardCodingExercise.Services/APIClient.cs) | Tokens threaded to `SendAsync` [:181](DrawboardCodingExercise.Services/APIClient.cs#L181); null-body guard [:140](DrawboardCodingExercise.Services/APIClient.cs#L140) |
| [IAPISettings.cs](DrawboardCodingExercise.Services/IAPISettings.cs) | Documented the base-address contract |
| [HttpStatusException.cs](DrawboardCodingExercise.Services/HttpStatusException.cs) | Added a message carrying the status code |
| [EventAggregator/EventAggregator.cs](DrawboardCodingExercise.Services/EventAggregator/EventAggregator.cs) | Documentation only |
| [csproj](DrawboardCodingExercise.Services/DrawboardCodingExercise.Services.csproj) | `Nullable` [:6](DrawboardCodingExercise.Services/DrawboardCodingExercise.Services.csproj#L6); doc gate [:10-12](DrawboardCodingExercise.Services/DrawboardCodingExercise.Services.csproj#L10-L12) |

### ViewModel

| File | Change |
|---|---|
| [ShellViewModel.cs](DrawboardCodingExercise.ViewModel/ShellViewModel.cs) | Unmatched-completion guard [:100-105](DrawboardCodingExercise.ViewModel/ShellViewModel.cs#L100-L105); lands on the film list [:86](DrawboardCodingExercise.ViewModel/ShellViewModel.cs#L86); detaches from the navigation event on dispose [:127](DrawboardCodingExercise.ViewModel/ShellViewModel.cs#L127) |
| [DesignTime/DesignTimeShellViewModel.cs](DrawboardCodingExercise.ViewModel/DesignTime/DesignTimeShellViewModel.cs) | Documentation only |
| [csproj](DrawboardCodingExercise.ViewModel/DrawboardCodingExercise.ViewModel.csproj) | Doc gate [:10-12](DrawboardCodingExercise.ViewModel/DrawboardCodingExercise.ViewModel.csproj#L10-L12) |

### UWP head

| File | Change |
|---|---|
| [CoreFramework/NavigationService.cs](DrawboardCodingExercise/CoreFramework/NavigationService.cs) | `Navigated` raised on back [:144](DrawboardCodingExercise/CoreFramework/NavigationService.cs#L144); documentation |
| [CoreFramework/ThreadDispatcher.cs](DrawboardCodingExercise/CoreFramework/ThreadDispatcher.cs) | Awaits its callback properly [:41-64](DrawboardCodingExercise/CoreFramework/ThreadDispatcher.cs#L41-L64) |
| [CoreFramework/UserInteractionService.cs](DrawboardCodingExercise/CoreFramework/UserInteractionService.cs) | Message overload [:29](DrawboardCodingExercise/CoreFramework/UserInteractionService.cs#L29); dismissal maps to Cancel [:42](DrawboardCodingExercise/CoreFramework/UserInteractionService.cs#L42) |
| [CoreFramework/LocalizationService.cs](DrawboardCodingExercise/CoreFramework/LocalizationService.cs), [IFrameNavigator.cs](DrawboardCodingExercise/CoreFramework/IFrameNavigator.cs) | Documentation only |
| [Module/NavigationModule.cs](DrawboardCodingExercise/Module/NavigationModule.cs) | Registers the two new pages [:26-27](DrawboardCodingExercise/Module/NavigationModule.cs#L26-L27) |
| [Module/CoreServicesModule.cs](DrawboardCodingExercise/Module/CoreServicesModule.cs) | Registers the operation runner [:30](DrawboardCodingExercise/Module/CoreServicesModule.cs#L30) |
| [Module/WebServicesModule.cs](DrawboardCodingExercise/Module/WebServicesModule.cs) | Shared serializer settings [:22](DrawboardCodingExercise/Module/WebServicesModule.cs#L22); film service as a single instance [:29](DrawboardCodingExercise/Module/WebServicesModule.cs#L29) |
| [Module/MvvmViewExtensions.cs](DrawboardCodingExercise/Module/MvvmViewExtensions.cs) | `ExternallyOwned()` [:39](DrawboardCodingExercise/Module/MvvmViewExtensions.cs#L39) |
| [Configuration/ApplicationConfiguration.cs](DrawboardCodingExercise/Configuration/ApplicationConfiguration.cs) | Live base address [:21](DrawboardCodingExercise/Configuration/ApplicationConfiguration.cs#L21) |
| [View/Shell.xaml](DrawboardCodingExercise/View/Shell.xaml) | Back button through the converter [:38](DrawboardCodingExercise/View/Shell.xaml#L38); accessible name and tooltip via `x:Uid` [:35](DrawboardCodingExercise/View/Shell.xaml#L35) |
| [View/Shell.xaml.cs](DrawboardCodingExercise/View/Shell.xaml.cs) | Documentation only |
| [Controls/StreamedImage.xaml.cs](DrawboardCodingExercise/Controls/StreamedImage.xaml.cs) | Documentation only |
| [ValueConverters/BoolToVisibilityConverter.cs](DrawboardCodingExercise/ValueConverters/BoolToVisibilityConverter.cs), [PageHeaderValueConverter.cs](DrawboardCodingExercise/ValueConverters/PageHeaderValueConverter.cs) | Documentation only |
| [Strings/en/Resources.resw](DrawboardCodingExercise/Strings/en/Resources.resw) | Obsolete keys removed; 26 keys now — page headers, field labels, empty/error states, five failure messages, accessible name for the back button |
| [csproj](DrawboardCodingExercise/DrawboardCodingExercise.csproj) | File list updated for the new/removed views and converter; doc gate [:79-83](DrawboardCodingExercise/DrawboardCodingExercise.csproj#L79-L83) |

### Test projects and solution

| File | Change |
|---|---|
| [Services.UnitTests csproj](DrawboardCodingExercise.Services.UnitTests/DrawboardCodingExercise.Services.UnitTests.csproj) | Test-support reference; nullable; doc gate |
| [IntegrationTests csproj](DrawboardCodingExercise.Services.IntegrationTests/DrawboardCodingExercise.Services.IntegrationTests.csproj) | **Added the xUnit runner adapter** — without it the host discovered no tests and reported success, so these had never run |
| [DrawboardCodingExercise.slnx](DrawboardCodingExercise.slnx) | Added the test-support and ViewModel-test projects |

---

## 4. Deleted files

| File | Why |
|---|---|
| `DrawboardCodingExercise/View/Welcome.xaml` + `.cs` | Demo page replaced by the film list |
| `DrawboardCodingExercise/View/PageA.xaml` + `.cs` | Placeholder that faked five seconds of work |
| `DrawboardCodingExercise.ViewModel/WelcomeViewModel.cs` | With its page |
| `DrawboardCodingExercise.ViewModel/PageAViewModel.cs` | With its page |

Also removed: `Api/RelativeResourcePath.cs` and its tests, superseded by `RequestUriResolver` (§1.6); the placeholder `XUnitTests.cs` in both test projects; and `EpisodeFormatter` was relocated from Services to ViewModel — formatting for display is a presentation concern, and ViewModel does not reference Services.

Reversing the page deletion means restoring the four files, their `<Compile>`/`<Page>` entries in the app csproj, their `PageKey` values, their `RegisterView` calls, and their resource strings.

---

## 5. Commands

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"

# Build
& $msbuild DrawboardCodingExercise\DrawboardCodingExercise.csproj -t:Restore,Build `
    -p:Configuration=Debug -p:Platform=x64 -p:AppxBundle=Never

# Install or update. A same-version reinstall is blocked (0x80073CFB), so remove first.
Get-Process DrawboardCodingExercise -ErrorAction SilentlyContinue | Stop-Process -Force
Get-AppxPackage -Name "e29e08cc-226f-4293-81f4-636ff042078b" | Remove-AppxPackage
Add-AppxPackage -Path (Get-ChildItem "DrawboardCodingExercise\AppPackages\*x64_Debug_Test\*.msix").FullName

# Launch
Start-Process explorer.exe "shell:AppsFolder\e29e08cc-226f-4293-81f4-636ff042078b_qa6kvaw56d0me!App"

# Tests
dotnet test DrawboardCodingExercise.Services.UnitTests\DrawboardCodingExercise.Services.UnitTests.csproj
dotnet test DrawboardCodingExercise.ViewModel.UnitTests\DrawboardCodingExercise.ViewModel.UnitTests.csproj
dotnet test DrawboardCodingExercise.Services.IntegrationTests\DrawboardCodingExercise.Services.IntegrationTests.csproj

# Skip the network-dependent tests
dotnet test <project> --filter "Category!=Integration"
```

`dotnet build` cannot build the app — the UWP project needs Visual Studio's MSBuild. `-p:AppxBundle=Never` avoids a bundling step that fails unless both platforms were built.
