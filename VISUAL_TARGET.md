# CROSSROADS — Visual Target (presentation bible)

This document is the single source of truth for how the game must **look and feel** on screen.
It governs the visual transformation pass and every future location/scene: each new area is
judged against this document, not against "what the prototype looked like".

Identity: **Crossroads is not a template mobile game.** Its own visual signature is
*fractured light in a waking hall* — dark glass surfaces, one luminous accent (cyan), warm
ember counterpoint, clean geometric architecture, and a heroine (Ari) who is always readable
against the world. No copied assets or UI from other games; the layout *principles* of
professional third-person mobile games (clear HUD zones, thumb-reach controls, contextual
prompts) are adopted, the styling is ours.

---

## 1. Gameplay camera target

- **Third-person over-the-shoulder, always.** The player occupies roughly the left third of
  the screen (or right, if left-handed mirroring applies), never a dot in the distance and
  never covering the screen centre where combat/interaction happens.
- Orbit rig: yaw/pitch driven by the right-side look area; **camera-relative movement**
  (joystick up = away from camera) is the law.
- Smooth follow + rotation damping; **no snapping** outside location arrival.
- **Collision:** sphere-cast pull-in is instant, ease-out is smoothed; camera may never clip
  through walls/floors; indoor headroom bias keeps the roof out of frame.
- Gameplay baseline: distance ≈ 4.4 m, pivot ≈ 1.45 m above feet, slight right-shoulder
  offset (≈ 0.32 m) so the world's centre stays open.
- Dialogue/decisions: closer, lower, opposite-shoulder framing of the NPC (existing
  `Cinematic` blend). Combat: wider + slightly higher (existing `CombatBlend`), plus a tiny
  decaying impulse on heavy hits (never full screen-shake).
- Framing blends are time-based (≥ 0.5 s); a viewer should never see a cut.

## 2. Character presentation target

- **Ari (player)** is the canonical rigged FBX (`Assets/_Project/Art/Characters/Ari/`)
  with the shared animation vocabulary — the same model in gameplay, dialogue and
  cinematics. Idle / walk / run blend from joystick magnitude (gentle tilt = walk, full
  tilt = run), with smooth turn transitions and pivot on sharp reversals.
- **NPCs/enemies** use their canonical avatars (release-pass prefabs) with primitive
  stand-ins only as invisible fallbacks. Distance LOD keeps 60+ characters affordable.
- Hit/wind-up/alert/defeat reactions are always visible on the body, not only in HUD.
- No capsule-with-eyes is ever visible to a player in normal gameplay.

## 3. Environment target (visual slice = FirstLocation / Fracture Hall)

- The first playable location is the **quality benchmark**: an actual place, not a test room.
  Required ingredients: believable architecture (walls, glazing, trusses, balconies),
  a **clear focal point** (the First Light monument at the hall centre), props with purpose
  (crates, benches, planters), vegetation (foliage masses in planters), emissive lanterns,
  and a readable floor plan.
- **Lighting:** one shadowed directional sun through the glazing, bright ambient (trilight),
  emissive surfaces for the glow language. The hall must read clearly at gameplay distance —
  never a dark void. Fog is atmosphere, not fog-of-war: light, cool, distance-only.
- **Sky:** real skybox (procedural sky) visible through glazing and roof trusses; exterior
  skyline silhouettes beyond the glass so the building feels placed in a city.
- Every future location matches this density bar before it ships.

## 4. HUD target (normal gameplay)

Only useful gameplay information, in one visual language (dark glass panels, cyan accent,
rounded shapes, consistent type sizes):

| Zone | Content |
|---|---|
| Top-left | portrait ring + name, **health bar**, echoes chip, area chip (on arrival), autosave pip |
| Top-centre | toasts (temporary, important only); enemy plate while fighting |
| Top-right | **mini-map** (circular radar), compact **objective tracker** (current objective + counter), chapter chip |
| Corners | pause + map buttons (top-right), safe-area aware |
| Bottom-left | movement joystick (floating origin) |
| Bottom-right | **ability buttons** (owned lines only, cooldown state), attack + dodge (combat only) |
| Contextual | interact prompt appears near the action cluster when a target is in range |

Never visible in normal play: raw state dumps, flag names, objective ids, save paths,
diagnostic counters, developer buttons. All of that lives behind the **dev overlay toggle**
(`#if UNITY_EDITOR || DEVELOPMENT_BUILD` + runtime toggle; release builds can never show it).

## 5. Dialogue target

- Transition: camera eases into over-the-shoulder framing, world HUD recedes, dialogue
  sheet rises from the bottom with speaker nameplate + accent strip (per-character colour).
- The NPC is clearly visible; Ari is visible in frame on the near shoulder.
- Decisions are **polished selectable cards** (staggered reveal, path-tinted borders,
  countdown shown when the choice is timed). No internal ids, flags or debug state.
- After the choice, feedback arrives as in-fiction toast lines, never raw effect dumps.

## 6. Combat target

- Uses the existing combat system; presentation upgrades only:
  tracked enemy plate (name + health), player health always on the HUD, floating damage
  numbers (pooled), hit-flash and body reactions on enemies, pooled VFX per ability line,
  wider combat camera framing, and a clean combat HUD (no state machine labels like
  "Alert/Approach" — the body language tells the player).

## 7. Menu target

- **Main menu** on launch: title, CONTINUE (when an autosave exists) / NEW GAME (with
  confirm) / SETTINGS / CREDITS / QUIT — presented over a live view of the hall, input-locked
  until entered. It must feel like a game's front door, not a developer tool.
- **Pause menu:** RESUME / SETTINGS / SAVE & CLOSE / MAIN MENU — same styling.
- **Settings:** the live-stepper panel (sensitivity, camera distance, volume, quality,
  control scale/opacity, handedness) reused by both menus.
- **Save/load presentation:** autosave status is surfaced (pip + "saved" chip + menu
  status); CONTINUE restores the exact run (single autosave slot + recovery mirror).

## 8. Lighting target

- Sun + sky ambient + emissive materials; ≤ 1 shadowed realtime light; per-location
  environment profiles (ambient/fog/sun colour) applied on arrival; baked/static flags for
  everything that never moves; shadow distance tiered by quality (Low: short, no bloom).
- Post-processing restrained: slight bloom + vignette on Balanced/High only.

## 9. Mobile UI target (landscape Android)

- Reference resolution 1920×1080, safe-area fitted, thumb-reach layout above.
- Touch targets ≥ 88 dp; controls scale/opacity/handedness configurable.
- Zero per-frame allocations in HUD/controls; event-driven refresh; pooled effects.

## 10. Performance guardrails (unchanged)

`docs/PERF_BUDGET.json` remains the law: ≤ 820 active renderers, ≤ 420 worst-case draw
calls, ≥ 55 % static-batchable, ≤ 64 ticking behaviours, far clip 120 m. Visual polish is
never bought with budget overruns — see `scripts/profile_scene.py --check`.

---

**Acceptance criterion (the eye test):** a screenshot of normal gameplay must read
instantly as a polished third-person mobile game — lit world, character on screen, clean
HUD, thumb controls — not a Unity prototype with debug overlays.
