# Integrated Semantic Search Design

## Outcome

Semantic search becomes a browse condition instead of a separate page. A natural-language query narrows the existing photo wall to CLIP-ranked candidates, while the existing date, rating, category, retouch, file-type, smart-category, and people filters continue to apply.

## Architecture

`SemanticSearchViewModel` remains the asynchronous coordinator for `ClipSemanticSearchService`, `SqliteSemanticIndexStore`, and `ModelCatalog`. It exposes query, progress, cancellation, result paths, and result-change notification to `MainWindowViewModel`. On the first non-empty query it calls `EnsureIndexAsync` before `SearchAsync`; later queries reuse the index.

`MainWindowViewModel.ApplyFilters` first applies all existing browse predicates, then intersects the remaining photos with semantic result paths and orders them by semantic rank. Clearing the semantic query removes that candidate set and restores normal browse ordering. The existing grid, treemap, viewer, and navigation commands remain the only photo presentation and opening paths.

## UI

The browse conditions expander receives an `Input.TextBox` with a natural-language prompt. Index/search state appears directly below it using the shared progress bar, secondary text brush, and `Button.Ghost` cancellation action. No page-local colors, control templates, radii, shadows, or fonts are introduced. The standalone sidebar item and `SemanticSearchView` page host are removed from the shell.

## Error and Empty States

Model/index errors are shown in the inline status text without breaking normal browsing. Cancellation preserves the current library and filter state. A completed query with no matches leaves the photo wall empty and explains that the user can change or clear the description.

## Verification

Tests cover first-query indexing, result publication, cancellation-capable state, semantic intersection and rank ordering, query clearing, Design System resource use, and removal of the standalone navigation/page. Release build, full tests, self-contained publish, installed-app launch, CPU sampling, browse-page UI inspection, and a real description query provide final evidence.
