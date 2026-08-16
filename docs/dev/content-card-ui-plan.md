# Content Card UI Plan — Multi-Axis Variants + Glassmorphic Cards

**Status:** Ready for a UI-focused agent  
**Audience:** Fresh agent with no prior conversation context  
**Stack:** Avalonia UI / .NET 8 / CommunityToolkit.Mvvm  
**Primary surfaces:** `ContentCardView.axaml`, `ContentGridItemViewModel.cs`, `InstallableVariant.cs`, optionally `ContentDetailView.axaml`

---

## 0. Read this first — what is already done

**Do not re-implement discovery, catalog JSON, or resolver work.** That path compiles clean (`dotnet build GenHub/GenHub.csproj` → 0 errors) and is intentionally out of scope for this plan.

### Already complete (data path)

| Area | What landed | Key files |
|------|-------------|-----------|
| Test catalog | Lemon Control Bar Pro (L3-M/GeneralsControlBar v1.3) with 5 resolution artifacts | `GenHub/GenHub/SampleCatalogs/genhub-test-catalog.catalog.json` (`id`: `lemon-controlbar`) |
| Artifact schema | Optional `variantAxis` / `variant` / `isDefaultVariant` on `ReleaseArtifact` (backward-compatible) | `GenHub/GenHub.Core/Models/Providers/ReleaseArtifact.cs` |
| Generic discoverer split | Multi-variant releases → sibling `ContentSearchResult`s sharing `VariantGroupId`; each sibling's release JSON has **only its own artifact** | `GenHub/GenHub/Features/Content/Services/Catalog/GenericCatalogDiscoverer.cs` |
| Resolver | **No changes needed** — picks the single artifact in the selected sibling's release JSON | `GenericCatalogResolver` (leave alone) |
| Browser grouping | Already collapses siblings by `VariantGroupId` into one card + dropdown | `DownloadsBrowserViewModel` |

### How lemon controlbar verifies the data path today

Subscribe to the test catalog → find **"Control Bar Pro Lemon Edition ZH"** → **one card**, **one ComboBox** with 5 options (720p / 900p / 1080p default / 1440p / 4K). Selecting a variant swaps the underlying `ContentSearchResult` via `VariantSwap` so Download targets the correct ZIP.

### Intentionally out of scope

- True multi-axis **cross-product selection** (e.g. resolution × language → pick both axes independently and resolve a combined artifact). No current content needs this. This plan only builds **rendering infrastructure** so multiple ComboBoxes can appear when multiple axes exist in the flat variant list.
- Re-introducing deleted `VariantSelectionView` / `VariantSelectionViewModel` / `VariantOptionViewModel` (already removed; selection lives on the card + detail page).
- Catalog / discoverer / resolver changes unless a UI binding gap forces a tiny plumbing fix (prefer plumbing `VariantType` through existing models only).

---

## 1. Design direction (for the UI agent)

GenHub already has a dark, purple-glass language. **Match it — do not invent a new brand.**

**Tone:** Refined industrial glass — deep charcoal cards with frosted blur, subtle purple edge light, restrained motion. Not neon cyberpunk, not purple-on-white SaaS, not cream/serif editorial.

**Existing references to copy from:**

1. `GenHub/GenHub/Common/Controls/SidebarLayoutStyles.axaml` — proven glass stack:
   - `ExperimentalAcrylicBorder` (`BackgroundSource="Digger"`, dark `TintColor`, `TintOpacity` ~0.9, `MaterialOpacity` ~0.5)
   - Gradient tint `Border` on top (`#CC020024` → `#E63D004D` → `#FF000000`)
   - Content layer above both
2. `GenHub/GenHub/Features/Downloads/Views/ContentDetailView.axaml` — `Button.glass-action` / `--success` / `--warning` / `--ghost`
3. `GenHub/GenHub/Features/Downloads/Views/SubscriptionConfirmationDialog.axaml` — acrylic + `#4A2E80` glass frame
4. `GenHub/GenHub/Assets/Styles/ThemeResources.axaml` — `SidebarGlassBorder` (`#334527A0`), `CardBackground`

**Card aesthetic goals:**

