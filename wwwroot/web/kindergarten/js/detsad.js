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

    window.DetsadUI = {
        loaderHtml: loaderHtml,
        showOverlay: showOverlay,
        hideOverlay: hideOverlay,
        setButtonLoading: setButtonLoading,
        withButtonLoading: withButtonLoading,
        reloadWithOverlay: reloadWithOverlay
    };

    document.addEventListener('DOMContentLoaded', function () {
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
