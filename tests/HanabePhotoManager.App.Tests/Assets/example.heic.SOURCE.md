HEIF integration fixture from the libheif project's public example:

https://github.com/strukturag/libheif/blob/master/examples/example.heic

Retrieved 2026-10-23. SHA-256:
7F8B363E4936C0666A25F64F3A92FDA10BD8E5453BE4592530B65A55DD98F3F2

Used only by tests, not distributed in the application output. Tests select
the primary image, decode the resulting JPEG, compare original bytes, verify
repeat-import skipping, cancellation, collisions, and resume behavior.
