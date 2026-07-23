// JavaScript для страницы "Дети" детсада
document.addEventListener('DOMContentLoaded', function() {
    const searchInput = document.getElementById('searchInput');
    const statusFilter = document.getElementById('statusFilter');
    const debtorsFilter = document.getElementById('debtorsFilter');
    const childrenCards = document.getElementById('childrenCards');
    
    // Выбранные группы для фильтра (мультиселект)
    const groupFilterCheckboxes = document.querySelectorAll('.group-filter-checkbox');
    const groupsFilterBtn = document.getElementById('groupsFilterBtn');
    const groupsFilterMenu = document.getElementById('groupsFilterMenu');
    const groupsFilterSearch = document.getElementById('groupsFilterSearch');
    const groupsFilterList = document.getElementById('groupsFilterList');

    function getSelectedGroupIds() {
        if (!groupFilterCheckboxes.length) return [];
        return Array.from(groupFilterCheckboxes)
            .filter(cb => cb.checked)
            .map(cb => cb.value);
    }

    function areAllGroupsSelected() {
        if (!groupFilterCheckboxes.length) return true;
        return Array.from(groupFilterCheckboxes).every(cb => cb.checked);
    }

    function updateGroupsFilterButtonText() {
        const btnText = groupsFilterBtn?.querySelector('.groups-filter-btn-text');
        if (!btnText) return;
        const ids = getSelectedGroupIds();
        const total = groupFilterCheckboxes.length;
        if (total === 0 || ids.length === 0) {
            btnText.textContent = 'По группам';
        } else if (ids.length === total) {
            btnText.textContent = 'Все группы';
        } else {
            btnText.textContent = 'По группам (' + ids.length + ')';
        }
    }

    // Функция для применения фильтров (поддержка и карточек .child-card, и строк таблицы .child-row)
    function applyFilters() {
        if (!childrenCards) return;
        const search = (searchInput?.value ?? '').toLowerCase().trim();
        const selectedGroupIds = getSelectedGroupIds();
        // Фильтруем только если выбрана часть групп.
        // Если выбраны все (или ни одна) — показываем всех клиентов, включая без группы.
        const filterByGroups = selectedGroupIds.length > 0 && !areAllGroupsSelected();
        
        const cards = childrenCards.querySelectorAll('.child-card');
        const rows = childrenCards.querySelectorAll('.child-row');
        const items = rows.length ? rows : cards;
        let visibleCount = 0;
        const isTable = rows.length > 0;
        const showDisplay = isTable ? 'table-row' : 'block';
        const hideDisplay = 'none';

        items.forEach(item => {
            const searchText = item.getAttribute('data-search-text')?.toLowerCase() || '';
            const groupId = item.getAttribute('data-org-client-group-id') || '';

            let matchesSearch = !search || searchText.includes(search);
            let matchesGroups = !filterByGroups || selectedGroupIds.includes(groupId);
            
            if (matchesSearch && matchesGroups) {
                item.style.display = showDisplay;
                visibleCount++;
                if (!isTable) item.style.animation = 'cardSlideIn 0.3s ease forwards';
            } else {
                item.style.display = hideDisplay;
            }
        });

        const filterEmptyEl = document.getElementById('childrenFilterEmptyState');
        const tableEl = document.getElementById('childrenTable');
        if (filterEmptyEl && tableEl) {
            const showEmpty = visibleCount === 0 && items.length > 0;
            filterEmptyEl.style.display = showEmpty ? 'block' : 'none';
            tableEl.style.display = showEmpty ? 'none' : 'table';
        }

        const emptyState = childrenCards.querySelector('.empty-state');
        if (!isTable && visibleCount === 0 && cards.length > 0) {
            if (!emptyState) {
                const emptyDiv = document.createElement('div');
                emptyDiv.className = 'empty-state';
                emptyDiv.innerHTML = `
                    <div class="empty-state-icon">
                        <svg width="64" height="64" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z" fill="currentColor"/>
                        </svg>
                    </div>
                    <h3 class="empty-state-title">Ничего не найдено</h3>
                    <p class="empty-state-description">Попробуйте изменить параметры поиска</p>
                `;
                childrenCards.appendChild(emptyDiv);
            }
        } else if (emptyState) {
            emptyState.remove();
        }
    }
    
    // Обработчики событий
    let searchTimeout;
    if (searchInput) {
        searchInput.addEventListener('input', function() {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                applyFilters();
            }, 300);
        });
    }

    if (statusFilter) {
        statusFilter.addEventListener('change', function() {
            const params = new URLSearchParams(window.location.search);
            params.set('statusFilter', statusFilter.value);
            if (searchInput && searchInput.value) {
                params.set('search', searchInput.value);
            }
            if (debtorsFilter && debtorsFilter.checked) {
                params.set('debtorsOnly', 'true');
            } else {
                params.delete('debtorsOnly');
            }
            window.location.search = params.toString();
        });
    }

    if (debtorsFilter) {
        debtorsFilter.addEventListener('change', function() {
            const params = new URLSearchParams(window.location.search);
            if (statusFilter) params.set('statusFilter', statusFilter.value);
            if (searchInput && searchInput.value) {
                params.set('search', searchInput.value);
            }
            if (debtorsFilter.checked) {
                params.set('debtorsOnly', 'true');
            } else {
                params.delete('debtorsOnly');
            }
            window.location.search = params.toString();
        });
    }
    
    // Фильтр по группам: открытие/закрытие выпадающего списка
    if (groupsFilterBtn && groupsFilterMenu) {
        groupsFilterBtn.addEventListener('click', function(e) {
            e.stopPropagation();
            const isOpen = groupsFilterMenu.classList.toggle('is-open');
            groupsFilterBtn.setAttribute('aria-expanded', isOpen);
            if (isOpen && groupsFilterSearch) {
                groupsFilterSearch.value = '';
                document.querySelectorAll('.groups-filter-item').forEach(el => el.classList.remove('is-hidden'));
                setTimeout(() => groupsFilterSearch.focus(), 50);
            }
        });
        document.addEventListener('click', function() {
            groupsFilterMenu.classList.remove('is-open');
            groupsFilterBtn.setAttribute('aria-expanded', 'false');
        });
        groupsFilterMenu.addEventListener('click', function(e) {
            e.stopPropagation();
        });
    }

    // Поиск внутри списка групп
    if (groupsFilterSearch && groupsFilterList) {
        groupsFilterSearch.addEventListener('input', function() {
            const term = this.value.toLowerCase().trim();
            groupsFilterList.querySelectorAll('.groups-filter-item').forEach(item => {
                const name = (item.querySelector('.group-filter-checkbox')?.getAttribute('data-group-name') || '').toLowerCase();
                item.classList.toggle('is-hidden', term && !name.includes(term));
            });
        });
    }

    // При изменении чекбокса группы — применяем фильтр и обновляем подпись кнопки
    groupFilterCheckboxes.forEach(cb => {
        cb.checked = true;
        cb.addEventListener('change', function() {
            applyFilters();
            updateGroupsFilterButtonText();
        });
    });
    updateGroupsFilterButtonText();
    applyFilters();

    // Анимация карточек при загрузке (только для карточного вида)
    const cards = childrenCards.querySelectorAll('.child-card');
    cards.forEach((card, index) => {
        card.style.animationDelay = `${index * 0.05}s`;
    });

    // Клик по карточке — открыть модальное окно с вкладками
    childrenCards.querySelectorAll('.child-card--clickable').forEach(function(card) {
        card.addEventListener('click', function() {
            const clientId = card.getAttribute('data-client-id');
            const clientName = card.getAttribute('data-client-name') || 'Ребенок';
            if (clientId) openChildDetailModal(clientId, clientName);
        });
    });

    // Клик по строке таблицы — открыть модальное окно
    if (childrenCards) {
        childrenCards.querySelectorAll('.child-row').forEach(function (row) {
            row.addEventListener('click', function (e) {
                if (e.target.closest('.btn-row-action, .btn-issue-invoice, .child-actions-menu, .child-actions-trigger')) return;
                const clientId = row.getAttribute('data-client-id');
                const clientName = row.getAttribute('data-client-name') || 'Ребенок';
                if (clientId) openChildDetailModal(clientId, clientName);
            });
        });
    }

    // После редиректа (ошибка / предпросмотр) снова открыть окно импорта
    var importAutoOpen = document.getElementById('childrenImportModalAutoOpen');
    if (importAutoOpen && importAutoOpen.value === '1' && typeof bootstrap !== 'undefined') {
        var importModalEl = document.getElementById('importChildrenModal');
        if (importModalEl) {
            bootstrap.Modal.getOrCreateInstance(importModalEl).show();
        }
    }
    
    console.log('Detsad children page loaded');
});


