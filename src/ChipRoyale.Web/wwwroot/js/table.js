// Chip Royale – table effects: parallax tilt, hotkeys, flying chips, confetti/fireworks, synthesized sounds.

let dotnet = null;
let cleanup = [];
let soundOn = true;
let audio = null;

export function init(dotnetRef) {
    dispose();
    dotnet = dotnetRef;
    try { soundOn = localStorage.getItem("cr-sound") !== "off"; } catch { /* storage may be blocked */ }

    const scene = document.getElementById("scene");
    const table = document.getElementById("table3d");
    if (scene && table) {
        let raf = 0;
        const onMove = (e) => {
            const r = scene.getBoundingClientRect();
            const x = (e.clientX - r.left) / r.width - 0.5;
            const y = (e.clientY - r.top) / r.height - 0.5;
            cancelAnimationFrame(raf);
            raf = requestAnimationFrame(() => {
                table.style.setProperty("--ry", `${(x * 5).toFixed(2)}deg`);
                table.style.setProperty("--rx", `${(y * 4).toFixed(2)}deg`);
                const felt = table.querySelector(".felt");
                if (felt) {
                    const fr = felt.getBoundingClientRect();
                    felt.style.setProperty("--mx", `${((e.clientX - fr.left) / fr.width * 100).toFixed(1)}%`);
                    felt.style.setProperty("--my", `${((e.clientY - fr.top) / fr.height * 100).toFixed(1)}%`);
                }
            });
        };
        const onLeave = () => {
            table.style.setProperty("--ry", "0deg");
            table.style.setProperty("--rx", "0deg");
        };
        scene.addEventListener("mousemove", onMove);
        scene.addEventListener("mouseleave", onLeave);
        cleanup.push(() => {
            scene.removeEventListener("mousemove", onMove);
            scene.removeEventListener("mouseleave", onLeave);
        });
    }

    const onKey = (e) => {
        if (e.target instanceof HTMLInputElement && e.target.type !== "range") return;
        if (e.ctrlKey || e.metaKey || e.altKey) return;
        const key = e.key.length === 1 ? e.key.toLowerCase() : e.key;
        if (["f", "c", "r", "a", " ", "ArrowUp", "ArrowDown", "Enter"].includes(key)) {
            if (key === " " || key.startsWith("Arrow")) e.preventDefault();
            dotnet?.invokeMethodAsync("Hotkey", key).catch(() => { });
        }
    };
    document.addEventListener("keydown", onKey);
    cleanup.push(() => document.removeEventListener("keydown", onKey));

    // Audio may only start after a user gesture.
    const unlock = () => { ensureAudio(); };
    document.addEventListener("pointerdown", unlock, { once: true });
    cleanup.push(() => document.removeEventListener("pointerdown", unlock));
}

export function dispose() {
    cleanup.forEach((f) => f());
    cleanup = [];
    dotnet = null;
}

export function getSound() { return soundOn; }

export function setSound(on) {
    soundOn = on;
    try { localStorage.setItem("cr-sound", on ? "on" : "off"); } catch { /* ignore */ }
}

// ------------------------------------------------------------------ flying chips

const CHIP_COLORS = ["#d8352f", "#1f9a5b", "#26262b", "#7b3fc4", "#e8b23a", "#f2efe8"];

export function flyChips(fromId, toId, count = 10) {
    const from = document.getElementById(fromId);
    const to = document.getElementById(toId);
    if (!from || !to) return Promise.resolve();
    const a = from.getBoundingClientRect();
    const b = to.getBoundingClientRect();
    const sx = a.left + a.width / 2, sy = a.top + a.height / 2;
    const tx = b.left + b.width / 2, ty = b.top + b.height / 2;

    const flights = [];
    for (let i = 0; i < count; i++) {
        const chip = document.createElement("div");
        chip.className = "fly-chip";
        chip.style.setProperty("--c", CHIP_COLORS[i % CHIP_COLORS.length]);
        chip.style.left = `${sx}px`;
        chip.style.top = `${sy}px`;
        document.body.appendChild(chip);
        const jx = (Math.random() - 0.5) * 40, jy = (Math.random() - 0.5) * 20;
        const dx = tx - sx + jx, dy = ty - sy + jy;
        const lift = -80 - Math.random() * 60;
        const anim = chip.animate([
            { transform: "translate(0,0) scale(.6) rotateX(60deg)", opacity: 0 },
            { transform: `translate(${dx * 0.5}px, ${dy * 0.5 + lift}px) scale(1.15) rotateX(0deg)`, opacity: 1, offset: 0.5 },
            { transform: `translate(${dx}px, ${dy}px) scale(.9) rotateX(60deg)`, opacity: 1, offset: 0.92 },
            { transform: `translate(${dx}px, ${dy}px) scale(.6)`, opacity: 0 },
        ], { duration: 750 + Math.random() * 200, delay: i * 45, easing: "cubic-bezier(.4,0,.3,1)", fill: "both" });
        flights.push(anim.finished.then(() => chip.remove()).catch(() => chip.remove()));
        if (i % 3 === 0) setTimeout(() => sfx("chip"), i * 45 + 700);
    }
    return Promise.all(flights);
}

