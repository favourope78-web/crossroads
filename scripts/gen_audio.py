#!/usr/bin/env python3
"""
CROSSROADS audio pass - 27 clips: 16 recorded CC0 SFX + 11 clearly-marked procedural beds.

Release state (see reference/audio_source/AUDIO_STATUS.json, written by this script):
  * RECORDED (CC0, Kenney - reference/audio_source/*.ogg + SOURCES.json/LICENSE.txt): every
    combat / UI / dialogue / save / transition / objective / unlock one-shot. Decoded with
    ffmpeg (or python soundfile), silence-trimmed, peak-normalised, resampled to 22.05 kHz mono.
    A missing recording or decoder is a hard error (CROSSROADS_AUDIO_ALLOW_FALLBACK=1 overrides
    for local iteration and records the substitution as a placeholder).
  * PROCEDURAL PLACEHOLDERS (the only remaining ones): the four Fracture ability palettes
    (Ember crackle / Tide shimmer / Stone impact / Hollow drone), 4 ambient loops and 3 music
    beds - no licensed recording fits them, so they are synthesised deterministically (numpy,
    per-clip seed) until composed audio exists. They are listed in AUDIO_STATUS.json.
Everything is 22.05 kHz mono 16-bit WAV (small, decodes cheaply on Android); metas set Vorbis +
DecompressOnLoad for SFX, CompressedInMemory for ambient loops, Streaming for music beds.

Outputs (idempotent - byte-identical on every run, registry-tracked GUIDs 0x300..):
  Assets/_Project/Audio/SFX/sfx_*.wav       one-shots (mono)
  Assets/_Project/Audio/Ambient/amb_*.wav   seamless 8 s loops
  Assets/_Project/Audio/Music/mus_*.wav     seamless 8-bar placeholder beds
The scene generator binds them to the GameAudio component (scripts/gen_firstlocation_scene.py).
"""
import json, os, struct, wave
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AUD = os.path.join(ROOT, "Assets/_Project/Audio")
REG_PATH = os.path.join(ROOT, "scripts/hall_guids.json")
SR = 22050
rng = np.random.default_rng(20260905)


def g32(n): return ("c0a1fed2" + ("%024x" % n))[:32]


# ---------------------------------------------------------------- dsp helpers
def t(seconds): return np.arange(int(SR * seconds)) / SR


def env(n, a=0.005, d=0.1, s=0.0, r=0.1, hold=None):
    """ADSR in seconds -> array of n samples."""
    a_n, d_n, r_n = int(a * SR), int(d * SR), int(r * SR)
    if hold is None:
        h_n = max(n - a_n - d_n - r_n, 0)
    else:
        h_n = int(hold * SR)
    out = np.concatenate([
        np.linspace(0, 1, max(a_n, 1)),
        np.linspace(1, s, max(d_n, 1)),
        np.full(h_n, s),
        np.linspace(s, 0, max(r_n, 1))])
    if len(out) < n: out = np.concatenate([out, np.zeros(n - len(out))])
    return out[:n]


def sine(freq, seconds, phase=0.0):
    tt = t(seconds)
    if np.isscalar(freq): return np.sin(2 * np.pi * freq * tt + phase)
    return np.sin(2 * np.pi * np.cumsum(freq) / SR + phase)   # freq array = sweep


def sweep(f0, f1, seconds, curve=1.0):
    n = int(SR * seconds)
    x = np.linspace(0, 1, n) ** curve
    return f0 + (f1 - f0) * x


def noise(seconds): return rng.uniform(-1, 1, int(SR * seconds))