// Текущий клиент в модальном окне карточки ребёнка
let currentDetailClientId = null;

function getRequestVerificationToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
}

function dsChildDetailUrls() {
    var modal = document.getElementById('childDetailModal');
    return {
        info: modal && modal.getAttribute('data-client-info-url') || '/Clients/GetInfo',
        invoices: modal && modal.getAttribute('data-client-invoices-url') || '/Clients/GetClientInvoicesDashboard',
        transactions: modal && modal.getAttribute('data-client-transactions-url') || '/Payments/GetClientTransactions'
    };
}

function dsFormatSom(value) {
    var n = Number(value || 0);
    return n.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' сом';
}

function dsFormatDate(value, withTime) {
    if (!value) return '—';
    var d = new Date(value);
    if (isNaN(d.getTime())) return '—';
    return withTime
        ? d.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
        : d.toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function dsInvoiceStatusLabel(status) {
    var s = String(status || '').toLowerCase();
    if (s === 'actual') return 'Актуален';
    if (s === 'closed') return 'Закрыт';
    if (s === 'suspended') return 'Приостановлен';
    return status || '—';
}

function dsInvoiceStatusClass(status) {
    var s = String(status || '').toLowerCase();
    if (s === 'actual') return 'ds-badge--success';
    if (s === 'closed') return 'ds-badge--neutral';
    if (s === 'suspended') return 'ds-badge--warning';
    return 'ds-badge--neutral';
}

function dsTxStatusClass(status) {
    var s = String(status || '').toLowerCase();
    if (s.indexOf('success') >= 0 || s.indexOf('paid') >= 0) return 'ds-badge--success';
    if (s.indexOf('error') >= 0 || s.indexOf('fail') >= 0) return 'ds-badge--danger';
    return 'ds-badge--neutral';
}

function renderDsChildProfile(client, additionalFields) {
    var name = client.clientName || client.name || '—';
    var statusText = 'Неизвестно', statusClass = 'status-unknown';
    var st = client.clientStatus != null ? client.clientStatus : client.status;
    if (st == 1) { statusText = 'Активный'; statusClass = 'status-active'; }
    else if (st == 0) { statusText = 'Приостановлен'; statusClass = 'status-suspended'; }
    else if (st != null) { statusText = 'Удалён'; statusClass = 'status-deleted'; }
    var balanceRaw = client.clientBalance != null ? client.clientBalance : client.balance;
    var balanceSom = balanceRaw != null ? (Number(balanceRaw) / 100).toFixed(2) : '0.00';
    var balanceClass = Number(balanceRaw) < 0 ? 'ds-balance--debt' : '';
    var logo = client.clientLogo || client.logo;
    var logoBlock = logo
        ? '<div class="ds-child-profile__avatar"><img src="' + escapeHtmlText(logo) + '" alt="" loading="lazy" /></div>'
        : '<div class="ds-child-profile__avatar ds-child-profile__avatar--empty" aria-hidden="true">' + escapeHtmlText((name || '?').charAt(0).toUpperCase()) + '</div>';

    var rows = [
        ['Телефон', client.clientPhone || client.phone],
        ['Email', client.clientEmail || client.email],
        ['Адрес', client.clientAddress || client.address],
        ['ИНН', client.clientInn || client.inn],
        ['Баланс', balanceSom + ' сом']
    ];
    var grid = rows.map(function (r) {
        var valClass = r[0] === 'Баланс' ? balanceClass : '';
        return '<div class="ds-kv"><span class="ds-kv__label">' + escapeHtmlText(r[0]) + '</span><span class="ds-kv__value ' + valClass + '">' + escapeHtmlText(r[1] || '—') + '</span></div>';
    }).join('');

    if (additionalFields && additionalFields.length) {
        additionalFields.forEach(function (field) {
            grid += '<div class="ds-kv"><span class="ds-kv__label">' + escapeHtmlText(field.fieldName || '—') + '</span><span class="ds-kv__value">' + escapeHtmlText(field.value || '—') + '</span></div>';
        });
    }

    return '<div class="ds-child-profile">' + logoBlock +
        '<div class="ds-child-profile__main">' +
        '<div class="ds-child-profile__name">' + escapeHtmlText(name) + '</div>' +
        '<span class="child-status-badge ' + statusClass + '">' + escapeHtmlText(statusText) + '</span>' +
        '</div></div>' +
        '<div class="ds-kv-grid">' + grid + '</div>';
}

function renderDsChildInvoices(invoices, paymentSummary) {
    if (!invoices || !invoices.length) {
        return '<p class="ds-empty-note">Счетов пока нет. Выставьте счёт из списка детей.</p>';
    }
    var sharedPayCode = invoices[0].payCode || '—';
    var html = '<div class="ds-shared-paycode"><span class="ds-shared-paycode__label">Лицевой счёт</span><span class="ds-shared-paycode__value">' + escapeHtmlText(sharedPayCode) + '</span></div>';

    if (paymentSummary) {
        var monthly = Number(paymentSummary.monthlyTotalSom || 0);
        var debt = Number(paymentSummary.debtSom || 0);
        var nextPay = Number(paymentSummary.nextPaymentSom || 0);
        html += '<div class="ds-invoice-pay-summary">';
        html += '<div class="ds-invoice-pay-summary__row"><span class="ds-invoice-pay-summary__label">Итого сумма ежемесячного платежа:</span><span class="ds-invoice-pay-summary__value">' + escapeHtmlText(dsFormatSom(monthly)) + '</span></div>';
        html += '<div class="ds-invoice-pay-summary__row"><span class="ds-invoice-pay-summary__label">Долг на текущий момент:</span><span class="ds-invoice-pay-summary__value ' + (debt > 0 ? 'ds-balance--debt' : '') + '">' + escapeHtmlText(debt > 0 ? dsFormatSom(debt) : '0,00 сом') + '</span></div>';
        var nextText = dsFormatSom(nextPay);
        var planned = Number(paymentSummary.plannedSom || 0);
        if (debt > 0 && planned > 0) {
            nextText += ' <span class="ds-invoice-pay-summary__hint">(долг ' + escapeHtmlText(dsFormatSom(debt)) + ' + плановая)</span>';
        }
        html += '<div class="ds-invoice-pay-summary__row"><span class="ds-invoice-pay-summary__label">Сумма следующего платежа:</span><span class="ds-invoice-pay-summary__value ds-invoice-pay-summary__value--next">' + nextText + '</span></div>';
        html += '</div>';
    }

    html += '<div class="ds-invoice-cards">';
    invoices.forEach(function (inv) {
        var pdfUrl = '/Invoices/DownloadPdf?id=' + encodeURIComponent(inv.id);
        html += '<article class="ds-invoice-card">' +
            '<div class="ds-invoice-card__top">' +
            '<div class="ds-invoice-card__title">' + escapeHtmlText(inv.nameInvoice || 'Счёт') + '</div>' +
            '<span class="ds-badge ' + dsInvoiceStatusClass(inv.invoiceStatus) + '">' + escapeHtmlText(dsInvoiceStatusLabel(inv.invoiceStatus)) + '</span>' +
            '</div>' +
            '<div class="ds-invoice-card__meta">' +
            '<span>Создан: ' + escapeHtmlText(dsFormatDate(inv.dateCreated, false)) + '</span>' +
            '<span>Баланс: <strong class="' + (Number(inv.balanceSom) < 0 ? 'ds-balance--debt' : '') + '">' + escapeHtmlText(dsFormatSom(inv.balanceSom)) + '</strong></span>' +
            '</div>' +
            '<div class="ds-invoice-card__foot">' +
            '<span class="text-muted small">' + escapeHtmlText(inv.periodicity === 'monthly' ? 'Ежемесячно' : (inv.periodicity || '—')) + '</span>' +
            '<a href="' + pdfUrl + '" class="ds-link-btn" download>Скачать PDF</a>' +
            '</div></article>';
    });
    html += '</div>';
    return html;
}

function renderDsChildPayments(transactions) {
    var rows = (transactions || []).map(function (t) {
        var summSom = t.summSom != null ? Number(t.summSom) : (t.summ != null ? Number(t.summ) / 100 : 0);
        var feeSom = t.transactionSummSom != null ? Number(t.transactionSummSom) : summSom;
        var agentName = t.agentName || (t.agent && t.agent.name) || '—';
        return '<tr>' +
            '<td>' + escapeHtmlText(dsFormatDate(t.transactionDate, true)) + '</td>' +
            '<td>' + escapeHtmlText(dsFormatSom(summSom)) + '</td>' +
            '<td>' + escapeHtmlText(dsFormatSom(feeSom)) + '</td>' +
            '<td><span class="ds-badge ' + dsTxStatusClass(t.transactionStatus) + '">' + escapeHtmlText(t.transactionStatus || '—') + '</span></td>' +
            '<td>' + escapeHtmlText(agentName) + '</td>' +
            '</tr>';
    }).join('');
    if (!rows) {
        rows = '<tr><td colspan="5" class="ds-table-empty">Платежей через API пока нет</td></tr>';
    }
    return '<div class="table-responsive ds-payments-table-wrap">' +
        '<table class="table ds-payments-table mb-0">' +
        '<thead><tr><th>Дата</th><th>Сумма</th><th>С учётом комиссии</th><th>Статус</th><th>Агент</th></tr></thead>' +
        '<tbody>' + rows + '</tbody></table></div>';
}

function collectApiTransactionsFromInvoices(invoices) {
    var list = [];
    (invoices || []).forEach(function (inv) {
        (inv.transactions || []).forEach(function (t) {
            if (String(t.transactionType || '') === 'payFromAPI') {
                list.push(t);
            }
        });
    });
    list.sort(function (a, b) {
        var da = new Date(a.transactionDate || 0).getTime();
        var db = new Date(b.transactionDate || 0).getTime();
        return db - da;
    });
    return list;
}

async function loadChildDetailDashboard(clientId) {
    var urls = dsChildDetailUrls();
    var infoEl = document.getElementById('childDetailInfoContent');
    var invEl = document.getElementById('childDetailInvoicesContent');
    var payEl = document.getElementById('childDetailPaymentsContent');
    var subtitleEl = document.getElementById('childDetailModalSubtitle');
    var loader = window.DetsadUI ? DetsadUI.loaderHtml('Загрузка') : '<div class="text-center py-4"><div class="spinner-border text-primary"></div></div>';
    if (infoEl) infoEl.innerHTML = loader;
    if (invEl) invEl.innerHTML = loader;
    if (payEl) payEl.innerHTML = loader;

    try {
        var infoReq = fetch(urls.info + '?clientId=' + encodeURIComponent(clientId));
        var invReq = fetch(urls.invoices + '?clientId=' + encodeURIComponent(clientId), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        var results = await Promise.all([infoReq, invReq]);
        var infoData = results[0].ok ? await results[0].json() : null;
        var invData = results[1].ok ? await results[1].json() : null;

        if (!infoData || infoData.success === false || !infoData.client) {
            if (infoEl) infoEl.innerHTML = '<div class="alert alert-danger mb-0">Не удалось загрузить данные ребёнка.</div>';
        } else {
            var fields = infoData.client.additionalFields || infoData.additionalFields || [];
            if (infoEl) infoEl.innerHTML = renderDsChildProfile(infoData.client, fields);
        }

        if (invData && invData.success && invData.client) {
            if (subtitleEl) {
                subtitleEl.textContent = 'Баланс по счетам: ' + dsFormatSom(invData.client.totalBalanceSom) +
                    (invData.client.activeInvoices != null ? ' · Активных счетов: ' + invData.client.activeInvoices : '');
            }
            var invoices = invData.invoices || [];
            if (invEl) invEl.innerHTML = renderDsChildInvoices(invoices, invData.paymentSummary);
            if (payEl) payEl.innerHTML = renderDsChildPayments(collectApiTransactionsFromInvoices(invoices));
        } else {
            if (invEl) invEl.innerHTML = '<p class="ds-empty-note">Не удалось загрузить счета.</p>';
            if (payEl) payEl.innerHTML = renderDsChildPayments([]);
        }
    } catch (err) {
        console.error('loadChildDetailDashboard', err);
        var errHtml = '<div class="alert alert-danger mb-0">Ошибка загрузки данных.</div>';
        if (infoEl) infoEl.innerHTML = errHtml;
        if (invEl) invEl.innerHTML = errHtml;
        if (payEl) payEl.innerHTML = errHtml;
    }
}

function openChildDetailModal(clientId, clientName, onShown) {
    const modalEl = document.getElementById('childDetailModal');
    const modalTitle = document.getElementById('childDetailModalLabel');
    const subtitleEl = document.getElementById('childDetailModalSubtitle');

    if (!modalEl || !modalTitle) return;

    currentDetailClientId = clientId;
    modalTitle.textContent = clientName || 'Ребёнок';
    if (subtitleEl) subtitleEl.textContent = '';

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();

    loadChildDetailDashboard(clientId);

    if (typeof onShown === 'function') {
        modalEl.addEventListener('shown.bs.modal', function onceShown() {
            modalEl.removeEventListener('shown.bs.modal', onceShown);
            onShown();
        });
    }
}

async function showChildInfo(clientId) {
    openChildDetailModal(clientId, 'Ребёнок');
}

function showInvoicesInfo(clientId, invoiceId) {
    openChildDetailModal(clientId, 'Ребёнок', function () {
        var panel = document.getElementById('dsChildPanelInvoices');
        if (panel) panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
        if (invoiceId) {
            document.querySelectorAll('.ds-invoice-card').forEach(function (card) {
                var link = card.querySelector('a[href*="DownloadPdf"]');
                if (link && link.getAttribute('href').indexOf(invoiceId) >= 0) {
                    card.classList.add('ds-invoice-card--highlight');
                }
            });
        }
    });
}

window.refreshInvoicesInDetailModal = function () {
    if (currentDetailClientId) loadChildDetailDashboard(currentDetailClientId);
};

window.showChildInfo = showChildInfo;
window.showInvoicesInfo = showInvoicesInfo;

// Функции для модального окна добавления ребенка
var addChildComposeApi = null;

function escapeHtmlText(s) {
    var div = document.createElement('div');
    div.textContent = s == null ? '' : s;
    return div.innerHTML;
}

var DS_BIRTH_MONTHS = [
    'Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь',
    'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'
];

function applyBirthDateToSelects(wrap, iso) {
    if (!wrap || !iso) return;
    var hidden = wrap.querySelector('input[data-field-id]');
    var daySel = wrap.querySelector('.ds-birth-date__day');
    var monthSel = wrap.querySelector('.ds-birth-date__month');
    var yearSel = wrap.querySelector('.ds-birth-date__year');
    if (!daySel || !monthSel || !yearSel) return;
    var parts = String(iso).split('T')[0].split('-');
    if (parts.length !== 3) return;
    yearSel.value = parts[0];
    monthSel.value = String(parseInt(parts[1], 10));
    daySel.value = String(parseInt(parts[2], 10));
    if (hidden) hidden.value = parts[0] + '-' + parts[1] + '-' + parts[2];
}

function initBirthDateWidgets(container) {
    container = container || document;
    var now = new Date();
    var maxYear = now.getFullYear();
    var minYear = maxYear - 18;

    container.querySelectorAll('.ds-birth-date').forEach(function (wrap) {
        var daySel = wrap.querySelector('.ds-birth-date__day');
        var monthSel = wrap.querySelector('.ds-birth-date__month');
        var yearSel = wrap.querySelector('.ds-birth-date__year');
        var hidden = wrap.querySelector('input[data-field-id]');
        if (!daySel || !monthSel || !yearSel || !hidden) return;

        if (!wrap.dataset.birthReady) {
            wrap.dataset.birthReady = '1';
            if (daySel.options.length <= 1) {
                for (var d = 1; d <= 31; d++) {
                    var od = document.createElement('option');
                    od.value = String(d);
                    od.textContent = String(d);
                    daySel.appendChild(od);
                }
            }
            if (monthSel.options.length <= 1) {
                DS_BIRTH_MONTHS.forEach(function (name, i) {
                    var om = document.createElement('option');
                    om.value = String(i + 1);
                    om.textContent = name;
                    monthSel.appendChild(om);
                });
            }
            if (yearSel.options.length <= 1) {
                for (var y = maxYear; y >= minYear; y--) {
                    var oy = document.createElement('option');
                    oy.value = String(y);
                    oy.textContent = String(y);
                    yearSel.appendChild(oy);
                }
            }
            function syncBirthHidden() {
                var d = daySel.value;
                var m = monthSel.value;
                var y = yearSel.value;
                if (d && m && y) {
                    hidden.value = y + '-' + String(m).padStart(2, '0') + '-' + String(d).padStart(2, '0');
                } else {
                    hidden.value = '';
                }
            }
            daySel.addEventListener('change', syncBirthHidden);
            monthSel.addEventListener('change', syncBirthHidden);
            yearSel.addEventListener('change', syncBirthHidden);
        }
        if (hidden.value) {
            applyBirthDateToSelects(wrap, hidden.value);
        }
    });
}

window.initBirthDateWidgets = initBirthDateWidgets;
window.applyBirthDateToSelects = applyBirthDateToSelects;

function initAddChildComposer() {
    var root = document.querySelector('#addChildStepInvoice .ds-inv-compose');
    if (!root || !window.DetsadInvoiceCompose) return;
    addChildComposeApi = DetsadInvoiceCompose.init(root, {
        onChange: function () {
            updateAddChildInvoiceSubmitButton();
        }
    });
    updateAddChildInvoiceSubmitButton();
}

function updateAddChildInvoiceSubmitButton() {
    var submitBtn = document.getElementById('submitBtn');
    var ctaBtn = document.getElementById('addChildCreateInvoiceBtn');
    var stepInvoice = document.getElementById('addChildStepInvoice');
    if (!stepInvoice || stepInvoice.style.display === 'none') return;
    var valid = addChildComposeApi && addChildComposeApi.validate().ok;
    if (submitBtn) submitBtn.disabled = !valid;
    if (ctaBtn) ctaBtn.disabled = !valid;
}

function setAddChildBusy(on, message) {
    var layer = document.getElementById('addChildBusyLayer');
    if (!layer) return;
    if (on) {
        layer.hidden = false;
        layer.setAttribute('aria-hidden', 'false');
        layer.innerHTML = window.DetsadUI
            ? DetsadUI.loaderHtml(message || 'Сохранение...')
            : '<div class="text-center py-4"><div class="spinner-border text-primary"></div></div>';
    } else {
        layer.hidden = true;
        layer.setAttribute('aria-hidden', 'true');
        layer.innerHTML = '';
    }
}

function showAddChildResult(success, message) {
    if (success) {
        finishAddChildRegistrationSuccess(message);
        return;
    }
    setAddChildBusy(false);
    showAddChildStep('result');
    var el = document.getElementById('addChildStepResult');
    var submitBtn = document.getElementById('submitBtn');
    var btnBack = document.getElementById('addChildBtnBack');
    var btnCancel = document.getElementById('addChildBtnCancel');
    if (submitBtn) submitBtn.style.display = 'none';
    if (btnBack) btnBack.style.display = 'none';
    if (btnCancel) btnCancel.textContent = 'Закрыть';
    if (!el) return;
    el.innerHTML =
        '<div class="text-center py-3">' +
        '<div class="alert alert-danger text-start">' + escapeHtmlText(message) + '</div>' +
        '<button type="button" class="btn btn-secondary" id="addChildResultAction">Попробовать снова</button></div>';
    var actionBtn = document.getElementById('addChildResultAction');
    if (actionBtn) {
        actionBtn.addEventListener('click', function () {
            showAddChildStep(1);
            if (submitBtn) submitBtn.style.display = '';
            if (btnCancel) btnCancel.textContent = 'Отмена';
            if (el) { el.innerHTML = ''; el.style.display = 'none'; }
        });
    }
}

function finishAddChildRegistrationSuccess(message) {
    setAddChildBusy(false);
    var modalEl = document.getElementById('addChildModal');
    if (modalEl) {
        var inst = bootstrap.Modal.getInstance(modalEl);
        if (inst) inst.hide();
    }
    dsClientsFlashSuccess(message || 'Регистрация выполнена успешно.');
}

function dsClientsFlashSuccess(message, reloadMessage) {
    var text = message || 'Готово.';
    if (window.DetsadUI && typeof DetsadUI.flashAndReload === 'function') {
        DetsadUI.flashAndReload(text, reloadMessage || 'Обновление списка...');
    } else if (window.DetsadUI) {
        DetsadUI.reloadWithOverlay(reloadMessage || 'Обновление...');
    } else {
        alert(text);
        window.location.reload();
    }
}

function dsClientsShowError(message) {
    var text = message || 'Ошибка';
    if (window.DetsadUI && typeof DetsadUI.showToast === 'function') {
        DetsadUI.showToast(text, 'danger');
    } else {
        alert(text);
    }
}

window.dsClientsFlashSuccess = dsClientsFlashSuccess;
window.dsClientsShowError = dsClientsShowError;

function openAddChildModal() {
    const modal = new bootstrap.Modal(document.getElementById('addChildModal'));
    resetModal();
    modal.show();
    setTimeout(() => {
        initPhoneMask();
        initInnValidation();
        initWhatsAppSync();
        initBirthDateWidgets(document.getElementById('childForm'));
        initAddChildComposer();
    }, 300);
}

function initWhatsAppSync() {
    const phoneInput = document.getElementById('clientPhone');
    const waInput = document.getElementById('clientWa');
    const phoneIsWaCheck = document.getElementById('phoneIsWa');
    if (!phoneInput || !waInput || !phoneIsWaCheck) return;

    function syncWaFromPhone() {
        if (phoneIsWaCheck.checked) {
            waInput.value = phoneInput.value;
        }
    }

    phoneIsWaCheck.addEventListener('change', function() {
        if (this.checked) {
            waInput.value = phoneInput.value;
        } else {
            waInput.value = '';
        }
    });
    phoneInput.addEventListener('input', syncWaFromPhone);
    phoneInput.addEventListener('change', syncWaFromPhone);
}

function resetModal() {
    document.getElementById('childForm').reset();
    const addPhoto = document.getElementById('addChildPhoto');
    if (addPhoto) addPhoto.value = '';
    document.getElementById('errorMessage').style.display = 'none';
    const resultEl = document.getElementById('addChildStepResult');
    if (resultEl) { resultEl.innerHTML = ''; resultEl.style.display = 'none'; }
    if (addChildComposeApi && addChildComposeApi.reset) addChildComposeApi.reset();
    else initAddChildComposer();

    showAddChildStep(1);

    const submitBtn = document.getElementById('submitBtn');
    const btnCancel = document.getElementById('addChildBtnCancel');
    if (submitBtn) submitBtn.style.display = '';
    if (btnCancel) btnCancel.textContent = 'Отмена';

    const form = document.getElementById('childForm');
        if (form) {
            form.classList.remove('was-validated');
            const inputs = form.querySelectorAll('.form-control');
            inputs.forEach(input => {
                input.classList.remove('is-invalid', 'is-valid');
            });
        }
    
    initPhoneMask();
}

function showAddChildStep(step) {
    const step1 = document.getElementById('addChildStep1');
    const stepChoice = document.getElementById('addChildStepChoice');
    const stepInvoice = document.getElementById('addChildStepInvoice');
    const stepResult = document.getElementById('addChildStepResult');
    const submitBtn = document.getElementById('submitBtn');
    const btnBack = document.getElementById('addChildBtnBack');
    const btnCancel = document.getElementById('addChildBtnCancel');
    const subtitle = document.getElementById('addChildStepSubtitle');
    const stepsBar = document.getElementById('addChildStepsBar');

    if (step1) step1.style.display = step === 1 ? 'block' : 'none';
    if (stepChoice) stepChoice.style.display = step === 2 ? 'block' : 'none';
    if (stepInvoice) stepInvoice.style.display = step === 3 ? 'block' : 'none';
    if (stepResult) stepResult.style.display = step === 'result' ? 'block' : 'none';

    if (stepsBar && step !== 'result') {
        stepsBar.querySelectorAll('.add-child-steps__item').forEach(function (el) {
            var n = parseInt(el.getAttribute('data-step'), 10);
            el.classList.toggle('is-active', n === step);
            el.classList.toggle('is-done', typeof step === 'number' && n < step);
        });
    }

    if (subtitle && step !== 'result') {
        var titles = { 1: 'Шаг 1 · Данные', 2: 'Шаг 2 · Счёт', 3: 'Шаг 3 · Услуги' };
        subtitle.textContent = titles[step] || '';
    }

    if (!submitBtn) return;

    if (step === 1) {
        if (submitBtn) {
            submitBtn.style.display = '';
            submitBtn.disabled = false;
        }
        submitBtn.textContent = 'Далее';
        submitBtn.onclick = function () { goAddChildToChoice(); };
        if (btnBack) btnBack.style.display = 'none';
    } else if (step === 2) {
        submitBtn.style.display = 'none';
        if (btnBack) { btnBack.style.display = 'inline-block'; btnBack.onclick = function () { showAddChildStep(1); }; }
    } else if (step === 3) {
        submitBtn.style.display = 'none';
        if (btnBack) { btnBack.style.display = 'inline-block'; btnBack.onclick = function () { showAddChildStep(2); }; }
        if (!addChildComposeApi) initAddChildComposer();
        else updateAddChildInvoiceSubmitButton();
    }
    if (btnCancel && step !== 'result') btnCancel.classList.toggle('me-auto', step === 3);
}

function goAddChildToChoice() {
    const errorDiv = document.getElementById('errorMessage');
    errorDiv.style.display = 'none';

    const form = document.getElementById('childForm');
        const innInput = document.getElementById('clientInn');
    const innValue = (innInput && innInput.value.replace(/\D/g, '')) || '';
        if (innValue.length !== 14) {
        if (innInput) { innInput.classList.add('is-invalid'); innInput.classList.remove('is-valid'); }
            form.classList.add('was-validated');
            return;
        }
    if (innInput) { innInput.classList.remove('is-invalid'); innInput.classList.add('is-valid'); }
        
        const phoneInput = document.getElementById('clientPhone');
    const phoneValue = (phoneInput && phoneInput.value) || '';
        const phoneRegex = /^\+996\(\d{3}\) \d{3} \d{3}$/;
        if (!phoneRegex.test(phoneValue)) {
        if (phoneInput) { phoneInput.classList.add('is-invalid'); phoneInput.classList.remove('is-valid'); }
            form.classList.add('was-validated');
        form.reportValidity();
            return;
        }
    if (phoneInput) { phoneInput.classList.remove('is-invalid'); phoneInput.classList.add('is-valid'); }
        
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            form.reportValidity();
            return;
        }
        form.classList.add('was-validated');
    showAddChildStep(2);
}

document.addEventListener('DOMContentLoaded', function() {
    const btnBack = document.getElementById('addChildBtnBack');
    if (btnBack) btnBack.addEventListener('click', function() { /* handled in showAddChildStep */ });
    var choiceYes = document.getElementById('addChildChoiceYes');
    var choiceNo = document.getElementById('addChildChoiceNo');
    if (choiceYes) choiceYes.addEventListener('click', function () {
        if (!addChildComposeApi) initAddChildComposer();
        showAddChildStep(3);
    });
    if (choiceNo) choiceNo.addEventListener('click', function () { submitChildOnly(); });
    var addChildCta = document.getElementById('addChildCreateInvoiceBtn');
    if (addChildCta) addChildCta.addEventListener('click', function () { submitChildAndInvoice(); });
});

function getChildFormData() {
    const form = document.getElementById('childForm');
    const childData = {
        clientName: document.getElementById('clientName').value.trim(),
        clientInn: document.getElementById('clientInn').value.replace(/\D/g, ''),
        clientPhone: document.getElementById('clientPhone').value,
        clientEmail: document.getElementById('clientEmail').value.trim(),
        clientAdres: document.getElementById('clientAdres').value.trim(),
        clientWa: (document.getElementById('clientWa') && document.getElementById('clientWa').value.trim()) || null,
        clientTg: (document.getElementById('clientTg') && document.getElementById('clientTg').value.trim()) || null
    };
    const groupSelect = document.getElementById('orgClientGroupId');
    if (groupSelect && groupSelect.value) childData.orgClientGroupId = groupSelect.value;
    const additionalFieldsSection = document.getElementById('additionalFieldsSection');
    if (additionalFieldsSection) {
        const fieldInputs = additionalFieldsSection.querySelectorAll('[data-field-id]');
        const additionalFields = {};
        fieldInputs.forEach(el => {
            const fieldId = el.getAttribute('data-field-id');
            const value = (el.value || '').trim();
            if (fieldId && value) additionalFields[fieldId] = value;
        });
        if (Object.keys(additionalFields).length > 0) childData.additionalFields = additionalFields;
    }
    return childData;
}

/** Загрузка фото клиента после создания/редактирования (multipart). */
async function uploadChildPhoto(clientId, file, removePhoto) {
    if (!clientId) return { success: false, message: 'Нет clientId' };
    const fd = new FormData();
    fd.append('clientId', clientId);
    if (removePhoto) {
        fd.append('removePhoto', 'true');
    } else if (file) {
        fd.append('photo', file);
        } else {
        return { success: true };
    }
    const csrfToken = getRequestVerificationToken();
    const response = await fetch('/Clients/UploadPhoto', {
        method: 'POST',
        headers: {
            'X-Requested-With': 'XMLHttpRequest',
            ...(csrfToken ? { 'RequestVerificationToken': csrfToken } : {})
        },
        body: fd
    });
    return await response.json();
}

async function submitChild() {
    goAddChildToChoice();
}

async function submitChildOnly() {
    const errorDiv = document.getElementById('errorMessage');
    errorDiv.style.display = 'none';

    const form = document.getElementById('childForm');
    const innInput = document.getElementById('clientInn');
    const innValue = (innInput && innInput.value.replace(/\D/g, '')) || '';
    if (innValue.length !== 14) {
        if (innInput) { innInput.classList.add('is-invalid'); innInput.classList.remove('is-valid'); }
        form.classList.add('was-validated');
        showAddChildStep(1);
        return;
    }
    if (innInput) { innInput.classList.remove('is-invalid'); innInput.classList.add('is-valid'); }

    const phoneInput = document.getElementById('clientPhone');
    const phoneValue = (phoneInput && phoneInput.value) || '';
    const phoneRegex = /^\+996\(\d{3}\) \d{3} \d{3}$/;
    if (!phoneRegex.test(phoneValue)) {
        if (phoneInput) { phoneInput.classList.add('is-invalid'); phoneInput.classList.remove('is-valid'); }
        form.classList.add('was-validated');
        form.reportValidity();
        showAddChildStep(1);
        return;
    }
    if (phoneInput) phoneInput.classList.remove('is-invalid');
    if (!form.checkValidity()) { form.classList.add('was-validated'); form.reportValidity(); showAddChildStep(1); return; }
    form.classList.add('was-validated');

    const childData = getChildFormData();

    try {
        setAddChildBusy(true, 'Регистрация ребёнка...');

        const csrfToken = getRequestVerificationToken();
        const response = await fetch('/Clients/Create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                ...(csrfToken ? { 'RequestVerificationToken': csrfToken } : {})
            },
            body: JSON.stringify(childData)
        });
        const result = await response.json();

        if (result.success) {
            const clientId = result.clientId;
            const photoInput = document.getElementById('addChildPhoto');
            if (clientId && photoInput && photoInput.files && photoInput.files.length) {
                setAddChildBusy(true, 'Загрузка фото...');
                const up = await uploadChildPhoto(clientId, photoInput.files[0], false);
                if (!up.success) {
                    setAddChildBusy(false);
                    showAddChildResult(false, (result.message || 'Ребёнок создан.') + ' ' + (up.message || 'Не удалось загрузить фото.'));
                    return;
                }
            }
            setAddChildBusy(false);
            showAddChildResult(true, result.message || 'Ребёнок успешно зарегистрирован.');
        } else {
            setAddChildBusy(false);
            showAddChildResult(false, result.message || 'Ошибка при добавлении ребенка');
        }
    } catch (error) {
        console.error('Error submitting child:', error);
        setAddChildBusy(false);
        showAddChildResult(false, 'Ошибка при отправке данных. Попробуйте позже.');
    }
}

