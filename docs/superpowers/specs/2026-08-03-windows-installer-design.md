# Windows Installer and Desktop Shortcut Design

## Goal

Deliver Hanabe Photo Manager as a Windows installation that can be found and opened from a stable desktop shortcut on the user's local computer.

## User-visible behavior

- The release produces `HanabePhotoManager-Setup-x64.exe` as the primary artifact.
- Running Setup installs the self-contained x64 application, creates a desktop shortcut named `Hanabe Photo Manager`, creates a Start menu shortcut, and registers uninstall information.
- Installing a newer version upgrades the existing installation instead of creating a second copy.
- The desktop and Start menu shortcuts are recreated or repaired during upgrade and point to the newly installed executable.
- Downgrades are blocked with a clear message. Uninstall removes installed binaries and shortcuts but never deletes the photo library or user application data.
- The app's existing `设置 → 启动与窗口 → 开机自启动` option remains visible. Remote-sharing service startup status is added by the remote-sharing feature branch, separately from application startup.
- Settings exposes a selectable version tree. The current version is marked, newer catalog entries are marked `可更新`, older entries are view-only history, and the selected release notes are read in a bounded vertical scrolling pane.
- Application version, release catalog, MSI version, Setup version, and artifact name come from the same release version input.

## Packaging architecture

- `installer/HanabePhotoManager.Installer` owns an SDK-style WiX project restored through NuGet; it does not require a globally installed packaging application.
- The MSI payload consumes a clean `dotnet publish` directory. A WiX Burn bundle exposes the single Setup EXE requested by the user.
- Product version is passed once to the publish script and propagated to assembly metadata, artifact names, MSI, and bundle metadata.
- Upgrade identity is stable across versions. Product/package identities may change according to Windows Installer major-upgrade rules.
- Installer sources contain no credentials, user settings, caches, logs, media, or absolute personal paths.

## Installation scope

The initial local build installs per machine under `Program Files\Hanabe Photo Manager` and therefore requests elevation. This matches the future ShareHost Windows service requirement. Project sources and generated release artifacts remain under `D:\HanabePhoto`; only installed application files use the normal Windows program directory.

## Release and verification

`tools/Publish-Clean.ps1` becomes version-parameterized and builds the published payload, MSI, Setup EXE, and checksums under `D:\HanabePhoto\artifacts\<version>`. The release gate installs an older disposable build, upgrades it with the new Setup, resolves the desktop shortcut target, launches the installed executable, and verifies uninstall leaves user data untouched.

The portable ZIP is not a user-facing deliverable for this requirement.
