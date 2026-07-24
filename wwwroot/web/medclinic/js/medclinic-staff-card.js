(function () {
    const form = document.getElementById("staffCardForm");
    const fieldset = document.getElementById("staffCardFields");
    const saveBtn = document.getElementById("staffCardSaveBtn");

    if (!form) return;

    function getPageConfig() {
        return window.staffCardPage || {};
    }

    function canEditForm() {
        return getPageConfig().canEdit !== false;
    }

    let initialSnapshot = "";
    let submitConfirmed = false;

    function formSnapshot() {
        const data = new FormData(form);
        const pairs = [];

        for (const [key, value] of data.entries()) {
            if (key.startsWith("__")) continue;
            pairs.push(`${key}=${String(value)}`);
        }

        pairs.sort();
        return pairs.join("&");
    }

    function updateDirtyState() {
        if (!saveBtn || !canEditForm()) return;

        const dirty = formSnapshot() !== initialSnapshot;
        saveBtn.classList.toggle("d-none", !dirty);
        saveBtn.classList.toggle("is-visible", dirty);
    }

    function showFlashMessages() {
        const page = getPageConfig();
        const ui = window.MedclinicUI;

        if (page.flashMessage && ui?.showToast) {
            ui.showToast(page.flashMessage, "success");
        } else if (page.flashMessage) {
            window.alert(page.flashMessage);
        }

        if (page.flashError && ui?.showToast) {
            ui.showToast(page.flashError, "danger");
        } else if (page.flashError) {
            window.alert(page.flashError);
        }
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    async function requestSaveConfirmation() {
        const page = getPageConfig();
        const ui = window.MedclinicUI;

        if (ui?.confirm) {
            return ui.confirm({
                title: "Сохранить изменения?",
                message: "Данные сотрудника будут обновлены в системе.",
                details: page.staffName
                    ? `<strong>${escapeHtml(page.staffName)}</strong>`
                    : "",
                confirmLabel: "Сохранить",
                cancelLabel: "Отмена"
            });
        }

        return window.confirm("Сохранить изменения в карточке сотрудника?");
    }

    if (saveBtn && canEditForm()) {
        saveBtn.addEventListener("click", async () => {
            if (saveBtn.classList.contains("d-none")) return;

            if (typeof window.jQuery !== "undefined" && window.jQuery(form).valid && !window.jQuery(form).valid()) {
                return;
            }

            const confirmed = await requestSaveConfirmation();
            if (!confirmed) return;

            submitConfirmed = true;

            const ui = window.MedclinicUI;
            if (ui) {
                ui.setButtonLoading(saveBtn, true, { label: "Сохранение..." });
                ui.showPageOverlay("Сохранение...");
            } else {
                saveBtn.disabled = true;
            }

            if (fieldset) fieldset.disabled = false;
            form.submit();
        });
    }

    form.addEventListener("input", updateDirtyState);
    form.addEventListener("change", updateDirtyState);

    form.addEventListener("submit", (event) => {
        if (!submitConfirmed) {
            event.preventDefault();
            return;
        }

        if (fieldset) fieldset.disabled = false;
    });

    const overrideModeInputs = form.querySelectorAll('input[name="Form.OverrideMode"]');
    const overrideStart = form.querySelector('input[name="Form.OverrideStartTime"]');
    const overrideEnd = form.querySelector('input[name="Form.OverrideEndTime"]');

    function syncOverrideMode() {
        const mode = form.querySelector('input[name="Form.OverrideMode"]:checked')?.value || "default";
        const disabled = mode !== "custom";
        if (overrideStart) overrideStart.readOnly = disabled;
        if (overrideEnd) overrideEnd.readOnly = disabled;
    }

    overrideModeInputs.forEach((input) => input.addEventListener("change", syncOverrideMode));
    syncOverrideMode();

    initialSnapshot = formSnapshot();
    updateDirtyState();
    showFlashMessages();
})();