async function submitChildAndInvoice() {
    const errorDiv = document.getElementById('errorMessage');
    if (errorDiv) errorDiv.style.display = 'none';

    if (!addChildComposeApi) initAddChildComposer();
    var validation = addChildComposeApi ? addChildComposeApi.validate() : { ok: false, message: 'Форма счёта не загружена.' };
    if (!validation.ok) {
        updateAddChildInvoiceSubmitButton();
        var composeErr = document.querySelector('#addChildStepInvoice .ds-inv-compose__error');
        if (composeErr) {
            composeErr.textContent = validation.message || 'Заполните данные счёта.';
            composeErr.hidden = false;
        } else {
            showError(validation.message || 'Заполните данные счёта.');
        }
        return;
    }
    var invoicePart = addChildComposeApi.getPayloadPart();
    if (!invoicePart) {
        showError('Не удалось сформировать счёт.');
        return;
    }

    const form = document.getElementById('childForm');
    const innInput = document.getElementById('clientInn');
    if ((innInput && innInput.value.replace(/\D/g, '').length !== 14) || !form.checkValidity()) {
        showAddChildStep(1);
        form.reportValidity();
        return;
    }

    const childData = getChildFormData();

    setAddChildBusy(true, 'Регистрация ребёнка...');

    try {
        const csrfToken = getRequestVerificationToken();
        const createChildRes = await fetch('/Clients/Create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                ...(csrfToken ? { 'RequestVerificationToken': csrfToken } : {})
            },
            body: JSON.stringify(childData)
        });
        const createChildResult = await createChildRes.json();
        if (!createChildResult.success) {
            setAddChildBusy(false);
            showAddChildResult(false, createChildResult.message || 'Ошибка при добавлении ребенка');
            return;
        }

        const clientId = createChildResult.clientId;
        if (!clientId) {
            setAddChildBusy(false);
            showAddChildResult(false, 'Не получен идентификатор ребёнка.');
            return;
        }

        const photoInput = document.getElementById('addChildPhoto');
        if (photoInput && photoInput.files && photoInput.files.length) {
            setAddChildBusy(true, 'Загрузка фото...');
            const up = await uploadChildPhoto(clientId, photoInput.files[0], false);
            if (!up.success) {
                setAddChildBusy(false);
                showAddChildResult(false, 'Ребёнок создан, но фото не загрузилось: ' + (up.message || ''));
                return;
            }
        }

        setAddChildBusy(true, 'Создание счёта...');

        const invoicePayload = {
            nameInvoice: invoicePart.nameInvoice,
            autoProlongation: true,
            periodicity: 'monthly',
            useCurrentDateTime: true,
            clientIds: [clientId],
            serviceItems: invoicePart.serviceItems || [],
            hassameaccount: true
        };
        if (invoicePart.manualServicePriceSom != null) {
            invoicePayload.manualServicePriceSom = invoicePart.manualServicePriceSom;
        }

        const invoiceRes = await fetch('/Invoices/CreateInvoices', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                ...(csrfToken ? { 'RequestVerificationToken': csrfToken } : {})
            },
            body: JSON.stringify(invoicePayload)
        });
        const invoiceResult = await invoiceRes.json();

        setAddChildBusy(false);
        if (invoiceResult.success !== false) {
            showAddChildResult(true, 'Ребёнок зарегистрирован, счёт на оплату создан.');
        } else {
            showAddChildResult(false, invoiceResult.message || 'Ребёнок добавлен, но не удалось создать счёт.');
        }
    } catch (error) {
        console.error('Error submitChildAndInvoice:', error);
        setAddChildBusy(false);
        showAddChildResult(false, 'Ошибка при отправке данных. Попробуйте позже.');
    }
}

