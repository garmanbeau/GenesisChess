// Small DOM helpers called from Blazor components.
window.genesisChess = {
    select: (element) => {
        element?.focus();
        element?.select();
    },

    // Low-time warning tone, generated with the Web Audio API (no audio files or external requests).
    sound: (() => {
        const storageKey = "genesisChess.sound";
        let context = null;

        const getContext = () => {
            const Ctor = window.AudioContext || window.webkitAudioContext;
            if (!Ctor) {
                return null;
            }
            context ??= new Ctor();
            return context;
        };

        // Browsers only let audio start after a user gesture, so unlock the context on the
        // first click or key press (players always click before their clock runs low).
        const unlock = () => {
            const ctx = getContext();
            if (ctx?.state === "suspended") {
                ctx.resume().catch(() => { });
            }
        };
        window.addEventListener("pointerdown", unlock, { capture: true, passive: true });
        window.addEventListener("keydown", unlock, { capture: true, passive: true });

        const beep = (ctx, start, frequency, duration) => {
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "sine";
            osc.frequency.value = frequency;
            gain.gain.setValueAtTime(0.0001, start);
            gain.gain.exponentialRampToValueAtTime(0.25, start + 0.01);
            gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
            osc.connect(gain).connect(ctx.destination);
            osc.start(start);
            osc.stop(start + duration + 0.02);
        };

        return {
            isEnabled: () => {
                try {
                    return localStorage.getItem(storageKey) !== "off";
                } catch {
                    return true;
                }
            },

            setEnabled: (enabled) => {
                try {
                    localStorage.setItem(storageKey, enabled ? "on" : "off");
                } catch {
                    // Storage blocked (private mode etc.): the toggle still works for this page.
                }
                if (enabled) {
                    unlock();
                }
            },

            // Two short rising beeps.
            lowTime: async () => {
                const ctx = getContext();
                if (!ctx) {
                    return;
                }
                if (ctx.state === "suspended") {
                    try {
                        await ctx.resume();
                    } catch {
                        return;
                    }
                }
                const now = ctx.currentTime;
                beep(ctx, now, 880, 0.12);
                beep(ctx, now + 0.18, 1175, 0.16);
            },
        };
    })(),
};
