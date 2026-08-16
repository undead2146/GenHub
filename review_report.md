# Code Review Report — `feat/ui-downloads` vs. `development`

## Scope and method

- Baseline: `origin/development` (the available tracked `development` ref).
- Review target: staged index (`git diff --cached origin/development`).
- Reviewed scope: 618 files, 84,482 additions, and 8,970 deletions.
- Method: read-only review using the `codex-code-review` workflow, independent static analysis of the Downloads UI, content pipeline, profile flows, and staged tests, plus targeted staged-blob verification.
- No source, test, configuration, or staged files were modified during this review.

## Findings

### H1 — Untrusted catalog images can access local/UNC paths

- Severity: High
- File and line number: `GenHub/GenHub/Infrastructure/Services/ImageCacheService.cs:153-160`; untrusted source is marked at `GenHub/GenHub/Features/Content/ViewModels/Catalog/SubscriptionConfirmationViewModel.cs:157-165` and propagated at `GenHub/GenHub/Features/Content/Services/Catalog/GenericCatalogDiscoverer.cs:677-681`.
- Category: Security / trust boundary
- Explanation: Community subscriptions are explicitly untrusted, yet a catalog-provided image URL reaches `ImageCacheService`. Any rooted value is treated as a local file and passed to `File.Exists` and then `Bitmap`. On Windows, a UNC value such as `\\attacker\\share\\image.png` is rooted, so merely rendering a catalog card can probe an attacker SMB endpoint and expose network credentials or host information. It also permits arbitrary local path probing/loading.
- Recommended fix: Put URI validation at the catalog/image boundary. For catalog content, allow only app-owned `avares` resources and validated `https`/`http` endpoints; reject local, rooted, UNC, loopback, link-local, and private-network destinations (including redirects, as required by the threat model). Add malicious local/UNC/redirect URI tests.
- Blocks merging: Yes

### H2 — Image cache has unbounded network, memory, native-image, and disk use

- Severity: High
- File and line number: `GenHub/GenHub/Infrastructure/Services/ImageCacheService.cs:22, 216-228`; automatic invocation at `GenHub/GenHub/Infrastructure/Controls/ImageLoader.cs:76-79`.
- Category: Security / resource exhaustion / performance
- Explanation: Every unique image URL can be fully buffered with `ReadAsByteArrayAsync`, decoded on the UI thread, written to disk, and retained in a process-lifetime `ConcurrentDictionary<string, Bitmap>`. There is no response-size, content-type, pixel-dimension, entry-count, memory-cache, disk-cache, TTL, or eviction bound, and evicted bitmaps are never disposed because there is no eviction. A subscribed catalog with many or oversized images can exhaust managed memory, native image memory, disk, and UI responsiveness simply through browsing.
- Recommended fix: Use one bounded image service that streams with a maximum byte count, verifies image content type and decoded dimensions, observes cancellation, uses a byte-bounded LRU memory cache that disposes evicted bitmaps, and maintains a size-bounded/TTL disk cache. Add limit and eviction tests.
- Blocks merging: Yes

### H3 — Card and detail thumbnails bypass the new image safety path