function showError(message) {
    const errorDiv = document.getElementById('errorMessage');
    errorDiv.textContent = message;
    errorDiv.style.display = 'block';
}

// Маска для телефона +996(***) *** ***
function initPhoneMask() {
    const phoneInput = document.getElementById('clientPhone');
    if (!phoneInput) return;
    
    // Удаляем старые обработчики событий
    const newPhoneInput = phoneInput.cloneNode(true);
    phoneInput.parentNode.replaceChild(newPhoneInput, phoneInput);
    const phoneInputEl = document.getElementById('clientPhone');
    
    phoneInputEl.addEventListener('input', function(e) {
        let value = e.target.value.replace(/\D/g, ''); // Убираем все нецифровые символы
        
        // Если начинается не с 996, добавляем 996
        if (value.length > 0 && !value.startsWith('996')) {
            value = '996' + value;
        }
        
        // Ограничиваем до 12 цифр (996 + 9 цифр)
        if (value.length > 12) {
            value = value.substring(0, 12);
        }
        
        // Форматируем: +996(***) *** ***
        let formatted = '+996';
        if (value.length > 3) {
            formatted += '(' + value.substring(3, 6);
            if (value.length > 6) {
                formatted += ') ' + value.substring(6, 9);
                if (value.length > 9) {
                    formatted += ' ' + value.substring(9, 12);
                }
            } else {
                formatted += ')';
            }
        }
        
        e.target.value = formatted;
    });
    
    phoneInputEl.addEventListener('keypress', function(e) {
        // Разрешаем только цифры
        const char = String.fromCharCode(e.which);
        if (!/[0-9]/.test(char)) {
            e.preventDefault();
        }
    });
    
    // При фокусе, если поле пустое, начинаем с +996(
    phoneInputEl.addEventListener('focus', function(e) {
        if (!e.target.value || e.target.value === '') {
            e.target.value = '+996(';
            e.target.setSelectionRange(5, 5);
        }
    });
}

