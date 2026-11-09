# Disabled tests

Tests disabled because they failed during the final run of a delivery. Each one is dealt with later; re-enabling a
test removes its line.

| Test | Error | Date | Suspected cause |
|---|---|---|---|
| `OmniEurope.Documents.StressTests.Jpeg2000ScaleTests.A_large_tiled_image_decodes_exactly_within_the_time_limit` | Failed in the full parallel run (23.5 s against a 20 s wall-clock limit), passed when rerun alone. | 2026-11-09 | Timing limit sensitive to the load of the parallel run; no JPEG 2000 code changed in that delivery. |
