// Page-wide keyboard shortcuts for stepping through the move history.
const KEYS = new Set(["ArrowLeft", "ArrowRight", "Home", "End"]);

function isTyping(target) {
    if (!(target instanceof Element)) return false;
    if (target.isContentEditable) return true;
    const tag = target.tagName;
    return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
}

export function attach(dotnet) {
    const onKeyDown = (e) => {
        if (!KEYS.has(e.key) || e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey || e.shiftKey || isTyping(e.target)) {
            return;
        }

        e.preventDefault(); // Home/End and arrows would otherwise scroll the page.
        dotnet.invokeMethodAsync("OnHistoryKey", e.key).catch(() => { /* circuit gone */ });
    };

    document.addEventListener("keydown", onKeyDown);
    return {
        dispose: () => document.removeEventListener("keydown", onKeyDown),
    };
}