- Severity: High
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/ContentGridItemViewModel.cs:593-654`; `GenHub/GenHub/Features/Downloads/ViewModels/ContentDetailViewModel.cs:1052-1094`.
- Category: Security / resource exhaustion / duplication
- Explanation: These code paths directly call `GetByteArrayAsync` for catalog-controlled thumbnail URLs and construct bitmaps themselves. They have no response-size limit, content validation, shared cache, cancellation, or shared URI policy. Consequently, fixing `ImageCacheService` alone would still leave arbitrary remote images able to cause repeated downloads or out-of-memory failures.
- Recommended fix: Remove the duplicate loaders and route all card/detail/catalog images through the single bounded, validated service from H2. Test that no view-model path can fetch an unvalidated or over-limit image.
- Blocks merging: Yes

### H4 — GitHub archive extraction is vulnerable to decompression and entry-count exhaustion

- Severity: High
- File and line number: `GenHub/GenHub/Features/Content/Services/GitHub/GitHubContentDeliverer.cs:272-306`.
- Category: Security / resource exhaustion
- Explanation: A remotely downloaded GitHub archive is opened and every entry is extracted without enforcing a maximum entry count or cumulative uncompressed size. A small high-ratio archive or many-entry release can consume disk and CPU before the manifest factory can reject or process it. The catalog extractor already has bounded-extraction logic, making the inconsistency especially risky.
- Recommended fix: Centralize bounded archive extraction and enforce entry-count, per-entry, path, and cumulative-uncompressed-size limits before every write. Add tests for an oversized and a many-entry archive.
- Blocks merging: Yes

### H5 — Cancellation during GitHub extraction returns successful partial content

- Severity: High
- File and line number: `GenHub/GenHub/Features/Content/Services/GitHub/GitHubContentDeliverer.cs:149-175, 280-283`.
- Category: Cancellation / correctness
- Explanation: Cancellation only breaks the extraction loop. The caller then deletes the archive and returns a successful delivery result, leaving a partially extracted payload to advance through the pipeline as if it were complete.
- Recommended fix: Call `ThrowIfCancellationRequested` while extracting and before success/cleanup transitions. Ensure cancellation yields a canceled/failed operation and cleans the staging directory transactionally. Add a cancellation-mid-archive test.
- Blocks merging: Yes

### H6 — Singleton ModDB discoverer races on per-request challenge state

- Severity: High
- File and line number: `GenHub/GenHub/Infrastructure/DependencyInjection/ContentPipelineModule.cs:377-378`; `GenHub/GenHub/Features/Content/Services/ContentDiscoverers/ModDBDiscoverer.cs:36, 61, 418, 536`; consumer `GenHub/GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs:702, 734`.
- Category: Concurrency / correctness / architecture
- Explanation: The DI change makes `ModDBDiscoverer` a singleton even though `ChallengeDetected` is mutable per-discovery state. Concurrent requests can reset or set that flag for each other: a verification prompt can be suppressed for the request that encountered a challenge, or a successful request can be treated as challenged and return an empty result.
- Recommended fix: Restore a transient lifetime, or return challenge state as part of each discovery result. If the browser session itself must be shared, serialize access behind a per-request result/lease rather than sharing mutable flags. Add overlapping-discovery tests.
- Blocks merging: Yes

### H7 — Existing local CAS files bypass hash verification

- Severity: High
- File and line number: `GenHub/GenHub/Features/Content/Services/ContentValidator.cs:132-135, 166-173`.
- Category: Integrity / validation
- Explanation: When a `ContentAddressable` file already exists at the resolved content path, the validator accepts its existence and explicitly excludes it from hash verification. A stale or tampered local file therefore passes validation even when its bytes do not match `ContentFile.Hash`.
- Recommended fix: Verify the hash of every materialized local CAS file whenever the manifest provides one; only use CAS existence as a fallback when there is no local materialization. Add a mismatched-local-CAS regression test.
- Blocks merging: Yes

### H8 — Multi-axis catalog variants cannot represent a complete selection

- Severity: High
- File and line number: `GenHub/GenHub/Features/Content/Services/Catalog/GenericCatalogDiscoverer.cs:579-660` (especially `:586-591, 612-635`); `GenHub/GenHub/Features/Content/Services/Catalog/CatalogBundleComponentBuilder.cs:92-121, 246-271`.
- Category: Correctness / API-schema contract
- Explanation: Artifacts from every multi-option axis are flattened into individual one-artifact releases while the UI presents axes independently. For example, a release with Resolution (720p/1080p) and Language (EN/FR) cannot yield a valid combined package: selecting a resolution drops the language artifact and vice versa. Generated IDs also omit the axis, so identical option labels across axes can collide.
- Recommended fix: Model complete variant combinations, or retain artifacts required by all selected axes and include the axis in identity. Until that model exists, reject multi-axis catalogs rather than presenting invalid choices. Add a catalog-to-selection-to-resolver integration test with two independent axes.
- Blocks merging: Yes

### H9 — Failed CAS tracking deletes a pre-existing manifest instead of restoring it

- Severity: High
- File and line number: `GenHub/GenHub/Features/Manifest/ContentManifestPool.cs:57-78`.
- Category: Data integrity / error handling
- Explanation: `AddManifestAsync` overwrites metadata and then tracks CAS references. If tracking fails, its rollback unconditionally deletes the manifest file. For an update to an already stored manifest, this deletes the previous valid metadata rather than restoring it, leaving stored content orphaned or unavailable.
- Recommended fix: Make the operation transactional: track before atomically replacing metadata, or preserve the prior bytes and restore them on failure. Add a test that forces tracking failure while updating an existing manifest and asserts the prior file remains intact.
- Blocks merging: Yes

### H10 — Partial profile reconciliation is reported as success before destructive cleanup

- Severity: High
- File and line number: `GenHub/GenHub/Features/Content/Services/ContentReconciliationService.cs:279-324, 440-555, 558-631`.
- Category: Data integrity / error handling / architecture
- Explanation: The reconciliation internals collect profile-update failures but return `OperationResult.Success`. Callers then untrack CAS references and remove the old manifest. A profile whose `UpdateProfileAsync` failed still references the old manifest, but that manifest and eventually its CAS objects can be removed. The code also cleans a workspace before attempting the profile update, so a failed update can leave the profile pointing to an invalidated workspace.
- Recommended fix: Do not remove/untrack a manifest while any affected profile failed to update. Return a failure or a result that explicitly prevents cleanup, and make workspace/profile changes transactional or recoverable. Add fault-injection tests for replacement and removal where one profile update fails.
- Blocks merging: Yes

### H11 — Playwright browser contexts leak on ordinary fetch/download paths

- Severity: High
- File and line number: `GenHub/GenHub/Features/Content/Services/Tools/PlaywrightService.cs:85-87, 225-242, 692-694, 845-852`.
- Category: Resource lifecycle / performance
- Explanation: `CreatePageAsync` creates a new browser context for every nonpersistent page, but the callers close only the page. Closing a page does not close its owning browser context, so repeated HTML fetches and downloads retain contexts and their associated browser resources.
- Recommended fix: Close the owning nonpersistent context in each `finally`, or return a disposable page/context lease that owns both objects. Add a lifecycle test that verifies one context is closed per nonpersistent operation.
- Blocks merging: Yes

### M1 — Batch profile operations do not validate conflicts within the incoming set

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/GameProfiles/Services/ProfileContentService.cs:128-152, 267-360, 434-441`.
- Category: Correctness / validation / test coverage
- Explanation: Multi-manifest add checks each requested item only against the persisted profile, not against previously requested items. Multi-manifest create bypasses this conflict check altogether. A bundle containing mutually exclusive Community Outpost category items can therefore create or update a profile with both enabled.
- Recommended fix: Validate the combined candidate set (existing enabled IDs plus all requested IDs, including dependencies) before persisting; reject or explicitly resolve pairwise conflicts. Add regression tests for both multi-add and multi-create paths.
- Blocks merging: Yes