- Replace flat `#2A2A2A` fill with acrylic + gradient tint (same 3-layer Panel pattern as the sidebar).
- Keep **240×340** footprint unless multi-axis dropdowns force a small height bump (prefer internal spacing compression over growing the grid).
- Borders: soft purple glass edge (`SidebarGlassBorder` or `#4A2E80` at low opacity), brighter on `:pointerover`.
- CTA buttons: migrate toward `glass-action` styling used on the detail page (still purple primary, green add-to-profile, amber update) — avoid opaque Material purple slabs if they clash with the glass shell.
- Motion: keep the existing 150ms scale/border transitions; optional soft border glow on hover only.
- **Do not** put `BoxShadow` on `Button` — Avalonia warns (AVLN). Shadows belong on `Border` wrappers only.

**Acrylic caveat for cards:** Cards are `UserControl`s inside a scrollable grid, not top-level windows. `ExperimentalAcrylicBorder` with `BackgroundSource="Digger"` still works (sidebar proves it), but if blur looks flat/empty on some platforms, fall back to a semi-transparent layered `Border` + gradient that still reads as glass. Prefer real acrylic first.

---

## 2. Variant data model trace (do not break this)

```
ContentVariantInfo.VariantType          (Core — already exists)
        │  set by GenericCatalogDiscoverer / GitHubTopicsDiscoverer / etc.
        ▼
DownloadsBrowserViewModel               groups by VariantGroupId
        │  builds InstallableVariant per sibling  ← VariantType NOT copied yet
        ▼
ContentGridItemViewModel
        │  Variants : ObservableCollection<InstallableVariant>
        │  SelectedVariant → VariantSwap.Apply(SearchResult, sibling)
        ▼
ContentCardView.axaml                   single ComboBox ItemsSource={Binding Variants}
```

**Critical gap for Task A:** `ContentVariantInfo` already has `VariantType`, but `InstallableVariant` does **not**. Browser + detail VMs construct `InstallableVariant` without copying the axis. That is the only data plumbing this UI work needs.

Relevant line anchors (may drift slightly; search by symbol if needed):

| Symbol | File |
|--------|------|
| `InstallableVariant` | `GenHub/GenHub/Features/Downloads/ViewModels/InstallableVariant.cs` |
| `ContentVariantInfo` | `GenHub/GenHub.Core/Models/Results/Content/ContentVariantInfo.cs` |
| Grouping + `new InstallableVariant` | `DownloadsBrowserViewModel.cs` ~563–596 |
| Detail `new InstallableVariant` | `ContentDetailViewModel.cs` ~373–378 |
| `AddVariant` / `OnSelectedVariantChanged` | `ContentGridItemViewModel.cs` ~593+, ~718+ |
| `VariantSwap` | `GenHub/GenHub/Features/Downloads/ViewModels/VariantSwap.cs` |
| Card ComboBox | `ContentCardView.axaml` ~212–235 |
| Detail ComboBox | `ContentDetailView.axaml` ~687–714 |
| Tests | `GenHub.Tests/.../ReleaseVariantSelectionTests.cs` |

---

## 3. Task A — Multiple dropdown support (multi-axis infrastructure)

**Goal:** When variants share one axis (lemon controlbar → `resolution`), UI looks like today (one ComboBox). When the flat list contains **two or more distinct `VariantType` values**, render one labeled ComboBox per axis.

**Out of scope:** Computing a cross-product selection that filters a second axis based on the first, or resolving a combined multi-axis artifact. For now, each axis ComboBox still selects among the **subset of `InstallableVariant` rows that share that `VariantType`**, and choosing an option sets `SelectedVariant` to that row (same swap path as today). If axes are mutually exclusive sibling cards (current discoverer behavior: one axis per release split), only one axis will ever appear — which is fine.

### A1. Add `VariantType` to `InstallableVariant`

```csharp
/// <summary>
/// Gets or sets the variant axis (e.g. "resolution", "language", "game-type").
/// Empty means untyped — treated as a single default axis in the UI.
/// </summary>
public string VariantType { get; set; } = string.Empty;
```

### A2. Copy `VariantType` when constructing installables

In **both**:

- `DownloadsBrowserViewModel` (two `new InstallableVariant` sites — single-item inline variants path and multi-sibling path)
- `ContentDetailViewModel` (one site)

Set:

```csharp
VariantType = info.VariantType ?? string.Empty,
```

(or from `ContentVariantInfo` / synthesized info). Empty `VariantType` → treat as `"default"` for grouping so untyped content still shows one dropdown.

