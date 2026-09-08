# ART_GAPS.md — genuinely unfinished art (honest ledger)

Rule: nothing in this file may appear in the normal player-facing build as a
placeholder. These are known, tracked gaps with owners and plans. Anything not
listed here is considered shipped production art.

## Environment
| Gap | Status | Plan |
|-----|--------|------|
| LOD1/LOD2 meshes for the 8 dressing clusters | Not built (clusters are already low-poly, single merged mesh; static-batched) | Add decimated LOD variants + LODGroup if profiling on device shows a cost |
| Unique interior architecture per campaign room (rooms currently share the hall kit vocabulary) | Ships as themed clusters + per-room env lighting; rooms are deliberately same-city architecture | Author 2-3 room-specific signature pieces (pier water plane, arena ring) |
| Exterior terrain beyond the plaza ring | Ground plane + skyline towers ship; no rolling terrain | Heightfield ring if the campaign ever goes outdoors |

## Characters
| Gap | Status | Plan |
|-----|--------|------|
| Facial animation / lip sync | Not built (dialogue is over-the-shoulder; faces not in close-up) | Blendshape pass only if cutscenes move to close-ups |
| Unique idle variations per NPC personality | Single shared idle set | 1 variant clip per major NPC (Mara, Sera, Dax) |

## VFX
| Gap | Status | Plan |
|-----|--------|------|
| Particle-based ability bursts (ember sparks, tide splash) | Ships with emissive materials + camera feedback + damage numbers; no GPU particles | Author 3 small particle systems (one per ability line) once device perf is measured |
| Weather / ambient particles (dust motes in hall shafts) | Not built | One cheap dust system in the hall only |

## Audio
| Gap | Status | Plan |
|-----|--------|------|
| 11 procedural placeholder clips (noted by validate_assets: 27 clips, 16 recorded CC0) | Ship as-is, they are mixed low | Replace with recorded versions |
| Voice-over | Not built | Out of scope for this pass |

## UI
| Gap | Status | Plan |
|-----|--------|------|
| Animted menu background (video/parallax) | Ships with static keyart + glass | Parallax layers if desired |

## Verification
| Gap | Status | Plan |
|-----|--------|------|
| On-device screenshots | Not possible in this sandbox (no Unity editor/runtime) | Unity 6 + URP open → Android build → capture the four launch states; acceptance per brief |
| Unity in-editor visual confirmation of the new keyart/menu/intro/chapter screens | Offline composites only (real art + real layout constants) | Same editor pass |
