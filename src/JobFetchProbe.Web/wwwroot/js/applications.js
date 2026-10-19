// The Applications page. Without this script the page still works; only the keys need it.
//  ↓ / ↑ open the next / previous application (not while typing in the note).
//  Ctrl+Enter (or ⌘+Enter) in the note adds it; an empty note is not sent.
(() => {
    const pane = document.querySelector(".detail");
    const noteForm = document.querySelector("[data-note-form]");
    const text = noteForm?.querySelector("textarea");

    noteForm?.addEventListener("submit", e => {
        if (text.value.trim() === "") {
            e.preventDefault();
            text.focus();
        }
    });
    text?.addEventListener("keydown", e => {
        if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            noteForm.requestSubmit();
        }
    });

    document.addEventListener("keydown", e => {
        if (!pane || e.ctrlKey || e.metaKey || e.altKey || e.target.closest("input, textarea, select")) {
            return;
        }
        if (e.key === "ArrowDown" && pane.dataset.next) {
            e.preventDefault();
            location.href = pane.dataset.next;
        } else if (e.key === "ArrowUp" && pane.dataset.previous) {
            e.preventDefault();
            location.href = pane.dataset.previous;
        }
    });
})();
