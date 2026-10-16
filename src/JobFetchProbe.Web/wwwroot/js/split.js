// Two small helpers for the jobs page. Without this script the page still works; only dragging stops.
//  1. Clicking anywhere on a table row opens that job (the title is the real link).
//  2. The divider between the table and the details pane can be dragged; the width is remembered in this browser.
//     Double-click resets it; with the keyboard, focus it and use the arrow keys.
(() => {
    document.querySelectorAll("tr[data-href]").forEach(row => {
        row.addEventListener("click", e => {
            if (!e.target.closest("a")) {
                location.href = row.dataset.href;
            }
        });
    });

    document.querySelector("tr.selected")?.scrollIntoView({ block: "nearest" });

    const workspace = document.querySelector(".workspace");
    const splitter = document.querySelector(".splitter");
    if (!workspace || !splitter) {
        return;
    }

    const key = "jobfetchprobe.paneWidth";
    const minPane = 280;
    const minList = 480;

    const list = workspace.querySelector(".list");
    const current = () => document.querySelector(".detail").getBoundingClientRect().width;

    // The table and the pane share the space; the table keeps at least minList pixels.
    const apply = width => {
        const max = list.getBoundingClientRect().width + current() - minList;
        const clamped = Math.round(Math.max(minPane, Math.min(max, width)));
        workspace.style.setProperty("--pane", clamped + "px");
        return clamped;
    };
    const save = width => { try { localStorage.setItem(key, String(width)); } catch { /* storage unavailable */ } };
    try {
        const stored = Number(localStorage.getItem(key));
        if (stored > 0) {
            apply(stored);
        }
    } catch { /* storage unavailable */ }

    splitter.addEventListener("pointerdown", down => {
        down.preventDefault();
        splitter.setPointerCapture(down.pointerId);
        workspace.classList.add("dragging");
        let width = current();

        const move = e => { width = apply(workspace.getBoundingClientRect().right - e.clientX); };
        const up = e => {
            splitter.releasePointerCapture(e.pointerId);
            splitter.removeEventListener("pointermove", move);
            splitter.removeEventListener("pointerup", up);
            workspace.classList.remove("dragging");
            save(width);
        };
        splitter.addEventListener("pointermove", move);
        splitter.addEventListener("pointerup", up);
    });

    splitter.addEventListener("dblclick", () => {
        workspace.style.removeProperty("--pane");
        try { localStorage.removeItem(key); } catch { /* storage unavailable */ }
    });

    splitter.addEventListener("keydown", e => {
        const step = e.shiftKey ? 80 : 24;
        if (e.key === "ArrowLeft") {
            save(apply(current() + step));
            e.preventDefault();
        } else if (e.key === "ArrowRight") {
            save(apply(current() - step));
            e.preventDefault();
        }
    });
})();
