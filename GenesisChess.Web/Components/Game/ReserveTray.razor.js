// Drag a reserve piece onto the board to place it. The drag mechanics are shared with board moves.
import { trackPieceDrag } from "./Board.razor.js";

export function attach(root, dotNet) {
    let drag = null;

    const onDown = (e) => {
        if (e.button !== 0 || !e.isPrimary || drag || !root.classList.contains("interactive")) return;
        const slot = e.target.closest(".slot");
        const glyph = slot?.querySelector(".piece");
        if (!glyph || slot.disabled) return;

        const type = slot.dataset.type;
        drag = trackPieceDrag(e, {
            glyph,
            onStart: () => dotNet.invokeMethodAsync("DragStart", type).catch(() => { }),
            onDrop: (sq) => dotNet.invokeMethodAsync("Drop", type, sq.file, sq.rank).catch(() => { }),
            onEnd: () => { drag = null; },
        });
    };

    root.addEventListener("pointerdown", onDown);

    return {
        dispose() {
            drag?.cancel();
            root.removeEventListener("pointerdown", onDown);
        },
    };
}
