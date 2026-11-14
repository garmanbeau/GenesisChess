// Board input that stays in the browser: right-click annotations and left-button/touch piece dragging.
// Hit-testing and the in-progress arrow or ghost piece live here, so a drag costs a call or two
// to .NET instead of one per pointer move.

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

// ---------------------------------------------------------------------------------------------
// Left-button / touch piece dragging, shared by the board and the reserve tray (ReserveTray.razor.js).
// The ghost, hit-testing and drop-square outline all live here; .NET hears about a drag twice:
// once when it starts (to select the piece and show its legal targets) and once on release.
// .NET validates every drop against the legal moves, so a bad drop can never become a move.
// ---------------------------------------------------------------------------------------------

/** Pixels the pointer must travel before a press becomes a drag; anything less is a click/tap. */
const DRAG_THRESHOLD = { mouse: 4, pen: 4, touch: 8 };

/** The board grid on the page (there is only ever one). */
export function findBoard() {
    return document.querySelector(".board-wrap .board");
}

function boardSquareAt(board, clientX, clientY) {
    return hitTest(board, board.classList.contains("flipped"), clientX, clientY);
}

/** Viewport rectangle of a square on the board. */
function squareRect(board, sq) {
    const r = board.getBoundingClientRect();
    const flipped = board.classList.contains("flipped");
    const col = flipped ? 7 - sq.file : sq.file;
    const row = flipped ? sq.rank : 7 - sq.rank;
    const size = r.width / 8;
    return { left: r.left + col * size, top: r.top + row * size, size };
}

/** Swallows the click the browser fires right after a drag ends, so a drag is never also a click. */
function swallowNextClick() {
    const swallow = (e) => {
        e.stopPropagation();
        e.preventDefault();
    };
    window.addEventListener("click", swallow, { capture: true, once: true });
    setTimeout(() => window.removeEventListener("click", swallow, { capture: true }), 0);
}

/**
 * Follows one press that may turn into a piece drag.
 * - glyph: the piece element being dragged (cloned for the ghost, dimmed in place while dragging).
 * - onStart(): called once the press passes the drag threshold.
 * - onDrop(square): called on release over a board square ({ file, rank }); may return a promise, during which
 *   the ghost waits on the square so the piece doesn't flash back before the move renders. Return null to snap back.
 * - onEnd(): called when tracking stops, however it stops.
 * Returns { cancel() }.
 */