// Валидация ИНН при вводе
function initInnValidation() {
    const innInput = document.getElementById('clientInn');
    if (!innInput) return;
    
    innInput.addEventListener('input', function(e) {
        // Оставляем только цифры
        let value = e.target.value.replace(/\D/g, '');
        // Ограничиваем до 14 цифр
        if (value.length > 14) {
            value = value.substring(0, 14);
        }
        e.target.value = value;
        
        // Валидация
        if (value.length === 14) {
            e.target.classList.remove('is-invalid');
            e.target.classList.add('is-valid');
        } else if (value.length > 0) {
            e.target.classList.remove('is-valid');
            e.target.classList.add('is-invalid');
        } else {
            e.target.classList.remove('is-invalid', 'is-valid');
        }
    });
}

// Инициализация при загрузке страницы (для повторного использования)
document.addEventListener('DOMContentLoaded', function() {
    // Инициализируем маски и валидацию при открытии модального окна
    const addChildModal = document.getElementById('addChildModal');
    if (addChildModal) {
        addChildModal.addEventListener('shown.bs.modal', function() {
            initPhoneMask();
            initInnValidation();
            initBirthDateWidgets(document.getElementById('childForm'));
        });
    }
});

// Экспортируем функции для глобального доступа (вне DOMContentLoaded — onclick в разметке должен находить функцию сразу)
function openImportChildrenModal() {
    var modalEl = document.getElementById('importChildrenModal');
    if (!modalEl || typeof bootstrap === 'undefined') return;
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

window.openImportChildrenModal = openImportChildrenModal;
window.openAddChildModal = openAddChildModal;
window.submitChild = submitChild;
window.uploadChildPhoto = uploadChildPhoto;

function dsShowLoaderIn(el, text) {
    if (!el) return;
    if (window.DetsadUI) el.innerHTML = DetsadUI.loaderHtml(text || 'Загрузка');
    else el.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"></div></div>';
}

function dsPickPrimaryQrFromDashboard(invoices) {
    if (!invoices || !invoices.length) return null;
    var actual = invoices.filter(function(i) {
        return (i.invoiceStatus || '').toLowerCase() === 'actual';
    });
    var list = actual.length ? actual : invoices;
    for (var i = 0; i < list.length; i++) {
        var qr = list[i].qr;
        if (qr && (qr.qrLink || qr.qrCodeBase64)) {
            return { invoice: list[i], qr: qr };
        }
    }
    return null;
}

function dsRenderPayQr(bodyEl, qrPayload, payCode, clientName) {
    if (!bodyEl) return;
    var link = qrPayload && qrPayload.qrLink;
    var b64 = qrPayload && qrPayload.qrCodeBase64;
    var logoSrc = '/web/kindergarten/img/secore-logo.png';

    function buildFrame(innerHtml) {
        bodyEl.innerHTML =
            '<div class="ds-pay-qr-frame">' +
            innerHtml +
            '<img class="ds-pay-qr-logo" src="' + logoSrc + '" alt="SECORE" width="52" height="52" />' +
            '</div>' +
            '<p class="ds-pay-qr-hint">Отсканируйте код в приложении банка для оплаты' +
            (payCode ? '<span class="ds-pay-qr-paycode d-block">Лицевой счёт: ' + escapeHtmlText(payCode) + '</span>' : '') +
            '</p>';
    }

    if (b64) {
        buildFrame('<img src="data:image/png;base64,' + b64 + '" alt="QR для оплаты" width="280" height="280" />');
        return;
    }

    if (link && typeof QRCode !== 'undefined') {
        bodyEl.innerHTML =
            '<div class="ds-pay-qr-frame">' +
            '<canvas id="dsPayQrCanvas" width="280" height="280" aria-label="QR для оплаты"></canvas>' +
            '<img class="ds-pay-qr-logo" src="' + logoSrc + '" alt="SECORE" width="52" height="52" />' +
            '</div>' +
            '<p class="ds-pay-qr-hint">Отсканируйте код в приложении банка для оплаты' +
            (payCode ? '<span class="ds-pay-qr-paycode d-block">Лицевой счёт: ' + escapeHtmlText(payCode) + '</span>' : '') +
            '</p>';
        var canvas = document.getElementById('dsPayQrCanvas');
        QRCode.toCanvas(canvas, link, {
            width: 280,
            margin: 1,
            color: { dark: '#0c4a6e', light: '#ffffff' }
        }, function(err) {
            if (err) bodyEl.innerHTML = '<div class="alert alert-danger">Не удалось построить QR</div>';
        });
        return;
    }

    bodyEl.innerHTML = '<div class="alert alert-warning mb-0">QR ещё не сформирован. Выставьте счёт или дождитесь обновления после оплаты.</div>';
}

function dsRenderInvoicesManageList(bodyEl, clientId, clientName, invoices) {
    if (!bodyEl) return;
    var open = (invoices || []).filter(function(i) {
        return (i.invoiceStatus || '').toLowerCase() !== 'closed';
    });
    if (!open.length) {
        bodyEl.innerHTML = '<div class="ds-invoices-manage-empty">У клиента пока нет счетов. Используйте «Выставить новый счёт» в меню действий.</div>';
        return;
    }
    var html = '<div class="ds-invoices-manage-list">';
    open.forEach(function(inv) {
        var title = escapeHtmlText(inv.nameInvoice || inv.payCode || 'Счёт');
        var payCode = escapeHtmlText(inv.payCode || '—');
        var bal = typeof inv.balanceSom === 'number' ? inv.balanceSom.toFixed(2) : '0.00';
        var fixed = typeof inv.fixedSummSom === 'number' ? inv.fixedSummSom.toFixed(2) : '—';
        html += '<article class="ds-invoice-manage-card" data-invoice-id="' + escapeHtmlText(inv.id) + '">' +
            '<div class="ds-invoice-manage-card__body">' +
            '<div class="ds-invoice-manage-card__title">' + title + '</div>' +
            '<div class="ds-invoice-manage-card__meta">' +
            'Лицевой счёт: <strong>' + payCode + '</strong><br/>' +
            'Статус: <strong>' + escapeHtmlText(dsInvoiceStatusLabel(inv.invoiceStatus)) + '</strong> · ' +
            'Создан: <strong>' + dsFormatDate(inv.dateCreated, false) + '</strong><br/>' +
            'Сумма: <strong>' + fixed + ' сом</strong> · Баланс: <strong>' + bal + ' сом</strong>' +
            '</div></div>' +
            '<button type="button" class="btn btn-outline-danger ds-invoice-manage-delete" data-delete-invoice="' + escapeHtmlText(inv.id) + '">Удалить</button>' +
            '</article>';
    });
    html += '</div>';
    bodyEl.innerHTML = html;

    bodyEl.querySelectorAll('[data-delete-invoice]').forEach(function(btn) {
        btn.addEventListener('click', function() {
            var invoiceId = btn.getAttribute('data-delete-invoice');
            if (!invoiceId) return;
            if (!confirm('Удалить счёт? QR для связанного лицевого счёта будет пересчитан.')) return;
            if (window.DetsadUI) DetsadUI.setButtonLoading(btn, true, { label: '…' });
            else btn.disabled = true;
            fetch('/Invoices/DeleteInvoice', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ invoiceId: invoiceId })
            }).then(function(r) { return r.json(); })
            .then(function(res) {
                if (res.success) {
                    if (window.DetsadUI && DetsadUI.showToast) {
                        DetsadUI.showToast(res.message || 'Счёт удалён', 'success');
                    }
                    dsReloadInvoicesManage(clientId, clientName);
                    var row = document.querySelector('tr.child-row[data-client-id="' + clientId.replace(/"/g, '\\"') + '"]');
                    if (row && !bodyEl.querySelector('.ds-invoice-manage-card')) {
                        row.dataset.firstInvoiceId = '';
                    }
                } else {
                    dsClientsShowError(res.message || 'Не удалось удалить счёт');
                    if (window.DetsadUI) DetsadUI.setButtonLoading(btn, false);
                    else btn.disabled = false;
                }
            }).catch(function() {
                dsClientsShowError('Ошибка сети');
                if (window.DetsadUI) DetsadUI.setButtonLoading(btn, false);
                else btn.disabled = false;
            });
        });
    });
}

