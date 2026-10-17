// Small DOM helpers called from Blazor components.
window.genesisChess = {
    select: (element) => {
        element?.focus();
        element?.select();
    },
};
