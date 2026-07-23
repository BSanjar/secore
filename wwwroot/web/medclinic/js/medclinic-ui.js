/**
 * Medclinic UI helpers — loading states, toasts (detsad-style).
 */
(function (window, document) {
    "use strict";

    var PAGE_OVERLAY_ID = "mcPageOverlay";
    var pageOverlayDepth = 0;

    function escapeHtml(str) {
        return String(str == null ? "" : str)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function loaderHtml(label, compact) {
        var text = label || "Загрузка";
        var cls = "mc-loader" + (compact ? " mc-loader--compact" : "");
        return (
            '<div class="' + cls + '" role="status" aria-live="polite">' +
            '<div class="mc-loader__mark" aria-hidden="true">' +
            '<span class="mc-loader__ring"></span>' +
            '<span class="mc-loader__ring mc-loader__ring--delayed"></span>' +
            '<span class="mc-loader__cloud"></span>' +
            "</div>" +
            '<p class="mc-loader__label"><span class="mc-loader__dots">' + escapeHtml(text) + "</span></p>" +
            "</div>"
        );
    }

    function ensurePageOverlay() {
        var el = document.getElementById(PAGE_OVERLAY_ID);
        if (el) return el;
        el = document.createElement("div");
        el.id = PAGE_OVERLAY_ID;
        el.className = "mc-page-overlay";
        el.setAttribute("aria-hidden", "true");
        el.innerHTML = '<div class="mc-page-overlay__card">' + loaderHtml("Подождите", true) + "</div>";
        document.body.appendChild(el);
        return el;
    }

    function showPageOverlay(message) {
        var el = ensurePageOverlay();
        var label = el.querySelector(".mc-loader__dots");
        if (label) label.textContent = message || "Подождите";
        pageOverlayDepth += 1;
        el.classList.add("is-visible");
        el.setAttribute("aria-hidden", "false");
    }

    function hidePageOverlay() {
        pageOverlayDepth = Math.max(0, pageOverlayDepth - 1);
        if (pageOverlayDepth > 0) return;
        var el = document.getElementById(PAGE_OVERLAY_ID);
        if (!el) return;
        el.classList.remove("is-visible");
        el.setAttribute("aria-hidden", "true");
    }

    function setButtonLoading(btn, loading, options) {
        if (!btn) return;
        options = options || {};
        var label = options.label;
        if (loading) {
            if (!btn.dataset.mcOriginalHtml) {
                btn.dataset.mcOriginalHtml = btn.innerHTML;
            }
            if (btn.dataset.mcOriginalDisabled === undefined) {
                btn.dataset.mcOriginalDisabled = btn.disabled ? "1" : "0";
            }
            btn.disabled = true;
            btn.classList.add("is-loading");
            btn.setAttribute("aria-busy", "true");
            var text = label || btn.dataset.loadingText || "Сохранение...";
            btn.innerHTML =
                '<span class="mc-btn-spinner" aria-hidden="true"></span>' +
                '<span class="mc-btn-label">' + escapeHtml(text) + "</span>";
        } else {
            btn.classList.remove("is-loading");
            btn.removeAttribute("aria-busy");
            if (btn.dataset.mcOriginalHtml != null) {
                btn.innerHTML = btn.dataset.mcOriginalHtml;
                delete btn.dataset.mcOriginalHtml;
            }
            if (btn.dataset.mcOriginalDisabled === "0") {
                btn.disabled = false;
            } else if (btn.dataset.mcOriginalDisabled === "1") {
                btn.disabled = true;
            } else {
                btn.disabled = false;
            }
            delete btn.dataset.mcOriginalDisabled;
            if (typeof window.medclinicValidateAppointmentForm === "function") {
                window.medclinicValidateAppointmentForm();
            }
        }
    }

    var TOAST_CONTAINER_ID = "mcToastContainer";

    function ensureToastContainer() {
        var container = document.getElementById(TOAST_CONTAINER_ID);
        if (container) return container;
        container = document.createElement("div");
        container.id = TOAST_CONTAINER_ID;
        container.className = "mc-toast-container";
        document.body.appendChild(container);
        return container;
    }

    function showToast(message, type) {
        if (!message) return;
        type = type === "danger" || type === "error" ? "danger" : "success";
        if (typeof bootstrap === "undefined" || !bootstrap.Toast) {
            window.alert(message);
            return;
        }
        var container = ensureToastContainer();
        var el = document.createElement("div");
        el.className = "toast mc-toast mc-toast--" + type + " border-0";
        el.setAttribute("role", "alert");
        el.setAttribute("aria-live", "assertive");
        el.setAttribute("aria-atomic", "true");

        if (type === "success") {
            el.innerHTML =
                '<div class="mc-toast__inner">' +
                '<div class="mc-toast__icon-wrap" aria-hidden="true">' +
                '<svg class="mc-toast__icon" width="32" height="32" viewBox="0 0 24 24" fill="none">' +
                '<circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>' +
                '<path d="M8 12.5l2.5 2.5L16 9.5" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>' +
                "</svg></div>" +
                '<div class="mc-toast__text">' +
                '<div class="mc-toast__title">Готово!</div>' +
                '<div class="mc-toast__message">' +
                escapeHtml(message) +
                "</div></div>" +
                '<button type="button" class="btn-close mc-toast__close" data-bs-dismiss="toast" aria-label="Закрыть"></button>' +
                "</div>";
        } else {
            el.innerHTML =
                '<div class="mc-toast__inner mc-toast__inner--danger">' +
                '<div class="mc-toast__text">' +
                '<div class="mc-toast__title">Ошибка</div>' +
                '<div class="mc-toast__message">' +
                escapeHtml(message) +
                "</div></div>" +
                '<button type="button" class="btn-close mc-toast__close" data-bs-dismiss="toast" aria-label="Закрыть"></button>' +
                "</div>";
        }

        container.appendChild(el);
        var toast = new bootstrap.Toast(el, { delay: type === "success" ? 7000 : 5500, autohide: true });
        el.addEventListener("hidden.bs.toast", function () {
            el.remove();
        });
        toast.show();
    }

    var confirmModalInstance = null;
    var confirmResolve = null;

    function confirm(options) {
        options = options || {};
        var modalEl = document.getElementById("mcConfirmModal");
        if (!modalEl || typeof bootstrap === "undefined" || !bootstrap.Modal) {
            var fallback = (options.title ? options.title + "\n\n" : "") + (options.message || "Подтвердить?");
            return Promise.resolve(window.confirm(fallback));
        }

        var titleEl = document.getElementById("mcConfirmModalTitle");
        var messageEl = document.getElementById("mcConfirmModalMessage");
        var detailsEl = document.getElementById("mcConfirmModalDetails");
        var okBtn = document.getElementById("mcConfirmModalOk");
        var cancelBtn = document.getElementById("mcConfirmModalCancel");

        if (titleEl) titleEl.textContent = options.title || "Подтверждение";
        if (messageEl) messageEl.textContent = options.message || "";
        if (detailsEl) {
            if (options.details) {
                detailsEl.innerHTML = options.details;
                detailsEl.classList.remove("d-none");
            } else {
                detailsEl.innerHTML = "";
                detailsEl.classList.add("d-none");
            }
        }
        if (okBtn) okBtn.textContent = options.confirmLabel || "Подтвердить";
        if (cancelBtn) cancelBtn.textContent = options.cancelLabel || "Отмена";

        if (!confirmModalInstance) {
            confirmModalInstance = new bootstrap.Modal(modalEl, { focus: true, backdrop: "static" });
            modalEl.addEventListener("hidden.bs.modal", function () {
                if (confirmResolve) {
                    var fn = confirmResolve;
                    confirmResolve = null;
                    fn(false);
                }
            });
            okBtn?.addEventListener("click", function () {
                if (confirmResolve) {
                    var fn = confirmResolve;
                    confirmResolve = null;
                    confirmModalInstance.hide();
                    fn(true);
                }
            });
        }

        return new Promise(function (resolve) {
            confirmResolve = resolve;
            confirmModalInstance.show();
        });
    }

    var paidCancelModalInstance = null;
    var paidCancelResolve = null;

    function confirmPaidCancel(options) {
        options = options || {};
        var modalEl = document.getElementById("mcPaidCancelModal");
        if (!modalEl || typeof bootstrap === "undefined" || !bootstrap.Modal) {
            var msg =
                (options.message || "По записи уже была оплата.") +
                "\n\n1 — с возвратом\n2 — без возврата\nОтмена — не отменять";
            var raw = window.prompt(msg, "");
            if (raw === null) return Promise.resolve(null);
            if (raw === "1") return Promise.resolve("refund");
            if (raw === "2") return Promise.resolve("no_refund");
            return Promise.resolve(null);
        }

        var titleEl = document.getElementById("mcPaidCancelModalTitle");
        var messageEl = document.getElementById("mcPaidCancelModalMessage");
        var detailsEl = document.getElementById("mcPaidCancelModalDetails");
        var refundBtn = document.getElementById("mcPaidCancelRefundBtn");
        var noRefundBtn = document.getElementById("mcPaidCancelNoRefundBtn");
        var abortBtn = document.getElementById("mcPaidCancelAbortBtn");

        if (titleEl) titleEl.textContent = options.title || "Отменить запись?";
        if (messageEl) {
            messageEl.textContent =
                options.message ||
                "По записи уже была оплата. Отменить с возвратом суммы или без возврата?";
        }
        if (detailsEl) {
            if (options.details) {
                detailsEl.innerHTML = options.details;
                detailsEl.classList.remove("d-none");
            } else {
                detailsEl.innerHTML = "";
                detailsEl.classList.add("d-none");
            }
        }

        if (!paidCancelModalInstance) {
            paidCancelModalInstance = new bootstrap.Modal(modalEl, { focus: true, backdrop: "static" });
            modalEl.addEventListener("hidden.bs.modal", function () {
                if (paidCancelResolve) {
                    var fn = paidCancelResolve;
                    paidCancelResolve = null;
                    fn(null);
                }
            });
            refundBtn?.addEventListener("click", function () {
                if (paidCancelResolve) {
                    var fn = paidCancelResolve;
                    paidCancelResolve = null;
                    paidCancelModalInstance.hide();
                    fn("refund");
                }
            });
            noRefundBtn?.addEventListener("click", function () {
                if (paidCancelResolve) {
                    var fn = paidCancelResolve;
                    paidCancelResolve = null;
                    paidCancelModalInstance.hide();
                    fn("no_refund");
                }
            });
            abortBtn?.addEventListener("click", function () {
                if (paidCancelResolve) {
                    var fn = paidCancelResolve;
                    paidCancelResolve = null;
                    fn(null);
                }
            });
        }

        return new Promise(function (resolve) {
            paidCancelResolve = resolve;
            paidCancelModalInstance.show();
        });
    }

    window.MedclinicUI = {
        setButtonLoading: setButtonLoading,
        showPageOverlay: showPageOverlay,
        hidePageOverlay: hidePageOverlay,
        showToast: showToast,
        confirm: confirm,
        confirmPaidCancel: confirmPaidCancel
    };
})(window, document);