export function trackPieceDrag(down, { glyph, onStart, onDrop, onEnd }) {
    const pointerId = down.pointerId;
    const touch = down.pointerType === "touch";
    const threshold = DRAG_THRESHOLD[down.pointerType] ?? 4;
    let board = null;
    let ghost = null;
    let hover = null;
    let size = 0;
    let finished = false;

    const place = (x, y) => {
        // On touch the piece rides above the finger so it stays visible; the drop square is still under the finger.
        const lift = touch ? size * 0.6 : 0;
        ghost.style.transform = `translate(${x - size / 2}px, ${y - size / 2 - lift}px)${touch ? " scale(1.5)" : ""}`;
    };

    const showHover = (sq) => {
        if (!hover) return;
        if (!sq) {
            hover.style.display = "none";
            return;
        }

        const flipped = board.classList.contains("flipped");
        hover.style.left = `${(flipped ? 7 - sq.file : sq.file) * 12.5}%`;
        hover.style.top = `${(flipped ? sq.rank : 7 - sq.rank) * 12.5}%`;
        hover.style.display = "block";
    };

    const begin = () => {
        board = findBoard();
        if (!board) return false;
        size = board.getBoundingClientRect().width / 8;
        hover = board.parentElement.querySelector(".drag-hover");
        ghost = glyph.cloneNode(true);
        Object.assign(ghost.style, {
            position: "fixed",
            left: "0",
            top: "0",
            width: `${size}px`,
            height: `${size}px`,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: `${size * 0.84}px`,
            lineHeight: "1",
            pointerEvents: "none",
            zIndex: "1000",
            transformOrigin: "50% 50%",
            willChange: "transform",
        });
        document.body.appendChild(ghost);
        glyph.style.opacity = "0.3";
        onStart();
        return true;
    };

    const cleanup = () => {
        ghost?.remove();
        ghost = null;
        showHover(null);
        glyph.style.opacity = "";
    };

    const stopListening = () => {
        if (finished) return;
        finished = true;
        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);
        window.removeEventListener("pointercancel", onInterrupt);
        window.removeEventListener("blur", onInterrupt);
        onEnd?.();
    };

    function cancel() {
        stopListening();
        cleanup();
    }

    // pointercancel (the browser took the gesture over) or blur; other fingers are not ours to end.
    function onInterrupt(e) {
        if (e.pointerId === undefined || e.pointerId === pointerId) cancel();
    }

    function onMove(e) {
        if (e.pointerId !== pointerId) return;
        // The button was released somewhere we never heard about (outside the window, devtools...).
        if (e.pointerType === "mouse" && (e.buttons & 1) === 0) {
            cancel();
            return;
        }

        if (!ghost) {
            if (Math.hypot(e.clientX - down.clientX, e.clientY - down.clientY) < threshold) return;
            if (!begin()) {
                cancel();
                return;
            }
        }

        e.preventDefault();
        place(e.clientX, e.clientY);
        showHover(boardSquareAt(board, e.clientX, e.clientY));
    }

    function onUp(e) {
        if (e.pointerId !== pointerId) return;
        stopListening();
        if (!ghost) return; // Never became a drag: let the click through.

        swallowNextClick();
        showHover(null);
        const sq = boardSquareAt(board, e.clientX, e.clientY);
        const pending = sq ? onDrop(sq) : null;
        if (!pending) {
            cleanup();
            return;
        }

        const rect = squareRect(board, sq);
        ghost.style.transform = `translate(${rect.left}px, ${rect.top}px)`;
        pending.finally(cleanup);
    }

    window.addEventListener("pointermove", onMove, { passive: false });
    window.addEventListener("pointerup", onUp);
    window.addEventListener("pointercancel", onInterrupt);
    window.addEventListener("blur", onInterrupt);
    return { cancel };
}

export function attach(root, dotNet) {
    const grid = root.querySelector(".board");
    const draft = root.querySelector("svg.annotation-draft");
    let drag = null;
    let pieceDrag = null;

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

    // Left-button / touch drag of one of the mover's pieces. Board.razor marks those squares .draggable.
    const onPieceDown = (e) => {
        if (e.button !== 0 || !e.isPrimary || pieceDrag) return;
        const square = e.target.closest(".sq.draggable");
        const glyph = square?.querySelector(".piece");
        const from = glyph && hitTest(grid, flipped(), e.clientX, e.clientY);
        if (!from) return;

        // The position the drag started on; .NET ignores a drop that races a move.
        const ply = Number(root.dataset.ply);
        pieceDrag = trackPieceDrag(e, {
            glyph,
            onStart: () => dotNet.invokeMethodAsync("PieceDragStart", from.file, from.rank, ply).catch(() => { }),
            onDrop: (to) => to.file === from.file && to.rank === from.rank
                ? null
                : dotNet.invokeMethodAsync("PieceDrop", from.file, from.rank, to.file, to.rank, ply).catch(() => { }),
            onEnd: () => { pieceDrag = null; },
        });
    };

    // The context menu itself is suppressed by @oncontextmenu:preventDefault in Board.razor.
    root.addEventListener("mousedown", onDown);
    grid.addEventListener("pointerdown", onPieceDown);

    return {
        dispose() {
            cancel();
            pieceDrag?.cancel();
            root.removeEventListener("mousedown", onDown);
            grid.removeEventListener("pointerdown", onPieceDown);
        },
    };
}
