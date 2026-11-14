// Right-click annotation input for the board.
// The whole drag (hit-testing and the in-progress arrow) stays in the browser, so a drag
// costs one call to .NET on release instead of one per mousemove.

const SVG_NS = "http://www.w3.org/2000/svg";

/**
 * Modifier keys pick the colour: plain = green, Ctrl/Cmd = red, Alt = blue, Ctrl/Cmd+Alt = yellow.
 * Shift is deliberately unused: Firefox always opens its native context menu on Shift+right-click.
 */
function colorFor(e) {
    const ctrl = e.ctrlKey || e.metaKey;
    if (ctrl && e.altKey) return "yellow";
    if (ctrl) return "red";
    if (e.altKey) return "blue";
    return "green";
}

/** Converts a pointer position to { file, rank } (0-7), or null when outside the board. */
function hitTest(grid, flipped, clientX, clientY) {
    const r = grid.getBoundingClientRect();
    const col = Math.floor(((clientX - r.left) / r.width) * 8);
    const row = Math.floor(((clientY - r.top) / r.height) * 8);
    if (col < 0 || col > 7 || row < 0 || row > 7) return null;
    return flipped ? { file: 7 - col, rank: row } : { file: col, rank: 7 - row };
}

/** Arrowhead length (3 x the 0.16 stroke width, see the marker in Board.razor) plus a small gap. */
const ARROW_PULL_BACK = 0.56;

/**
 * Polyline points for an arrow, used for both the drag preview and the arrow Board.razor keeps.
 * Knight jumps bend (long leg first); the last leg is pulled back so the head's tip lands near the centre.
 */
function arrowPoints(a, b) {
    const dx = Math.abs(b.x - a.x);
    const dy = Math.abs(b.y - a.y);
    const points = [a];
    if ((dx === 1 && dy === 2) || (dx === 2 && dy === 1)) {
        points.push(dx > dy ? { x: b.x, y: a.y } : { x: a.x, y: b.y });
    }

    const s = points[points.length - 1];
    const len = Math.hypot(b.x - s.x, b.y - s.y);
    const t = (len - ARROW_PULL_BACK) / len;
    points.push({ x: s.x + (b.x - s.x) * t, y: s.y + (b.y - s.y) * t });
    return points.map((p) => `${p.x.toFixed(3)},${p.y.toFixed(3)}`).join(" ");
}

export function attach(root, dotNet) {
    const grid = root.querySelector(".board");
    const draft = root.querySelector("svg.annotation-draft");
    let drag = null;

    const flipped = () => grid.classList.contains("flipped");

    // The only board-to-overlay mapping: Board.razor renders whatever coordinates this produces.
    // 8x8 user space, square centres at (col + 0.5, row + 0.5).
    const centre = (sq) => {
        const col = flipped() ? 7 - sq.file : sq.file;
        const row = flipped() ? sq.rank : 7 - sq.rank;
        return { x: col + 0.5, y: row + 0.5 };
    };

    const clearDraft = () => draft.replaceChildren();

    const drawDraft = (from, to, color) => {
        clearDraft();
        if (to.file === from.file && to.rank === from.rank) {
            const c = centre(from);
            const circle = document.createElementNS(SVG_NS, "circle");
            circle.setAttribute("cx", c.x);
            circle.setAttribute("cy", c.y);
            circle.setAttribute("r", 0.46);
            circle.setAttribute("class", `ann-circle ann-${color}`);
            draft.appendChild(circle);
            return;
        }

        const line = document.createElementNS(SVG_NS, "polyline");
        line.setAttribute("points", arrowPoints(centre(from), centre(to)));
        line.setAttribute("class", `ann-arrow ann-${color}`);
        line.setAttribute("marker-end", `url(#gc-ah-${color})`);
        draft.appendChild(line);
    };

    const cancel = () => {
        drag = null;
        clearDraft();
        window.removeEventListener("mousemove", onMove);
        window.removeEventListener("mouseup", onUp);
        window.removeEventListener("blur", cancel);
    };

    const onMove = (e) => {
        if (!drag) return;
        // The right button was released somewhere we never heard about (outside the window, devtools...).
        if ((e.buttons & 2) === 0) {
            cancel();
            return;
        }

        const sq = hitTest(grid, flipped(), e.clientX, e.clientY);
        if (sq) drawDraft(drag.from, sq, drag.color);
    };

    const onUp = (e) => {
        if (!drag || e.button !== 2) return;
        const { from, color, ply } = drag;
        const to = hitTest(grid, flipped(), e.clientX, e.clientY);
        cancel();
        if (to) {
            const same = to.file === from.file && to.rank === from.rank;
            const c = centre(from);
            const points = same ? `${c.x.toFixed(3)},${c.y.toFixed(3)}` : arrowPoints(c, centre(to));
            // Fails only if the circuit is reconnecting or the board is gone; the annotation is just dropped.
            dotNet.invokeMethodAsync("Annotate", from.file, from.rank, to.file, to.rank, color, points, ply).catch(() => { });
        }
    };

    const onDown = (e) => {
        if (e.button !== 2) {
            // Any other button ends an in-progress right-drag without drawing anything.
            if (drag) cancel();
            return;
        }

        const sq = hitTest(grid, flipped(), e.clientX, e.clientY);
        if (!sq) return;
        e.preventDefault();
        // The position the drag is drawn on; .NET ignores the result if a move has landed since.
        drag = { from: sq, color: colorFor(e), ply: Number(root.dataset.ply) };
        drawDraft(sq, sq, drag.color);
        window.addEventListener("mousemove", onMove);
        window.addEventListener("mouseup", onUp);
        window.addEventListener("blur", cancel);
    };

    // The context menu itself is suppressed by @oncontextmenu:preventDefault in Board.razor.
    root.addEventListener("mousedown", onDown);

    return {
        dispose() {
            cancel();
            root.removeEventListener("mousedown", onDown);
        },
    };
}
