/**
 * SECORE detsad UI helpers — loading states, overlays, button feedback.
 */
(function (window, document) {
    'use strict';

    var OVERLAY_ID = 'dsPageOverlay';

    function escapeHtml(str) {
        return String(str == null ? '' : str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function loaderHtml(label, compact) {
        var text = label || 'Загрузка';
        var cls = 'ds-loader' + (compact ? ' ds-loader--compact' : '');
        return (
            '<div class="' + cls + '" role="status" aria-live="polite">' +
                '<div class="ds-loader__mark" aria-hidden="true">' +
                    '<span class="ds-loader__ring"></span>' +
                    '<span class="ds-loader__ring ds-loader__ring--delayed"></span>' +
                    '<span class="ds-loader__cloud"></span>' +
                '</div>' +
                '<p class="ds-loader__label"><span class="ds-loader__dots">' + escapeHtml(text) + '</span></p>' +
            '</div>'
        );
    }

    function ensureOverlay() {
        var el = document.getElementById(OVERLAY_ID);
        if (el) return el;
        el = document.createElement('div');
        el.id = OVERLAY_ID;
        el.className = 'ds-page-overlay';
        el.setAttribute('aria-hidden', 'true');
        el.innerHTML =
            '<div class="ds-page-overlay__card">' +
                loaderHtml('Подождите', true) +
            '</div>';
        document.body.appendChild(el);
        return el;
    }

    function showOverlay(message) {
        var el = ensureOverlay();
        var label = el.querySelector('.ds-loader__dots');
        if (label) label.textContent = message || 'Подождите';
        el.classList.add('is-visible');
        el.setAttribute('aria-hidden', 'false');
    }

    function hideOverlay() {
        var el = document.getElementById(OVERLAY_ID);
        if (!el) return;
        el.classList.remove('is-visible');
        el.setAttribute('aria-hidden', 'true');
    }

    function setButtonLoading(btn, loading, options) {
        if (!btn) return;
        options = options || {};
        var label = options.label;
        if (loading) {
            if (!btn.dataset.dsOriginalHtml) {
                btn.dataset.dsOriginalHtml = btn.innerHTML;
            }
            if (btn.dataset.dsOriginalDisabled === undefined) {
                btn.dataset.dsOriginalDisabled = btn.disabled ? '1' : '0';
            }
            btn.disabled = true;
            btn.classList.add('is-loading');
            btn.setAttribute('aria-busy', 'true');
            var text = label || btn.dataset.loadingText || 'Сохранение...';
            btn.innerHTML =
                '<span class="ds-btn-spinner" aria-hidden="true"></span>' +
                '<span class="ds-btn-label">' + escapeHtml(text) + '</span>';
        } else {
            btn.classList.remove('is-loading');
            btn.removeAttribute('aria-busy');
            if (btn.dataset.dsOriginalHtml != null) {
                btn.innerHTML = btn.dataset.dsOriginalHtml;
                delete btn.dataset.dsOriginalHtml;
            }
            if (btn.dataset.dsOriginalDisabled === '0') {
                btn.disabled = false;
            } else if (btn.dataset.dsOriginalDisabled === '1') {
                btn.disabled = true;
            } else {
                btn.disabled = false;
            }
            delete btn.dataset.dsOriginalDisabled;
        }
    }

    function withButtonLoading(btn, promiseOrFn, options) {
        options = options || {};
        setButtonLoading(btn, true, options);
        var p = typeof promiseOrFn === 'function' ? promiseOrFn() : promiseOrFn;
        return Promise.resolve(p).finally(function () {
            if (!options.keepLoading) setButtonLoading(btn, false);
        });
    }

    function reloadWithOverlay(message) {
        showOverlay(message || 'Обновление...');
        window.setTimeout(function () {
            window.location.reload();
        }, 120);
    }

    var TOAST_CONTAINER_ID = 'dsToastContainer';
    var FLASH_KEY = 'detsadFlashToast';

    function ensureToastContainer() {
        var container = document.getElementById(TOAST_CONTAINER_ID);
        if (container) return container;
        container = document.createElement('div');
        container.id = TOAST_CONTAINER_ID;
        container.className = 'ds-toast-container';
        document.body.appendChild(container);
        return container;
    }

    function showToast(message, type) {
        if (!message) return;
        type = type === 'danger' || type === 'error' ? 'danger' : 'success';
        if (typeof bootstrap === 'undefined' || !bootstrap.Toast) {
            window.alert(message);
            return;
        }
        var container = ensureToastContainer();
        var el = document.createElement('div');
        el.className = 'toast ds-toast ds-toast--' + type + ' border-0';
        el.setAttribute('role', 'alert');
        el.setAttribute('aria-live', 'assertive');
        el.setAttribute('aria-atomic', 'true');

        if (type === 'success') {
            el.innerHTML =
                '<div class="ds-toast__inner">' +
                '<div class="ds-toast__icon-wrap" aria-hidden="true">' +
                '<svg class="ds-toast__icon" width="32" height="32" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">' +
                '<circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>' +
                '<path d="M8 12.5l2.5 2.5L16 9.5" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>' +
                '</svg></div>' +
                '<div class="ds-toast__text">' +
                '<div class="ds-toast__title">Готово!</div>' +
                '<div class="ds-toast__message">' + escapeHtml(message) + '</div>' +
                '</div>' +
                '<button type="button" class="btn-close ds-toast__close" data-bs-dismiss="toast" aria-label="Закрыть"></button>' +
                '</div>';
        } else {
            el.innerHTML =
                '<div class="ds-toast__inner ds-toast__inner--danger">' +
                '<div class="ds-toast__text">' +
                '<div class="ds-toast__title">Ошибка</div>' +
                '<div class="ds-toast__message">' + escapeHtml(message) + '</div>' +
                '</div>' +
                '<button type="button" class="btn-close ds-toast__close" data-bs-dismiss="toast" aria-label="Закрыть"></button>' +
                '</div>';
        }

        container.appendChild(el);
        var toast = new bootstrap.Toast(el, { delay: type === 'success' ? 7000 : 5500, autohide: true });
        el.addEventListener('hidden.bs.toast', function () {
            el.remove();
        });
        toast.show();
    }

    function queueFlashToast(message, type) {
        try {
            sessionStorage.setItem(FLASH_KEY, JSON.stringify({ message: message, type: type || 'success' }));
        } catch (e) { /* ignore */ }
    }

    function consumeFlashToast() {
        try {
            var raw = sessionStorage.getItem(FLASH_KEY);
            if (!raw) return;
            sessionStorage.removeItem(FLASH_KEY);
            var data = JSON.parse(raw);
            if (data && data.message) {
                window.setTimeout(function () {
                    showToast(data.message, data.type);
                }, 200);
            }
        } catch (e) { /* ignore */ }
    }

    function flashAndReload(message, reloadMessage) {
        queueFlashToast(message, 'success');
        reloadWithOverlay(reloadMessage || 'Обновление списка...');
    }

    window.DetsadUI = {
        loaderHtml: loaderHtml,
        showOverlay: showOverlay,
        hideOverlay: hideOverlay,
        setButtonLoading: setButtonLoading,
        withButtonLoading: withButtonLoading,
        reloadWithOverlay: reloadWithOverlay,
        showToast: showToast,
        flashAndReload: flashAndReload
    };

    document.addEventListener('DOMContentLoaded', function () {
        consumeFlashToast();
        // Soft press feedback for interactive controls without explicit handlers
        document.body.addEventListener('click', function (e) {
            var t = e.target && e.target.closest
                ? e.target.closest('.btn, .action-btn, .detsad-btn-primary, .detsad-btn-secondary')
                : null;
            if (!t || t.disabled || t.classList.contains('is-loading')) return;
            t.classList.add('ds-pressed');
            window.setTimeout(function () { t.classList.remove('ds-pressed'); }, 180);
        }, true);
    });
})(window, document);
