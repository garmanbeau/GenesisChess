// Small DOM helpers called from Blazor components.
window.genesisChess = {
    select: (element) => {
        element?.focus();
        element?.select();
    },
    /** Scrolls the move list so the move being viewed is visible. */
    revealCurrent: (container) => {
        container?.querySelector(".current")?.scrollIntoView({ block: "nearest" });
    },
};
