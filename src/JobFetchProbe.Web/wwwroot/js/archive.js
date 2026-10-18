// Archiving on the Overview. Without this script the page still works: the bar is always shown and the forms post.
//  A. Ticking rows: the bar appears with the count; the header box ticks the whole page; Clear unticks.
//  C. The details pane: keys 1, 2, 3 archive the job with that reason; ↓ / ↑ open the next / previous job.
//  Other needs a note: the note field is focused instead of posting without one.
(() => {
    const form = document.querySelector("[data-archive-form]");
    const bar = document.querySelector("[data-archive-bar]");
    const picks = [...document.querySelectorAll("[data-archive-pick]")];
    const all = document.querySelector("[data-archive-all]");

    const needNote = (reason, note) => {
        if (reason === "Other" && note && note.value.trim() === "") {
            note.classList.add("need");
            note.focus();
            return true;
        }
        return false;
    };

    if (form && bar) {
        bar.classList.add("js");
        const count = bar.querySelector("[data-archive-count]");
        const go = bar.querySelector("[data-archive-go]");
        const reason = bar.querySelector("select[name=reason]");
        const note = bar.querySelector("input[name=note]");

        const update = () => {
            const ticked = picks.filter(p => p.checked).length;
            bar.classList.toggle("shown", ticked > 0);
            count.textContent = ticked === 1 ? "1 selected" : ticked + " selected";
            go.textContent = "Archive " + ticked;
            picks.forEach(p => p.closest("tr").classList.toggle("ticked", p.checked));
            if (all) {
                all.checked = ticked > 0 && ticked === picks.length;
                all.indeterminate = ticked > 0 && ticked < picks.length;
                all.disabled = picks.length === 0;
            }
        };

        picks.forEach(p => p.addEventListener("change", update));
        // A click anywhere in the first cell ticks the box, so it is easy to hit.
        document.querySelectorAll("td.pick").forEach(cell => cell.addEventListener("click", e => {
            const box = cell.querySelector("[data-archive-pick]");
            if (box && e.target !== box) {
                box.checked = !box.checked;
                update();
            }
        }));
        all?.addEventListener("change", () => {
            picks.forEach(p => { p.checked = all.checked; });
            update();
        });
        bar.querySelector("[data-archive-clear]").addEventListener("click", () => {
            picks.forEach(p => { p.checked = false; });
            update();
        });
        form.addEventListener("submit", e => {
            if (picks.every(p => !p.checked) || needNote(reason.value, note)) {
                e.preventDefault();
            }
        });
        note.addEventListener("input", () => note.classList.remove("need"));
        update();
    }

    const box = document.querySelector("[data-archive-box]");
    if (box) {
        const note = box.querySelector("input[name=note]");
        note.addEventListener("input", () => note.classList.remove("need"));
        box.addEventListener("submit", e => {
            if (needNote(e.submitter?.value, note)) {
                e.preventDefault();
            }
        });
    }

    const pane = document.querySelector(".detail");
    document.addEventListener("keydown", e => {
        if (!pane || e.ctrlKey || e.metaKey || e.altKey || e.target.closest("input:not([type=checkbox]), textarea, select")) {
            return;
        }
        const quick = box?.querySelector(`[data-archive-key="${e.key}"]`);
        if (quick) {
            e.preventDefault();
            quick.click(); // submits the form with this reason
        } else if (e.key === "ArrowDown" && pane.dataset.next) {
            e.preventDefault();
            location.href = pane.dataset.next;
        } else if (e.key === "ArrowUp" && pane.dataset.previous) {
            e.preventDefault();
            location.href = pane.dataset.previous;
        }
    });
})();
