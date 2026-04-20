(function () {
    "use strict";

    var I18N = window.standartClientsI18n || {};
    var LOCALE = window.standartClientsLocale || "ru-RU";

    function t(key, fallback) {
        return I18N[key] || fallback || key;
    }

    function esc(text) {
        var div = document.createElement("div");
        div.textContent = text == null ? "" : String(text);
        return div.innerHTML;
    }

    function formatSom(value) {
        var n = Number(value || 0);
        return n.toLocaleString(LOCALE, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " " + t("currency", "som");
    }

    function formatDate(value, withTime) {
        if (!value) return t("dash", "-");
        var d = new Date(value);
        if (isNaN(d.getTime())) return t("dash", "-");
        return withTime
            ? d.toLocaleString(LOCALE, { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" })
            : d.toLocaleDateString(LOCALE, { day: "2-digit", month: "2-digit", year: "numeric" });
    }

    function statusClass(value) {
        var v = String(value || "").toLowerCase();
        if (v.indexOf("closed") >= 0 || v.indexOf("anulated") >= 0 || v.indexOf("error") >= 0 || v.indexOf("disabled") >= 0) return "sc-badge--danger";
        if (v.indexOf("suspended") >= 0 || v.indexOf("non_paid") >= 0) return "sc-badge--warning";
        if (v.indexOf("actual") >= 0 || v.indexOf("paid") >= 0 || v.indexOf("success") >= 0 || v.indexOf("active") >= 0) return "sc-badge--paid";
        return "sc-badge--neutral";
    }

    function periodicityLabel(value) {
        if (value === "daily") return t("periodDaily", "Daily");
        if (value === "weekly") return t("periodWeekly", "Weekly");
        if (value === "monthly") return t("periodMonthly", "Monthly");
        if (value === "yearly") return t("periodYearly", "Yearly");
        if (value === "oneTime") return t("periodOneTime", "One-time");
        if (value === "any") return t("periodAny", "Any time");
        var parsed = Number(value);
        if (!isNaN(parsed) && parsed > 0) return t("periodEveryNDays", "Every {0} days").replace("{0}", parsed);
        return value || t("dash", "-");
    }

    function paymentStatusLabel(value) {
        if (value === "paid") return t("paymentPaid", "Paid");
        if (value === "non_paid") return t("paymentNonPaid", "Not paid");
        if (value === "anulated") return t("paymentAnulated", "Canceled");
        return value || t("dash", "-");
    }

    function periodicityClass(value) {
        return String(value || "") === "oneTime" ? "sc-badge--one-time" : "sc-badge--periodic";
    }

    function getLatestPaymentDate(invoice) {
        var txDate = (invoice.transactions || [])
            .filter(function (tx) { return String(tx.transactionType || "") === "payPaymentInvoice" && tx.transactionDate; })
            .map(function (tx) { return tx.transactionDate; })
            .sort()
            .reverse()[0];
        return txDate || null;
    }

    function getStatusPresentation(invoice, isCompact) {
        var payments = invoice.payments || [];
        var nonPaid = payments.find(function (p) {
            return String(p.paymentStatus || "").toLowerCase() === "non_paid";
        });

        if (nonPaid) {
            return {
                badgeClass: statusClass("non_paid"),
                main: t("paymentNonPaid", "Not paid"),
                sub: nonPaid.periodValue || t("dash", "-")
            };
        }

        var invoiceStatus = String(invoice.invoiceStatus || "");
        var isActual = invoiceStatus.toLowerCase() === "actual";
        var latestPaymentDate = getLatestPaymentDate(invoice);

        if (isActual && isCompact) {
            return {
                badgeClass: statusClass("paid"),
                main: t("paymentPaid", "Paid"),
                sub: latestPaymentDate ? formatDate(latestPaymentDate, false) : t("dash", "-")
            };
        }

        if (isActual && !isCompact) {
            return {
                badgeClass: statusClass("paid"),
                main: t("lastPayment", "Last payment"),
                sub: latestPaymentDate ? formatDate(latestPaymentDate, false) : t("dash", "-")
            };
        }

        return {
            badgeClass: statusClass(invoiceStatus),
            main: invoiceStatus || t("dash", "-"),
            sub: ""
        };
    }

    function renderSummary(client) {
        return [
            { label: t("summaryTotalInvoices", "Total invoices"), value: client.invoicesCount || 0, note: t("summaryTotalInvoicesNote", "All client invoices") },
            { label: t("summaryActiveInvoices", "Active invoices"), value: client.activeInvoices || 0, note: t("summaryActiveInvoicesNote", "With status actual") },
            { label: t("summaryDebtInvoices", "Invoices with debt"), value: client.debtInvoices || 0, note: t("summaryDebtInvoicesNote", "Negative balance") },
            { label: t("summaryNearestPayment", "Nearest payment"), value: formatDate(client.nearestDue, false), note: t("summaryNearestPaymentNote", "Next expected period") },
            { label: t("summaryTotalBalance", "Total balance"), value: formatSom(client.totalBalanceSom), note: t("summaryTotalBalanceNote", "Across all invoices") }
        ].map(function (x) {
            return '<div class="sc-card sc-summary-card">' +
                '<div class="sc-summary-label">' + esc(x.label) + '</div>' +
                '<div class="sc-summary-value">' + esc(x.value) + '</div>' +
                '<div class="sc-summary-note">' + esc(x.note) + '</div>' +
                '</div>';
        }).join("");
    }

    function renderServicesTable(invoice) {
        var rows = (invoice.services || []).map(function (s) {
            return '<tr><td>' + esc(s.serviceName || t("service", "Service")) + '</td><td>' + esc(formatSom(s.serviceSummSom)) + '</td></tr>';
        }).join("");
        if (!rows) rows = '<tr><td colspan="2">' + esc(t("noServices", "No services")) + '</td></tr>';
        return '<div class="sc-sub-block">' +
            '<div class="sc-sub-title">' + esc(t("servicesInInvoice", "Services in invoice")) + '</div>' +
            '<table class="sc-mini-table"><thead><tr><th>' + esc(t("service", "Service")) + '</th><th>' + esc(t("cost", "Cost")) + '</th></tr></thead><tbody>' + rows + '</tbody></table>' +
            '</div>';
    }

    function renderCompactState(invoice) {
        var payment = (invoice.payments || [])[0] || {};
        return '<div class="sc-sub-block">' +
            '<div class="sc-sub-title">' + esc(t("oneTimeStateTitle", "One-time invoice payment state")) + '</div>' +
            '<div class="sc-sub-note">' + esc(t("oneTimeStateNote", "For one-time invoices, simplified payment state is shown.")) + '</div>' +
            '<div class="sc-state-grid">' +
            '<div class="sc-state-card"><div class="sc-state-label">' + esc(t("paymentType", "Payment type")) + '</div><div class="sc-state-value">' + esc(t("oneTime", "One-time")) + '</div></div>' +
            '<div class="sc-state-card"><div class="sc-state-label">' + esc(t("amount", "Amount")) + '</div><div class="sc-state-value">' + esc(formatSom(invoice.fixedSummSom)) + '</div></div>' +
            '<div class="sc-state-card"><div class="sc-state-label">' + esc(t("status", "Status")) + '</div><div class="sc-state-value"><span class="sc-badge ' + statusClass(payment.paymentStatus) + '">' + esc(paymentStatusLabel(payment.paymentStatus)) + '</span></div></div>' +
            '<div class="sc-state-card"><div class="sc-state-label">' + esc(t("balance", "Balance")) + '</div><div class="sc-state-value">' + esc(formatSom(invoice.balanceSom)) + '</div></div>' +
            '</div>' +
            '</div>';
    }

    function renderPeriodicBlock(invoice) {
        var cards = (invoice.payments || []).map(function (p) {
            return '<div class="sc-period-card">' +
                '<div class="sc-period-title">' + esc(p.periodValue || t("period", "Period")) + '</div>' +
                '<span class="sc-badge ' + statusClass(p.paymentStatus) + '">' + esc(paymentStatusLabel(p.paymentStatus)) + '</span>' +
                '<div class="sc-period-meta">' + esc(t("from", "From")) + ': ' + esc(formatDate(p.dateFrom, false)) + '<br>' + esc(t("to", "To")) + ': ' + esc(formatDate(p.dateTo, false)) + '<br>' + esc(t("amount", "Amount")) + ': ' + esc(formatSom(p.paymentSummSom)) + '</div>' +
                '</div>';
        }).join("");

        if (!cards) cards = '<div class="sc-period-card"><div class="sc-period-title">' + esc(t("periodsMissing", "No periods")) + '</div></div>';

        return '<div class="sc-sub-block">' +
            '<div class="sc-sub-title">' + esc(t("periodSchedule", "Payment schedule / periods")) + '</div>' +
            '<div class="sc-sub-note">' + esc(t("periodScheduleNote", "For monthly and other periodic invoices, full period block is shown.")) + '</div>' +
            '<div class="sc-period-cards">' + cards + '</div>' +
            '</div>';
    }

    function renderTransactions(invoice) {
        var filteredTransactions = (invoice.transactions || []).filter(function (tx) {
            return String(tx.transactionType || "") === "payPaymentInvoice";
        });

        var rows = filteredTransactions.map(function (tx) {
            return '<tr>' +
                '<td>' + esc(formatDate(tx.transactionDate, true)) + '</td>' +
                '<td><span class="sc-badge ' + statusClass(tx.transactionStatus) + '">' + esc(tx.transactionStatus || t("dash", "-")) + '</span></td>' +
                '<td>' + esc(formatSom(tx.summSom)) + '</td>' +
                '<td>' + esc(formatSom(tx.transactionSummSom)) + '</td>' +
                '<td>' + esc(tx.transactionSystem || t("dash", "-")) + '</td>' +
                '<td>' + esc((tx.agent && tx.agent.name) ? tx.agent.name : t("dash", "-")) + '</td>' +
                '<td>' + esc(tx.txnId || t("dash", "-")) + '</td>' +
                '</tr>';
        }).join("");

        if (!rows) rows = '<tr><td colspan="7">' + esc(t("transactionsNone", "No transactions")) + '</td></tr>';

        return '<div class="sc-sub-block">' +
            '<div class="sc-sub-title">' + esc(t("transactions", "Transactions")) + '</div>' +
            '<table class="sc-mini-table"><thead><tr><th>' + esc(t("date", "Date")) + '</th><th>' + esc(t("status", "Status")) + '</th><th>' + esc(t("amount", "Amount")) + '</th><th>' + esc(t("amountWithFee", "Amount with fee")) + '</th><th>' + esc(t("system", "System")) + '</th><th>' + esc(t("agent", "Agent")) + '</th><th>TxnId</th></tr></thead><tbody>' + rows + '</tbody></table>' +
            '</div>';
    }

    function renderInvoice(invoice, index) {
        var isCompact = String(invoice.periodicity || "") === "oneTime";
        var statusView = getStatusPresentation(invoice, isCompact);

        var qr = invoice.qr || {};
        var qrStatus = qr.status || "disabled";
        var isQrActive = String(qrStatus).toLowerCase() === "active";
        var qrPreviewHtml = (isQrActive && qr.qrCodeBase64)
            ? '<img class="sc-qr-image" src="data:image/png;base64,' + qr.qrCodeBase64 + '" alt="' + esc(t("qrAlt", "Invoice QR code")) + '" />'
            : "";

        return '<div class="sc-invoice-item' + (index === 0 ? ' open' : '') + '" data-invoice>' +
            '<div class="sc-invoice-head">' +
            '<div><div class="sc-invoice-title">' + esc(invoice.nameInvoice || t("invoice", "Invoice")) + '</div><div class="sc-invoice-sub">' + esc(invoice.id) + ' · ' + esc(t("payCode", "PayCode")) + ': ' + esc(invoice.payCode || t("dash", "-")) + '</div></div>' +
            '<div><div class="sc-box-label">' + esc(t("status", "Status")) + '</div><div class="sc-box-value sc-box-value--status"><span class="sc-badge ' + statusView.badgeClass + ' sc-status-main">' + esc(statusView.main) + '</span>' + (statusView.sub ? '<span class="sc-status-sub">' + esc(statusView.sub) + '</span>' : '') + '</div></div>' +
            '<div><div class="sc-box-label">' + esc(t("period", "Periodicity")) + '</div><div class="sc-box-value"><span class="sc-badge ' + periodicityClass(invoice.periodicity) + '">' + esc(periodicityLabel(invoice.periodicity)) + '</span></div></div>' +
            '<div><div class="sc-box-label">' + esc(t("balance", "Balance")) + '</div><div class="sc-box-value">' + esc(formatSom(invoice.balanceSom)) + '</div></div>' +
            '<div><div class="sc-box-label">' + esc(t("services", "Services")) + '</div><div class="sc-box-value">' + esc((invoice.services || []).length) + '</div></div>' +
            '<div><div class="sc-box-label">' + esc(t("dateCreated", "Date created")) + '</div><div class="sc-box-value">' + esc(formatDate(invoice.dateCreated, false)) + '</div></div>' +
            '<div><button type="button" class="sc-expand-btn">⌄</button></div>' +
            '</div>' +
            '<div class="sc-invoice-body">' +
            '<div class="sc-detail-grid">' +
            '<div class="sc-detail-card">' +
            '<div class="sc-detail-title">' + esc(t("invoicePassport", "Invoice passport")) + '</div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("dateCreated", "Created date")) + '</span><span class="sc-line-value">' + esc(formatDate(invoice.dateCreated, false)) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("dateStart", "Start date")) + '</span><span class="sc-line-value">' + esc(formatDate(invoice.dateStartInvoice, false)) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("dateEnd", "End date")) + '</span><span class="sc-line-value">' + esc(formatDate(invoice.dateEndInvoice, false)) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("nextPeriod", "Next period")) + '</span><span class="sc-line-value">' + esc(formatDate(invoice.nextStartInvoice, false)) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("fixedAmount", "Fixed amount")) + '</span><span class="sc-line-value">' + esc(formatSom(invoice.fixedSummSom)) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("balance", "Balance")) + '</span><span class="sc-line-value">' + esc(formatSom(invoice.balanceSom)) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("autoProlongation", "Auto prolongation")) + '</span><span class="sc-line-value">' + (invoice.autoProlongation ? esc(t("yes", "Yes")) : esc(t("no", "No"))) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("sharedAccount", "Shared account")) + '</span><span class="sc-line-value">' + (invoice.hasSameAccount ? esc(t("yes", "Yes")) : esc(t("no", "No"))) + '</span></div>' +
            '</div>' +
            '<div class="sc-detail-card">' +
            '<div class="sc-detail-title">' + esc(t("qrByInvoice", "QR by invoice")) + '</div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("qrStatus", "QR status")) + '</span><span class="sc-line-value"><span class="sc-badge ' + statusClass(qrStatus) + '">' + esc(qrStatus) + '</span></span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("created", "Created")) + '</span><span class="sc-line-value">' + esc(formatDate(qr.createdAt, true)) + '</span></div>' +
            '<div class="sc-line-row"><span class="sc-line-label">' + esc(t("disabled", "Disabled")) + '</span><span class="sc-line-value">' + esc(formatDate(qr.disabledAt, true)) + '</span></div>' +
            (qrPreviewHtml ? '<div class="sc-qr-preview">' + qrPreviewHtml + '</div>' : '') +
            '</div>' +
            '</div>' +
            renderServicesTable(invoice) +
            (isCompact ? renderCompactState(invoice) : renderPeriodicBlock(invoice)) +
            renderTransactions(invoice) +
            '</div>' +
            '</div>';
    }

    function bindInvoiceExpanders(container) {
        container.querySelectorAll('[data-invoice]').forEach(function (item) {
            var head = item.querySelector('.sc-invoice-head');
            if (!head) return;
            head.addEventListener('click', function () {
                item.classList.toggle('open');
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var root = document.getElementById('standartClientsPage');
        if (!root) return;

        var endpointUrl = root.getAttribute('data-client-invoices-url') || '';
        var clientsScreen = document.getElementById('clientsScreen');
        var detailScreen = document.getElementById('detailScreen');
        var backBtn = document.getElementById('backToClientsBtn');

        var clientNameEl = document.getElementById('scClientName');
        var clientPhoneEl = document.getElementById('scClientPhone');
 
        var clientBalanceEl = document.getElementById('scClientBalance');
        var summaryGridEl = document.getElementById('scSummaryGrid');
        var invoiceListEl = document.getElementById('scInvoiceList');

        function showError(message) {
            invoiceListEl.innerHTML = '<div class="sc-error">' + esc(message || t('loadClientInvoicesError', 'Failed to load invoices.')) + '</div>';
        }

        function openClient(clientId) {
            if (!clientId || !endpointUrl) return;
            clientsScreen.classList.add('hidden');
            detailScreen.classList.add('show');
            invoiceListEl.innerHTML = '<div class="sc-loading">' + esc(t('loadingInvoices', 'Loading invoices...')) + '</div>';

            fetch(endpointUrl + '?clientId=' + encodeURIComponent(clientId), {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(function (r) {
                    if (!r.ok) throw new Error(t('loadClientDataError', 'Failed to load client data.'));
                    return r.json();
                })
                .then(function (data) {
                    if (!data || data.success !== true || !data.client) {
                        throw new Error((data && data.message) ? data.message : t('clientNotFound', 'Client not found.'));
                    }

                    data.client.invoicesCount = (data.invoices || []).length;
                    clientNameEl.textContent = data.client.name || t('clientDefaultName', 'Client');
           
                    clientBalanceEl.textContent = t('summaryTotalBalance', 'Total balance') + ': ' + formatSom(data.client.totalBalanceSom);

                    summaryGridEl.innerHTML = renderSummary(data.client);
                    invoiceListEl.innerHTML = (data.invoices || []).map(renderInvoice).join('');
                    if (!data.invoices || data.invoices.length === 0) {
                        invoiceListEl.innerHTML = '<div class="sc-loading">' + esc(t('clientHasNoInvoices', 'Client has no invoices.')) + '</div>';
                        return;
                    }

                    bindInvoiceExpanders(invoiceListEl);
                })
                .catch(function (e) {
                    showError(e.message);
                });
        }

        root.querySelectorAll('.js-open-client').forEach(function (button) {
            button.addEventListener('click', function () {
                openClient(button.getAttribute('data-client-id'));
            });
        });

        backBtn.addEventListener('click', function () {
            detailScreen.classList.remove('show');
            clientsScreen.classList.remove('hidden');
        });
    });
})();
