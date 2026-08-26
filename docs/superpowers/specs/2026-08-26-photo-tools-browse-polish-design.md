# Photo Tools and Browse Polish Design

## Scope

This change delivers seven bounded improvements without changing unrelated workflows:

1. Signature watermarks drag continuously inside the displayed photo and keep their normalized center when resized.
2. Automatic tiled watermarks become very dense at the maximum density, including slight overlap.
3. Collage output optionally fills cross-axis letterboxing with a blurred, softened copy of the current source image before drawing the sharp source.
4. Primary page transitions cover every current page host and remain interruptible during rapid navigation.
5. Primary navigation uses one selected surface plus one slim indicator, without a nested icon outline.
6. Browse date sections expose every filtered date and every expanded item; import advanced options and the primary action use standard vertical spacing.
7. The rotating import tip reserves the longest message dimensions and animates only its text, so copy changes never resize the card.

## Watermark Interaction

The preview computes the actual `Stretch=Uniform` image rectangle and converts pointer positions into normalized coordinates within that rectangle. The signature visual is positioned by its center on a canvas-sized overlay. Width changes affect only its dimensions; `CenterX` and `CenterY` remain unchanged. Export continues to consume the same normalized center values.

Automatic tile spacing maps density from a relaxed positive gap at zero to a small negative gap at one. Preview and export share the same spacing calculation so density is visually truthful.

## Optional Collage Background

`CollageOptions` gains a boolean blurred-background option. When enabled, each source occupies its existing slot; if its cross-axis dimension is smaller than the canvas, a cover-scaled copy of that same source is cropped to the slot, blurred, slightly darkened/softened, then the unmodified source is centered above it. When disabled, existing white fill behavior remains unchanged.

## Navigation and Layout

Page transition host resolution is the single source of truth for all current first-level destinations. The transition cancels/replaces prior animations and applies the shared motion duration/easing. The sidebar selected state removes the icon-local outline and retains a single semantic selected background and slim animated indicator. The import primary button receives tokenized vertical margin.

The import tip card uses a fixed width and minimum height sized for the longest supported Chinese message. Text changes use a short shared fade/translate transition inside that stable container.

## Browse Completeness

Date grouping is built from the full filtered cache, never from a visible-page slice. Expansion rebuilds the flat wall with all items in the selected section. Tests cover multiple dates and an expanded group larger than the former page size, and verify that wall counts equal header plus expanded item counts. Thumbnail decoding remains viewport-driven.

## Verification

Add focused xUnit regression tests for calculator/state/service/view contracts, then run the affected test project, Release build with warnings as errors, full Release tests, and publish as required by `docs/testing.md`. Append the verified changes to `docs/agent-change-log.md`.
