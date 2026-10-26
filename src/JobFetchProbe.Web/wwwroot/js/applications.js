// The Applications page. Without this script the page still works; only the keys need it.
//  ↓ / ↑ open the next / previous application (not while typing in the note).
//  Ctrl+Enter (or ⌘+Enter) in the note adds it; an empty note is not sent.
//  / puts the cursor in the search box (not while typing); Esc in the box clears the search; an empty search clears it.
(() => {
    const searchForm = document.querySelector("[data-app-search]");
    const searchBox = searchForm?.querySelector("[data-app-q]");
    const clear = searchForm?.querySelector("[data-app-clear]");

    searchForm?.addEventListener("submit", e => {
        if (searchBox.value.trim() === "" && clear) {
            e.preventDefault();
            location.href = clear.href;
        }
    });
    searchBox?.addEventListener("keydown", e => {
        if (e.key === "Escape") {
            e.preventDefault();
            if (clear) {
                location.href = clear.href;
            } else {
                searchBox.value = "";
                searchBox.blur();
            }
        }
    });
    document.addEventListener("keydown", e => {
        if (e.key === "/" && searchBox && !e.ctrlKey && !e.metaKey && !e.altKey && !e.target.closest("input, textarea, select")) {
            e.preventDefault();
            searchBox.focus();
            searchBox.select();
        }
    });

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
