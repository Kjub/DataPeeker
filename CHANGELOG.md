# Changelog

### [0.4.1] - 2026-06-11

#### Fixed
 - Model windows now survive domain reloads (script recompile, entering play mode) instead of going blank — the model type is serialized and the tree is rebuilt on reload
 - Reopening a model after a domain reload focuses the existing window instead of opening a duplicate
 - Closing a stale window no longer unregisters a freshly opened window of the same model type

### [0.4.0] - 2026-05-14

#### Added
 - Color based on how deep the user is, its easier to distinguish the elements based on depth

#### Fixed
 - Fixed the issue with infinite loop when model had an item that references itself

### [0.3.2] - 2026-01-29

#### Added
 - Simplification of types based on namespace (simplify all data elements if they are from namespace "UnityEngine" for example)

### [0.3.1] - 2025-10-21

#### Fixed
 - Correctly showing "Nullable" variables if not null

### [0.3.0] - 2025-06-23

#### Added
 - Selection of element which stays after clearing search
 - Focus on search bar after opening window

### [0.2.0] - 2025-05-15

#### Added
- Setters for writeable variables

#### Changed
- Search overhaul so it isn't all expanded after searching

### [0.1.2] - 2024-10-22
#### Fixed
- Model window now shows nullable types correctly

### [0.1.1] - 2024-10-22
#### Added
- Search bar to models list

### [0.1.0] - 2024-09-22
#### Added
- Data Peeker prototype
