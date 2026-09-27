# NAME-001 — Messy real-world naming

> **Provenance:** `synthetic_engineering_benchmark` · `etabs_verified: false` — this is a hand-designed synthetic benchmark. It is **not** ETABS output and must never be labelled as such.

**Category:** naming · **Fixture set:** synthetic

## Purpose

Names, IDs and story names in real-world styles, including a beam labelled 'C5'. Quentra must rely on the `type` field, not on names, and must pass the source's own warning through.

**Expected Quentra behaviour:** classify by type field; pass source warning through

## A. Structural truth

- Beam 'Beam_A12', beam 'B-01', column 'COLUMN_CORE_3', beam labelled 'C5' (type Beam!), wall 'Wall Pier 02', slab 'SLAB-L02-A'
- Stories 'Base-L01' and 'L02 (Typ)'; section names with spaces; material 'C30/37'

## B. ETABS-like input (`snapshot.json`)

- Length unit in the snapshot: `m`. All coordinates, section dimensions, bar diameters and spacings are in this unit; areas in unit², shear areas per length in unit²/unit.
- Elements: 6, materials: 1, sections: 4, design results: 0.
- Run options (from `manifest.json`): `{"quantifySteel":false}`

## C. Expected Quentra output — calculation

- Model length unit: m (1 m = 1 m).

### Beam `frm-000417`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section BM 300X600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Beam `frm-000418`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section BM 300X600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Column `frm-000102`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(0^2 + 0^2 + 3.5^2) = 3.5 m
Section COL-450SQ: rectangle b x h = 0.45 m x 0.45 m, A = 0.2025 m2
Gross volume = A x L = 0.2025 x 3.5 = 0.70875 m3 (no openings: net = gross)
Status: Included
```

### Beam `frm-000419`

```text
Length L = sqrt(dx^2 + dy^2 + dz^2) = sqrt(6^2 + 0^2 + 0^2) = 6 m
Section BM 300X600: rectangle b x h = 0.3 m x 0.6 m, A = 0.18 m2
Gross volume = A x L = 0.18 x 6 = 1.08 m3 (no openings: net = gross)
Status: Included
```

### Wall `area-000031`

```text
Host polygon (4 vertices) area in its own plane = 14 m2 (shoelace on local u-v coordinates)
Thickness t = 0.3 m (Core Wall 300)
Gross volume = 14 x 0.3 = 4.2 m3
Net volume = (14 - 0) x 0.3 = 4.2 m3
Status: Included
```

### Slab `area-000077`

```text
Host polygon (4 vertices) area in its own plane = 72 m2 (shoelace on local u-v coordinates)
Thickness t = 0.2 m (SLAB 200)
Gross volume = 72 x 0.2 = 14.4 m3
Net volume = (72 - 0) x 0.2 = 14.4 m3
Status: Included
```

## Independent hand check

These values were worked out by hand when the case was written; the generator refuses to write the fixture unless the calculator reproduces every one of them (relative tolerance 1e-9).

| Quantity | Working | Value |
|---|---|---|
| `byElementType.Beam.netConcreteM3` | three beams incl. the one named 'C5' | 3.24 |
| `byElementType.Column.netConcreteM3` | 0.45² × 3.5 | 0.70875 |
| `byStory.Base-L01.netConcreteM3` | column 0.70875 + wall 4.2 | 4.90875 |

## Expected totals

| Total | Value |
|---|---|
| Gross concrete | 22.54875 m³ |
| Opening deduction | 0 m³ |
| Net concrete | 22.54875 m³ |
| Steel (known) | unknown (null) |
| Concrete completeness | Complete |
| Steel completeness | NotEvaluated |

**By story** (Σ equals the model total):

| Story | Gross m³ | Net m³ | Steel kg |
|---|---|---|---|
| Base-L01 | 4.90875 | 4.90875 | unknown (null) |
| L02 (Typ) | 17.64 | 17.64 | unknown (null) |

## Expected warnings

| Code | Severity | Subject | Blocks finalization | Meaning |
|---|---|---|---|---|
| `SOURCE_WARNING` | Warning | `EXTRACT_ROUNDING` | no | A warning reported by the extractor/source was passed through. Subject = the source's own code. |

**Finalization:** allowed

See `docs/CONVENTIONS.md` for every detailing and counting convention used above.
