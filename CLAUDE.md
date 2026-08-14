# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A Drawboard UWP coding exercise, **implemented**. The task (see [README.md](README.md)) was to extend the scaffold with two pages driven by a public REST API, called directly through the provided `IAPIClient`.

Delivered: a film list page and a film detail page against `https://swapi.info`, including the bonus opening crawl. Read [CHANGES.md](CHANGES.md) for every change with line references, [SOLUTION.md](SOLUTION.md) for design decisions and limitations, and [ARCHITECTURE.md](ARCHITECTURE.md) for the scaffold as originally supplied.

## Build & test

`dotnet build` **cannot** build the solution — [DrawboardCodingExercise.csproj](DrawboardCodingExercise/DrawboardCodingExercise.csproj) is a legacy (non-SDK) UWP project that needs Visual Studio's MSBuild and the WindowsXaml targets. Use VS MSBuild (18.x / VS 2026 verified here; locate with `vswhere -latest -property installationPath`):

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"

# App (x64 or ARM64 only — there is no AnyCPU/x86 solution config)
& $msbuild DrawboardCodingExercise\DrawboardCodingExercise.csproj -t:Restore,Build -p:Configuration=Debug -p:Platform=x64 -p:AppxBundle=Never

```

`-p:AppxBundle=Never` matters: building the whole `.slnx` (or the csproj without it) triggers bundling for `AppxBundlePlatforms=x64|arm64`, which fails with `error APPX0502: File '...BundleArtifacts\arm64.txt' not found` unless both platforms were built. The package is signed with the checked-in test certificate (`AppxPackageSigningEnabled=true`), so `Add-AppxPackage` works once that certificate is trusted in `LocalMachine\TrustedPeople` — a one-time admin step. There is no standalone `Deploy` target.

Tests are plain net8.0 projects, so the .NET SDK works on them directly:

```powershell
dotnet test DrawboardCodingExercise.Services.UnitTests\DrawboardCodingExercise.Services.UnitTests.csproj
dotnet test DrawboardCodingExercise.ViewModel.UnitTests\DrawboardCodingExercise.ViewModel.UnitTests.csproj
dotnet test DrawboardCodingExercise.Services.IntegrationTests\DrawboardCodingExercise.Services.IntegrationTests.csproj  # needs network
dotnet test <project> --filter "Category!=Integration"                                                                  # skip network tests
```

xUnit + Shouldly + NSubstitute, 141 tests. Shared test doubles live in `DrawboardCodingExercise.TestSupport`, referenced by all three test projects — put a new double there rather than duplicating it. A test project without `xunit.runner.visualstudio` silently discovers **no** tests and reports success, which is how the integration tests went unrun in the original scaffold.

**Installing and running the app** (the certificate is already trusted; a same-version reinstall is blocked, so remove first):

```powershell
Get-Process DrawboardCodingExercise -ErrorAction SilentlyContinue | Stop-Process -Force
Get-AppxPackage -Name "e29e08cc-226f-4293-81f4-636ff042078b" | Remove-AppxPackage
Add-AppxPackage -Path (Get-ChildItem "DrawboardCodingExercise\AppPackages\*x64_Debug_Test\*.msix").FullName
Start-Process explorer.exe "shell:AppsFolder\e29e08cc-226f-4293-81f4-636ff042078b_qa6kvaw56d0me!App"
```

## Architecture

Dependencies point inward; only the UWP head references `Windows.*`, which is what keeps ViewModels unit-testable without a UI:

| Project | TFM | Role |
|---|---|---|
| `.Contracts` | netstandard2.0, `Nullable` enabled | Interfaces, domain model (`Film`), `PageKey`, events, `ActionContext`. Hosts PolySharp with `PolySharpUsePublicAccessibilityForGeneratedTypes`, so records/`init`/ranges work in all netstandard2.0 projects that reference it. |
| `.Services` | netstandard2.0, `Nullable` enabled | `APIClient` + `RequestUriResolver`, `FilmService`, `BusyOperationRunner`, `EventAggregator`, DTOs and mapping — platform-free. |
| `.ViewModel` | netstandard2.0, `Nullable` enabled | ViewModels on CommunityToolkit.Mvvm `ObservableObject`, plus `PageViewModelBase` and `SourceOrderedCollection`. |
| `DrawboardCodingExercise` | UAP 10.0.26100 (min 18362) | Views, XAML, Autofac modules, and every UWP-specific service (`NavigationService`, `ThreadDispatcher`, `LocalizationService`, `UserInteractionService`). |

### The legacy csproj trap

New `.cs` and `.xaml` files in the UWP project are **not** globbed. Every file must be added by hand to the `<Compile>` / `<Page>` ItemGroups in [DrawboardCodingExercise.csproj](DrawboardCodingExercise/DrawboardCodingExercise.csproj) (`<Page>` entries need `<Generator>MSBuild:Compile</Generator>`, and code-behind needs `<DependentUpon>`). Missing entries fail as unresolved-type or missing-`InitializeComponent` errors. The other four projects are SDK-style and glob normally.

### DI and navigation

[App.xaml.cs](DrawboardCodingExercise/App.xaml.cs) builds the Autofac container with `RegisterAssemblyModules`, so any `Autofac.Module` in the UWP assembly is picked up automatically — see [Module/](DrawboardCodingExercise/Module/) (`CoreServicesModule`, `WebServicesModule`, `NavigationModule`). `App` resolves `Shell` + `ShellViewModel` itself and calls `ShellViewModel.OnNavigatedToAsync(null)`, which navigates to `PageKey.FilmList`.

[NavigationService](DrawboardCodingExercise/CoreFramework/NavigationService.cs) drives everything else: it resolves the page `Type` keyed by `PageKey`, resolves the ViewModel keyed as `ObservableObject`, navigates the `Shell`'s frame, property-injects the view (`InjectUnsetProperties`), assigns `DataContext`, then awaits `INavigateToAware.OnNavigatedToAsync(parameter)`. Consequences worth knowing:

- A ViewModel must derive from `ObservableObject` or it will not be resolved or attached.
- ViewModel registrations are instance-per-dependency, so **each navigation — including `BackAsync()` — constructs a fresh ViewModel and re-runs `OnNavigatedToAsync`**. Nothing caches or disposes navigated ViewModels; don't rely on page state surviving a back navigation. Cache in a singleton service if you need it.
- `NavigateAsync` self-dispatches to the UI thread via `IThreadDispatcher`, and back-navigation replays the original parameter from `NavigationDetails` stored in the frame's back stack.
- Page ViewModels are registered `ExternallyOwned`. They implement `IDisposable` via `PageViewModelBase`, and Autofac would otherwise retain every instance ever created for disposal — one uncollectable object per navigation.

**Adding a page** means four edits: a new `PageKey` value ([PageKey.cs](DrawboardCodingExercise.Contracts/PageKey.cs)), `builder.RegisterView<TView, TViewModel>(PageKey.X)` in [NavigationModule.cs](DrawboardCodingExercise/Module/NavigationModule.cs), the `<Page>`/`<Compile>` entries in the csproj, and a `PageHeader.X.Text` string in the resw (below).

### Cross-cutting services

- **Page headers** — a ViewModel implementing `IProvidePageHeader` returns a *resource-key fragment*, not display text. [PageHeaderValueConverter](DrawboardCodingExercise/ValueConverters/PageHeaderValueConverter.cs) looks up `PageHeader/{PageHeader}/Text`; a missing entry renders as `[PageHeader.X.Text]`.
- **Localization** — [Resources.resw](DrawboardCodingExercise/Strings/en/Resources.resw) names use dots (`Errors.Retry`); `ILocalizationService.Translate` rewrites dots to slashes for `ResourceLoader` and supports `string.Format` args. XAML uses `x:Uid`.
- **Busy indicator** — never post `NotifyBusyEvent` / `NotifyDoneEvent` by hand. Run the work through `IBusyOperationRunner`, which owns the pairing, the retry prompt and the failure-to-message mapping. The shell matches the two by text, so an unpaired notification leaves the indicator running forever; the shell now ignores an unmatched completion rather than throwing, but that is a safety net, not a licence.
- **`IEventAggregator`** — ordered synchronous fan-out; `SubscribeOnUI` marshals handlers onto the UI thread. Delivery order across separate subscriptions is guaranteed, which the busy/done pairing depends on. Subscriptions return `IDisposable`; collect them in a `CompositeDisposable`.
- **`IThreadDispatcher`** — the only sanctioned way to reach the UI thread. ViewModels must never touch `CoreDispatcher` directly, or they stop being testable.
- **`IUserInteractionService.ShowRetryDialogAsync()`** — the intended response to a failed API call.

### Calling the API

`IAPIClient` (`GetAsync<T>`, `PostAsync<TReq,TResp>`, `GetImageAsync`) resolves paths against `IAPISettings.ServerAddress`, set to `https://swapi.info/api/` in [ApplicationConfiguration.cs](DrawboardCodingExercise/Configuration/ApplicationConfiguration.cs). Non-2xx responses throw `HttpStatusException`; the `HttpClient` is static and unauthenticated, and requests carry an `X-Correlation-Id` from `ActionContext`.

