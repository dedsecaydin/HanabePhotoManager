# Settings Sound Section and Safety Dialog Implementation Plan

**Goal:** Make sound settings discoverable as a dedicated settings section and unify move-import confirmation with Hanabe UI.

1. Add failing XAML/source tests for the sound navigation section and removal of the native move-confirmation MessageBox.
2. Add the token-driven `ImportMoveSafetyConfirmationWindow` and route preflight confirmation through it.
3. Move sound controls from Appearance to a standalone `SoundSection`; move duplicate-window restore to Library settings.
4. Update settings navigation visibility logic and documentation.
5. Run Release build, full tests, publish, deploy, and runtime launch verification.