### A3. Add `VariantAxisGroup` helper (same Views/ViewModels folder or nested in the VM file)

Minimal shape:

```csharp
public sealed class VariantAxisGroup
{
    public string AxisKey { get; init; } = string.Empty;   // raw VariantType
    public string AxisLabel { get; init; } = string.Empty; // Title-cased for UI ("Resolution")
    public ObservableCollection<InstallableVariant> Options { get; init; } = [];
    public InstallableVariant? SelectedOption { get; set; } // two-way from ComboBox
}
```

Prefer a small ObservableObject if two-way selection needs `INotifyPropertyChanged`.

### A4. Expose grouping on `ContentGridItemViewModel` (and detail VM if doing parity)

Computed / refreshed whenever `Variants` changes:

```csharp
public ObservableCollection<VariantAxisGroup> VariantAxes { get; }
public bool HasMultipleVariantAxes => VariantAxes.Count > 1;
// HasVariants stays: Variants.Count > 0
```

Grouping rules:

1. Group by `VariantType` (case-insensitive); empty → `"default"`.
2. Preserve insertion order of first-seen axes.
3. Axis label: Title-case the key (`resolution` → `Resolution`); special-case known keys if desired (`game-type` → `Game`).
4. When user picks an option in any axis group → set card `SelectedVariant` to that `InstallableVariant` (existing swap path).
5. When `SelectedVariant` changes externally → update the matching axis group's `SelectedOption`.

**Selection semantics (single-axis / current content):** Unchanged. One group, one ComboBox, five options.

**Selection semantics (future multi-axis list):** Choosing an option in axis A sets `SelectedVariant` to that row. Do **not** invent cross-filtering. Document this limitation in a brief code comment on `VariantAxes`.

### A5. Card XAML — replace single ComboBox with ItemsControl

Replace the block at `ContentCardView.axaml` ~212–235 with roughly:

```xml
<ItemsControl ItemsSource="{Binding VariantAxes}"
              IsVisible="{Binding HasVariants}">
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <StackPanel Spacing="2" Margin="0,0,0,2">
        <TextBlock Text="{Binding AxisLabel}"
                   FontSize="10"
                   Foreground="#888888"
                   IsVisible="{Binding DataContext.HasMultipleVariantAxes, ElementName=Root}" />
        <ComboBox ItemsSource="{Binding Options}"
                  SelectedItem="{Binding SelectedOption}"
                  FontSize="11"
                  HorizontalAlignment="Stretch"
                  MinHeight="24"
                  Padding="6,2">
          <!-- keep existing item template: state + Name -->
        </ComboBox>
      </StackPanel>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

**Single-vs-multi caveat:** Axis label `TextBlock` should be **hidden when only one axis** so lemon controlbar looks identical to today (no "Resolution" caption clutter). Use `HasMultipleVariantAxes` (via `ElementName=Root` or a converter). If ElementName binding is awkward inside the template, put `ShowAxisLabels` on each `VariantAxisGroup`.

### A6. Detail-page parity (recommended same PR, or immediate follow-up)

Mirror the same `VariantAxes` binding in `ContentDetailView.axaml` (~687–714). Detail already uses a "Variant" caption — for multi-axis, either:

- Hide the static "Variant" label and use per-axis labels, or
- Keep "Variant" only when `!HasMultipleVariantAxes`.

**Open decision for human:** ship detail parity in the same PR vs card-only first. Default recommendation: **same PR** — the plumbing is shared and the XAML change is small.

### A7. Tests

Extend `ReleaseVariantSelectionTests` (or add a focused test file):

1. Single-axis list → `VariantAxes.Count == 1`, options count matches, no axis label required for UI flag.
2. Mixed `VariantType` values → `VariantAxes.Count == 2`, options partitioned correctly.
3. Selecting an option still swaps `SearchResult` metadata (existing swap test must keep passing).
4. Empty `VariantType` groups into one default axis (regression for SuperHackers / older content if type not set).

Update construction sites in tests to set `VariantType` where relevant (`ReleaseVariantSelectionTests` already has examples with `VariantType = "resolution"` / `"game-type"` on `ContentVariantInfo`).

---

## 4. Task B — Glassmorphic content cards

**Goal:** Make `ContentCardView` match GenHub's glass language used by sidebar + detail actions.

### B1. Restructure the root visual tree

Today:

```xml
<Border Classes="content-card">
  <Grid RowDefinitions="Auto,*,Auto">...</Grid>
