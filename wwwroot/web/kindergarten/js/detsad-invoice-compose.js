/**
 * Detsad: название счёта вручную (без услуг) ИЛИ выбор услуг (название автоматически).
 */
(function (window, document) {
    'use strict';

    function parseServices(wrap) {
        var dataEl = wrap.querySelector('.ds-inv-compose-data');
        var raw = dataEl ? (dataEl.textContent || '[]') : '[]';
        try {
            var list = JSON.parse(raw);
            return Array.isArray(list) ? list : [];
        } catch (e) {
            return [];
        }
    }

    function escapeHtml(str) {
        return String(str == null ? '' : str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function init(root, options) {
        if (!root) return null;
        if (root._composeApi) {
            root._composeApi.reset();
            return root._composeApi;
        }
        var wrap = root.closest('.ds-inv-compose-wrap') || root.parentElement || root;
        options = options || {};
        var services = parseServices(wrap).map(function (s) {
            return {
                id: String(s.id || s.Id || ''),
                name: String(s.name || s.Name || '').trim(),
                price: parseFloat(s.price != null ? s.price : s.ServiceSumm) || 0
            };
        }).filter(function (s) { return s.id && s.name; });

        var nameEl = root.querySelector('.ds-inv-compose__invoice-name');
        var catalogEl = root.querySelector('.ds-inv-compose__catalog');
        var catalogWrap = root.querySelector('.ds-inv-compose__catalog-wrap');
        var catalogEmptyEl = root.querySelector('.ds-inv-compose__catalog-empty');
        var amountRow = root.querySelector('.ds-inv-compose__amount-row');
        var amountEl = root.querySelector('.ds-inv-compose__amount');
        var summaryEl = root.querySelector('.ds-inv-compose__summary');
        var summaryTotalEl = root.querySelector('.ds-inv-compose__summary-total');
        var errorEl = root.querySelector('.ds-inv-compose__error');

        var selected = [];
        var mode = 'empty'; // empty | manual | services

        function notify() {
            if (typeof options.onChange === 'function') options.onChange();
        }

        function hideError() {
            if (errorEl) {
                errorEl.hidden = true;
                errorEl.textContent = '';
            }
        }

        function isSelected(id) {
            return selected.some(function (s) { return s.id === id; });
        }

        function setManualMode(on) {
            if (on) {
                mode = 'manual';
                selected = [];
                if (nameEl) {
                    nameEl.readOnly = false;
                }
                if (catalogWrap) catalogWrap.classList.add('is-locked');
                if (amountRow) amountRow.hidden = false;
                renderCatalog();
            } else if (mode === 'manual') {
                mode = 'empty';
                if (catalogWrap) catalogWrap.classList.remove('is-locked');
                if (amountRow) amountRow.hidden = true;
                if (amountEl) amountEl.value = '';
            }
        }

        function applyServicesMode() {
            mode = 'services';
            if (catalogWrap) catalogWrap.classList.remove('is-locked');
            if (amountRow) amountRow.hidden = true;
            if (amountEl) amountEl.value = '';
            if (nameEl) {
                nameEl.value = selected.map(function (s) { return s.name; }).join(', ');
                nameEl.readOnly = true;
            }
        }

        function applyEmptyMode() {
            mode = 'empty';
            selected = [];
            if (nameEl) {
                nameEl.value = '';
                nameEl.readOnly = false;
            }
            if (catalogWrap) catalogWrap.classList.remove('is-locked');
            if (amountRow) amountRow.hidden = true;
            if (amountEl) amountEl.value = '';
            renderCatalog();
        }

        function toggleService(svc) {
            if (!svc || mode === 'manual') return;
            if (isSelected(svc.id)) {
                selected = selected.filter(function (x) { return x.id !== svc.id; });
                if (selected.length) {
                    applyServicesMode();
                } else {
                    if (nameEl) {
                        nameEl.readOnly = false;
                        nameEl.value = '';
                    }
                    mode = 'empty';
                    if (nameEl) nameEl.readOnly = false;
                }
            } else {
                selected.push({ id: svc.id, name: svc.name, price: svc.price });
                applyServicesMode();
            }
            renderCatalog();
            updateUi();
            notify();
        }

        function renderCatalog() {
            if (!catalogEl) return;
            if (catalogEmptyEl) {
                catalogEmptyEl.hidden = services.length > 0;
                catalogEmptyEl.textContent = 'Нет услуг';
            }
            if (!services.length) {
                catalogEl.innerHTML = '';
                return;
            }
            catalogEl.innerHTML = services.map(function (s) {
                var sel = isSelected(s.id);
                return (
                    '<button type="button" class="ds-inv-compose__tile' + (sel ? ' is-selected' : '') + '" data-id="' + escapeHtml(s.id) + '" role="option" aria-selected="' + (sel ? 'true' : 'false') + '">' +
                    '<span class="ds-inv-compose__tile-mark" aria-hidden="true"></span>' +
                    '<span class="ds-inv-compose__tile-body">' +
                    '<span class="ds-inv-compose__tile-name">' + escapeHtml(s.name) + '</span>' +
                    '<span class="ds-inv-compose__tile-price">' + s.price.toFixed(2) + ' сом</span>' +
                    '</span></button>'
                );
            }).join('');
            catalogEl.querySelectorAll('.ds-inv-compose__tile').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    if (mode === 'manual') return;
                    var id = btn.getAttribute('data-id');
                    var svc = services.find(function (x) { return x.id === id; });
                    toggleService(svc);
                });
            });
        }

        function updateUi() {
            hideError();
            root.classList.toggle('ds-inv-compose--manual', mode === 'manual');
            root.classList.toggle('ds-inv-compose--services', mode === 'services');

            if (mode === 'services' && selected.length) {
                var total = selected.reduce(function (sum, s) { return sum + s.price; }, 0);
                if (summaryEl) summaryEl.hidden = false;
                if (summaryTotalEl) {
                    summaryTotalEl.textContent = total.toFixed(2) + ' сом · ' + selected.length + ' усл.';
                }
            } else if (mode === 'manual') {
                if (summaryEl) summaryEl.hidden = true;
            } else {
                if (summaryEl) summaryEl.hidden = true;
            }
        }

        function reset() {
            selected = [];
            mode = 'empty';
            if (nameEl) {
                nameEl.value = '';
                nameEl.readOnly = false;
            }
            if (amountEl) amountEl.value = '';
            if (amountRow) amountRow.hidden = true;
            if (catalogWrap) catalogWrap.classList.remove('is-locked');
            renderCatalog();
            updateUi();
            hideError();
        }

        function validate() {
            if (mode === 'services' && selected.length) {
                return { ok: true, mode: 'services' };
            }
            if (mode === 'manual') {
                var name = nameEl ? nameEl.value.trim() : '';
                if (!name) {
                    return { ok: false, message: 'Укажите наименование счёта.' };
                }
                var amount = amountEl ? parseFloat(amountEl.value) : NaN;
                if (isNaN(amount) || amount < 0) {
                    return { ok: false, message: 'Укажите сумму, сом.' };
                }
                return { ok: true, mode: 'manual' };
            }
            return { ok: false, message: 'Введите название или выберите услуги.' };
        }

        function getPayloadPart() {
            var v = validate();
            if (!v.ok) return null;
            if (v.mode === 'services') {
                return {
                    nameInvoice: selected.map(function (s) { return s.name; }).join(', '),
                    serviceItems: selected.map(function (s) { return { serviceId: s.id, qty: 1 }; }),
                    manualServicePriceSom: null
                };
            }
            return {
                nameInvoice: nameEl.value.trim(),
                serviceItems: [],
                manualServicePriceSom: parseFloat(amountEl.value)
            };
        }

        function getPreviewLines() {
            if (mode === 'services' && selected.length) {
                var total = 0;
                var lines = selected.map(function (s) {
                    total += s.price;
                    return { name: s.name, price: s.price, qty: 1, total: s.price };
                });
                return {
                    name: selected.map(function (s) { return s.name; }).join(', '),
                    total: total,
                    lines: lines
                };
            }
            var name = nameEl ? nameEl.value.trim() : '';
            var amount = amountEl ? parseFloat(amountEl.value) : 0;
            if (mode !== 'manual' || !name || isNaN(amount)) {
                return { name: '—', total: 0, lines: [] };
            }
            return {
                name: name,
                total: amount,
                lines: [{ name: name, price: amount, qty: 1, total: amount }]
            };
        }

        function syncHiddenName(nameInput) {
            if (!nameInput) return;
            if (mode === 'services' && selected.length) {
                nameInput.value = selected.map(function (s) { return s.name; }).join(', ');
            } else if (mode === 'manual' && nameEl) {
                nameInput.value = nameEl.value.trim();
            } else {
                nameInput.value = nameEl ? nameEl.value.trim() : '';
            }
        }

        if (nameEl) {
            nameEl.addEventListener('input', function () {
                if (mode === 'services') return;
                var v = nameEl.value.trim();
                if (v) {
                    if (mode !== 'manual') {
                        setManualMode(true);
                    }
                } else {
                    setManualMode(false);
                    mode = 'empty';
                    if (catalogWrap) catalogWrap.classList.remove('is-locked');
                    if (amountRow) amountRow.hidden = true;
                    if (amountEl) amountEl.value = '';
                    renderCatalog();
                }
                updateUi();
                notify();
            });
        }
        if (amountEl) {
            amountEl.addEventListener('input', function () { notify(); });
            amountEl.addEventListener('change', function () { notify(); });
        }

        renderCatalog('');
        updateUi();

        var api = {
            reset: reset,
            validate: validate,
            getPayloadPart: getPayloadPart,
            getPreviewLines: getPreviewLines,
            syncHiddenName: syncHiddenName
        };
        root._composeApi = api;
        return api;
    }

    window.DetsadInvoiceCompose = { init: init };
})(window, document);
