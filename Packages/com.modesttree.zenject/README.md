# Zenject package provenance

This embedded Unity package contains the unmodified Zenject core and editor source from upstream v9.1.0, commit `1993ccaa816b63aff13ff44387294a311f8b76ca`:

https://github.com/modesttree/Zenject/tree/1993ccaa816b63aff13ff44387294a311f8b76ca

The `Source` directory, original assembly definitions, metadata, usage DLL, version and MIT license were copied from `UnityProject/Assets/Plugins/Zenject`. OptionalExtras, tests, examples and reflection baking tools are not installed. The local `package.json` and this document wrap the upstream distribution as an embedded UPM package; runtime sources are unchanged.

Runtime assembly: `Zenject`. Editor assembly: `Zenject-Editor`. The precompiled `Zenject-usage.dll` supplies injection attributes and reflection metadata. Keep its `link.xml` for managed stripping.

Update this package explicitly from a pinned upstream release, retaining the license. No third-party package registry is required.
