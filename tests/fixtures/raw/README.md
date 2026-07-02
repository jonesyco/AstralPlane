# RAW fixtures

Camera RAW files are large (10–25 MB), so they are **not committed**. Drop one
or more RAW files here to enable the RAW develop integration test
(`RawDevelopTests.DevelopsRawToFullResolution`). Supported extensions:
`.arw .cr2 .cr3 .nef .dng .orf .raf .rw2`.

When no RAW file is present the develop test is skipped. An always-on test still
verifies that Magick.NET's RAW decode delegate is available in the build.
