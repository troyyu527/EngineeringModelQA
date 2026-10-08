# Sample fixtures

All IFC files in this folder are synthetic. They were generated for this project with the xBIM toolkit and contain no
data from real projects. They may be used under the same license as the project.

| File | Content |
|------|---------|
| `baseline.ifc` | IFC4, two storeys: 14 beams, 12 columns, 2 slabs, 4 walls (one `IfcWallStandardCase`). Seeded defects below |
| `revision.ifc` | Later submission of the same model: some defects fixed, one new, one beam removed and one added |
| `empty-model.ifc` | IFC4 project with one storey and no elements |
| `warnings.ifc` | One beam plus data the reader does not read (a door, an enumerated property) |
| `invalid/ifc2x3.ifc` | Header declares IFC2X3 (rejected: IFC4 only) |
| `invalid/not-ifc.ifc` | Plain text |
| `invalid/empty-file.ifc` | Zero bytes |

Elements are identified below by their design key, which is also written to the element's `Description`.
Member marks are in the property `ProjectInformation.MemberMark`.

## Seeded defects in `baseline.ifc`

| Element | Defect |
|---------|--------|
| B-103 | No `ProjectInformation` property set (missing mark) |
| C-104 | Mark is blank |
| B-105, B-106 | Both use the mark `B-105` |
| C-203 | Mark `C-2O3` (letter O) does not match `^[BC]-\d{3}$` |
| B-204 | Mark `B204` does not match the pattern |
| B-207 | No material on the element or its type |
| C-206 | Not contained in any storey |
| W-4 | Non-load-bearing (`Pset_WallCommon.LoadBearing = FALSE`), no material: excluded by the structural-full profile |

Materials: beams use a material profile set usage, columns a direct `IfcMaterial`, slabs and walls W-1/W-2 a layer set
usage, wall W-3 only has a material on its wall type. Beams and columns also get `Pset_*Common.Reference` from their
type.

## Changes in `revision.ifc`

| Element | Change |
|---------|--------|
| B-103, C-104 | Marks filled in (resolved) |
| B-204 | Renamed to `B-204` (resolved) |
| C-203 | Renamed to `C203`, still wrong (persistent with a changed value) |
| B-104 | Mark removed (new defect) |
| B-105, B-106 | Still share `B-105` (persistent) |
| B-207 | Removed (unmatched, not resolved) |
| B-208 | Added |
| C-206 | Still not in a storey (persistent) |

Expected results: `samples/expected/manifest.json`.