### M2 — Creating a profile drops bundle members for the next selection

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/ProfileSelectionViewModel.cs:137-148, 313-315, 432-455`.
- Category: Bundle correctness / UI state
- Explanation: The initial picker correctly stores all acquired `ContentManifestIds`, and creating a new profile enables all of them. Its refresh then calls `LoadProfilesAsync` with only the primary manifest ID, discarding additional bundle members. Because the dialog stays open, selecting another profile afterwards silently adds only the primary manifest.
- Recommended fix: Preserve and pass the current additional IDs when refreshing the picker. Add a test for bundle picker → create profile → select existing profile, asserting that every bundle member is added.
- Blocks merging: Yes

### M3 — Switching to a cached publisher can leave the browser permanently loading

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs:227, 291-308, 422-426`; UI effects at `GenHub/GenHub/Features/Downloads/Views/DownloadsBrowserView.axaml:178-181, 198-202`.
- Category: Async lifecycle / UI correctness
- Explanation: Switching from in-flight publisher A to cached publisher B cancels A, restores B, and returns without clearing `IsLoading`. A's `finally` intentionally leaves it true because its token was canceled. The cached results appear, but the loading indicator remains and Load More stays disabled.
- Recommended fix: Track request generation/identity and clear loading state for a cache restore while preventing a stale request from clearing a newer request's state. Add an A-in-flight → cached-B regression test.
- Blocks merging: Yes

### M4 — Filter-only results are cached and restored as an unfiltered browse

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs:235-240, 291-308, 332-337, 405-408, 710-719`.
- Category: Filtering / cache correctness
- Explanation: Toolbar Search applies active filters, but `_hasCustomQuery` is set only when search text is nonempty. A filter-only search is therefore put in the publisher's default browse cache. On a publisher switch the filter is cleared, then returning restores filtered cards while the UI shows no active filter.
- Recommended fix: Key cache entries by the complete query/filter state, or mark all active-filter searches as custom/noncacheable. Add a filter-only search → switch away → restore test.
- Blocks merging: Yes

### M5 — Several advertised filters have no usable application path

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Downloads/Views/FilterPanelView.axaml:86-296, 311-326`; `GenHub/GenHub/Features/Downloads/ViewModels/Filters/ModDBFilterViewModel.cs:126-145, 175-183`; `GenHub/GenHub/Features/Downloads/ViewModels/Filters/GitHubFilterViewModel.cs:94-107`; `GenHub/GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs:323-328`.
- Category: Functional completeness / UX / maintainability
- Explanation: ModDB and GitHub templates provide selection controls but no direct Apply action equivalent to the other publisher panels, and their selection hooks do not publish a filter event. ModDB also exposes no Clear action despite supporting it. The generic Search button happens to apply current state, but that behavior is undiscoverable and inconsistent. Separately, GitHub's Authors dropdown is never populated because `UpdateAvailableAuthors` has no caller, leaving only “All Authors.”
- Recommended fix: Standardize filter behavior: provide Apply/Clear controls or consistently auto-apply, then populate GitHub authors from discovery results while retaining a valid selection. Add UI/browser-VM tests for apply, clear, and author population.
- Blocks merging: Yes