// ------------------------------------------------------------------ confetti & fireworks

export function celebrate(level = 1) {
    const canvas = document.createElement("canvas");
    canvas.className = "fx-canvas";
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = innerWidth * dpr;
    canvas.height = innerHeight * dpr;
    document.body.appendChild(canvas);
    const ctx = canvas.getContext("2d");
    ctx.scale(dpr, dpr);

    const colors = ["#f5c451", "#fff3c4", "#e0554b", "#3fbf7f", "#5aa9ff", "#c77dff", "#ffffff"];
    const parts = [];
    const W = innerWidth, H = innerHeight;

    const confetti = (x, y, n, spread, power) => {
        for (let i = 0; i < n; i++) {
            const ang = -Math.PI / 2 + (Math.random() - 0.5) * spread;
            const v = power * (0.5 + Math.random() * 0.7);
            parts.push({
                kind: "confetti", x, y,
                vx: Math.cos(ang) * v, vy: Math.sin(ang) * v,
                w: 6 + Math.random() * 6, h: 3 + Math.random() * 4,
                rot: Math.random() * Math.PI, vr: (Math.random() - 0.5) * 0.3,
                color: colors[(Math.random() * colors.length) | 0],
                life: 1, decay: 0.004 + Math.random() * 0.004,
            });
        }
    };
    const firework = (x, y) => {
        const hue = (Math.random() * 360) | 0;
        const n = 70;
        for (let i = 0; i < n; i++) {
            const ang = (i / n) * Math.PI * 2;
            const v = 3 + Math.random() * 3.5;
            parts.push({
                kind: "spark", x, y, px: x, py: y,
                vx: Math.cos(ang) * v, vy: Math.sin(ang) * v,
                color: `hsl(${hue + Math.random() * 40}, 95%, ${60 + Math.random() * 20}%)`,
                life: 1, decay: 0.012 + Math.random() * 0.01,
            });
        }
        sfx("pop");
    };
    const coins = (n) => {
        for (let i = 0; i < n; i++) {
            parts.push({
                kind: "coin", x: Math.random() * W, y: -20 - Math.random() * H * 0.6,
                vx: (Math.random() - 0.5) * 1.5, vy: 2 + Math.random() * 3,
                r: 8 + Math.random() * 6, spin: Math.random() * Math.PI, vs: 0.1 + Math.random() * 0.15,
                life: 1, decay: 0.003,
            });
        }
    };

    confetti(W * 0.15, H * 0.85, 60 * level, 1.0, 16);
    confetti(W * 0.85, H * 0.85, 60 * level, 1.0, 16);
    if (level >= 2) { confetti(W * 0.5, H * 0.55, 90, Math.PI * 2, 12); coins(30 * (level - 1)); }
    if (level >= 3) {
        [0, 350, 700, 1050, 1400, 1800].forEach((t, i) =>
            setTimeout(() => firework(W * (0.2 + Math.random() * 0.6), H * (0.15 + Math.random() * 0.3)), t + i * 20));
    }

    const start = performance.now();
    const maxTime = level >= 3 ? 5200 : 3800;
    const tick = (now) => {
        ctx.clearRect(0, 0, W, H);
        for (let i = parts.length - 1; i >= 0; i--) {
            const p = parts[i];
            p.life -= p.decay;
            if (p.life <= 0 || p.y > H + 40) { parts.splice(i, 1); continue; }
            ctx.globalAlpha = Math.min(1, p.life * 1.5);
            if (p.kind === "confetti") {
                p.vy += 0.28; p.vx *= 0.985; p.vy *= 0.985;
                p.x += p.vx; p.y += p.vy; p.rot += p.vr;
                ctx.save();
                ctx.translate(p.x, p.y);
                ctx.rotate(p.rot);
                ctx.scale(1, Math.cos(p.rot * 2));
                ctx.fillStyle = p.color;
                ctx.fillRect(-p.w / 2, -p.h / 2, p.w, p.h);
                ctx.restore();
            } else if (p.kind === "spark") {
                p.px = p.x; p.py = p.y;
                p.vy += 0.05; p.vx *= 0.97; p.vy *= 0.97;
                p.x += p.vx; p.y += p.vy;
                ctx.strokeStyle = p.color;
                ctx.lineWidth = 2;
                ctx.beginPath(); ctx.moveTo(p.px, p.py); ctx.lineTo(p.x, p.y); ctx.stroke();
            } else {
                p.vy += 0.06; p.x += p.vx; p.y += p.vy; p.spin += p.vs;
                const sx = Math.abs(Math.cos(p.spin));
                const g = ctx.createRadialGradient(p.x - 2, p.y - 2, 1, p.x, p.y, p.r);
                g.addColorStop(0, "#fff6d0"); g.addColorStop(0.5, "#f5c451"); g.addColorStop(1, "#a8761c");
                ctx.fillStyle = g;
                ctx.beginPath(); ctx.ellipse(p.x, p.y, p.r * Math.max(sx, 0.15), p.r, 0, 0, Math.PI * 2); ctx.fill();
            }
        }
        ctx.globalAlpha = 1;
        if (now - start < maxTime && (parts.length > 0 || now - start < 2000)) requestAnimationFrame(tick);
        else canvas.remove();
    };
    requestAnimationFrame(tick);
}

