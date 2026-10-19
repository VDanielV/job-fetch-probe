// "I applied" on the Overview: the key A presses the button in the details pane (not while typing in a field).
// Without this script the button still works.
(() => {
    const button = document.querySelector("[data-apply-button]");
    if (!button) {
        return;
    }
    document.addEventListener("keydown", e => {
        if (e.ctrlKey || e.metaKey || e.altKey || e.target.closest("input:not([type=checkbox]), textarea, select")) {
            return;
        }
        if (e.key === "a" || e.key === "A") {
            e.preventDefault();
            button.click();
        }
    });
})();
