// The Overview's text filter. Without this script the filter still works (Enter submits the box; the scope links apply).
//  /           puts the cursor in the filter box.
//  N, Shift+N  in the details pane: the next / previous match in the description.
//  "Tick all matches" ticks this page's matches for the archive bar.
(() => {
    const box = document.querySelector("[data-filter-q]");
    const marks = [...document.querySelectorAll(".detail .description mark")];
    let current = -1;

    const show = index => {
        if (marks.length === 0) {
            return;
        }
        marks[current]?.classList.remove("current");
        current = (index + marks.length) % marks.length;
        marks[current].classList.add("current");
        marks[current].scrollIntoView({ block: "center", behavior: "smooth" });
    };

    document.addEventListener("keydown", e => {
        if (e.ctrlKey || e.metaKey || e.altKey || e.target.closest("input:not([type=checkbox]), textarea, select")) {
            return;
        }
        if (e.key === "/" && box) {
            e.preventDefault();
            box.focus();
            box.select();
        } else if ((e.key === "n" || e.key === "N") && marks.length > 0) {
            e.preventDefault();
            show(e.shiftKey ? current - 1 : current + 1);
        }
    });

    // Escape in the box leaves it, so the keys work again.
    box?.addEventListener("keydown", e => {
        if (e.key === "Escape") {
            box.blur();
        }
    });

    const tick = document.querySelector("[data-filter-tick]");
    if (tick) {
        tick.hidden = false;
        tick.addEventListener("click", () => {
            document.querySelectorAll("[data-archive-pick]").forEach(p => {
                if (!p.checked) {
                    p.checked = true;
                    p.dispatchEvent(new Event("change"));
                }
            });
        });
    }
})();
