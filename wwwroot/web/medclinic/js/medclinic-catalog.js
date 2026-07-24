(function () {
    const page = window.mcCatalogPage || {};
    const ui = window.MedclinicUI || {};

    function showFlashMessages() {
        if (page.flashMessage) {
            ui.showToast ? ui.showToast(page.flashMessage, "success") : window.alert(page.flashMessage);
        }
        if (page.flashWarning) {
            ui.showToast ? ui.showToast(page.flashWarning, "success") : window.alert(page.flashWarning);
        }
        if (page.flashError) {
            ui.showToast ? ui.showToast(page.flashError, "danger") : window.alert(page.flashError);
        }
    }

    function bindSubmitOverlay(selector, overlayText) {
        document.querySelectorAll(selector).forEach((form) => {
            form.addEventListener("submit", () => {
                const btn = form.querySelector('button[type="submit"]:not([form])') || form.querySelector('button[type="submit"]');
                const text = btn?.dataset?.loadingText || overlayText;
                ui.showPageOverlay?.(text);
                ui.setButtonLoading?.(btn, true);
            });
        });
    }

    bindSubmitOverlay(".mc-catalog-form", "Сохранение...");
    bindSubmitOverlay(".mc-catalog-import-form", "Загрузка файла...");
    showFlashMessages();
})();
