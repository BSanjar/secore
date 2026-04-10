(function() {
    var baseUrl = '/Clients';
    var selectedClientIds = [];

    function fmtDate(d) {
        var day = ('0' + d.getDate()).slice(-2);
        var month = ('0' + (d.getMonth() + 1)).slice(-2);
        return day + '.' + month + '.' + d.getFullYear();
    }
    function fmtDateTime(d) {
        var day = ('0' + d.getDate()).slice(-2);
        var month = ('0' + (d.getMonth() + 1)).slice(-2);
        var h = ('0' + d.getHours()).slice(-2);
        var min = ('0' + d.getMinutes()).slice(-2);
        return day + '.' + month + '.' + d.getFullYear() + ' ' + h + ':' + min;
    }
    function parseDateVal(val) {
        if (!val) return null;
        return new Date(val.indexOf('T') !== -1 ? val : val + 'T12:00:00');
    }
    function formatDateTimeForPreview(val) {
        if (!val || val === '—') return val;
        var d = parseDateVal(val);
        return isNaN(d.getTime()) ? val : (val.indexOf('T') !== -1 ? fmtDateTime(d) : fmtDate(d));
    }
    /** Формат даты для input datetime-local: локальное время YYYY-MM-DDTHH:mm (не UTC) */
    function toDateTimeLocalString(d) {
        var y = d.getFullYear();
        var m = ('0' + (d.getMonth() + 1)).slice(-2);
        var day = ('0' + d.getDate()).slice(-2);
        var h = ('0' + d.getHours()).slice(-2);
        var min = ('0' + d.getMinutes()).slice(-2);
        return y + '-' + m + '-' + day + 'T' + h + ':' + min;
    }

    window.openCreateInvoiceModal = function() {
        var modal = document.getElementById('createInvoiceModal');
        var body = document.getElementById('createInvoiceModalBody');
        if (!modal || !body) return;
        body.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary"></div></div>';
        var modalBs = new bootstrap.Modal(modal);
        modalBs.show();

        fetch(baseUrl + '/CreateInvoicePartial')
            .then(function(r) { return r.text(); })
            .then(function(html) {
                body.innerHTML = html;
                initStep1(body);
            })
            .catch(function() {
                body.innerHTML = '<div class="alert alert-danger">Ошибка загрузки формы.</div>';
            });
    };

    function collectClientIdsFromStep1(container) {
        var checked = container.querySelectorAll('#invoiceTreeWrap .tree-client-check:checked');
        return [].map.call(checked, function(cb) { return cb.closest('.tree-client').getAttribute('data-client-id'); }).filter(Boolean);
    }

    function updateSelectedClientsList(container) {
        var listEl = container.querySelector('#selectedClientsList');
        if (!listEl) return;
        var items = container.querySelectorAll('#invoiceTreeWrap .tree-client-check:checked');
        if (items.length === 0) {
            listEl.innerHTML = '<p class="text-muted small mb-0">Выберите клиентов в дереве слева.</p>';
            return;
        }
        var html = '<ul class="selected-clients-ul list-unstyled mb-0">';
        items.forEach(function(cb) {
            var li = cb.closest('.tree-client');
            var id = li.getAttribute('data-client-id');
            var name = li.getAttribute('data-client-name') || id;
            html += '<li class="selected-client-item d-flex align-items-center justify-content-between py-1 border-bottom" data-client-id="' + escapeAttr(id) + '">';
            html += '<span class="selected-client-name">' + escapeHtml(name) + '</span>';
            html += '<button type="button" class="btn btn-sm btn-link text-danger p-0 selected-client-remove" title="Убрать">×</button>';
            html += '</li>';
        });
        html += '</ul>';
        listEl.innerHTML = html;
        listEl.querySelectorAll('.selected-client-remove').forEach(function(btn) {
            btn.addEventListener('click', function() {
                var id = btn.closest('.selected-client-item').getAttribute('data-client-id');
                container.querySelectorAll('#invoiceTreeWrap .tree-client').forEach(function(li) {
                    if (li.getAttribute('data-client-id') === id) {
                        var c = li.querySelector('.tree-client-check');
                        if (c) c.checked = false;
                    }
                });
                updateSelectedClientsList(container);
                updateGroupCheckboxes(container);
            });
        });
    }

    function escapeAttr(s) {
        return String(s).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function updateGroupCheckboxes(container) {
        container.querySelectorAll('.tree-group').forEach(function(gr) {
            var checks = gr.querySelectorAll('.tree-client-check');
            var checked = gr.querySelectorAll('.tree-client-check:checked');
            var groupCheck = gr.querySelector('.tree-group-check');
            if (!groupCheck) return;
            groupCheck.checked = checks.length > 0 && checked.length === checks.length;
            groupCheck.indeterminate = checked.length > 0 && checked.length < checks.length;
        });
    }

    function applyTreeSearch(container) {
        var term = (container.querySelector('#invoiceRecipientSearch') || {}).value || '';
        var type = (container.querySelector('#invoiceRecipientSearchType') || {}).value || 'all';
        term = term.toLowerCase().trim();
        container.querySelectorAll('.tree-group').forEach(function(gr) {
            var groupName = (gr.getAttribute('data-group-name') || '').toLowerCase();
            var clients = gr.querySelectorAll('.tree-client');
            var anyClientVisible = false;
            clients.forEach(function(cl) {
                var clientName = (cl.getAttribute('data-client-name') || '').toLowerCase();
                var showGroup = type === 'groups' ? groupName.indexOf(term) >= 0 : true;
                var showClient = type === 'clients' ? clientName.indexOf(term) >= 0 : (type === 'groups' ? false : (groupName.indexOf(term) >= 0 || clientName.indexOf(term) >= 0));
                if (type === 'all') showClient = !term || groupName.indexOf(term) >= 0 || clientName.indexOf(term) >= 0;
                cl.style.display = showClient ? '' : 'none';
                if (showClient) anyClientVisible = true;
            });
            var headerVisible = !term || (type === 'groups' && groupName.indexOf(term) >= 0) || (type === 'clients' && anyClientVisible) || (type === 'all' && (groupName.indexOf(term) >= 0 || anyClientVisible));
            gr.style.display = headerVisible ? '' : 'none';
        });
    }

    function initStep1(container) {
        var step1Next = container.querySelector('#invoiceStep1Next');
        var step1 = container.querySelector('#createInvoiceStep1');
        var step2 = container.querySelector('#createInvoiceStep2');
        var treeWrap = container.querySelector('#invoiceTreeWrap');

        if (treeWrap) {
            treeWrap.querySelectorAll('.tree-group-header').forEach(function(h) {
                var gr = h.closest('.tree-group');
                var toggle = h.querySelector('.tree-toggle');
                var groupCheck = h.querySelector('.tree-group-check');
                if (toggle) {
                    toggle.addEventListener('click', function() {
                        gr.classList.toggle('tree-group-expanded');
                        toggle.textContent = gr.classList.contains('tree-group-expanded') ? '▼' : '▶';
                    });
                }
                if (groupCheck) {
                    groupCheck.addEventListener('change', function() {
                        gr.querySelectorAll('.tree-client-check').forEach(function(cb) {
                            cb.checked = groupCheck.checked;
                        });
                        updateSelectedClientsList(container);
                    });
                }
            });
            treeWrap.querySelectorAll('.tree-client-check').forEach(function(cb) {
                cb.addEventListener('change', function() {
                    updateSelectedClientsList(container);
                    updateGroupCheckboxes(container);
                });
            });
        }

        var searchInput = container.querySelector('#invoiceRecipientSearch');
        var searchType = container.querySelector('#invoiceRecipientSearchType');
        if (searchInput) searchInput.addEventListener('input', function() { applyTreeSearch(container); });
        if (searchType) searchType.addEventListener('change', function() { applyTreeSearch(container); });

        if (step1Next && step1 && step2) {
            step1Next.addEventListener('click', function() {
                selectedClientIds = collectClientIdsFromStep1(container);
                if (selectedClientIds.length === 0) {
                    alert('Выберите хотя бы одного клиента в дереве.');
                    return;
                }
                step1.classList.add('d-none');
                step2.classList.remove('d-none');
                initStep2(container);
            });
        }

        updateSelectedClientsList(container);
    }

    function initStep2(container) {
        var step2Back = container.querySelector('#invoiceStep2Back');
        var step1 = container.querySelector('#createInvoiceStep1');
        var step2 = container.querySelector('#createInvoiceStep2');
        var payCodeEl = container.querySelector('#invoicePayCode');
        var dateStartEl = container.querySelector('#invoiceDateStart');
        var nameEl = container.querySelector('#invoiceName');
        var periodicityEl = container.querySelector('#invoicePeriodicity');
        var qrBtn = container.querySelector('#invoiceGenerateQr');
        var qrContainer = container.querySelector('#invoiceQrContainer');
        var createBtn = container.querySelector('#invoiceCreateBtn');
        var servicesBody = container.querySelector('#invoiceServicesBody');

        if (dateStartEl) dateStartEl.value = toDateTimeLocalString(new Date());

        var wrap = container.querySelector('.create-invoice-wrap') || container;
        var disableServiceSelection = wrap.getAttribute('data-disable-invoice-service-selection') === 'true';
        var allowedHassame = wrap.getAttribute('data-allowed-hassame-account') === 'true';
        var payCodeMode = (wrap.getAttribute('data-invoice-pay-code-mode') || 'new_only');
        var showPayCodeSelect = (payCodeMode === 'duplicate_only' || payCodeMode === 'both');
        var allowNewPayCode = (payCodeMode === 'both');

        var autoProlongationEl = container.querySelector('#invoiceAutoProlongation');
        var useCurrentDateTimeEl = container.querySelector('#invoiceUseCurrentDateTime');
        var dateEndBlock = container.querySelector('#invoiceDateEndBlock');
        var dateEndEl = container.querySelector('#invoiceDateEnd');

        function toggleDateEndBlock() {
            var on = autoProlongationEl && autoProlongationEl.checked;
            var periodicity = (periodicityEl || {}).value || 'monthly';
            var isOneTime = periodicity === 'oneTime';
            if (dateEndBlock) dateEndBlock.classList.toggle('d-none', !!on || isOneTime);
            if (dateEndEl) {
                if (on) { dateEndEl.value = ''; dateEndEl.disabled = true; } else dateEndEl.disabled = false;
            }
            updateShortcutsVisibility(container);
            updatePreview(container);
        }
        function updateShortcutsVisibility(container) {
            var periodicity = (periodicityEl || {}).value || 'monthly';
            var oneTime = periodicity === 'oneTime';
            container.querySelectorAll('.invoice-shortcuts-group').forEach(function(gr) {
                var match = gr.getAttribute('data-periodicity') === periodicity;
                gr.classList.toggle('d-none', !match || oneTime);
            });
        }
        if (autoProlongationEl) {
            autoProlongationEl.addEventListener('change', toggleDateEndBlock);
            toggleDateEndBlock();
        }
        if (periodicityEl) {
            periodicityEl.addEventListener('change', function() {
                toggleDateEndBlock();
            });
        }

        function getPeriodStart(d, periodicity, useCurrent) {
            if (periodicity === 'monthly') return new Date(d.getFullYear(), d.getMonth(), 1, 0, 0, 0);
            if (periodicity === 'weekly') {
                if (useCurrent) return new Date(d.getTime());
                var m = new Date(d);
                m.setDate(d.getDate() - (d.getDay() + 6) % 7);
                m.setHours(0, 0, 0, 0);
                return m;
            }
            if (periodicity === 'yearly') return useCurrent ? new Date(d.getTime()) : new Date(d.getFullYear(), 0, 1, 0, 0, 0);
            return new Date(d);
        }
        function getPeriodEnd(d, periodicity, useCurrent) {
            if (periodicity === 'monthly') return new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 0);
            if (periodicity === 'weekly') {
                if (useCurrent) {
                    var end = new Date(d);
                    end.setDate(end.getDate() + 7);
                    end.setHours(d.getHours(), d.getMinutes(), 0, 0);
                    return end;
                }
                var m = getPeriodStart(d, 'weekly', false);
                m.setDate(m.getDate() + 6);
                m.setHours(23, 59, 0, 0);
                return m;
            }
            if (periodicity === 'yearly') {
                if (useCurrent) return new Date(d.getFullYear() + 1, d.getMonth(), d.getDate(), d.getHours(), d.getMinutes(), 0);
                return new Date(d.getFullYear(), 11, 31, 23, 59, 0);
            }
            return new Date(d);
        }

        container.querySelectorAll('.invoice-date-end-shortcut').forEach(function(btn) {
            btn.addEventListener('click', function() {
                var startVal = (dateStartEl || {}).value;
                if (!startVal || !dateEndEl) return;
                var type = btn.getAttribute('data-type') || 'months';
                var val = parseInt(btn.getAttribute('data-value'), 10) || 1;
                var hasTime = startVal.indexOf('T') !== -1;
                var d = new Date(hasTime ? startVal : startVal + 'T12:00:00');
                var useCurrent = useCurrentDateTimeEl && useCurrentDateTimeEl.checked;
                var periodicity = (periodicityEl || {}).value || 'monthly';
                if (useCurrent) {
                    if (type === 'days') d.setDate(d.getDate() + val);
                    else if (type === 'weeks') d.setDate(d.getDate() + val * 7);
                    else if (type === 'months') d.setMonth(d.getMonth() + val);
                    else if (type === 'years') d.setFullYear(d.getFullYear() + val);
                } else {
                    var periodStart = getPeriodStart(d, periodicity);
                    if (type === 'months' && periodicity === 'monthly') {
                        d = new Date(periodStart.getFullYear(), periodStart.getMonth() + val, 0, 23, 59, 0);
                    } else if (type === 'weeks' && periodicity === 'weekly') {
                        var end = getPeriodStart(d, 'weekly');
                        end.setDate(end.getDate() + val * 7 - 1);
                        end.setHours(23, 59, 0, 0);
                        d = end;
                    } else if (type === 'years' && periodicity === 'yearly') {
                        d = new Date(periodStart.getFullYear() + val - 1, 11, 31, 23, 59, 0);
                    } else {
                        if (type === 'days') d.setDate(d.getDate() + val);
                        else if (type === 'weeks') d.setDate(d.getDate() + val * 7);
                        else if (type === 'months') d.setMonth(d.getMonth() + val);
                        else if (type === 'years') {
                            d.setFullYear(d.getFullYear() + val);
                            d.setMonth(11);
                            d.setDate(31);
                            d.setHours(23, 59, 0, 0);
                        }
                    }
                }
                dateEndEl.value = toDateTimeLocalString(d);
                updatePreview(container);
            });
        });
        if (dateEndEl) {
            dateEndEl.addEventListener('input', function() { updatePreview(container); });
            dateEndEl.addEventListener('change', function() { updatePreview(container); });
        }
        if (dateStartEl) {
            dateStartEl.addEventListener('input', function() { updatePreview(container); });
            dateStartEl.addEventListener('change', function() { updatePreview(container); });
        }

        if (step2Back && step1 && step2) {
            step2Back.addEventListener('click', function() {
                step2.classList.add('d-none');
                step1.classList.remove('d-none');
            });
        }

        function setPayCodeAndPreview(val) {
            if (payCodeEl) payCodeEl.value = val || '—';
            updatePreview(container);
        }
        if (showPayCodeSelect) {
            var payCodeSelect = container.querySelector('#invoicePayCodeSelect');
            var clientIdsParam = selectedClientIds.length ? '?clientIds=' + encodeURIComponent(selectedClientIds.join(',')) : '';
            fetch(baseUrl + '/GetInvoicePayCodeOptions' + clientIdsParam)
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!payCodeSelect) return;
                    payCodeSelect.innerHTML = '';
                    if (allowNewPayCode) {
                        var optNew = document.createElement('option');
                        optNew.value = '__new__';
                        optNew.textContent = 'Сгенерировать новый';
                        payCodeSelect.appendChild(optNew);
                    }
                    (data.payCodeOptions || []).forEach(function(o) {
                        var opt = document.createElement('option');
                        opt.value = o.payCode || '';
                        var label = (o.nameInvoice && o.nameInvoice.trim()) ? (o.nameInvoice.trim() + ' (' + (o.payCode || '') + ')') : (o.payCode || '—');
                        opt.textContent = label;
                        payCodeSelect.appendChild(opt);
                    });
                    payCodeSelect.addEventListener('change', function() {
                        var v = this.value;
                        if (v === '__new__' || v === '') {
                            fetch(baseUrl + '/GetNextInvoiceNumber')
                                .then(function(r) { return r.json(); })
                                .then(function(d) { setPayCodeAndPreview(d.payCode); })
                                .catch(function() { setPayCodeAndPreview('00001000000001'); });
                        } else {
                            setPayCodeAndPreview(v);
                        }
                    });
                    if (allowNewPayCode) {
                        payCodeSelect.value = '__new__';
                        setPayCodeAndPreview('');
                        fetch(baseUrl + '/GetNextInvoiceNumber')
                            .then(function(r) { return r.json(); })
                            .then(function(d) {
                                setPayCodeAndPreview(d.payCode);
                                payCodeSelect.value = '__new__';
                            })
                            .catch(function() {
                                setPayCodeAndPreview('00001000000001');
                                payCodeSelect.value = '__new__';
                            });
                    } else {
                        var opts = data.payCodeOptions || [];
                        if (opts.length > 0) {
                            payCodeSelect.value = opts[0].payCode || '';
                            setPayCodeAndPreview(opts[0].payCode || '');
                        } else {
                            setPayCodeAndPreview('—');
                        }
                    }
                })
                .catch(function() {
                    if (payCodeSelect) payCodeSelect.innerHTML = allowNewPayCode ? '<option value="__new__">Сгенерировать новый</option>' : '<option value="">Нет доступных лицевых счетов</option>';
                    if (allowNewPayCode) {
                        fetch(baseUrl + '/GetNextInvoiceNumber')
                            .then(function(r) { return r.json(); })
                            .then(function(d) { setPayCodeAndPreview(d.payCode); })
                            .catch(function() { setPayCodeAndPreview('00001000000001'); });
                    } else if (payCodeEl) payCodeEl.value = '—';
                });
        } else {
            fetch(baseUrl + '/GetNextInvoiceNumber')
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (payCodeEl && data.payCode) payCodeEl.value = data.payCode;
                    updatePreview(container);
                })
                .catch(function() {
                    if (payCodeEl) payCodeEl.value = '00001000000001';
                    updatePreview(container);
                });
        }

        function bindServiceRows() {
            if (!servicesBody) return;
            var rows = servicesBody.querySelectorAll('.invoice-service-row');
            rows.forEach(function(tr) {
                var check = tr.querySelector('.service-include');
                var qtyInput = tr.querySelector('.service-qty');
                function upd() { updatePreview(container); }
                if (check) check.addEventListener('change', upd);
                if (qtyInput) { qtyInput.addEventListener('input', upd); qtyInput.addEventListener('change', upd); }
            });
        }
        bindServiceRows();
        if (disableServiceSelection) {
            var manualPriceEl = container.querySelector('#invoiceManualPriceSom');
            if (manualPriceEl) {
                manualPriceEl.addEventListener('input', function() { updatePreview(container); });
                manualPriceEl.addEventListener('change', function() { updatePreview(container); });
            }
        }

        var servicesSearch = container.querySelector('#invoiceServicesSearch');
        if (servicesSearch) {
            servicesSearch.addEventListener('input', function() {
                var term = (this.value || '').toLowerCase().trim();
                (servicesBody.querySelectorAll('.invoice-service-row') || []).forEach(function(tr) {
                    var name = (tr.getAttribute('data-name') || '').toLowerCase();
                    tr.style.display = !term || name.indexOf(term) >= 0 ? '' : 'none';
                });
            });
        }

        [nameEl, dateStartEl, periodicityEl].forEach(function(el) {
            if (el) { el.addEventListener('input', function() { updatePreview(container); }); el.addEventListener('change', function() { updatePreview(container); }); }
        });
        if (autoProlongationEl) {
            autoProlongationEl.addEventListener('input', function() { updatePreview(container); });
            autoProlongationEl.addEventListener('change', function() { updatePreview(container); });
        }
        if (useCurrentDateTimeEl) {
            useCurrentDateTimeEl.addEventListener('change', function() { updatePreview(container); });
        }

        if (qrBtn && qrContainer) {
            qrBtn.addEventListener('click', function() {
                var code = (container.querySelector('#invoicePayCode') || {}).value || 'TEST';
                qrContainer.innerHTML = '<img src="https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=' + encodeURIComponent(code) + '" alt="QR" />';
            });
        }

        if (createBtn) {
            createBtn.addEventListener('click', function() {
                var payload = {
                    nameInvoice: (nameEl || {}).value || '',
                    dateStartInvoice: (dateStartEl || {}).value || null,
                    dateEndInvoice: null,
                    periodicity: (periodicityEl || {}).value || 'monthly',
                    autoProlongation: autoProlongationEl ? autoProlongationEl.checked : true,
                    useCurrentDateTime: useCurrentDateTimeEl ? useCurrentDateTimeEl.checked : true,
                    clientIds: selectedClientIds,
                    serviceItems: []
                };
                var autoProlong = payload.autoProlongation;
                var dateEndVal = (!autoProlong && dateEndEl && dateEndEl.value) ? dateEndEl.value : null;
                payload.dateEndInvoice = dateEndVal;
                if (!autoProlong && !dateEndVal) {
                    alert('Укажите дату конца счёта или включите автопролонгацию.');
                    return;
                }
                if (disableServiceSelection) {
                    var manualPriceEl = container.querySelector('#invoiceManualPriceSom');
                    var manualPrice = manualPriceEl ? parseFloat(manualPriceEl.value) : NaN;
                    if (isNaN(manualPrice) || manualPrice < 0) {
                        alert('Укажите цену (режим «услуга по счёту»).');
                        return;
                    }
                    payload.manualServicePriceSom = manualPrice;
                } else {
                    (container.querySelectorAll('#invoiceServicesBody .invoice-service-row') || []).forEach(function(tr) {
                        var check = tr.querySelector('.service-include');
                        if (!check || !check.checked) return;
                        var id = tr.getAttribute('data-service-id');
                        var qty = parseInt((tr.querySelector('.service-qty') || {}).value, 10) || 1;
                        if (id) payload.serviceItems.push({ serviceId: id, qty: qty });
                    });
                    if (payload.serviceItems.length === 0) {
                        alert('Выберите хотя бы одну услугу.');
                        return;
                    }
                }
                if (showPayCodeSelect) {
                    var payCodeSelect = container.querySelector('#invoicePayCodeSelect');
                    var selectedPayCode = payCodeSelect && payCodeSelect.value && payCodeSelect.value !== '__new__' && payCodeSelect.value !== '' ? payCodeSelect.value : null;
                    if (payCodeMode === 'duplicate_only' && !selectedPayCode) {
                        alert('Выберите лицевой счёт из списка.');
                        return;
                    }
                    if (selectedPayCode) {
                        payload.payCode = selectedPayCode;
                        payload.hassameaccount = true;
                    }
                }
                createBtn.disabled = true;
                createBtn.textContent = 'Создание...';
                var url = baseUrl + '/CreateInvoices';
                fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
                    body: JSON.stringify(payload)
                })
                    .then(function(r) {
                        return r.text().then(function(text) {
                            try {
                                var data = text ? JSON.parse(text) : {};
                                return { ok: r.ok, status: r.status, data: data };
                            } catch (e) {
                                return { ok: false, status: r.status, data: { message: r.status === 0 ? 'Ошибка сети. Проверьте подключение.' : 'Ответ сервера: ' + r.status } };
                            }
                        });
                    })
                    .then(function(result) {
                        if (result.ok && result.data.success) {
                            alert(result.data.message || 'Счета созданы.');
                            bootstrap.Modal.getInstance(container.closest('.modal')).hide();
                            if (typeof window.location.reload === 'function') window.location.reload();
                        } else {
                            alert(result.data.message || 'Ошибка ' + result.status + '.');
                        }
                    })
                    .catch(function(err) {
                        console.error('CreateInvoices error', err);
                        alert('Ошибка сети или сервера. Проверьте консоль браузера (F12).');
                    })
                    .finally(function() {
                        createBtn.disabled = false;
                        createBtn.textContent = 'Создать счёт на оплату';
                    });
            });
        }

        updatePreview(container);
    }

    function updatePreview(container) {
        function getPeriodStartDate(d, periodicity, useCurrent) {
            if (periodicity === 'monthly') return new Date(d.getFullYear(), d.getMonth(), 1, 0, 0, 0);
            if (periodicity === 'daily') return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0);
            if (periodicity === 'weekly') {
                if (useCurrent) return new Date(d.getTime());
                var m = new Date(d);
                m.setDate(d.getDate() - (d.getDay() + 6) % 7);
                m.setHours(0, 0, 0, 0);
                return m;
            }
            if (periodicity === 'yearly') return useCurrent ? new Date(d.getTime()) : new Date(d.getFullYear(), 0, 1, 0, 0, 0);
            return new Date(d);
        }
        function getPeriodEndDate(d, periodicity, useCurrent) {
            if (periodicity === 'monthly') return new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 0);
            if (periodicity === 'daily') return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 0);
            if (periodicity === 'weekly') {
                if (useCurrent) {
                    var end = new Date(d);
                    end.setDate(end.getDate() + 7);
                    end.setHours(d.getHours(), d.getMinutes(), 0, 0);
                    return end;
                }
                var m = getPeriodStartDate(d, 'weekly', false);
                m.setDate(m.getDate() + 6);
                m.setHours(23, 59, 0, 0);
                return m;
            }
            if (periodicity === 'yearly') {
                if (useCurrent) return new Date(d.getFullYear() + 1, d.getMonth(), d.getDate(), d.getHours(), d.getMinutes(), 0);
                return new Date(d.getFullYear(), 11, 31, 23, 59, 0);
            }
            return new Date(d);
        }

        var payCode = (container.querySelector('#invoicePayCode') || {}).value || '—';
        var name = (container.querySelector('#invoiceName') || {}).value || '—';
        var dateStart = (container.querySelector('#invoiceDateStart') || {}).value || '—';
        var periodicityEl = container.querySelector('#invoicePeriodicity');
        var periodicityLabels = { daily: 'Ежедневно', weekly: 'Еженедельно', monthly: 'Ежемесячно', yearly: 'Ежегодно', oneTime: 'Разовая', any: 'Любая' };
        var periodicity = periodicityEl ? (periodicityLabels[periodicityEl.value] || periodicityEl.value || '—') : '—';

        var previewNumber = container.querySelector('#previewInvoiceNumber');
        var previewName = container.querySelector('#previewName');
        var previewDateStart = container.querySelector('#previewDateStart');
        var previewPeriodicity = container.querySelector('#previewPeriodicity');
        var previewLinesBody = container.querySelector('#previewLinesBody');
        var previewTotal = container.querySelector('#previewTotal');
        var previewExecutor = container.querySelector('#previewExecutor');

        var autoProlongationEl = container.querySelector('#invoiceAutoProlongation');
        var dateEndVal = (container.querySelector('#invoiceDateEnd') || {}).value || '';
        var periodicityValForPreview = (container.querySelector('#invoicePeriodicity') || {}).value || 'monthly';
        var showDateEnd = periodicityValForPreview !== 'oneTime' && autoProlongationEl && !autoProlongationEl.checked && dateEndVal;
        var previewDateEndRow = container.querySelector('#previewDateEndRow');
        var previewDateEnd = container.querySelector('#previewDateEnd');
        if (previewDateEndRow) previewDateEndRow.classList.toggle('d-none', !showDateEnd);
        if (previewDateEnd) previewDateEnd.textContent = showDateEnd ? formatDateTimeForPreview(dateEndVal) : '—';

        if (previewNumber) previewNumber.textContent = payCode;
        if (previewName) previewName.textContent = name;
        if (previewDateStart) previewDateStart.textContent = formatDateTimeForPreview(dateStart);
        if (previewPeriodicity) previewPeriodicity.textContent = periodicity;
        if (previewExecutor) previewExecutor.textContent = 'Пользователь';

        var wrap = container.querySelector('.create-invoice-wrap') || container;
        var disableServiceSelection = wrap.getAttribute('data-disable-invoice-service-selection') === 'true';
        var total = 0;
        var html = '';
        if (disableServiceSelection) {
            var manualName = (container.querySelector('#invoiceName') || {}).value || '—';
            var manualPrice = parseFloat((container.querySelector('#invoiceManualPriceSom') || {}).value) || 0;
            total = manualPrice;
            if (manualName !== '—' || manualPrice > 0) {
                html = '<tr><td>' + escapeHtml(manualName) + '</td><td>' + manualPrice.toFixed(2) + '</td><td>1</td><td>' + manualPrice.toFixed(2) + '</td></tr>';
            }
        } else {
            (container.querySelectorAll('#invoiceServicesBody .invoice-service-row') || []).forEach(function(tr) {
                var check = tr.querySelector('.service-include');
                if (!check || !check.checked) return;
                var nameVal = tr.getAttribute('data-name') || '—';
                var price = parseFloat(tr.getAttribute('data-price')) || 0;
                var qty = parseInt((tr.querySelector('.service-qty') || {}).value, 10) || 1;
                var lineTotal = price * qty;
                total += lineTotal;
                html += '<tr><td>' + escapeHtml(nameVal) + '</td><td>' + price.toFixed(2) + '</td><td>' + qty + '</td><td>' + lineTotal.toFixed(2) + '</td></tr>';
            });
        }
        if (previewLinesBody) previewLinesBody.innerHTML = html || '<tr><td colspan="4" class="text-muted text-center">Нет позиций</td></tr>';
        if (previewTotal) previewTotal.textContent = total.toFixed(2);

        var previewPaymentsBody = container.querySelector('#previewPaymentsBody');
        if (previewPaymentsBody) {
            var dateStartVal = (container.querySelector('#invoiceDateStart') || {}).value;
            var dateEndValPay = (container.querySelector('#invoiceDateEnd') || {}).value;
            var autoOn = autoProlongationEl && autoProlongationEl.checked;
            var periodicityVal = (container.querySelector('#invoicePeriodicity') || {}).value || 'monthly';
            var useCurrent = (container.querySelector('#invoiceUseCurrentDateTime') || {}).checked !== false;
            var paymentsHtml = '';
            if (!dateStartVal || total <= 0) {
                paymentsHtml = '<tr><td colspan="4" class="text-muted text-center small">Укажите даты и услуги</td></tr>';
            } else if (periodicityVal === 'oneTime') {
                var d0 = parseDateVal(dateStartVal);
                if (!d0 || isNaN(d0.getTime())) {
                    paymentsHtml = '<tr><td colspan="4" class="text-muted text-center small">Неверная дата</td></tr>';
                } else {
                    paymentsHtml = '<tr><td>1</td><td>' + fmtDateTime(d0) + '</td><td>' + fmtDateTime(d0) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                }
            } else if (autoOn) {
                var d0 = parseDateVal(dateStartVal);
                if (!d0 || isNaN(d0.getTime())) {
                    paymentsHtml = '<tr><td colspan="4" class="text-muted text-center small">Неверная дата</td></tr>';
                } else if (useCurrent) {
                    if (periodicityVal === 'daily') {
                        var from = new Date(d0);
                        var to = new Date(d0);
                        to.setDate(to.getDate() + 1);
                        paymentsHtml = '<tr><td>1</td><td>' + fmtDateTime(from) + '</td><td>' + fmtDateTime(to) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                    } else if (periodicityVal === 'weekly') {
                        var from = new Date(d0);
                        var to = new Date(d0);
                        to.setDate(to.getDate() + 7);
                        to.setHours(d0.getHours(), d0.getMinutes(), 0, 0);
                        paymentsHtml = '<tr><td>1</td><td>' + fmtDateTime(from) + '</td><td>' + fmtDateTime(to) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                    } else if (periodicityVal === 'yearly') {
                        var from = new Date(d0);
                        var to = new Date(d0.getFullYear() + 1, d0.getMonth(), d0.getDate(), d0.getHours(), d0.getMinutes(), 0);
                        paymentsHtml = '<tr><td>1</td><td>' + fmtDateTime(from) + '</td><td>' + fmtDateTime(to) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                    } else {
                        var from = new Date(d0);
                        var to = new Date(d0.getFullYear(), d0.getMonth() + 1, d0.getDate(), d0.getHours(), d0.getMinutes(), 0);
                        paymentsHtml = '<tr><td>1</td><td>' + fmtDateTime(from) + '</td><td>' + fmtDateTime(to) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                    }
                } else {
                    var from = getPeriodStartDate(d0, periodicityVal, false);
                    var to = getPeriodEndDate(d0, periodicityVal, false);
                    paymentsHtml = '<tr><td>1</td><td>' + fmtDateTime(from) + '</td><td>' + fmtDateTime(to) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                }
            } else if (dateEndValPay) {
                var start = parseDateVal(dateStartVal);
                var end = parseDateVal(dateEndValPay);
                if (start && end && !isNaN(start.getTime()) && !isNaN(end.getTime()) && start <= end) {
                    if (useCurrent) {
                        if (periodicityVal === 'daily') {
                            var dayStart = new Date(start.getFullYear(), start.getMonth(), start.getDate(), start.getHours(), start.getMinutes(), 0);
                            var num = 0;
                            while (dayStart < end) {
                                num++;
                                var dayEnd = new Date(dayStart);
                                dayEnd.setDate(dayEnd.getDate() + 1);
                                if (dayEnd > end) dayEnd = new Date(end);
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(dayStart) + '</td><td>' + fmtDateTime(dayEnd) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                                dayStart.setDate(dayStart.getDate() + 1);
                            }
                        } else if (periodicityVal === 'weekly') {
                            var num = 0;
                            var periodStart = new Date(start);
                            while (periodStart < end) {
                                num++;
                                var periodEnd = new Date(periodStart);
                                periodEnd.setDate(periodEnd.getDate() + 7);
                                periodEnd.setHours(periodStart.getHours(), periodStart.getMinutes(), 0, 0);
                                if (periodEnd > end) periodEnd = new Date(end);
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(periodStart) + '</td><td>' + fmtDateTime(periodEnd) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                                periodStart.setDate(periodStart.getDate() + 7);
                            }
                        } else if (periodicityVal === 'yearly') {
                            var num = 0;
                            var periodStart = new Date(start);
                            while (periodStart < end) {
                                num++;
                                var periodTo = new Date(periodStart.getFullYear() + 1, periodStart.getMonth(), periodStart.getDate(), periodStart.getHours(), periodStart.getMinutes(), 0);
                                if (periodTo > end) periodTo = new Date(end);
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(periodStart) + '</td><td>' + fmtDateTime(periodTo) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                                periodStart.setFullYear(periodStart.getFullYear() + 1);
                            }
                        } else {
                            var num = 0;
                            var periodStart = new Date(start);
                            while (periodStart < end) {
                                num++;
                                var periodEnd = new Date(periodStart.getFullYear(), periodStart.getMonth() + 1, periodStart.getDate(), periodStart.getHours(), periodStart.getMinutes(), 0);
                                if (periodEnd > end) periodEnd = new Date(end);
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(periodStart) + '</td><td>' + fmtDateTime(periodEnd) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                                periodStart = new Date(periodStart.getFullYear(), periodStart.getMonth() + 1, periodStart.getDate(), periodStart.getHours(), periodStart.getMinutes(), 0);
                            }
                        }
                    } else {
                        var periodStart = getPeriodStartDate(start, periodicityVal, false);
                        var periodEnd = getPeriodEndDate(start, periodicityVal, false);
                        if (periodicityVal === 'daily') {
                            var num = 0;
                            var dayStart = new Date(periodStart.getFullYear(), periodStart.getMonth(), periodStart.getDate(), 0, 0, 0);
                            var dayEnd = new Date(periodStart.getFullYear(), periodStart.getMonth(), periodStart.getDate(), 23, 59, 0);
                            while (dayStart <= end) {
                                num++;
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(dayStart) + '</td><td>' + fmtDateTime(dayEnd) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                                dayStart.setDate(dayStart.getDate() + 1);
                                dayEnd.setDate(dayEnd.getDate() + 1);
                            }
                        } else if (periodicityVal === 'weekly') {
                            var num = 0;
                            while (periodStart <= end) {
                                num++;
                                var toEdge = new Date(periodEnd);
                                if (toEdge > end) toEdge = new Date(end);
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(periodStart) + '</td><td>' + fmtDateTime(toEdge) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                                periodStart.setDate(periodStart.getDate() + 7);
                                periodEnd.setDate(periodEnd.getDate() + 7);
                            }
                        } else if (periodicityVal === 'yearly') {
                            var startYear = periodStart.getFullYear();
                            var endYear = end.getFullYear();
                            var num = 0;
                            for (var y = startYear; y <= endYear; y++) {
                                num++;
                                var periodFrom = new Date(y, 0, 1, 0, 0, 0);
                                var periodTo = new Date(y, 11, 31, 23, 59, 0);
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(periodFrom) + '</td><td>' + fmtDateTime(periodTo) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                            }
                        } else {
                            var m = new Date(periodStart.getFullYear(), periodStart.getMonth(), 1);
                            var endFirst = new Date(end.getFullYear(), end.getMonth(), 1);
                            var num = 0;
                            while (m <= endFirst) {
                                num++;
                                var y = m.getFullYear(), mn = m.getMonth();
                                var periodFrom = new Date(y, mn, 1, 0, 0, 0);
                                var periodTo = new Date(y, mn + 1, 0, 23, 59, 0);
                                paymentsHtml += '<tr><td>' + num + '</td><td>' + fmtDateTime(periodFrom) + '</td><td>' + fmtDateTime(periodTo) + '</td><td>' + total.toFixed(2) + ' сом</td></tr>';
                                m.setMonth(m.getMonth() + 1);
                            }
                        }
                    }
                }
                if (!paymentsHtml) paymentsHtml = '<tr><td colspan="4" class="text-muted text-center small">Нет периодов</td></tr>';
            } else {
                paymentsHtml = '<tr><td colspan="4" class="text-muted text-center small">Укажите дату конца или включите автопролонгацию</td></tr>';
            }
            previewPaymentsBody.innerHTML = paymentsHtml;
        }
    }

    function escapeHtml(s) {
        var div = document.createElement('div');
        div.textContent = s;
        return div.innerHTML;
    }
})();