</Border>
```

Target (mirror sidebar):

```xml
<Border Classes="content-card" Background="Transparent" ...>
  <Panel>
    <!-- 1. Acrylic blur -->
    <ExperimentalAcrylicBorder CornerRadius="12" IsHitTestVisible="False">
      <ExperimentalAcrylicBorder.Material>
        <ExperimentalAcrylicMaterial
            BackgroundSource="Digger"
            TintColor="#0B0814"
            TintOpacity="0.85"
            MaterialOpacity="0.55" />
      </ExperimentalAcrylicBorder.Material>
    </ExperimentalAcrylicBorder>

    <!-- 2. Gradient tint -->
    <Border CornerRadius="12" IsHitTestVisible="False" Opacity="0.55">
      <Border.Background>
        <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
          <GradientStop Offset="0" Color="#CC020024" />
          <GradientStop Offset="0.55" Color="#993D004D" />
          <GradientStop Offset="1" Color="#E6000000" />
        </LinearGradientBrush>
      </Border.Background>
    </Border>

    <!-- 3. Content (existing Grid) -->
    <Grid RowDefinitions="Auto,*,Auto" ClipToBounds="True">...</Grid>
  </Panel>
</Border>
```

Tune tint/opacity against the downloads browser background until cards read as frosted, not muddy.

### B2. Style edits on `Border.content-card`

- `Background` → `Transparent` (fill comes from acrylic/tint layers).
- `BorderBrush` → glass purple (`#4A2E80` or `{StaticResource SidebarGlassBorder}`); `:pointerover` → brighter (`#7E57C2` / `#AB47BC` at ~60% opacity).
- Keep `CornerRadius="12"`, `Width="240"`, `Height="340"`, existing scale transition.
- `BoxShadow` stays on this outer `Border` (not on buttons). Soften if acrylic already provides depth (`0 8 24 0 #40000000`).
- Thumbnail header: prefer translucent `#22FFFFFF` or dark glass over solid `#333333` so the acrylic shows through above the image; image itself stays opaque.

### B3. Buttons

Align with detail-page glass CTAs where practical:

- Primary Download → purple glass (existing `#9C27B0` can become translucent `#C99C27B0` over acrylic).
- Update → amber glass (`glass-action--warning` palette).
- Add to Profile → green glass (`glass-action--success` palette).

Either reuse shared style resources or duplicate a slim local `card-button` glass variant. Prefer extracting shared brushes to `ThemeResources.axaml` only if you touch both card + detail; otherwise keep local styles to minimize blast radius.

### B4. Badges / chips

Keep badge colors but slightly raise translucency so they sit on glass (`Background` with alpha). Do not redesign badge taxonomy.

### B5. ComboBox on glass

Ensure dropdown remains readable (Fluent dark ComboBox is fine). If contrast fails, set an explicit semi-opaque background on the card ComboBox only.

### B6. Performance note

Many acrylic cards in a virtualized grid can be expensive. If scroll jank appears:

1. Keep acrylic.
2. If needed, swap acrylic for a static frosted brush on cards while scrolling (advanced; only if measured).
3. Do **not** remove glass entirely without flagging it.

---

## 5. Files summary

| File | Task | Action |
|------|------|--------|
| `InstallableVariant.cs` | A | Add `VariantType` |
| `VariantAxisGroup.cs` (new) | A | Axis grouping VM/helper |
| `ContentGridItemViewModel.cs` | A | `VariantAxes`, selection sync, rebuild on variant add |
| `DownloadsBrowserViewModel.cs` | A | Copy `VariantType` into `InstallableVariant` |
| `ContentDetailViewModel.cs` | A | Same copy + optional `VariantAxes` |
| `ContentCardView.axaml` | A+B | Multi ComboBox + glass shell |
| `ContentDetailView.axaml` | A (+ optional B polish) | Multi ComboBox parity |
| `ThemeResources.axaml` | B (optional) | Shared glass border/brush keys |
| `ReleaseVariantSelectionTests.cs` | A | Axis grouping + regression |
| `ContentGridItemViewModelTests.cs` | A | If present, update constructions |

**Do not edit unless a binding gap forces it:**

- `GenericCatalogDiscoverer.cs`, `GenericCatalogResolver.cs`, `ReleaseArtifact.cs`, catalog JSON

