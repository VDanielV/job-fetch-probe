// "Check fit" in the details pane. Without this script the button still works (the page reloads with the score).
//  F           checks the fit of the job in the pane (when the cursor is not in a text field).
//  While it runs: a timer and Cancel; the rest of the pane keeps working.
//  After a score: the archive box's note is filled in with "Fit 46% · Skip: the reason", so 1, 2, 3 or Other record it.
(() => {
    const form = document.querySelector("[data-fit-form]");
    const slot = document.querySelector("[data-fit-slot]");
    const button = form?.querySelector("button");
    if (!form || !slot || !button) {
        return;
    }

    let running = null;

    const fillNote = () => {
        const card = slot.querySelector("[data-fit-note]");
        const note = document.querySelector("[data-archive-box] input[name=note]");
        if (card && note && card.dataset.fitNote && (note.value.trim() === "" || note.dataset.fitFilled === "1")) {
            note.value = card.dataset.fitNote;
            note.dataset.fitFilled = "1";
            note.classList.remove("need");
        }
    };

    const wireAgain = () => {
        slot.querySelector("[data-fit-again]")?.addEventListener("click", start);
    };

    const busy = () => {
        slot.innerHTML =
            '<div class="fit-card busy"><b>Scoring with the local model…</b> <span data-fit-seconds>0 s</span>' +
            '<div class="fit-progress"><i></i></div>' +
            '<span class="fit-hint">Usually 30–90 seconds on this laptop. You can keep reading. ' +
            '<button type="button" class="fit-cancel" data-fit-cancel>Cancel</button></span></div>';
        const started = Date.now();
        const seconds = slot.querySelector("[data-fit-seconds]");
        const timer = setInterval(() => { seconds.textContent = Math.round((Date.now() - started) / 1000) + " s"; }, 1000);
        slot.querySelector("[data-fit-cancel]").addEventListener("click", () => running?.abort());
        return timer;
    };

    async function start() {
        if (running || button.disabled) {
            return;
        }
        running = new AbortController();
        button.disabled = true;
        const timer = busy();
        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "fetch" },
                signal: running.signal,
            });
            slot.innerHTML = await response.text();
            if (!response.ok && slot.innerHTML.trim() === "") {
                slot.innerHTML = '<div class="fit-card error"><b>Couldn\'t score this job.</b> The web app answered ' + response.status + '. <button type="button" class="fit-again" data-fit-again>Try again</button></div>';
            }
            fillNote();
            wireAgain();
        } catch (e) {
            if (e.name === "AbortError") {
                slot.innerHTML = "";
            } else {
                slot.innerHTML = '<div class="fit-card error"><b>Couldn\'t score this job.</b> The web app is not reachable. <button type="button" class="fit-again" data-fit-again>Try again</button></div>';
                wireAgain();
            }
        } finally {
            clearInterval(timer);
            running = null;
            button.disabled = false;
        }
    }

    form.addEventListener("submit", e => {
        e.preventDefault();
        start();
    });

    document.addEventListener("keydown", e => {
        if ((e.key === "f" || e.key === "F") && !e.ctrlKey && !e.metaKey && !e.altKey && !e.shiftKey
            && !e.target.closest("input:not([type=checkbox]), textarea, select")) {
            e.preventDefault();
            start();
        }
    });

    // A score shown after a reload (without the script's help) also fills in the note.
    fillNote();
    wireAgain();
})();
