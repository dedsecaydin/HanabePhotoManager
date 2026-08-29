# Import Resume Dialog and Recovery Implementation Plan

**Goal:** Make interrupted-import recovery explicit, themed, responsive, and backward-compatible.

**Architecture:** A pure resolver converts resume entries into stable target directories; a themed dialog returns Continue/Discard/Later; MainWindow navigates before awaiting recovery and surfaces failures visibly.

### Task 1: Legacy target resolver
- [ ] Add failing tests for new path, legacy base path, unique remarked folder, and ambiguous folders.
- [ ] Implement resolver and integrate it into `ResumePendingImportAsync`.

### Task 2: Themed startup dialog
- [ ] Add failing XAML/source tests for the three explicit actions and no native resume MessageBox.
- [ ] Implement the dialog; navigate to Import before resume and expose visible outcome.

### Task 3: Verify and publish
- [ ] Update status/handoff/log, run Release build and full tests, merge, overwrite `D:\hanabe-publish-v2`, and launch.