### M6 — Explicit null collections pass catalog validation and later throw

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Content/Services/Catalog/JsonPublisherCatalogParser.cs:145-203`; dereferences at `GenHub/GenHub/Features/Content/Services/Catalog/GenericCatalogDiscoverer.cs:685, 710`, `GenHub/GenHub/Features/Content/Services/Catalog/GenericCatalogResolver.cs:113, 171`, and `GenHub/GenHub/Features/Content/Services/Catalog/CatalogBundleComponentBuilder.cs:41`.
- Category: Input validation / error handling
- Explanation: JSON values such as `tags: null` or `dependencies: null` override model initializers yet pass parser validation. Later discovery and resolution dereference them without guards, converting an invalid subscribed catalog into a `NullReferenceException` instead of a useful validation error.
- Recommended fix: Normalize nullable collections immediately after deserialization or reject explicit null collections during validation. Add parser and end-to-end malformed-catalog tests.
- Blocks merging: No

### M7 — Local file sources bypass the expected artifact hash

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Content/Services/ContentDeliverers/HttpContentDeliverer.cs:151-178, 206-213`.
- Category: Integrity / validation
- Explanation: For `file:` and rooted local download sources, the deliverer copies the source and then builds a new local-file manifest from whatever was copied. It never compares those bytes against the catalog's expected `file.Hash`, unlike remote download paths. A stale or modified local source is accepted and its new hash is silently re-minted.
- Recommended fix: Verify the copied local file against the declared hash before calling `AddLocalFileAsync`; fail if it differs. Add local-source mismatch coverage and define whether untrusted catalogs may use local paths at all.
- Blocks merging: No

### M8 — Card bitmaps are not disposed and stale asynchronous loads leak bitmaps

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/ContentGridItemViewModel.cs:400-415, 593-654`.
- Category: Native resource lifecycle / performance
- Explanation: `LoadIconAsync` creates publisher and content `Bitmap` instances, but `Dispose` neither clears nor disposes either property. If a newer load wins the version check, a completed older `loadedBitmap` is also not disposed. Browse-cache churn therefore retains native image resources until nondeterministic garbage collection.
- Recommended fix: Define a single owner for each bitmap; dispose superseded and detached instances exactly once (accounting for the publisher-logo alias), clear the properties, or centralize ownership in the bounded cache. Add disposal/lifecycle tests.
- Blocks merging: No

### M9 — Load More skips a page after a discovery failure

- Severity: Medium
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs:340-350, 416-419`.
- Category: Pagination / error handling
- Explanation: `LoadMoreAsync` increments `CurrentPage` before fetching. If discovery throws, the exception is only logged and the page number is not restored. Retrying therefore requests the next page and silently skips the failed one.
- Recommended fix: Advance the page only after a successful append, or roll it back on failure and surface a retryable error. Add a page-N failure/retry test.
- Blocks merging: No

### M10 — New developer shortcut scripts instruct unsafe direct builds

- Severity: Medium
- File and line number: `GenHub/GenHub/SampleCatalogs/generate-test-shortcuts.ps1:73-78`; `GenHub/GenHub/SampleCatalogs/generate-test-shortcuts.sh:140-145`.
- Category: Documentation / developer safety
- Explanation: Both generators tell contributors to run `dotnet build` directly. That contradicts this repository's explicit build-safety requirement to use `powershell -File scripts\\build-check.ps1`, and can reproduce the documented MSBuild/testhost locking and incremental-build-corruption failure mode.
- Recommended fix: Replace the direct-build instructions with the mandated build-check command. If launching is necessary afterwards, document `dotnet run --no-build` only after the safe build workflow.
- Blocks merging: Yes

### L1 — Replaced search cancellation-token sources are not disposed