function dsReloadInvoicesManage(clientId, clientName) {
    var body = document.getElementById('childInvoicesManageBody');
    if (!body) return;
    dsShowLoaderIn(body, 'Обновление списка');
    fetch('/Clients/GetClientInvoicesDashboard?clientId=' + encodeURIComponent(clientId))
        .then(function(r) { if (!r.ok) throw new Error(); return r.json(); })
        .then(function(data) {
            if (!data.success) throw new Error(data.message || '');
            dsRenderInvoicesManageList(body, clientId, clientName, data.invoices || []);
        })
        .catch(function() {
            body.innerHTML = '<div class="alert alert-danger">Не удалось загрузить счета</div>';
        });
}

window.openChildInvoicesManageModal = function(clientId, clientName) {
    var modal = document.getElementById('childInvoicesManageModal');
    var body = document.getElementById('childInvoicesManageBody');
    var sub = document.getElementById('childInvoicesManageSubtitle');
    if (!modal || !body) return;
    if (sub) sub.textContent = clientName || 'Клиент';
    dsShowLoaderIn(body, 'Загрузка счетов');
    bootstrap.Modal.getOrCreateInstance(modal).show();
    fetch('/Clients/GetClientInvoicesDashboard?clientId=' + encodeURIComponent(clientId))
        .then(function(r) { if (!r.ok) throw new Error(); return r.json(); })
        .then(function(data) {
            if (!data.success) throw new Error(data.message || '');
            dsRenderInvoicesManageList(body, clientId, clientName, data.invoices || []);
        })
        .catch(function() {
            body.innerHTML = '<div class="alert alert-danger">Не удалось загрузить счета</div>';
        });
};

window.openChildPayQrModal = function(clientId, clientName) {
    var modal = document.getElementById('childPayQrModal');
    var body = document.getElementById('childPayQrBody');
    var sub = document.getElementById('childPayQrSubtitle');
    if (!modal || !body) return;
    if (sub) sub.textContent = clientName || 'Клиент';
    dsShowLoaderIn(body, 'Подготовка QR');
    bootstrap.Modal.getOrCreateInstance(modal).show();
    fetch('/Clients/GetClientInvoicesDashboard?clientId=' + encodeURIComponent(clientId))
        .then(function(r) { if (!r.ok) throw new Error(); return r.json(); })
        .then(function(data) {
            if (!data.success) throw new Error(data.message || '');
            var picked = dsPickPrimaryQrFromDashboard(data.invoices || []);
            if (!picked) {
                body.innerHTML = '<div class="alert alert-warning mb-0">Нет счёта с QR. Сначала выставьте счёт клиенту.</div>';
                return;
            }
            var payCode = picked.invoice.payCode || '';
            dsRenderPayQr(body, picked.qr, payCode, clientName);
        })
        .catch(function() {
            body.innerHTML = '<div class="alert alert-danger">Не удалось загрузить QR</div>';
        });
};

