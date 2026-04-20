(function() {
    'use strict';

    var invoiceBaseUrl = '/Invoices';
    var oneTimeModalState = {
        formHtml: '',
        modalBody: null
    };

    document.addEventListener('DOMContentLoaded', function() {
        initInvoices();
    });

    function initInvoices() {
        initTableInteractions();
        initRowAnimations();
    }

    function initTableInteractions() {
        var tableRows = document.querySelectorAll('.invoice-row');

        tableRows.forEach(function(row, index) {
            row.style.opacity = '0';
            row.style.transform = 'translateX(-20px)';

            setTimeout(function() {
                row.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
                row.style.opacity = '1';
                row.style.transform = 'translateX(0)';
            }, index * 50);

            row.addEventListener('click', function() {
                this.classList.toggle('row-selected');
            });
        });
    }

    function initRowAnimations() {
        var table = document.querySelector('.invoices-table');
        if (!table) return;

        table.addEventListener('mouseover', function(e) {
            var row = e.target.closest('.invoice-row');
            if (row) row.style.transform = 'scale(1.01)';
        });

        table.addEventListener('mouseout', function(e) {
            var row = e.target.closest('.invoice-row');
            if (row) row.style.transform = 'scale(1)';
        });
    }

    function toDateTimeLocalString(date) {
        var year = date.getFullYear();
        var month = ('0' + (date.getMonth() + 1)).slice(-2);
        var day = ('0' + date.getDate()).slice(-2);
        var hours = ('0' + date.getHours()).slice(-2);
        var minutes = ('0' + date.getMinutes()).slice(-2);
        return year + '-' + month + '-' + day + 'T' + hours + ':' + minutes;
    }

    function formatDateTime(value) {
        if (!value) return '—';
        var date = new Date(value.indexOf('T') >= 0 ? value : value + 'T00:00:00');
        if (isNaN(date.getTime())) return value;
        return date.toLocaleString('ru-RU', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text == null ? '' : String(text);
        return div.innerHTML;
    }

    function renderModalError(body, message) {
        body.innerHTML = '<div class="alert alert-danger m-3">' + escapeHtml(message) + '</div>';
    }

    function submitModalForm(form, body, onSuccess) {
        var submitButton = form.querySelector('button[type="submit"]');
        if (submitButton) submitButton.disabled = true;

        fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
            .then(function(response) {
                return response.text().then(function(text) {
                    return {
                        ok: response.ok,
                        status: response.status,
                        text: text
                    };
                });
            })
            .then(function(result) {
                if (!result.ok) {
                    throw new Error(result.text || 'Не удалось выполнить запрос.');
                }

                onSuccess(result.text);
            })
            .catch(function(error) {
                renderModalError(body, error.message || 'Не удалось выполнить запрос.');
            })
            .finally(function() {
                if (submitButton) submitButton.disabled = false;
            });
    }

    function bindOneTimePaymentStep(container) {
        var confirmForm = container.querySelector('#oneTimePaymentConfirmForm');
        if (confirmForm) {
            confirmForm.addEventListener('submit', function(event) {
                event.preventDefault();
                submitModalForm(confirmForm, container, function(html) {
                    container.innerHTML = html;
                    bindOneTimePaymentStep(container);
                });
            });
        }

        container.querySelectorAll('[data-one-time-back="true"]').forEach(function(button) {
            button.addEventListener('click', function() {
                if (!oneTimeModalState.formHtml || !oneTimeModalState.modalBody) return;
                oneTimeModalState.modalBody.innerHTML = oneTimeModalState.formHtml;
                initStandartOneTimePaymentForm(oneTimeModalState.modalBody);
            });
        });
    }

    window.openCreateOneTimePaymentModal = function() {
        var modal = document.getElementById('createOneTimePaymentModal');
        var body = document.getElementById('createOneTimePaymentModalBody');
        if (!modal || !body) return;

        oneTimeModalState.modalBody = body;
        oneTimeModalState.formHtml = '';

        body.innerHTML = '<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div></div>';
        var modalInstance = new bootstrap.Modal(modal);
        modalInstance.show();

        fetch(invoiceBaseUrl + '/CreateOneTimePaymentPartial', {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
            .then(function(response) { return response.text(); })
            .then(function(html) {
                body.innerHTML = html;
                oneTimeModalState.formHtml = html;
                initStandartOneTimePaymentForm(body);
            })
            .catch(function() {
                renderModalError(body, 'Не удалось загрузить форму одноразового платежа.');
            });
    };

    function initStandartOneTimePaymentForm(container) {
        var form = container.querySelector('#oneTimePaymentForm');
        if (!form) return;

        var paymentAtInput = form.querySelector('#oneTimePaymentAt');
        var clientSelect = form.querySelector('#oneTimeClientId');
        var nameInput = form.querySelector('#oneTimeInvoiceName');
        var payCodeInput = form.querySelector('#oneTimePayCode');
        var payCodeSelect = form.querySelector('#oneTimePayCodeSelect');
        var manualAmountInput = form.querySelector('#oneTimeManualAmountSom');
        var servicesSearchInput = form.querySelector('#oneTimeServicesSearch');
        var previewClient = form.querySelector('#oneTimePreviewClient');
        var previewName = form.querySelector('#oneTimePreviewName');
        var previewPayCode = form.querySelector('#oneTimePreviewPayCode');
        var previewDate = form.querySelector('#oneTimePreviewDate');
        var previewLines = form.querySelector('#oneTimePreviewLines');
        var previewTotal = form.querySelector('#oneTimePreviewTotal');
        var hiddenItemsHost = document.createElement('div');
        hiddenItemsHost.className = 'd-none';
        form.appendChild(hiddenItemsHost);

        if (paymentAtInput && !paymentAtInput.value) {
            paymentAtInput.value = toDateTimeLocalString(new Date());
        }

        function setPayCode(value) {
            if (payCodeInput) payCodeInput.value = value || '';
            updatePreview();
        }

        function loadPayCodeOptions() {
            if (!payCodeSelect) {
                fetch(invoiceBaseUrl + '/GetNextInvoiceNumber')
                    .then(function(response) { return response.json(); })
                    .then(function(data) { setPayCode(data.payCode || ''); })
                    .catch(function() { setPayCode(''); });
                return;
            }

            var allowNew = payCodeSelect.getAttribute('data-allow-new') === 'true';
            fetch(invoiceBaseUrl + '/GetOrganizationHassamePayCodeOptions')
                .then(function(response) { return response.json(); })
                .then(function(data) {
                    payCodeSelect.innerHTML = '';
                    if (allowNew) {
                        var optNew = document.createElement('option');
                        optNew.value = '__new__';
                        optNew.textContent = 'Сгенерировать новый';
                        payCodeSelect.appendChild(optNew);
                    }

                    (data.payCodeOptions || []).forEach(function(option) {
                        var el = document.createElement('option');
                        el.value = option.payCode || '';
                        el.textContent = option.nameInvoice
                            ? option.nameInvoice + ' (' + (option.payCode || '') + ')'
                            : (option.payCode || '');
                        payCodeSelect.appendChild(el);
                    });

                    if (allowNew) {
                        payCodeSelect.value = '__new__';
                        fetch(invoiceBaseUrl + '/GetNextInvoiceNumber')
                            .then(function(response) { return response.json(); })
                            .then(function(data) { setPayCode(data.payCode || ''); });
                    } else if (payCodeSelect.options.length > 0) {
                        setPayCode(payCodeSelect.value);
                    } else {
                        setPayCode('');
                    }
                })
                .catch(function() { setPayCode(''); });
        }

        function getSelectedServiceRows() {
            return Array.prototype.slice.call(form.querySelectorAll('.one-time-service-row'))
                .filter(function(row) {
                    var checkbox = row.querySelector('.one-time-service-check');
                    return checkbox && checkbox.checked;
                });
        }

        function rebuildHiddenServiceInputs() {
            hiddenItemsHost.innerHTML = '';
            var selectedRows = getSelectedServiceRows();
            selectedRows.forEach(function(row, index) {
                var serviceId = row.getAttribute('data-service-id') || '';
                var qtyInput = row.querySelector('.one-time-service-qty');
                var qty = qtyInput ? (qtyInput.value || '1') : '1';

                var serviceField = document.createElement('input');
                serviceField.type = 'hidden';
                serviceField.name = 'ServiceItems[' + index + '].ServiceId';
                serviceField.value = serviceId;
                hiddenItemsHost.appendChild(serviceField);

                var qtyField = document.createElement('input');
                qtyField.type = 'hidden';
                qtyField.name = 'ServiceItems[' + index + '].Qty';
                qtyField.value = qty;
                hiddenItemsHost.appendChild(qtyField);
            });
        }

        function updatePreview() {
            var selectedOption = clientSelect && clientSelect.options.length > 0
                ? clientSelect.options[clientSelect.selectedIndex]
                : null;
            var selectedRows = getSelectedServiceRows();
            var total = 0;
            var linesHtml = [];

            previewClient.textContent = selectedOption && selectedOption.value ? selectedOption.textContent : '—';
            previewName.textContent = (nameInput && nameInput.value) ? nameInput.value : 'Разовый платёж';
            previewPayCode.textContent = (payCodeInput && payCodeInput.value) ? payCodeInput.value : '—';
            previewDate.textContent = formatDateTime(paymentAtInput ? paymentAtInput.value : '');

            selectedRows.forEach(function(row) {
                var qtyInput = row.querySelector('.one-time-service-qty');
                var qty = Math.max(1, parseInt(qtyInput ? qtyInput.value : '1', 10) || 1);
                var price = parseFloat(row.getAttribute('data-price') || '0') || 0;
                var name = row.getAttribute('data-name') || 'Услуга';
                var lineTotal = price * qty;
                total += lineTotal;
                linesHtml.push(
                    '<div class="d-flex justify-content-between gap-2 py-1 border-bottom">' +
                    '<span>' + escapeHtml(name) + ' × ' + qty + '</span>' +
                    '<strong>' + lineTotal.toFixed(2) + ' сом</strong>' +
                    '</div>'
                );
            });

            if (selectedRows.length === 0) {
                var manualAmount = parseFloat(manualAmountInput && manualAmountInput.value ? manualAmountInput.value : '0') || 0;
                total = manualAmount;
                if (manualAmount > 0) {
                    linesHtml.push(
                        '<div class="d-flex justify-content-between gap-2 py-1 border-bottom">' +
                        '<span>' + escapeHtml(previewName.textContent) + '</span>' +
                        '<strong>' + manualAmount.toFixed(2) + ' сом</strong>' +
                        '</div>'
                    );
                }
            }

            previewLines.innerHTML = linesHtml.length > 0 ? linesHtml.join('') : 'Нет позиций';
            previewTotal.textContent = total.toFixed(2) + ' сом';
            rebuildHiddenServiceInputs();
        }

        if (payCodeSelect) {
            payCodeSelect.addEventListener('change', function() {
                if (this.value === '__new__' || this.value === '') {
                    fetch(invoiceBaseUrl + '/GetNextInvoiceNumber')
                        .then(function(response) { return response.json(); })
                        .then(function(data) { setPayCode(data.payCode || ''); })
                        .catch(function() { setPayCode(''); });
                } else {
                    setPayCode(this.value);
                }
            });
        }

        if (servicesSearchInput) {
            servicesSearchInput.addEventListener('input', function() {
                var term = (this.value || '').trim().toLowerCase();
                form.querySelectorAll('.one-time-service-row').forEach(function(row) {
                    var name = (row.getAttribute('data-name') || '').toLowerCase();
                    row.style.display = !term || name.indexOf(term) >= 0 ? '' : 'none';
                });
            });
        }

        form.querySelectorAll('.one-time-service-check').forEach(function(checkbox) {
            checkbox.addEventListener('change', function() {
                var row = checkbox.closest('.one-time-service-row');
                var qtyInput = row ? row.querySelector('.one-time-service-qty') : null;
                if (qtyInput) qtyInput.disabled = !checkbox.checked;
                updatePreview();
            });
        });

        form.querySelectorAll('.one-time-service-qty').forEach(function(input) {
            input.addEventListener('input', updatePreview);
        });

        if (clientSelect) clientSelect.addEventListener('change', updatePreview);
        if (nameInput) nameInput.addEventListener('input', updatePreview);
        if (payCodeInput) payCodeInput.addEventListener('input', updatePreview);
        if (paymentAtInput) paymentAtInput.addEventListener('change', updatePreview);
        if (manualAmountInput) manualAmountInput.addEventListener('input', updatePreview);

        form.addEventListener('submit', function(event) {
            event.preventDefault();
            rebuildHiddenServiceInputs();
            oneTimeModalState.formHtml = container.innerHTML;

            submitModalForm(form, container, function(html) {
                container.innerHTML = html;
                bindOneTimePaymentStep(container);
            });
        });

        loadPayCodeOptions();
        updatePreview();
    }
})();