- Severity: Low
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/DownloadsBrowserViewModel.cs:200-201, 363-365`.
- Category: Resource lifecycle
- Explanation: A refresh cancels the previous `_lastSearchCts` and immediately overwrites it. Only the final source is disposed in `Dispose`, so rapid searching/publisher switching accumulates undisposed CTS instances until garbage collection.
- Recommended fix: Dispose the replaced source after its associated operation has completed, or wrap request state in a disposable per-operation object.
- Blocks merging: No

### L2 — Catalog-controlled URLs are shell-opened without a scheme allowlist

- Severity: Low
- File and line number: `GenHub/GenHub/Features/Downloads/ViewModels/ContentDetailViewModel.cs:1024-1041, 1548-1555, 2638-2667`.
- Category: Security / URL validation
- Explanation: `SourceUrl` and catalog `VideoUrl` can reach `Process.Start` with `UseShellExecute = true` without verifying that they are safe web URLs. This permits file or custom protocol handlers to be invoked when a user opens catalog media.
- Recommended fix: Accept only absolute `https`/`http` URLs (and optionally allowlist supported video hosts) before invoking the shell; reject and notify for all other schemes. Add URL validation tests.
- Blocks merging: No

### L3 — Sample catalog README has a stale item count

- Severity: Low
- File and line number: `GenHub/GenHub/SampleCatalogs/README.md:12`.
- Category: Documentation accuracy
- Explanation: The README says the combined catalog has 11 content items, but `genhub-test-catalog.catalog.json` contains 12 entries in its `content` array.
- Recommended fix: Correct the count or avoid a hard-coded count in the description.
- Blocks merging: No

### L4 — Staged diff contains whitespace errors

- Severity: Low
- File and line number: `GenHub/GenHub/Infrastructure/DependencyInjection/DownloadModule.cs:2, 7-49`; `GenHub/GenHub.Tests/GenHub.Tests.Core/Infrastructure/DependencyInjection/DownloadModuleTests.cs:38`; `docs/dev/catalog-identity-remediation.md:3-5, 47, 257, 362, 425`; `docs/dev/content-card-ui-plan.md:3-5`.
- Category: Maintainability / repository hygiene
- Explanation: `git diff --cached --check origin/development` reports trailing-whitespace errors in these staged lines. This produces a non-clean diff and can complicate linting or review tooling.
- Recommended fix: Normalize the affected lines to the repository's intended line-ending/whitespace policy and rerun `git diff --cached --check origin/development`.
- Blocks merging: No

## Validation and test coverage

- The repository-prescribed build validation, `powershell -File scripts\\build-check.ps1`, was attempted. It could not complete because a pre-existing `testhost` process (PID 499840) held `GenHub.Tests.Core\\bin\\Debug\\net8.0\\GenHub.Core.dll`, producing MSB3021/MSB3027 copy-lock errors. This is inconclusive; it did not reveal a source or XAML compile error. The process was not terminated.
- No direct `dotnet build` or `dotnet restore` was run, in accordance with the repository build-safety mandate.
- The staged test suite adds useful coverage, but it does not cover the high-risk cases above: malicious image URI/size limits, bounded archive extraction, cancellation during extraction, overlapping ModDB discoveries, CAS hash mismatch for locally materialized files, failed profile reconciliation before cleanup, or two-axis catalog selection. It also lacks focused coverage for the cached-publisher cancellation transition, filter-only cache restoration, and bundle re-selection after profile creation.

## Executive summary

The staged changes significantly expand catalog ingestion, downloads, images, variants, and reconciliation. The review found no Critical issue, but it found 11 High, 10 Medium, and 4 Low issues. The High findings include two untrusted-content resource-exhaustion paths, an automatic UNC/local-path access path, unsafe archive extraction, stale/tampered content passing validation, stateful-singleton races, invalid multi-axis variant selection, data loss during manifest/reconciliation failure handling, and leaked browser contexts. Several Medium findings also break primary downloads/profile/filter flows.

## Prioritized action list

1. Establish and test a single untrusted-URL/image policy: reject local/UNC/private-network targets, bound payloads and pixel dimensions, and eliminate direct thumbnail download bypasses.
2. Make archive delivery safe and cancellable: enforce extraction limits and propagate cancellation instead of returning partial success.
3. Restore integrity and transactional behavior: verify local CAS and `file:` artifacts against expected hashes; preserve previous manifest metadata on tracking failure; never remove data still referenced by a profile that failed to update.
4. Repair concurrency and variant modeling: remove the shared ModDB mutable state and model complete multi-axis artifact combinations.
5. Repair Downloads UI state: retain bundle IDs, keep cache/filter keys coherent, clear loading state on cached switches, and make every advertised filter operational.
6. Close Playwright contexts and bitmap resources, fix pagination retry behavior, correct documentation/build instructions, then add the missing regression coverage and rerun the prescribed build check after the external lock is released.

## Verdict

REQUEST_CHANGES