def lowpass(x, cutoff):
    """one-pole IIR lowpass (cheap, good enough for foley)."""
    rc = 1.0 / (2 * np.pi * max(cutoff, 10.0))
    alpha = (1.0 / SR) / (rc + 1.0 / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha * (x[i] - acc)
        y[i] = acc
    return y


def lowpass_sweep(x, c0, c1):
    """time-varying one-pole lowpass (cutoff sweeps c0 -> c1)."""
    n = len(x)
    cut = np.linspace(c0, c1, n)
    alpha = (1.0 / SR) / (1.0 / (2 * np.pi * np.maximum(cut, 10.0)) + 1.0 / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(n):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def bandpass(x, lo, hi):
    return lowpass(highpass(x, lo), hi)


def delay_tail(x, seconds=0.09, feedback=0.35, mix=0.3):
    """Cheap feedback delay for short reverb-ish tails."""
    d = int(SR * seconds)
    out = x.copy()
    tap = x.copy()
    for _ in range(3):
        tap = np.concatenate([np.zeros(d), tap[:-d] * feedback]) if len(tap) > d else tap * 0
        out = out + tap * mix
    return out


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def soft(x, drive=1.0): return np.tanh(x * drive)


def norm(x, peak=0.9):
    m = np.max(np.abs(x)) if len(x) else 1.0
    return x * (peak / m) if m > 1e-6 else x


def fade_edges(x, ms=8):
    n = int(SR * ms / 1000)
    if n * 2 >= len(x): return x
    x = x.copy()
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def loopable(x, xf_seconds=0.6):
    """crossfade the tail into the head so the clip loops seamlessly."""
    n = int(SR * xf_seconds)
    if n * 2 >= len(x): return x
    body = x[:-n].copy()
    tail = x[-n:]
    ramp = np.linspace(0, 1, n)
    body[:n] = body[:n] * ramp + tail * (1 - ramp)
    return body


def write_wav(path, data):
    data = np.clip(data, -1, 1)
    pcm = (data * 32767).astype("<i2")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


# ---------------------------------------------------------------- SFX recipes
def sfx_attack_swing():
    n = noise(0.28)
    x = lowpass_sweep(highpass(n, 400), 5200, 900) * env(len(n), 0.01, 0.12, 0.25, 0.14)
    return norm(x, 0.8)


def sfx_attack_hit():
    thump = sine(sweep(160, 55, 0.22, 0.6), 0.22) * env(int(SR * 0.22), 0.002, 0.1, 0.2, 0.1)
    click = highpass(noise(0.05), 2500) * env(int(SR * 0.05), 0.001, 0.02, 0.0, 0.03)
    x = np.concatenate([click, np.zeros(len(thump) - len(click))]) * 0.7 + soft(thump, 2.2)
    return norm(x, 0.9)


def sfx_dodge():
    n = noise(0.34)
    x = lowpass_sweep(highpass(n, 700), 1200, 6500) * env(len(n), 0.04, 0.1, 0.5, 0.18)
    return norm(x, 0.6)


def sfx_player_hurt():
    body = soft(sine(sweep(210, 70, 0.36), 0.36) * 1.6, 3.0) * env(int(SR * 0.36), 0.002, 0.15, 0.3, 0.18)
    grit = lowpass(noise(0.36), 900) * env(int(SR * 0.36), 0.001, 0.08, 0.0, 0.05)
    return norm(body + grit * 0.5, 0.9)


def sfx_enemy_defeat():
    tone = sine(sweep(330, 60, 0.7, 1.4), 0.7) * env(int(SR * 0.7), 0.01, 0.3, 0.35, 0.3)
    hiss = lowpass_sweep(noise(0.7), 3000, 300) * env(int(SR * 0.7), 0.02, 0.4, 0.2, 0.25)
    return norm(soft(tone * 1.4, 1.8) + hiss * 0.45, 0.9)


def sfx_ability_ember():
    """Ignition transient + flame roar + sub thump + crackle shower + ember tail."""
    dur = 0.8
    n = int(SR * dur)
    ignition = highpass(noise(0.05), 2500) * env(int(SR * 0.05), 0.001, 0.02, 0.0, 0.03)
    ignition = np.concatenate([ignition, np.zeros(n - len(ignition))])
    roar = bandpass(noise(dur), 300, 1800) * env(n, 0.015, 0.28, 0.35, 0.3)
    sub = soft(sine(sweep(150, 55, 0.35, 0.7), 0.35) * 2.2, 2.2) * env(int(SR * 0.35), 0.002, 0.12, 0.2, 0.18)
    sub = np.concatenate([sub, np.zeros(n - len(sub))])
    crackle = highpass(noise(dur), 2200) * (rng.uniform(0, 1, n) > 0.93) * env(n, 0.03, 0.4, 0.15, 0.3)
    pops = np.zeros(n)
    for pos in (0.18, 0.29, 0.41, 0.55, 0.67):
        i = int(pos * SR)
        seg = sine(sweep(rng.uniform(900, 1600), 300, 0.045), 0.045) * env(int(SR * 0.045), 0.001, 0.03, 0.0, 0.02)
        pops[i:i + len(seg)] += seg * 0.35
    x = soft(roar, 1.4) * 0.9 + ignition * 0.8 + sub + crackle * 0.7 + pops
    return norm(delay_tail(x, 0.07, 0.3, 0.25), 0.9)


def sfx_ability_tide():
    """Water whoosh both ways + droplet arpeggio + splash transient + glassy shimmer."""
    dur = 0.85
    n = int(SR * dur)
    whoosh = lowpass_sweep(noise(dur), 500, 4200) * env(n, 0.08, 0.25, 0.3, 0.3)
    backwash = np.zeros(n)
    bw = lowpass_sweep(noise(0.5), 3800, 700) * env(int(SR * 0.5), 0.15, 0.2, 0.2, 0.25)
    backwash[int(0.3 * SR):int(0.3 * SR) + len(bw)] += bw * 0.7
    splash = highpass(noise(0.09), 1600) * env(int(SR * 0.09), 0.001, 0.05, 0.0, 0.04)
    splash = np.concatenate([splash, np.zeros(n - len(splash))])
    drops = np.zeros(n)
    for k, pos in enumerate((0.12, 0.2, 0.28, 0.37, 0.47, 0.58, 0.7)):
        f = 900 + 180 * k + rng.uniform(-60, 60)
        i = int(pos * SR)
        seg = sine(sweep(f, f * 0.55, 0.06), 0.06) * env(int(SR * 0.06), 0.001, 0.035, 0.0, 0.025)
        drops[i:i + len(seg)] += seg * (0.3 - 0.02 * k)
    chord = sum(sine(f, dur, rng.uniform(0, 6.28)) * w for f, w in
                ((523.25, 0.5), (659.25, 0.35), (783.99, 0.3))) * env(n, 0.12, 0.35, 0.45, 0.35)
    x = whoosh * 0.8 + backwash + splash * 0.6 + drops + chord
    return norm(delay_tail(x, 0.06, 0.28, 0.2), 0.85)


def sfx_ability_stone():
    """Deep impact + grinding layer + random rubble rattles + aftershock thud."""
    dur = 0.75
    n = int(SR * dur)
    impact = soft(sine(sweep(95, 36, 0.45, 0.5), 0.45) * 2.2, 2.5) * env(int(SR * 0.45), 0.002, 0.18, 0.25, 0.22)
    impact = np.concatenate([impact, np.zeros(n - len(impact))])
    grind = bandpass(noise(dur), 90, 500) * env(n, 0.01, 0.2, 0.25, 0.35) * 0.8
    rattles = np.zeros(n)
    for pos, f in ((0.08, 700), (0.14, 520), (0.23, 610), (0.31, 440), (0.44, 560), (0.58, 400)):
        i = int(pos * SR)
        seg = bandpass(noise(0.07), 250, 1400) * env(int(SR * 0.07), 0.002, 0.03, 0.05, 0.03)
        seg *= 0.25 + 0.2 * sine(f * 0.01, 0.07)
        rattles[i:i + len(seg)] += seg * 0.5
    aftershock = soft(sine(sweep(70, 42, 0.3, 0.6), 0.3) * 1.8, 2.0) * env(int(SR * 0.3), 0.25, 0.1, 0.15, 0.15)
    aftershock = np.concatenate([np.zeros(int(SR * 0.42)), aftershock, np.zeros(n - int(SR * 0.72))])[:n]
    x = impact + grind + rattles + aftershock
    return norm(delay_tail(x, 0.08, 0.32, 0.22), 0.95)


def sfx_ability_hollow():
    """Reverse-swell into a hollow hit: tritone drone, air whisper, metallic ring, echo pips."""
    dur = 0.9
    n = int(SR * dur)
    d = sum(sine(f, dur) for f in (55, 56.7, 78.4, 110.9, 156.8)) / 5
    swell = d * env(n, 0.35, 0.25, 0.8, 0.25)          # slow reverse-ish swell
    whisper = bandpass(noise(dur), 1200, 6000) * env(n, 0.3, 0.15, 0.6, 0.25) * 0.3
    hit = soft(sine(sweep(180, 60, 0.22, 0.8), 0.22) * 2.0, 2.0) * env(int(SR * 0.22), 0.28, 0.08, 0.1, 0.12)
    hit = np.concatenate([np.zeros(int(SR * 0.28)), hit, np.zeros(n - int(SR * 0.5))])[:n]
    ring = (sine(1244.5, dur) + 0.5 * sine(1247.0, dur)) * env(n, 0.3, 0.2, 0.4, 0.4) * 0.12
    x = soft(swell * 2.0, 1.8) + whisper + hit + ring
    return norm(delay_tail(x, 0.11, 0.4, 0.3), 0.85)


def sfx_ui_tap():
    x = sine(1320, 0.06) * env(int(SR * 0.06), 0.002, 0.03, 0.0, 0.03)
    return norm(x, 0.5)


def sfx_ui_confirm():
    a = sine(880, 0.08) * env(int(SR * 0.08), 0.002, 0.04, 0.0, 0.04)
    b = sine(1320, 0.12) * env(int(SR * 0.12), 0.002, 0.06, 0.0, 0.06)
    return norm(np.concatenate([a, b]), 0.55)


def sfx_decision_lock():
    dur = 0.9
    stab = sum(sine(f, dur) * w for f, w in ((196.0, 1.0), (293.66, 0.8), (392.0, 0.6), (587.33, 0.3)))
    x = soft(stab * 0.8, 1.4) * env(int(SR * dur), 0.005, 0.35, 0.25, 0.5)
    return norm(x, 0.85)


def sfx_save():
    a = sine(1046.5, 0.18) * env(int(SR * 0.18), 0.005, 0.1, 0.2, 0.08)
    b = sine(1568.0, 0.3) * env(int(SR * 0.3), 0.005, 0.15, 0.2, 0.15)
    return norm(np.concatenate([a, b]) * 0.7, 0.5)


def sfx_transition():
    dur = 1.1
    x = lowpass_sweep(noise(dur), 300, 4500) * env(int(SR * dur), 0.35, 0.25, 0.6, 0.45)
    x += sine(sweep(60, 120, dur), dur) * env(int(SR * dur), 0.3, 0.3, 0.4, 0.4) * 0.3
    return norm(x, 0.7)


def sfx_dialogue_open():
    dur = 0.35
    x = (sine(440, dur) + 0.6 * sine(660, dur)) * env(int(SR * dur), 0.03, 0.15, 0.2, 0.15)
    return norm(x, 0.35)


def sfx_enemy_alert():
    a = sine(660, 0.12) * env(int(SR * 0.12), 0.003, 0.06, 0.2, 0.05)
    b = sine(990, 0.18) * env(int(SR * 0.18), 0.003, 0.08, 0.2, 0.09)
    return norm(soft(np.concatenate([a, b]) * 1.3, 1.6), 0.6)


def sfx_enemy_windup():
    dur = 0.42
    x = sine(sweep(180, 520, dur, 1.6), dur) * env(int(SR * dur), 0.02, 0.1, 0.8, 0.08)
    return norm(soft(x * 1.5, 2.0) * 0.8, 0.65)


def sfx_footstep():
    x = lowpass(noise(0.09), 700) * env(int(SR * 0.09), 0.001, 0.04, 0.0, 0.05)
    return norm(x, 0.35)


# ---------------------------------------------------------------- ambient loops (8 s)
def amb_hall():
    """Hall: deep floor hum + air bed + light-shaft shimmer + distant metal groans."""
    dur = 8.0
    n = int(SR * dur)
    hum = (sine(55, dur) + 0.4 * sine(110, dur) + 0.15 * sine(165, dur)) * (0.8 + 0.2 * sine(0.23, dur))
    air = lowpass(noise(dur), 900) * (0.5 + 0.5 * (0.5 + 0.5 * sine(0.11, dur)))
    shaft = bandpass(noise(dur), 2600, 7000) * (0.5 + 0.5 * sine(0.07, dur)) * 0.06   # light-shaft hiss
    flicker = highpass(noise(dur), 3500) * (rng.uniform(0, 1, n) > 0.996) * 0.35
    groans = np.zeros(n)
    for pos, f in ((1.9, 92.0), (5.2, 104.6)):
        i = int(pos * SR)
        seg = soft(sine(sweep(f, f * 0.92, 1.4, 0.7), 1.4) * 1.6, 1.8) * env(int(SR * 1.4), 0.5, 0.4, 0.5, 0.4)
        groans[i:i + len(seg)] += seg[:max(0, n - i)] * 0.16
    x = hum * 0.5 + air * 0.35 + flicker + shaft + groans
    return loopable(norm(x, 0.45))


def amb_dusk_wind():
    """Dusk wind: two gust layers crossfading + high leaf rustle + a low moan under."""
    dur = 8.0
    n = int(SR * dur)
    gust_a = lowpass(noise(dur), 700) * (0.4 + 0.6 * (0.5 + 0.5 * sine(0.09, dur)))
    gust_b = lowpass(noise(dur), 1100) * (0.3 + 0.7 * (0.5 + 0.5 * sine(0.147, dur, 0.8)))
    leaves = bandpass(noise(dur), 3000, 8000) * (0.15 + 0.85 * (0.5 + 0.5 * sine(0.19, dur, 1.7))) * 0.12
    moan = sine(sweep(72, 64, dur, 0.9), dur) * (0.3 + 0.2 * sine(0.05, dur)) * 0.18
    x = gust_a * 0.8 + gust_b * 0.6 + leaves + moan
    return loopable(norm(x, 0.4))


def amb_water():
    """Shrine water: laps + gurgling stream band + scattered drips with echo tails."""
    dur = 8.0
    n = int(SR * dur)
    lap = lowpass(noise(dur), 1200) * (0.3 + 0.7 * (0.5 + 0.5 * sine(0.35, dur)))
    gurgle = bandpass(noise(dur), 350, 900) * (0.4 + 0.6 * (0.5 + 0.5 * sine(0.6, dur, 2.2))) * 0.35
    bubbles = highpass(noise(dur), 2200) * (rng.uniform(0, 1, n) > 0.993) * 0.5
    drip = np.zeros(n)
    for pos in (0.8, 1.3, 2.6, 3.9, 4.4, 5.7, 6.1, 7.3):
        f = rng.uniform(1400, 2100)
        seg = sine(sweep(f, f * 0.5, 0.09), 0.09) * env(int(SR * 0.09), 0.001, 0.05, 0.0, 0.04)
        seg = delay_tail(seg, 0.12, 0.35, 0.3)
        i = int(pos * SR)
        drip[i:i + len(seg)] += seg[:max(0, n - i)] * 0.3
    x = lap * 0.5 + gurgle + lowpass(bubbles, 3000) + drip
    return loopable(norm(x, 0.42))


def amb_hollow():
    """Hollow places: beating tritone drone + breath + deep sub sweeps + cavern pips."""
    dur = 8.0
    n = int(SR * dur)
    d = sum(sine(f, dur) for f in (55, 56.7, 82.4, 84.9, 110.0)) / 5
    breath = lowpass(noise(dur), 500) * (0.5 + 0.5 * sine(0.14, dur))
    subs = np.zeros(n)
    for pos, f0, f1 in ((1.6, 40, 48), (4.8, 44, 38)):
        i = int(pos * SR)
        seg = sine(sweep(f0, f1, 2.2, 0.8), 2.2) * env(int(SR * 2.2), 0.8, 0.5, 0.6, 0.7)
        subs[i:i + len(seg)] += seg[:max(0, n - i)] * 0.22
    pips = np.zeros(n)
    for pos, f in ((2.4, 1568), (5.9, 1244.5)):
        i = int(pos * SR)
        seg = (sine(f, 0.5) + 0.4 * sine(f * 1.005, 0.5)) * env(int(SR * 0.5), 0.02, 0.3, 0.2, 0.35)
        seg = delay_tail(seg, 0.16, 0.42, 0.35)
        pips[i:i + len(seg)] += seg[:max(0, n - i)] * 0.1
    x = soft(d * 1.6, 1.5) * 0.6 + breath * 0.25 + subs + pips
    return loopable(norm(x, 0.45))


# ---------------------------------------------------------------- music beds (placeholders, 8 s)
def pad(chords, dur_per, detune=0.4):
    out = []
    for freqs in chords:
        seg = np.zeros(int(SR * dur_per))
        for f in freqs:
            seg += sine(f, dur_per) + 0.5 * sine(f * (1 + detune / 1000), dur_per) + 0.25 * sine(f * 2, dur_per)
        seg *= env(len(seg), 0.4, 0.5, 0.75, 0.6)
        out.append(seg)
    return np.concatenate(out)


A3, C4, D4, E4, F4, G4, A4, B4, C5, E5 = 220.0, 261.63, 293.66, 329.63, 349.23, 392.0, 440.0, 493.88, 523.25, 659.25


def mus_calm():
    """Calm: warm pad + soft pentatonic arpeggio with echo + gentle bass movement."""
    dur = 8.0
    n = int(SR * dur)
    x = pad([(A3, C4, E4), (F4 / 2, A3, C4), (C4, E4, G4), (G4 / 2, B4 / 2, D4)], 2.0)
    arp_notes = [A4, C5, E5, A4, C5, E5, G4, C5]     # one per bar-second, soft plucks
    arp = np.zeros(n)
    for k, f in enumerate(arp_notes):
        i = int((k + 0.5) * SR)
        seg = (sine(f, 0.5) + 0.3 * sine(f * 2, 0.5)) * env(int(SR * 0.5), 0.01, 0.25, 0.1, 0.3)
        seg = delay_tail(seg, 0.28, 0.3, 0.25)
        arp[i:i + len(seg)] += seg[:max(0, n - i)] * 0.22
    bass = np.zeros(n)
    for k, f in enumerate((A3 / 2, F4 / 4, C4 / 2, G4 / 4)):
        i = int(k * 2 * SR)
        seg = sine(f, 1.9) * env(int(SR * 1.9), 0.3, 0.8, 0.7, 0.5)
        bass[i:i + len(seg)] += seg[:max(0, n - i)] * 0.3
    x = lowpass(x, 1800) + arp + bass
    return loopable(norm(x, 0.4), 0.8)


def mus_tension():
    """Tension: pulse + beating drone + high clock tick + swell + low booms on turns."""
    dur = 8.0
    n = int(SR * dur)
    x = np.zeros(n)
    beat = 0.5
    for k in range(int(dur / beat)):
        f = 55.0 if k % 4 != 3 else 58.27
        seg = soft(sine(f, beat) * 1.8, 2.0) * env(int(SR * beat), 0.005, 0.2, 0.3, 0.2)
        i = int(k * beat * SR); x[i:i + len(seg)] += seg
    drone = (sine(110, dur) + sine(113.0, dur)) * 0.15 * (0.6 + 0.4 * sine(0.25, dur))
    tick = highpass(noise(dur), 5000) * 0.05 * (np.sin(2 * np.pi * 2 * t(dur)) > 0.94)   # clock grain
    swell = bandpass(noise(dur), 200, 900) * (0.5 + 0.5 * sine(0.125 * np.pi, dur)) * 0.2
    booms = np.zeros(n)
    for pos in (1.95, 5.95):
        i = int(pos * SR)
        seg = soft(sine(sweep(80, 40, 0.7, 0.8), 0.7) * 1.8, 2.0) * env(int(SR * 0.7), 0.02, 0.3, 0.3, 0.3)
        booms[i:i + len(seg)] += seg[:max(0, n - i)] * 0.3
    x = x * 0.6 + drone + tick + swell + booms
    return loopable(norm(x, 0.45), 0.4)


def mus_combat():
    """Combat: driving pattern + kick/hat/snare + moving bass + end-of-loop tom fill."""
    dur = 8.0
    n = int(SR * dur)
    x = np.zeros(n)
    step = 0.25
    pattern = [A3 / 2, A3 / 2, C4 / 2, A3 / 2, E4 / 2, A3 / 2, G4 / 2, F4 / 2]
    bass_pat = [A3 / 2, A3 / 2, A3 / 2, C4 / 2, E4 / 2, E4 / 2, G4 / 2, F4 / 2]
    for k in range(int(dur / step)):
        i = int(k * step * SR)
        f = pattern[k % 8]
        seg = soft(sine(f, step) * 2.2, 2.4) * env(int(SR * step), 0.003, 0.08, 0.35, 0.08)
        x[i:i + len(seg)] += seg
        if k % 2 == 0:
            kick = sine(sweep(120, 45, 0.15), 0.15) * env(int(SR * 0.15), 0.001, 0.08, 0.0, 0.06)
            x[i:i + len(kick)] += kick * 0.8
        if k % 8 == 4:  # backbeat snare
            sn = bandpass(noise(0.12), 900, 3200) * env(int(SR * 0.12), 0.001, 0.05, 0.05, 0.05)
            x[i:i + len(sn)] += sn * 0.45
        b = soft(sine(bass_pat[k % 8] / 2, step) * 1.6, 1.8) * env(int(SR * step), 0.01, 0.1, 0.4, 0.1)
        x[i:i + len(b)] += b * 0.35
    fill = np.zeros(n)
    for k in range(4):  # last beat: rising toms
        i = int((7.0 + k * 0.25) * SR)
        tom = sine(sweep(160 + 40 * k, 90 + 20 * k, 0.2, 0.8), 0.2) * env(int(SR * 0.2), 0.002, 0.1, 0.1, 0.08)
        fill[i:i + len(tom)] += tom * 0.5
    hat = highpass(noise(dur), 6000) * 0.12 * (np.sin(2 * np.pi * 4 * t(dur)) > 0.92)
    x = x * 0.55 + hat + fill
    return loopable(norm(x, 0.5), 0.3)


# ---------------------------------------------------------------- meta + registry
META = """fileFormatVersion: 2
guid: {g}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {load}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: {q}
    conversionMode: 0
    preloadAudioData: {preload}
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 1
  preloadAudioData: {preload}
  loadInBackground: {bg}
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

# ---------------------------------------------------------------- recorded sources (release pass P4)
# Production SFX come from CC0 recordings staged in reference/audio_source (Kenney.nl Impact Sounds /
# RPG Audio / Interface Sounds - see SOURCES.json + LICENSE.txt there). They are decoded with ffmpeg
# to the project's 22.05 kHz mono format, trimmed of leading silence, peak-normalised and edge-faded
# so the runtime mix (GameAudio) is unchanged. Where a recording is missing (or ffmpeg is absent)
# the procedural recipe below is used instead and the clip is reported as a PLACEHOLDER.
SRC = os.path.join(ROOT, "reference", "audio_source")
SOURCES = json.load(open(os.path.join(SRC, "SOURCES.json"))) if os.path.exists(os.path.join(SRC, "SOURCES.json")) else {}
PLACEHOLDERS = []


ALLOW_FALLBACK = os.environ.get("CROSSROADS_AUDIO_ALLOW_FALLBACK") == "1"


def decode_ogg(path):
    """OGG -> float64 mono at SR. Prefers ffmpeg; falls back to soundfile if installed."""
    import subprocess, shutil
    if shutil.which("ffmpeg"):
        raw = subprocess.run(["ffmpeg", "-v", "quiet", "-i", path, "-f", "s16le", "-ac", "1", "-ar", str(SR), "-"],
                             capture_output=True, check=True).stdout
        return np.frombuffer(raw, np.int16).astype(np.float64) / 32768.0
    try:
        import soundfile  # optional dependency
        x, sr = soundfile.read(path, dtype="float64", always_2d=True)
        x = x.mean(axis=1)
        if sr != SR:
            idx = np.arange(0, len(x), sr / SR)
            x = np.interp(idx, np.arange(len(x)), x)
        return x
    except ImportError:
        raise OSError("no OGG decoder: install ffmpeg (apt-get install ffmpeg) or python soundfile")


def recorded(key, fallback, peak=None):
    """Returns a recipe: the decoded recording. A missing source/decoder is an ERROR for a release
    build; set CROSSROADS_AUDIO_ALLOW_FALLBACK=1 to accept the procedural fallback (reported)."""
    def recipe():
        ogg = os.path.join(SRC, key + ".ogg")
        problem = None
        if key not in SOURCES or not os.path.exists(ogg):
            problem = "no recording staged for %s (reference/audio_source/%s.ogg)" % (key, key)
        else:
            try:
                x = decode_ogg(ogg)
                # trim leading/trailing silence (-48 dB), keep a 4 ms head
                nz = np.where(np.abs(x) > 0.004)[0]
                if len(nz):
                    x = x[max(0, nz[0] - int(SR * 0.004)):min(len(x), nz[-1] + int(SR * 0.05))]
                return fade_edges(norm(x, peak or SOURCES[key].get("peak", 0.85)), 4)
            except Exception as ex:  # decoder missing / corrupt file
                problem = "%s: %s" % (key, ex)
        if not ALLOW_FALLBACK:
            raise SystemExit("[AUDIO] " + problem + " - refusing to ship a synthesized stand-in "
                             "(export CROSSROADS_AUDIO_ALLOW_FALLBACK=1 to override for local iteration)")
        PLACEHOLDERS.append(key)
        return fallback()
    recipe.__name__ = "recorded_" + key
    return recipe


def sfx_objective():        # procedural fallback: two-note rising chime
    a = sine(660, 0.18) * env(int(SR * 0.18), 0.003, 0.08, 0.3, 0.08)
    b = sine(990, 0.30) * env(int(SR * 0.30), 0.003, 0.12, 0.3, 0.14)
    return norm(np.concatenate([a, b]) * 0.8)


def sfx_ability_unlock():   # procedural fallback: low bell + shimmer
    bell = sine(220, 1.1) * env(int(SR * 1.1), 0.002, 0.5, 0.2, 0.5) + 0.4 * sine(440.5, 1.1) * env(int(SR * 1.1), 0.002, 0.3, 0.1, 0.4)
    return norm(bell * 0.8)


CLIPS = [
    # (key, folder, recipe, loadType 0 DecompressOnLoad / 1 CompressedInMemory / 2 Streaming, quality, preload, loadInBackground)
    ("sfx_attack_swing", "SFX", recorded("sfx_attack_swing", sfx_attack_swing), 0, 0.6, 1, 0),
    ("sfx_attack_hit", "SFX", recorded("sfx_attack_hit", sfx_attack_hit), 0, 0.6, 1, 0),
    ("sfx_dodge", "SFX", recorded("sfx_dodge", sfx_dodge), 0, 0.6, 1, 0),
    ("sfx_player_hurt", "SFX", recorded("sfx_player_hurt", sfx_player_hurt), 0, 0.6, 1, 0),
    ("sfx_enemy_defeat", "SFX", recorded("sfx_enemy_defeat", sfx_enemy_defeat), 0, 0.6, 1, 0),
    # ability palette: no suitable licensed recordings exist for the Fracture lines - designed
    # synth layers stay (they are the remaining marked audio placeholders, see FINAL_RELEASE_REPORT)
    ("sfx_ability_ember", "SFX", sfx_ability_ember, 0, 0.7, 1, 0),
    ("sfx_ability_tide", "SFX", sfx_ability_tide, 0, 0.7, 1, 0),
    ("sfx_ability_stone", "SFX", sfx_ability_stone, 0, 0.7, 1, 0),
    ("sfx_ability_hollow", "SFX", sfx_ability_hollow, 0, 0.7, 1, 0),
    ("sfx_ui_tap", "SFX", recorded("sfx_ui_tap", sfx_ui_tap), 0, 0.5, 1, 0),
    ("sfx_ui_confirm", "SFX", recorded("sfx_ui_confirm", sfx_ui_confirm), 0, 0.5, 1, 0),
    ("sfx_decision_lock", "SFX", recorded("sfx_decision_lock", sfx_decision_lock), 0, 0.7, 1, 0),
    ("sfx_save", "SFX", recorded("sfx_save", sfx_save), 0, 0.5, 1, 0),
    ("sfx_transition", "SFX", recorded("sfx_transition", sfx_transition), 0, 0.6, 1, 0),
    ("sfx_dialogue_open", "SFX", recorded("sfx_dialogue_open", sfx_dialogue_open), 0, 0.5, 1, 0),
    ("sfx_enemy_alert", "SFX", recorded("sfx_enemy_alert", sfx_enemy_alert), 0, 0.6, 1, 0),
    ("sfx_enemy_windup", "SFX", recorded("sfx_enemy_windup", sfx_enemy_windup), 0, 0.6, 1, 0),
    ("sfx_footstep", "SFX", recorded("sfx_footstep", sfx_footstep), 0, 0.5, 1, 0),
    ("amb_hall", "Ambient", amb_hall, 1, 0.5, 0, 1),
    ("amb_dusk_wind", "Ambient", amb_dusk_wind, 1, 0.5, 0, 1),
    ("amb_water", "Ambient", amb_water, 1, 0.5, 0, 1),
    ("amb_hollow", "Ambient", amb_hollow, 1, 0.5, 0, 1),
    ("mus_calm", "Music", mus_calm, 2, 0.6, 0, 1),
    ("mus_tension", "Music", mus_tension, 2, 0.6, 0, 1),
    ("mus_combat", "Music", mus_combat, 2, 0.6, 0, 1),
    # release pass additions (guids continue the 0x300 family: index 25, 26)
    ("sfx_objective", "SFX", recorded("sfx_objective", sfx_objective), 0, 0.5, 1, 0),
    ("sfx_ability_unlock", "SFX", recorded("sfx_ability_unlock", sfx_ability_unlock), 0, 0.6, 1, 0),
]
SYNTH_BEDS = ["sfx_ability_ember", "sfx_ability_tide", "sfx_ability_stone", "sfx_ability_hollow",
              "amb_hall", "amb_dusk_wind", "amb_water", "amb_hollow", "mus_calm", "mus_tension", "mus_combat"]


def main():
    reg = json.load(open(REG_PATH))
    total = 0
    for i, (key, folder, recipe, load, q, preload, bg) in enumerate(CLIPS):
        guid = g32(0x300 + i)
        if key + ".wav" in reg and reg[key + ".wav"] != guid:
            raise SystemExit("GUID conflict for %s" % key)
        reg[key + ".wav"] = guid
        path = os.path.join(AUD, folder, key + ".wav")
        global rng
        rng = np.random.default_rng(20260905 + i)   # per-clip seed: byte-identical output in any run order
        data = fade_edges(recipe()) if folder == "SFX" else recipe()
        write_wav(path, data)
        open(path + ".meta", "w").write(META.format(g=guid, load=load, q=q, preload=preload, bg=bg))
        total += os.path.getsize(path)
        print("audio + %-22s %5.2fs  %6.1f KB" % (key, len(data) / SR, os.path.getsize(path) / 1024))
    # folder metas (deterministic)
    import hashlib
    for d in ["Assets/_Project/Audio/Ambient"]:
        meta = os.path.join(ROOT, d + ".meta")
        if not os.path.exists(meta):
            open(meta, "w").write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % hashlib.md5(("folder:" + d).encode()).hexdigest())
    json.dump(reg, open(REG_PATH, "w"), indent=1)
    recorded_n = len([1 for k, _f, r, *_ in CLIPS if r.__name__.startswith("recorded_") and k not in PLACEHOLDERS])
    status = {"recorded": recorded_n, "procedural_placeholders": sorted(set(SYNTH_BEDS) | set(PLACEHOLDERS)),
              "fallbacks_used": PLACEHOLDERS}
    json.dump(status, open(os.path.join(SRC, "AUDIO_STATUS.json"), "w"), indent=1)
    print("[AUDIO] %d clips, %.1f MB total; %d recorded (CC0), %d procedural placeholders%s" % (
        len(CLIPS), total / 1e6, recorded_n, len(status["procedural_placeholders"]),
        (" (FALLBACK for %s)" % PLACEHOLDERS) if PLACEHOLDERS else ""))


if __name__ == "__main__":
    main()