export function shake() {
    const scene = document.getElementById("scene");
    if (!scene) return;
    scene.classList.remove("shake");
    void scene.offsetWidth;
    scene.classList.add("shake");
}

export function bump(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.remove("bump");
    void el.offsetWidth;
    el.classList.add("bump");
}

// ------------------------------------------------------------------ sounds (synthesized, no assets)

function ensureAudio() {
    if (!audio) {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return null;
        audio = new Ctx();
    }
    if (audio.state === "suspended") audio.resume();
    return audio;
}

function tone(freq, start, dur, type = "sine", gain = 0.15, slideTo = null) {
    const a = audio;
    const o = a.createOscillator();
    const g = a.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, a.currentTime + start);
    if (slideTo) o.frequency.exponentialRampToValueAtTime(slideTo, a.currentTime + start + dur);
    g.gain.setValueAtTime(0.0001, a.currentTime + start);
    g.gain.exponentialRampToValueAtTime(gain, a.currentTime + start + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, a.currentTime + start + dur);
    o.connect(g).connect(a.destination);
    o.start(a.currentTime + start);
    o.stop(a.currentTime + start + dur + 0.05);
}

function noise(start, dur, freq = 3000, gain = 0.2, q = 1) {
    const a = audio;
    const len = Math.max(1, (a.sampleRate * dur) | 0);
    const buf = a.createBuffer(1, len, a.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / len);
    const src = a.createBufferSource();
    src.buffer = buf;
    const f = a.createBiquadFilter();
    f.type = "bandpass";
    f.frequency.value = freq;
    f.Q.value = q;
    const g = a.createGain();
    g.gain.value = gain;
    src.connect(f).connect(g).connect(a.destination);
    src.start(a.currentTime + start);
}

export function sfx(name) {
    if (!soundOn || !audio) return;
    try {
        switch (name) {
            case "chip":
                tone(2400 + Math.random() * 600, 0, 0.05, "triangle", 0.06);
                tone(3100 + Math.random() * 500, 0.035, 0.05, "triangle", 0.045);
                noise(0, 0.03, 5000, 0.08, 2);
                break;
            case "bet":
                for (let i = 0; i < 3; i++) {
                    tone(2300 + Math.random() * 800, i * 0.06, 0.05, "triangle", 0.05);
                    noise(i * 0.06, 0.03, 5200, 0.07, 2);
                }
                break;
            case "card":
                noise(0, 0.09, 2500, 0.18, 0.7);
                break;
            case "check":
                tone(180, 0, 0.07, "sine", 0.25, 90);
                tone(180, 0.11, 0.07, "sine", 0.22, 90);
                break;
            case "fold":
                noise(0, 0.18, 1200, 0.12, 0.5);
                break;
            case "turn":
                tone(880, 0, 0.12, "sine", 0.08);
                tone(1320, 0.08, 0.18, "sine", 0.07);
                break;
            case "allin":
                tone(220, 0, 0.5, "sawtooth", 0.05, 880);
                tone(330, 0.05, 0.45, "square", 0.025, 990);
                break;
            case "win":
                [523, 659, 784, 1047].forEach((f, i) => tone(f, i * 0.09, 0.3, "triangle", 0.12));
                break;
            case "bigwin":
                [523, 659, 784, 1047, 1319, 1568].forEach((f, i) => tone(f, i * 0.08, 0.45, "triangle", 0.12));
                [1047, 1319, 1568].forEach((f) => tone(f, 0.55, 0.9, "sine", 0.07));
                break;
            case "lose":
                [392, 330, 262].forEach((f, i) => tone(f, i * 0.14, 0.3, "sine", 0.1));
                break;
            case "pop":
                noise(0, 0.25, 900, 0.25, 0.4);
                tone(140, 0, 0.2, "sine", 0.2, 50);
                break;
        }
    } catch { /* audio is optional */ }
}

// ------------------------------------------------------------------ tutorial helpers

export function unlockAudio() {
    try { soundOn = localStorage.getItem("cr-sound") !== "off"; } catch { /* ignore */ }
    ensureAudio();
}

export function loadLessons() {
    try { return JSON.parse(localStorage.getItem("cr-lessons") || "[]"); } catch { return []; }
}

export function saveLessons(ids) {
    try { localStorage.setItem("cr-lessons", JSON.stringify(ids)); } catch { /* ignore */ }
}