`RequestUriResolver` builds every request URI and accepts **both** a relative path and an absolute URL beneath the base — payload links can be passed straight through. It uses `Uri`'s base-relative constructor rather than string concatenation, which would turn an absolute URL into `{base}/https://host/path`. Anything resolving outside the configured origin is rejected with `ArgumentException` rather than requested, so an image on a separate content host needs its own client.

Deserialization uses the `JsonSerializerSettings` registered in [WebServicesModule.cs](DrawboardCodingExercise/Module/WebServicesModule.cs) with a `CamelCasePropertyNamesContractResolver`. That does not handle snake_case, so swapi fields like `episode_id` / `opening_crawl` need explicit `[JsonProperty("episode_id")]` attributes on the models.

For image URLs (e.g. MET thumbnails) the [StreamedImage](DrawboardCodingExercise/Controls/StreamedImage.xaml.cs) control binds a `Stream` — pair it with `IAPIClient.GetImageAsync` — and renders `ErrorIcon` if the stream fails to decode.

## Conventions

Tabs for indentation, file-scoped namespaces, and braces required on all control-flow statements ([DotSettings](DrawboardCodingExercise.sln.DotSettings)); `API` and `UI` stay uppercase in identifiers. Use CommunityToolkit.Mvvm source generators rather than hand-rolled `INotifyPropertyChanged`/`ICommand` — `[ObservableProperty]` on a `_camelCase` field, `[RelayCommand]` on a method, and the ViewModel must be `partial`. README.md has worked before/after examples.

Because UWP's runtime is roughly .NET Core 2.1, newer BCL types are unavailable outside what PolySharp polyfills — prefer packages that target netstandard2.0.