---

## 6. GitNexus / safety workflow (repo rule)

Before editing a symbol:

```text
impact({ target: "InstallableVariant", direction: "upstream" })
impact({ target: "ContentGridItemViewModel", direction: "upstream" })
```

Report blast radius (callers, risk). Warn user if HIGH/CRITICAL before proceeding.

Before commit:

```text
detect_changes()
```

Confirm only expected UI/VM symbols moved. Do not rename via find-replace — use GitNexus rename if renaming symbols.

If GitNexus MCP is unavailable in the session, still grep callers of `InstallableVariant` / `AddVariant` / `SelectedVariant` and list them in the PR notes.

---

## 7. Verification checklist

### Build

```powershell
dotnet build GenHub/GenHub/GenHub.csproj
dotnet test GenHub/GenHub.Tests/GenHub.Tests.Core/GenHub.Tests.Core.csproj --filter "FullyQualifiedName~ReleaseVariantSelection|FullyQualifiedName~ContentGridItem"
```

Expect 0 errors. Fix any AVLN XAML warnings you introduce (especially BoxShadow-on-Button).

### Manual — single-axis (must not regress)

1. Register/subscribe test catalog (`SampleCatalogs` shortcuts / `genhub://` flow).
2. Open Downloads → test publisher catalog.
3. Find **Control Bar Pro Lemon Edition ZH**.
4. Expect **one card**, **one** ComboBox (no axis caption), **5** options, **1080p** selected by default.
5. Switch to 4K → Download size/metadata updates; Download acquires the 4K ZIP.
6. Cards without variants (ZH stack / GeneralsOnline pack in the same catalog) show **no** ComboBox.

### Manual — glass

1. Cards read as frosted glass over the downloads background (not solid gray slabs).
2. Hover: border brightens + slight scale; no clipped shadows / no button BoxShadow warnings.
3. Badges, title, and CTA remain legible.
4. Scroll a full grid — no catastrophic frame drop; if yes, note and apply fallback from B6.

### Manual — multi-axis (synthetic, optional)

If no real dual-axis content exists, unit tests are sufficient. Optional: temporarily stub two `VariantType`s in a test VM and screenshot two labeled ComboBoxes.

---

## 8. Open decisions (human / product)

| Decision | Default if unanswered |
|----------|------------------------|
| Accent / glass tint | Match sidebar + subscription dialog (deep purple/crimson gradient, `#4A2E80` border) |
| Detail-page multi-axis parity | **Include in same PR** |
| True cross-product multi-axis selection | **Out of scope** |
| Card height if two ComboBoxes | Prefer keep 340; compress description lines before growing |
| Shared `glass-action` extraction to theme | Only if both card + detail are touched for buttons |

---

## 9. Suggested implementation order

1. **A1–A4** (model + grouping + copy `VariantType`) + unit tests — verify lemon still one axis.
2. **A5** card XAML ItemsControl — verify identical single-axis UX.
3. **A6** detail parity.
4. **B1–B5** glass shell + styles — visual polish last so structure is stable.
5. Full build + manual catalog verification.
6. `detect_changes()` + PR.

---

## 10. Agent prompt seed (copy-paste)

```text
Implement docs/dev/content-card-ui-plan.md Tasks A and B only.

Constraints:
- Do NOT redo GenericCatalogDiscoverer / catalog JSON / resolver work.
- Lemon controlbar must still render as ONE card with ONE unlabeled ComboBox (5 resolution options).
- Multi-axis = rendering infrastructure only; no cross-product resolver.
- Match existing GenHub glass (SidebarLayoutStyles + ContentDetailView glass-action), not a new aesthetic.
- No BoxShadow on Button (AVLN). Run impact() before editing symbols; detect_changes() before commit.
- Verify with: build, ReleaseVariantSelection tests, subscribe test catalog, lemon controlbar card.
```

---

## 11. Resume / context crumbs

- Previously-blocking `ContentDetailView.axaml` AVLN errors are resolved — UI work is verifiable on a full build.
- Memory note from prior session (`catalog_generic_variants.md`) is superseded by **this document** for UI tasks; data-path facts above are the source of truth.
- Deleted (do not restore): `VariantSelectionView.axaml`, `VariantSelectionViewModel`, `VariantOptionViewModel`.
