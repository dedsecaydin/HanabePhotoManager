# HEIF paired import and duplicate lookup optimization

User requirements: automatically generate JPG when HEIF/HEIC/HIF is imported,
retain original HEIF bytes, and improve duplicate-import detection latency.
Project, temporary jobs, builds, evidence and backups remain on D:.

## Import behavior

- The verified original stays in the existing JPG生图 category (or the user's
  explicitly routed category) with its HEIF-family extension. The primary image
  is decoded by bundled libheif and saved as a quality-95 JPEG alongside it.
- The original is never rewritten to store Windows comments. JPEG is an 8-bit
  derivative; originals retain bit depth and metadata. ICC profiles exposed by
  the decoder are copied. Sony-camera HDR appearance has not been calibrated.
- Existing JPEGs and destinations reserved for camera JPEGs are not overwritten.
- A small adjacent content receipt identifies generated JPGs, allowing retries
  to reuse valid output and regenerate a missing output. Modified user JPGs are
  retained and a new name is selected.
- Copy/move receipts remain in the import recovery queue until conversion is
  complete. A skipped existing HEIF can acquire a missing JPG without copying
  its original again. Source-card paths are never conversion destinations.
- Old skipped recovery entries resolve their original location before settling;
  partial moves retain their original location through verified receipts.

## Duplicate lookup

- Check exact size/SHA-256 matches first. Build an original-name/length metadata
  index lazily, once per target folder per batch, rather than rereading every
  destination for every source. Cache metadata with file stamps across restarts.
- Confirm matched metadata with the source SHA-256 and revalidate candidate
  existence/stamps before accepting an indexed match. File-change stamps
  invalidate persistent cache entries. Transfer/deletion verification continues
  using fresh hashes.
- Reuse just-verified source hashes; source-only checks omit files already known
  to duplicate a library file. Enumerating destination files runs off the UI
  thread, with source count and filename shown during duplicate checks.

## Validation

Tests use the public libheif example file. Original bytes, primary-image JPEG
dimensions, repeat imports, reserved-name collisions, altered derivatives,
corrupt input, cancellation, skipped legacy records and partial moves are
covered. Full build/test evidence is in `.artifacts/heif-speed-20261023`.

The metadata microbenchmark uses 20 queries, 40 targets and simulated 1ms read
latency. It compares one target-index build per source (old access pattern)
with a shared batch index: 800 reads versus 40. Timings are fixture results,
not measurements of the user's NAS or storage card. First uncached SHA-256 of
large files still requires reading the content.
