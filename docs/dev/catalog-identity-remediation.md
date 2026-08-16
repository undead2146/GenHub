# Catalog identity + downloads remediation

**Status:** Execute this plan. Do not invent a new architecture.  
**Audience:** Fast model with no chat history.  
**Constraint:** Do **not** run `dotnet build` / `dotnet restore`. Use `powershell -File scripts/build-check.ps1`.  
**Constraint:** Do **not** hardcode person/author aliases (`L3-M`, `TheSuperHackers`, `superhacker`, `community outpost`, `l3m`, `genpatcher`, `lemon`, …) to guess a publisher.

This document is the single source of truth for fixing the staged `feat/ui-downloads` work (PR #265). Implement in the phase order below. After each phase, run the **Verify** steps for that phase before continuing.

---

## 0. Read this first — why the mess exists

A subscribed GenHub catalog (`genhub-test-catalog.catalog.json`) is a **host catalog**. Its `publisher.id` is `genhub-test-publishers`.

That catalog also contains a **ContentBundle** whose members are content that already has native GenHub pipelines:

| Catalog item id | What it actually is | Native factory that must process the zip/dat |
|---|---|---|
| `superhackers-zerohour-gamecode` | SuperHackers weekly GameClient | `SuperHackersManifestFactory` |
| `communityoutpost-controlbar-pro`, `legionnaire-hotkeys`, `gentool-suite-86` | Community Outpost / GenPatcher | `CommunityOutpostManifestFactory` |
| Generals Online client / maps | Generals Online | `GeneralsOnlineManifestFactory` |
| `lemon-controlbar` | Plain GitHub zip (L3-M), **no** native factory | `GenericCatalogManifestFactory` |

The current code tries to guess the native publisher by scanning `metadata.author` with `Contains("superhacker")` etc. That is forbidden. It caused:

- Discoverer IDs minted from `genhub-test-publishers`
- Bundle component IDs minted from remapped `thesuperhackers` / `communityoutpost`
- Resolver dependency IDs minted from the JSON `publisherId` (`genhub-test-publishers`)
- Native factories hijacking (or skipping) the wrong packages
- `GenericCatalogManifestFactory` never matching (`CanHandle` still wants `generic-catalog`)

**Correct model:** the catalog JSON **declares** which pipeline owns each item. Code never infers it from author names.

---

## 1. LOCKED architecture — publisher routing

### 1.1 New optional field on catalog items

Add to `CatalogContentItem` (`GenHub/GenHub.Core/Models/Providers/CatalogContentItem.cs`):

```csharp
/// <summary>
/// Native pipeline / publisher type that must process this item after download.
/// When omitted, the generic catalog factory handles extraction.
/// Must be a value from <see cref="PublisherTypeConstants"/>, 
/// <see cref="CommunityOutpostConstants.PublisherType"/>, or
/// <see cref="CatalogConstants.GenericCatalogResolverId"/>.
/// </summary>
[JsonPropertyName("publisherType")]
public string? PublisherType { get; set; }
```

Allowed values (use the **constants**, never string literals in C#):

| Constant | Value | Factory |
|---|---|---|
| `CatalogConstants.GenericCatalogResolverId` | `generic-catalog` | `GenericCatalogManifestFactory` |
| `PublisherTypeConstants.TheSuperHackers` | `thesuperhackers` | `SuperHackersManifestFactory` |
| `CommunityOutpostConstants.PublisherType` | `communityoutpost` | `CommunityOutpostManifestFactory` |
| `PublisherTypeConstants.GeneralsOnline` | `generalsonline` | `GeneralsOnlineManifestFactory` |
| `PublisherTypeConstants.GitHub` | `github` | `GitHubManifestFactory` |
| `PublisherTypeConstants.ModDB` | `moddb` | `ModDBManifestFactory` |
| omit / empty | treat as generic-catalog | `GenericCatalogManifestFactory` |

Unknown `publisherType` → fail catalog validation (do not silently remap).

### 1.2 How to pick the publisher for IDs and factories

One helper. Put it on `CatalogManifestIdentity` (or rename that class to stay, but **delete** `ResolveEffectivePublisherId`).

```text
ResolveDeclaredPublisherType(item, hostCatalogPublisherId):
  if item.PublisherType is non-empty:
    return item.PublisherType   // already validated against the allowlist
  return CatalogConstants.GenericCatalogResolverId
```

**Do not** use `hostCatalogPublisherId` (`genhub-test-publishers`) as `PublisherType` for factory routing. The host id is only the **subscription row** in the sidebar.

For **manifest ID segment 2 (publisher)**:

- If `item.PublisherType` is set → use that (so SuperHackers from a catalog matches SuperHackers from the GitHub tab).
- If omitted → use `CatalogConstants.GenericCatalogResolverId` **or** a normalized host catalog id, but **the same value everywhere**. Prefer `generic-catalog` for factory match, and put the host catalog id in `OriginalContentId` / metadata if you need to disambiguate two subscribed catalogs.

**Simplest rule that will not drift:**

1. `manifest.Publisher.PublisherType` = declared pipeline (`item.PublisherType` or `generic-catalog`).
2. Manifest ID publisher segment = **that same string**.
3. Bundle dependency `publisherId` in JSON **must equal** the sibling item’s declared `publisherType` (or `generic-catalog` if omitted).
4. Discoverer `ContentSearchResult.Id`, bundle `CatalogId`, and resolver `AddDependency` id are all `CatalogManifestIdentity.CreateContentId(declaredPublisher, contentType, contentId, version)`.

### 1.3 Factory routing

Keep `PublisherManifestFactoryResolver` as `FirstOrDefault(CanHandle)`.

Change:

- `GenericCatalogResolver` stamps `publisherType:` from §1.2. **Never** from author.
- `GenericCatalogManifestFactory.CanHandle` → `PublisherType` equals `CatalogConstants.GenericCatalogResolverId` (and empty, for safety).
- Native factories stay as they are (match their constants on `PublisherType`).
- Do **not** widen SuperHackers/CO `CanHandle` to “author looked similar”.
- If no factory matches, that is a bug in the catalog declaration or the stamp — fail acquisition loudly; do not skip extraction.

After extract, SuperHackers/CO/GO factories run **because the catalog said so**, not because someone named “TheSuperHackers” in a string.

### 1.4 Sample catalog JSON changes (required)

File: `GenHub/GenHub/SampleCatalogs/genhub-test-catalog.catalog.json`

On each **content item**, set `publisherType` explicitly:

```json
"id": "superhackers-zerohour-gamecode",
"publisherType": "thesuperhackers",

"id": "communityoutpost-controlbar-pro",
"publisherType": "communityoutpost",

"id": "legionnaire-hotkeys",
"publisherType": "communityoutpost",

"id": "gentool-suite-86",
"publisherType": "communityoutpost",

"id": "lemon-controlbar",
"publisherType": "generic-catalog",

Generals Online client / maps:
"publisherType": "generalsonline"
```

On **bundle dependencies**, stop using `"publisherId": "genhub-test-publishers"` for native members. Use the sibling’s pipeline:

```json
{ "publisherId": "thesuperhackers", "contentId": "superhackers-zerohour-gamecode", "contentType": "GameClient", ... }
{ "publisherId": "communityoutpost", "contentId": "legionnaire-hotkeys", "contentType": "Addon", ... }
{ "publisherId": "generic-catalog", "contentId": "lemon-controlbar", "contentType": "Addon", ... }
{ "publisherId": "ea", "contentId": "zerohour", "contentType": "GameInstallation", ... }
```

Parser: if a dependency `contentId` exists in the same catalog, `publisherId` must equal that item’s declared publisherType (or `ea`/`any` for base game). Fail validation otherwise.

### 1.5 DELETE these — do not “fix” them

| Delete / stop using | File |
|---|---|
| `ResolveEffectivePublisherId` entire method | `CatalogManifestIdentity.cs` |
| Author `Contains("superhacker")` / `"communityoutpost"` / `"l3m"` / `"genpatcher"` | same |
| Special-case `genhub-test-publishers` / `genhubtestpublishers` as remap sentinels | same |
| `IsCompatiblePublisherAlias` treating all known publishers as interchangeable | `ContentStateService.cs` |
| `p.Contains("catalog") \|\| p.Contains("genhub") \|\| p.Contains("test")` → true | `ContentStateService.cs` |
| Aliases `lemon-controlbar` → `cbpr` | `GenPatcherContentRegistry.cs` |
| Aliases `superhackers-zerohour-gamecode` → `community-patch` | `GenPatcherContentRegistry.cs` |

`ea` + `zerohour`/`generals` as **base-game** detection stays. Those are game-installation coordinates, not person aliases. Use constants (`ManifestConstants` / existing EA helpers), not new author heuristics.

---

## 2. Execution order

Do not start UI polish before Phase A–B. Identity bugs make UI tests lie.

| Phase | What | Stop condition |
|---|---|---|
| **A** | Schema + routing + sample catalog + delete remapping | One function mints every catalog ID; factories match declared `publisherType` |
| **B** | Dependency / profile identity matching | No bidirectional `StartsWith`; publisher segment is required |
| **C** | Downloads UI correctness | Card/detail stay in sync; images load; ComboBox not inside Button |
| **D** | Integrity / security | Hash, signature, `file://`, image fetch |
| **E** | Style, DI, tests, leftover docs | `build-check` + new tests listed below all pass |

---

## 3. Phase A — identity and factories

### A1. Delete author remapping; declare publisherType

**Files:**

- `GenHub/GenHub.Core/Models/Providers/CatalogContentItem.cs` — add `PublisherType`
- `GenHub/GenHub.Core/Models/Providers/CatalogManifestIdentity.cs` — delete `ResolveEffectivePublisherId`; add `ResolveDeclaredPublisherType(CatalogContentItem item)` that returns allowlisted `item.PublisherType` or `generic-catalog`
- `GenHub/GenHub/Features/Content/Services/Catalog/JsonPublisherCatalogParser.cs` — validate `publisherType` against allowlist; validate dep `publisherId` vs sibling
- `GenHub/GenHub/SampleCatalogs/genhub-test-catalog.catalog.json` — §1.4
- `GenHub/GenHub/Features/Content/Services/Catalog/GenericCatalogDiscoverer.cs` — mint IDs with declared publisher, not `catalog.Publisher.Id` when `publisherType` is set
- `GenHub/GenHub/Features/Content/Services/Catalog/CatalogBundleComponentBuilder.cs` — same; delete `ResolveEffectivePublisherId` call
- `GenHub/GenHub/Features/Content/Services/Catalog/GenericCatalogResolver.cs` — `WithPublisher(..., publisherType: declared)`; mint deps with `dependency.PublisherId` (already declared); **do not** remap

**Verify:**

```text
rg "ResolveEffectivePublisherId|Contains\(\"superhacker\"\)|Contains\(\"community outpost\"\)" GenHub
# zero hits

Test: discoverer Id == bundle CatalogId == resolver dependency Id
  for superhackers-zerohour-gamecode, lemon-controlbar, legionnaire-hotkeys
  using CatalogManifestIdentity.CreateContentId(declaredPublisher, type, id, version)
```

### A2. Stamp PublisherType so the right factory runs

**Files:**

- `GenericCatalogResolver.cs` — `publisherType:` = declared pipeline (`thesuperhackers` / `communityoutpost` / `generalsonline` / `generic-catalog`)
- `GenericCatalogManifestFactory.CanHandle` — only `generic-catalog` (and empty)
- Do not set `OriginalProviderName` to a guessed native id unless it **equals** declared `publisherType`

**Why:** SuperHackers zip layout is not a generic zip. Community Outpost `.dat` is not a generic zip. Lemon zip **is** generic.

**Verify:**

```text
Test: resolver-built SuperHackers GameClient → SuperHackersManifestFactory.CanHandle == true
      GenericCatalogManifestFactory.CanHandle == false
Test: resolver-built lemon addon → GenericCatalogManifestFactory.CanHandle == true
      SuperHackersManifestFactory.CanHandle == false
Test: resolver-built CO addon → CommunityOutpostManifestFactory.CanHandle == true
```

Use the **real** resolver (not a mock that stamps `generic-catalog` on everything).

### A3. `WithBasicInfo` must not use catalog slug as display name

**File:** `GenericCatalogResolver.cs:90`

Today: `WithBasicInfo(publisher.Id, contentItem.Id, release.Version)` → `manifest.Name` = `lemon-controlbar`.

**Fix:** `WithBasicInfo(declaredPublisher, contentItem.Name, release.Version)` then overwrite `Id` from `searchResult.Id` **or** add `IContentManifestBuilder.WithId(ManifestId)` and call it **before** `Build()` (preferred; do not mutate after `Build()`).

**Verify:** `GenericCatalogResolverTests` — `result.Data.Name == contentItem.Name` and `result.Data.Id == searchResult.Id`. Fix the mock that currently returns `"wrong"` / omits `Name`.

### A4. Variant content-id separator

**File:** `CatalogManifestIdentity.CreateVariantContentId`

Today: `$"{catalogContentId}{variantLabel}"` → `lemoncontrolbar720p` (collides; cannot strip suffix).

**Fix:** `$"{catalogContentId}-{variantLabel}"` (same as Community Outpost `cbpr-1080p`). `ContentStateService.StripVariantSuffix` already strips `-`.

**Verify:** `CreateVariantContentId("generic-catalog", Addon, "lemon-controlbar", "720p", "1.3")` contains a distinct suffix; `map`+`a` vs `ma`+`pa` do not collide.

### A5. Wrong GenPatcher aliases

**File:** `GenHub/GenHub.Core/Models/CommunityOutpost/GenPatcherContentRegistry.cs:427-432`

**Fix:** Remove:

- `lemon-controlbar` / `lemoncontrolbar` → `cbpr` (Lemon Edition ≠ Control Bar Pro ExiLe)
- `superhackers-zerohour-gamecode` / `superhackerszerohourgamecode` → `community-patch` (weekly client ≠ Community Patch package)

**Verify:** `GetMetadata("lemon-controlbar")` is not `cbpr`. `GetMetadata("superhackers-zerohour-gamecode")` is not `community-patch`.

### A6. Generals Online executable names

**File:** `GenHub/GenHub.Core/Models/Manifest/ManifestVariantResolver.cs:187`

**Wrong:** `generalsonline60.exe`, `generalsonline.exe`  
**Right:** `GameClientConstants.GeneralsOnline60HzExecutable` (`generalsonlinezh_60.exe`), `GameClientConstants.GeneralsOnlineDefaultExecutable` (`generalsonlinezh.exe`)

**Verify:** `ManifestVariantResolverTests` with those two filenames as the unique launch target.

### A7. Resolver metadata keys

**File:** `GenericCatalogResolver.cs:45-55`

Replace `"releaseJson"` / `"catalogItemJson"` / `"publisherProfileJson"` with `CatalogConstants.ReleaseJsonMetadataKey` etc.

**Verify:** grep those three literals in production C# (tests may still use constants). Zero hits in `Features/`.

### A8. Pass catalog index into dependency type resolution

**File:** `GenericCatalogResolver.cs:164`

Today: `ResolveDependencyContentType(dependency, contentItem)` without `catalogItems` → undeclared types become `Mod`.

**Fix:** Deserialize the host catalog items (or trust `CloneReleaseWithResolvedTypes` already on `releaseJson`). Prefer requiring `contentType` on every non-base dep (parser fail closed).

**Also:** `ResolveDependencyContentType` must **not** default every GameClient leftover dep to `GameInstallation` (`CatalogManifestIdentity.cs:188`). Only when `IsBaseGameDependency` is true.

**Verify:** Bundle dep on sibling GameClient without `contentType` in a raw JSON still becomes `GameClient` if the sibling exists; optional addon leftover of a GameClient is **not** a foundation dep.

### A9. Dead `publisherJson` in bundle builder

**File:** `CatalogBundleComponentBuilder.cs:38,166-167`

Delete the serialize + `_ = publisherJson`. Publisher JSON already lives on the bundle search result.

### A10. Share HumanizeContentId / variant-split helpers

Duplicate in `GenericCatalogDiscoverer` and `CatalogBundleComponentBuilder`. Move `HumanizeContentId`, multi-option axis detection, and single-artifact release clone to one type (`CatalogManifestIdentity` or a `CatalogVariantSplit` helper).

Default variant: one helper. Prefer `IsDefaultVariant`; else `1080p` / `1920x1080`; else first. Put the 1080p token in `UiConstants` or `CatalogConstants`, not two copies of `"1080p"`.

### A11. Split only the multi-option axis

**Files:** `GenericCatalogDiscoverer.GetVariantArtifacts`, `CatalogBundleComponentBuilder.GetMultiOptionVariantArtifacts`

If **any** axis has 2+ artifacts, do **not** emit singleton axes (e.g. `language=en`) as extra siblings. Emit only the axis with `Count > 1`.

### A12. Missing sibling must not look downloadable

**File:** `CatalogBundleComponentBuilder.cs:148`

If sibling/release is missing, do not emit `ReleaseJson = ""`. Skip or mark unavailable.

### A13. Catalog tabs match catalog content id

**File:** `CatalogTabProvider.cs:98`

`AppliesTo.Contains(searchResult.Id)` fails because `Id` is now 5-segment. Prefer `ResolverMetadata[CatalogConstants.CatalogContentIdMetadataKey]`, then `Id`. Case-insensitive.

### A14. GitHub Releases vs Topics IDs (if you touch GitHub in this PR)

Topics: 5-segment. Releases: `github.{owner}.{repo}.{tag}` (invalid). Resolver mints a third id.

**Fix if in scope:** mint Releases SuperHackers cards with the same helper as Topics; stamp `searchResult.Id` on the resolved manifest. Use `GitHubTopicsConstants.AssetNameMetadataKey` instead of `"asset-name"`.

If out of scope, leave a `// TODO` and do not make it worse.

### A15. Community Outpost parallel IDs (document or align)

CO uses `1.0.{pub}.{type}.{code}[-variant]`. Either call `CatalogManifestIdentity` from the parser **or** add a 10-line comment that CO is exempt and must keep dashed variants. Do not half-remap CO through author heuristics.

---

## 4. Phase B — dependency and profile matching

### B1. `DependencyResolver.HasCompatibleCatalogIdentity`

**File:** `GenHub/GenHub/Features/GameProfiles/Services/DependencyResolver.cs:284-313`

Bugs:

1. Publisher check uses `[0]` (schema, always `"1"`) **or** `[2]`, so publisher almost never matters.
2. Bidirectional `StartsWith` on name: `mod` matches `modpack`; `lemoncontrolbar` matches `lemoncontrolbar720p` **and** `lemoncontrolbarmalware`.

**Fix:**

```text
require parts.Length == 5
require declared[0] == acquired[0]          // schema
require declared[2] == acquired[2] OR declared[2] == "any"
require declared[3] == acquired[3]          // content type
name: equal OR acquired == declared + "-" + variantToken
NEVER declared.StartsWith(acquired) the other way
NEVER treat all of {communityoutpost, thesuperhackers, l3m, generalsonline} as aliases
```

**Verify:**

- Same publisher + `lemon-controlbar` binds `lemon-controlbar-720p`
- Same publisher + `mod` does **not** bind `modpack`
- `thesuperhackers` does **not** bind `communityoutpost` even if names match
- Cross-publisher same name rejected

### B2. `ContentStateService.IsCompatiblePublisherAlias`

**File:** `ContentStateService.cs:190-216`

**Fix:** Delete the “generic if contains catalog/genhub/test” rule. Delete “any two known publishers match”. If you need aliases, use an explicit map of **normalization** only:

- `community-outpost` → `communityoutpost` (hyphen vs concatenated)  
  That is ID normalization, not “L3-M means Generals Online”.

**Verify:** test catalog vs `thesuperhackers` is **false**; `community-outpost` vs `communityoutpost` may be **true**.

### B3. `ContentStateService.ContentNameMatches`

**File:** `:251` bidirectional `StartsWith`

Same rule as B1. Prefix only with `-` variant suffix.

### B4. `GameProfileSettingsViewModel`

**Files:** `:141` `HasCompatibleCatalogMatch`, `:534` install fallback, `:586` display-name substring

**Fix:**

- Reuse B1 helper (publisher + type + name/variant). Not publisher-blind.
- Delete `DisplayName.Contains(dependency.Name)` auto-enable.
- Delete `AvailableGameInstallations.FirstOrDefault()` with no game-type filter (`:534-537`). If nothing matches `CompatibleGameTypes` / target game, leave unsatisfied.

**Verify:** short name `maps` does not enable a random MapPack. Missing ZH install does not auto-pick Generals.

### B5. `ProfileContentService` batch add

**File:** `ProfileContentService.cs:123`

`CheckContentConflictsAsync` only on `requestedIds[0]`. Bundle add can be `[addon, gameClient]`.

**Fix:** conflict-check every exclusive type in the set. For create-profile, pick GameClient from the set, not list order.

**Also:** `HasSameVersionIndependentIdentity` (`:716`) requires exact name — will not bind `lemon-controlbar-720p`. Use B1 helper.

**Verify:** `AddContentToProfileAsync(profile, [addonId, gameClientId])` still swaps/reconciles the client. Lemon declared id binds acquired 720p variant.

---

## 5. Phase C — downloads UI

### C1. Card ↔ detail variant desync (P1)

**Files:** `DownloadsBrowserViewModel.ViewContent` `:758`, `CloseDetail` `:788`, download `:999`

Detail mutates the **shared** `SearchResult` via `VariantSwap.Apply`. Close does not copy `SelectedVariant` back. Download uses the card’s stale `SelectedVariant.ManifestId`.

**Fix:**

1. On open: seed detail selection from `item.SelectedVariant`.
2. On close (and/or on detail selection change): set card `SelectedVariant` by `ManifestId`.
3. After a swap, `originalContentId` must be the **current** selected variant id, not a stale card field.

**Verify:** card ZH → details pick Generals → back → card shows Generals → Download acquires Generals and marks Generals.

### C2. Detail CTAs ignore `InstallableVariant.CurrentState`

**File:** `ContentDetailViewModel.OnSelectedVariantChanged` `:606`

Card updates buttons from `value.CurrentState` immediately. Detail fire-and-forgets `LoadInitialStateAsync()`.

**Fix:** set `IsDownloaded` / `IsUpdateAvailable` from `value.CurrentState` synchronously, then refresh from the state service.

### C3. HTTP images never appear via `StringToImageConverter`

**File:** `StringToImageConverter.cs:52`  
**Call sites:** `ContentDetailView.axaml` ~224, 240, 337, 483, 556, 702, 967, 1327; publisher logo in `DownloadsBrowserView.axaml:47`

Converter returns `null` after fire-and-forget download; binding never refreshes.

**Fix:** bind HTTP images with `controls:ImageLoader.Source`. Keep converter for `avares://` / local files only.

### C4. ComboBoxes inside a card-wide Button

**File:** `DownloadsBrowserView.axaml:163`

```xml
<Button Command="{Binding ViewCommand}" CommandParameter="{Binding}">
  <views:ContentCardView />
</Button>
```

**Fix:** `Border`/`Panel` for chrome. View-details on thumbnail/title only, not variant row or Download/Add to Profile.

### C5. Releases-from-variants Add to Profile uses catalog key

**File:** `ContentDetailViewModel.PopulateReleasesFromVariants` `:2515`

Closure uses `variant.ManifestId` (catalog key), not `DownloadedManifestId`.

**Fix:** pass `releaseItem.DownloadedManifestId` or `GetLocalManifestIdAsync`.

### C6. Default variant is 720p for all Zero Hour catalog groups

**File:** `DownloadsBrowserViewModel.cs:494-498`

```csharp
i.TargetGame == GameType.ZeroHour ||  // FIRST — lemon is all ZH, so 720p wins
i.Variants?.Any(v => v.IsDefault && ...)
```

**Fix:** Prefer `v.IsDefault && v.ManifestId == sibling.Id` (discoverer already sets `ManifestId = sibling.Id`). Restrict the ZH heuristic to SuperHackers/GitHub **game-client** groups only.

**Verify:** lemon 5-way group → default 1080p. SuperHackers Generals/ZH pair → ZH default.

### C7. `HasVariants` inconsistency

Card/detail: `Count > 0`. Bundle row: `Count > 1`.

**Fix:** picker visible iff `Count > 1` everywhere.

### C8. Bundle `RefreshStateAsync` wrong fallback key

**File:** `BundleComponentViewModel.cs:310`

`AddVariant` keys by `ManifestId` or `searchResult.Id`. Refresh falls back to `variant.Name` → always miss.

**Fix:** use the same key as `AddVariant`.

### C9. Bundle component Version stolen from the bundle

**File:** `BundleComponentViewModel.CreateComponentSearchResult` `:377`

Sets `Version`/`LastUpdated` from the **bundle** card, not the sibling release.

**Fix:** deserialize `variant.ReleaseJson` (or store version on `CatalogBundleComponentVariantDescriptor`).

### C10. Library-clear / failed bundle download leave checkmarks

`ResetDownloadState` does not walk `Variants` / `BundleComponents`. Partial bundle download returns without `RefreshBundleComponentStatesAsync`.

**Fix:** reset every variant/component to `NotDownloaded`. `finally { await RefreshBundleComponentStatesAsync(); }` on bundle download.

### C11. `RefreshVariantStatesAsync` does not notify Show* buttons

**File:** `ContentGridItemViewModel.cs:770`

Also raise `ShowDownloadButton` / `ShowUpdateButton` / `ShowAddToProfileButton`.

### C12. Fire-and-forget without CT

**Files:** `ContentDetailViewModel` `_cts` cancelled on dispose but not passed to `LoadInitialStateAsync` / `LoadIconAsync` / `LoadCustomTabsAsync`; `DownloadableItemViewModel.ToggleExpandAsync` uses `CancellationToken.None`; `DownloadsBrowserViewModel:800` `Task.Run`

**Fix:** pass `_cts.Token`; catch and ignore cancel; generation counter on icon reloads so stale HTTP cannot overwrite.

### C13. `SelectVariantAsync` removed

**File:** `ProfileSelectionViewModel.cs:384` XML still says “shows variant selection dialog”.

**Fix:** update XML. Callers must pass the selected variant’s stored manifest id. Do not silently `FirstOrDefault` among acquired siblings without documenting it.

### C14. `ImageLoader` leak

**File:** `ImageLoader.cs:41`

Unsubscribe `AttachedToVisualTree` on `DetachedFromVisualTree`. Ignore apply if `Source` changed.

### C15. MVVM: VMs construct views

`ContentDetailViewModel` / `DownloadsBrowserViewModel` `new ProfileSelectionView(...)`. `DownloadableItemViewModel.CopyMd5Async` uses `Application.Current`.

**Fix:** resolve `ProfileSelectionViewModel` from DI; dialog service; inject clipboard.

### C16. Empty-string card title

`ContentGridItemViewModel.Name` → `SelectedVariant?.Name ?? SearchResult.Name`. Empty string does not fall through `??`.

**Fix:** `string.IsNullOrWhiteSpace(SelectedVariant?.Name) ? SearchResult.Name : SelectedVariant.Name`.

---

## 6. Phase D — integrity and security

### D1. Artifact SHA256 never copied

**File:** `GenericCatalogResolver.cs:147` `AddRemoteFileAsync` has no hash.

**Fix:** set `ManifestFile.Hash` from `primaryArtifact.Sha256`. `HttpContentDeliverer` already verifies `file.Hash` on HTTP (and should on local copy too).

**Verify:** resolver test asserts resolved file hash equals artifact sha256.

### D2. Signed catalogs pass without verification

**File:** `JsonPublisherCatalogParser.VerifySignature` `:180`

Returns `true` when `Signature` is present.

**Fix:** if signature non-empty, return **false** until verification exists. Unsigned remains optional.

### D3. `file://` / rooted paths from catalog JSON

**File:** `HttpContentDeliverer.cs` `CanDeliver` / copy branch

A remote subscribed catalog can point `downloadUrl` at a local file.

**Fix:** generic-catalog / HTTP pipeline: `http`/`https` only. Keep `file://` only for `CatalogDocumentReader` local preview. Hash-check copies. Reject `RelativePath` that escapes staging **before** copy.

### D4. Image fetch

**Files:** `ImageCacheService.cs`, `ImageLoader.cs`, `ContentGridItemViewModel.LoadIconAsync`

Problems: static singleton, own `HttpClient`, spoofed UA, no size cap, `file://` allowed, `pendingDownloads` uses first caller’s CT, `CreateClient("Images")` is **not registered**.

**Fix:**

- Register image cache (or named `HttpClient`) in DI using existing download UA/timeout.
- HTTPS (and `avares`) only.
- Cap bytes (e.g. 5 MB).
- Do not put CT in `GetOrAdd` (or clone a linked CTS per waiter).
- One path: ImageLoader **or** VM Bitmap, not both bypassing the cache.
- Dispose replaced `IconBitmap`.

### D5. Orchestrator store-on-error

**File:** `ContentOrchestrator.cs:689`

```csharp
if (!alreadyStoredResult.Success || !alreadyStoredResult.Data)
```

**Fix:** if `!Success`, fail acquisition. Skip store only when `Success && Data`.

### D6. Playwright profile path

**File:** `PlaywrightService.cs:111` `Path.Combine(..., profileName)`

Sanitize: `[A-Za-z0-9_-]` or `Path.GetFileName` and reject `..`.

`ManagedChromiumRuntime`: do not walk four parent directories for `node`; stay inside app output. Confirm `PLAYWRIGHT_DRIVER_PATH` is actually used by the shipped Playwright version.

---

## 7. Phase E — style, constants, tests, docs

### E1. Magic strings → constants

| Location | Use |
|---|---|
| `CatalogManifestIdentity` ea/any/zerohour/generals | existing Manifest/EA constants |
| GitHub `"asset-name"`, `"VariantCount"` | `GitHubTopicsConstants` |
| ModDB `"ModDB"` | `ModDBConstants.ResolverId` |
| CO `"contentCode"` | `CommunityOutpostCatalogConstants` |
| `IndentToMarginConverter` 5 / 24 | `UiConstants` |
| `VariantAxisGrouping` `"default"` / `"game-type"` | constants |

### E2. StyleCop / coding-style.md

- Class member order: fields, ctor, properties, methods. `ContentGridItemViewModel._variantSearchResults` is currently after methods. `ContentDetailViewModel` is a ~2700-line god class — split only if you touch it; at least restore field order.
- XML summaries start with a capital letter (`ContentStateToBrushConverter`).
- No empty `catch` (`CatalogManifestIdentity.ExtractVersionNumber`, `ImageCacheService`, `StringToImageConverter`, `LoadIconAsync`). Catch specific exceptions and log.
- `using` directives alphabetical (`DownloadableItemViewModel`).
- `DownloadModule.cs` newline at EOF.
- `InstallableVariant`: prefer `ObservableObject` like `VariantAxisGroup`.
- CONTRIBUTING: braces on control blocks.

### E3. `ExtractVersionNumber`

Empty `catch` then MD5. `1.4` and `1.04` both → 104. Three-part `2026.07.31` hashes.

Catch parse exceptions only. Document + test weekly tags, `1.3`, `1.04`, `>=weekly-...`. Prefer sharing `ManifestIdGenerator` logic.

### E4. Clone artifacts by value

`CloneReleaseWithResolvedTypes` copies `Artifacts` by reference; later `IsPrimary = true` mutates `_cachedCatalog`. Clone the artifact list.

`SelectRelease` ignores parent `versionConstraint` — select the latest that satisfies the constraint.

### E5. Docs leftovers

- `ProfileSelectionViewModel` XML: variant dialog is gone.
- `docs/features/downloads.md` mermaid still has `PublisherSidebarView`.
- `docs/features/content/content-pipeline.md` sequence still has `PublisherSidebar`.

### E6. Sample catalog copy

Changelog GenTool **8.6** vs description **8.9** (`genhub-test-catalog.catalog.json` ~519 vs ~542). Align.

`communityoutpost-controlbar-pro` is a single `.dat` with no `variantAxis` while changelog mentions resolutions — either add axes or fix changelog.

### E7. MapPack linker (P3)

`ProfileContentLinkerService` sends every MapPack Workspace file to `UserMapsDirectory`. Only remap files that look like maps, not `.big` overlays.

### E8. Windows Playwright package

`GenHub.Windows.csproj` extra `Microsoft.Playwright` is OK if needed for the WinExe. Do not add author aliases to “fix” Linux. `GenHub.csproj` already references Playwright.

### E9. PR extras

`scripts/build-check.ps1` and `docs/dev/content-card-ui-plan.md` in this feature PR are optional to split. Do not regress build-check.

---

## 8. Tests you must add (would have caught the bugs)

Implement these as xUnit facts. Names are suggestions.

| Test | Asserts |
|---|---|
| `DeclaredPublisher_UsedForDiscovererBundleAndResolverIds` | SH / lemon / CO ids equal across three paths |
| `NoAuthorRemap_L3M_IsGenericCatalog` | author `L3-M` does **not** become `l3m` / `generalsonline` |
| `SuperHackersCatalogItem_UsesSuperHackersFactory` | real resolver stamp → SH factory true, generic false |
| `LemonCatalogItem_UsesGenericFactory` | generic true, SH/CO false |
| `CreateVariantContentId_UsesHyphenSeparator` | collision + suffix strip |
| `HasCompatibleCatalogIdentity_RejectsCrossPublisher` | SH vs CO |
| `HasCompatibleCatalogIdentity_RejectsPrefixSquat` | `mod` vs `modpack` |
| `HasCompatibleCatalogIdentity_BindsHyphenVariant` | `lemon-controlbar` vs `lemon-controlbar-720p` |
| `IsCompatiblePublisherAlias_DoesNotTreatAllKnownAsEqual` | |
| `GenPatcherRegistry_DoesNotAliasLemonToCbpr` | |
| `ManifestVariantResolver_GeneralsOnlineExeConstants` | |
| `GenericCatalogResolver_CopiesSha256` | |
| `GenericCatalogResolver_SetsDisplayNameNotSlug` | |
| `VerifySignature_NonEmpty_FailsClosed` | |
| `HttpContentDeliverer_RejectsFileUri_ForHttpCatalog` | |
| `DownloadsBrowser_LemonGroup_DefaultsTo1080p` | |
| `DownloadsBrowser_CardDetail_SelectedVariantRoundTrip` | |
| `ContentDetail_OnVariantChange_UpdatesButtonsImmediately` | |
| `PopulateReleasesFromVariants_AddToProfile_UsesStoredManifestId` | |
| `BundleComponent_RefreshState_UsesManifestIdKey` | |
| `ProfileContentService_BatchAdd_ChecksEveryExclusiveId` | `[addon, client]` |
| `GameProfileSettings_DoesNotFallbackToAnyInstallation` | |
| `CatalogTabProvider_AppliesTo_UsesCatalogContentId` | |
| `JsonParser_RejectsUnknownPublisherType` | |
| `JsonParser_DepPublisherMustMatchSibling` | |

Existing tests to **fix** so they match the new rules:

- `GenericCatalogResolverTests` Name assertion / mock `PublisherType`
- `CatalogManifestIdentityTests` — cover `ResolveDeclaredPublisherType`, not deleted remap
- `CatalogBundleComponentBuilderTests` — assert CatalogId publisher == declared
- `DependencyResolverTests` — add cross-publisher rejection
- `CatalogTabProviderTests` — use `CatalogConstants.PublisherProfileJsonMetadataKey`
- `DownloadModuleTests` — if you register image HttpClient, assert it

---

## 9. Final verification checklist

Run from repo root:

```powershell
powershell -File scripts\build-check.ps1
```

Then grep (must be **zero** in `GenHub/GenHub` and `GenHub/GenHub.Core` production code):

```text
ResolveEffectivePublisherId
Contains("superhacker")
Contains("community outpost")
Contains("genpatcher")
Contains("generalsonline")   # inside CatalogManifestIdentity author remap only; other files may still compare constants
"genhub-test-publishers"
"genhubtestpublishers"
lemon-controlbar.*cbpr
superhackers-zerohour-gamecode.*community-patch
TryGetValue("releaseJson"
TryGetValue("catalogItemJson"
TryGetValue("publisherProfileJson"
generalsonline60.exe
generalsonline.exe
```

Manual smoke (sample catalog subscribe):

1. SuperHackers ZH card downloads → extracted as **game client** (exe present), not a stored zip.
2. Lemon card → generic unzip; default dropdown **1080p**.
3. Ultimate Stack bundle: each row’s publisher/id matches the standalone card; Add to Profile enables all required members.
4. Open details, change variant, go back → card dropdown matches; Download hits that variant.
5. Screenshot/banner HTTP (or avares) images visible.
6. Variant ComboBox does **not** open the detail page.

---

## 10. What not to do

- Do **not** add more `if (author.Contains(...)) return "thesuperhackers"`.
- Do **not** map random GitHub users / “L3-M” / “Xezon” / “Exile” to publisher ids.
- Do **not** treat `generic-catalog` as compatible with every native publisher in state matching.
- Do **not** force **all** catalog zips through `GenericCatalogManifestFactory` — SuperHackers and Community Outpost **must** keep their factories, selected by **declared** `publisherType`.
- Do **not** force **all** catalog zips through native factories either — lemon has no native factory.
- Do **not** run `dotnet build` / `dotnet restore`.
- Do **not** restore `VariantSelectionView` / `PublisherSidebarView`.
- Do **not** implement resolution×language cross-product.

---

## 11. Issue index (traceability)

| ID | Sev | One-line | Phase |
|---|---|---|---|
| A1 | P1 | Delete author remap; declared `publisherType` | A |
| A2 | P1 | Right factory from declared type | A |
| A3 | P1 | Display name not slug | A |
| A4 | P2 | Variant id hyphen | A |
| A5 | P1 | Drop lemon→cbpr and SH→community-patch aliases | A |
| A6 | P1 | GO exe constants | A |
| A7 | P2 | Metadata key constants | A |
| A8 | P1 | Dep contentType + no GameClient→GameInstallation default | A |
| A9 | P3 | Dead publisherJson | A |
| A10 | P2 | DRY humanize/split | A |
| A11 | P2 | Split only multi-option axis | A |
| A12 | P2 | Missing sibling not downloadable | A |
| A13 | P2 | Tab AppliesTo | A |
| A14 | P2 | GitHub ID alignment | A |
| A15 | P2 | CO ID scheme comment or align | A |
| B1 | P1 | DependencyResolver publisher + suffix | B |
| B2 | P1 | ContentStateService alias | B |
| B3 | P1 | ContentNameMatches | B |
| B4 | P1 | Profile settings match + no any-install | B |
| B5 | P1 | Batch conflict + variant bind | B |
| C1 | P1 | Card/detail desync | C |
| C2 | P1 | Detail CTA state | C |
| C3 | P1 | ImageLoader for HTTP | C |
| C4 | P1 | ComboBox not in Button | C |
| C5 | P1 | Add to Profile stored id | C |
| C6 | P1 | Lemon default 1080p | C |
| C7 | P2 | HasVariants > 1 | C |
| C8 | P2 | RefreshState key | C |
| C9 | P2 | Bundle version from sibling | C |
| C10 | P2 | Reset/refresh checkmarks | C |
| C11 | P2 | Notify Show* | C |
| C12 | P2 | CTS on loads | C |
| C13 | P3 | SelectVariant XML | C |
| C14 | P2 | ImageLoader detach | C |
| C15 | P2 | Dialog/clipboard DI | C |
| C16 | P3 | Empty Name | C |
| D1 | P1 | SHA256 on ManifestFile | D |
| D2 | P1 | Signature fail-closed | D |
| D3 | P1 | No file:// from remote catalog | D |
| D4 | P1 | Image cache DI + HTTPS + size | D |
| D5 | P1 | Orchestrator fail on store-check error | D |
| D6 | P2 | Playwright profile sanitize | D |
| E* | P3 | Style, tests, docs | E |
