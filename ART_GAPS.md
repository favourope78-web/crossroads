# ART_GAPS.md — genuinely unfinished art (honest ledger)

Rule: nothing in this file may appear in the normal player-facing build as a
placeholder. These are known, tracked gaps with owners and plans. Anything not
listed here is considered shipped production art.

## Environment
| Gap | Status | Plan |
|-----|--------|------|
| LOD1/LOD2 meshes for the dressing clusters + signatures | Skipped BY PLAN (conditional on device profiling, which is blocked on the UNITY_LICENSE secret) - clusters are one low-poly merged mesh each, static-batched | Add + LODGroup if device profiling shows a cost |
| Unique interior architecture per campaign room | **Closed:** signature pieces shipped - sculpted pier water (last_summer), octagonal arena ring (dax_arena), wall scaffolding (long_wall) + themed clusters + per-room env lighting in all 13 rooms | - |
| Exterior terrain beyond the plaza ring | Ground plane + skyline towers ship; no rolling terrain | Heightfield ring if the campaign ever goes outdoors |

## Characters
| Gap | Status | Plan |
|-----|--------|------|
| Facial animation / lip sync | Not built (dialogue is over-the-shoulder; faces not in close-up) | Blendshape pass only if cutscenes move to close-ups |
| Unique idle variations per NPC personality | Single shared idle set | 1 variant clip per major NPC (Mara, Sera, Dax) |

## VFX
| Gap | Status | Plan |
|-----|--------|------|
| Particle-based ability bursts | **Closed:** `VfxDirector` + `VfxMath` - one 64-quad pooled runtime system (line-coloured ability bursts, crit hit sparks, hall dust motes), 19 headless tests; +1 ticking behaviour (62/64) | - |
| Weather / ambient particles | **Closed:** hall dust motes included in VfxDirector (12-mote layer, buoyant drift) | - |

## Audio
| Gap | Status | Plan |
|-----|--------|------|
| 11 designed-synth clips (4 ability palettes, 4 ambients, 3 music beds) | **Upgraded** to designed v2 recipes (layered transients, echo tails, event scatter, arps/percussion); still honestly labelled procedural - no licensed recordings exist for the Fracture-specific palettes | Optional: commission/record final versions |
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
