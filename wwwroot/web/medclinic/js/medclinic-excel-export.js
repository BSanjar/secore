/**
 * Medclinic Excel export — loading state + blob download + success toast.
 */
(function (window, document) {
    "use strict";

    function parseFilenameFromDisposition(header) {
        if (!header) return null;
        var utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(header);
        if (utf8Match) {
            try {
                return decodeURIComponent(utf8Match[1]);
            } catch (e) {
                return utf8Match[1];
            }
        }
        var asciiMatch = /filename="?([^";]+)"?/i.exec(header);
        return asciiMatch ? asciiMatch[1] : null;
    }

    function isExcelResponse(response) {
        var ct = (response.headers.get("Content-Type") || "").toLowerCase();
        return (
            ct.indexOf("spreadsheetml") >= 0 ||
            ct.indexOf("ms-excel") >= 0 ||
            ct.indexOf("octet-stream") >= 0
        );
    }

    function triggerBlobDownload(blob, filename) {
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = filename || "export.xlsx";
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(function () {
            URL.revokeObjectURL(url);
        }, 0);
    }

    async function handleExcelExportClick(event) {
        var btn = event.target.closest("[data-mc-excel-export]");
        if (!btn || btn.disabled || btn.classList.contains("is-loading")) return;

        var url = btn.getAttribute("href");
        if (!url || url === "#") return;

        event.preventDefault();

        var ui = window.MedclinicUI || {};
        var successMessage = btn.dataset.successMessage || "Файл Excel сформирован и скачан.";
        var loadingLabel = btn.dataset.loadingText || "Выгрузка…";

        ui.setButtonLoading?.(btn, true, { label: loadingLabel });

        try {
            var response = await fetch(url, {
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!response.ok) {
                throw new Error("Не удалось сформировать файл Excel.");
            }

            if (!isExcelResponse(response)) {
                throw new Error("Сервер вернул неожиданный ответ. Проверьте права доступа.");
            }

            var blob = await response.blob();
            if (!blob || blob.size === 0) {
                throw new Error("Получен пустой файл Excel.");
            }

            var filename =
                parseFilenameFromDisposition(response.headers.get("Content-Disposition")) ||
                "export.xlsx";

            triggerBlobDownload(blob, filename);
            ui.showToast?.(successMessage, "success");
        } catch (error) {
            ui.showToast?.(error.message || "Не удалось выгрузить Excel.", "danger");
        } finally {
            ui.setButtonLoading?.(btn, false);
        }
    }

    document.addEventListener("click", handleExcelExportClick);

    window.MedclinicExcelExport = {
        handleClick: handleExcelExportClick
    };
})(window, document);
