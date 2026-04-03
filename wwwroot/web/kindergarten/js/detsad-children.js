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

    function updateGroupsFilterButtonText() {
        const btnText = groupsFilterBtn?.querySelector('.groups-filter-btn-text');
        if (!btnText) return;
        const ids = getSelectedGroupIds();
        btnText.textContent = ids.length > 0 ? 'По группам (' + ids.length + ')' : 'По группам';
    }

    // Функция для применения фильтров (поддержка и карточек .child-card, и строк таблицы .child-row)
    function applyFilters() {
        if (!childrenCards) return;
        const search = searchInput.value.toLowerCase().trim();
        const selectedGroupIds = getSelectedGroupIds();
        const filterByGroups = selectedGroupIds.length > 0;
        
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
    searchInput.addEventListener('input', function() {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => {
            applyFilters();
        }, 300);
    });
    
    statusFilter.addEventListener('change', function() {
        const params = new URLSearchParams(window.location.search);
        params.set('statusFilter', statusFilter.value);
        if (searchInput.value) {
            params.set('search', searchInput.value);
        }
        if (debtorsFilter.checked) {
            params.set('debtorsOnly', 'true');
        } else {
            params.delete('debtorsOnly');
        }
        window.location.search = params.toString();
    });
    
    debtorsFilter.addEventListener('change', function() {
        const params = new URLSearchParams(window.location.search);
        params.set('statusFilter', statusFilter.value);
        if (searchInput.value) {
            params.set('search', searchInput.value);
        }
        if (debtorsFilter.checked) {
            params.set('debtorsOnly', 'true');
        } else {
            params.delete('debtorsOnly');
        }
        window.location.search = params.toString();
    });
    
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
        cb.addEventListener('change', function() {
            applyFilters();
            updateGroupsFilterButtonText();
        });
    });
    updateGroupsFilterButtonText();

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
                if (e.target.closest('.btn-row-action')) return;
                const clientId = row.getAttribute('data-client-id');
                const clientName = row.getAttribute('data-client-name') || 'Ребенок';
                if (clientId) openChildDetailModal(clientId, clientName);
            });
        });
    }

    // Открыть модальное окно импорта детей из Excel
    window.openImportChildrenModal = function () {
        var modalEl = document.getElementById('importChildrenModal');
        if (!modalEl) return;
        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.show();
    };

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


// Текущий клиент в модальном окне с вкладками (для загрузки Счета/Платежи при переключении вкладок)
let currentDetailClientId = null;

// Открыть модальное окно с вкладками по клику на карточку. Опционально: onShown() вызовется после открытия (для переключения на нужную вкладку).
function openChildDetailModal(clientId, clientName, onShown) {
    const modalEl = document.getElementById('childDetailModal');
    const modalTitle = document.getElementById('childDetailModalLabel');
    const infoContent = document.getElementById('childDetailInfoContent');
    const paymentsContent = document.getElementById('childDetailPaymentsContent');
    const tabPayments = document.getElementById('tab-payments');

    if (!modalEl || !modalTitle) return;

    currentDetailClientId = clientId;
    modalTitle.textContent = clientName || 'Ребенок';

    if (infoContent) infoContent.innerHTML = '<div class="loading-spinner"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Загрузка...</span></div></div>';
    if (paymentsContent) paymentsContent.innerHTML = '<p class="text-muted mb-0">Выберите вкладку «Платежи» для загрузки данных.</p>';

    const modal = new bootstrap.Modal(modalEl);
    modal.show();
    
    loadChildInfoInto(clientId, infoContent);

    modalEl.addEventListener('shown.bs.modal', function onceShown() {
        modalEl.removeEventListener('shown.bs.modal', onceShown);
        var tabChildInfo = document.getElementById('tab-child-info');
        if (tabChildInfo) bootstrap.Tab.getOrCreateInstance(tabChildInfo).show();
        if (tabPayments) {
            tabPayments.addEventListener('shown.bs.tab', function onPayments() {
                if (currentDetailClientId === clientId && paymentsContent && paymentsContent.innerHTML.includes('Выберите вкладку')) {
                    loadPaymentsTab(clientId, paymentsContent, null);
                }
            });
        }
        if (typeof onShown === 'function') onShown();
    });
}

// Загрузить контент "О ребенке" в указанный элемент
async function loadChildInfoInto(clientId, contentEl) {
    if (!contentEl) return;
    contentEl.innerHTML = '<div class="loading-spinner"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Загрузка...</span></div></div>';
    try {
        const response = await fetch(`/Detsad/Cabinet/GetChildInfo?clientId=${clientId}`);
        if (!response.ok) throw new Error('Ошибка загрузки данных');
        const data = await response.json();
        const client = data.client;
        const additionalFields = data.additionalFields || [];
        
        // Основные данные по ребёнку
        let statusText = 'Неизвестно', statusClass = 'status-unknown';
        if (client.status == 1) { statusText = 'Активный'; statusClass = 'status-active'; }
        else if (client.status == 0) { statusText = 'Приостановлен'; statusClass = 'status-suspended'; }
        else { statusText = 'Удален'; statusClass = 'status-deleted'; }
        const createdDate = client.createdDate ? new Date(client.createdDate).toLocaleDateString('ru-RU') : '-';
        const updatedDate = client.updatedDate ? new Date(client.updatedDate).toLocaleDateString('ru-RU') : '-';
        const balance = client.balance ? (client.balance / 100).toFixed(2) : '0.00';
        const balanceClass = client.balance < 0 ? 'text-danger' : '';
        const logoBlock = client.logo
            ? `<div class="mb-3 text-center"><img src="${client.logo}" alt="" class="rounded-circle border" style="width:96px;height:96px;object-fit:cover;" loading="lazy" /></div>`
            : '';
        let html = `
            <div class="details-section">
                <h6 class="details-section-title">Основные данные</h6>
                ${logoBlock}
                <div class="details-grid">
                    <div class="detail-item"><span class="detail-label">ФИО</span><span class="detail-value">${client.name || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Телефон</span><span class="detail-value">${client.phone || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Email</span><span class="detail-value">${client.email || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Адрес</span><span class="detail-value">${client.address || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">ИНН</span><span class="detail-value">${client.inn || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Баланс</span><span class="detail-value ${balanceClass}">${balance} сом</span></div>
                    <div class="detail-item"><span class="detail-label">Статус</span><span class="detail-value"><span class="child-status-badge ${statusClass}">${statusText}</span></span></div>
                    <div class="detail-item"><span class="detail-label">Дата регистрации</span><span class="detail-value">${createdDate}</span></div>
                    <div class="detail-item"><span class="detail-label">Дата обновления</span><span class="detail-value">${updatedDate}</span></div>
                    </div>
            </div>`;

        // Информация о счёте (по умолчанию используем первый/выбранный счёт из GetInvoicesInfo)
        try {
            const invResp = await fetch(`/Detsad/Cabinet/GetInvoicesInfo?clientId=${clientId}`);
            if (invResp.ok) {
                const invData = await invResp.json();
                const selectedInvoice = invData.selectedInvoice;
                if (selectedInvoice) {
                    const invDateCreated = selectedInvoice.dateCreated ? new Date(selectedInvoice.dateCreated).toLocaleDateString('ru-RU') : '-';
                    const invBalance = selectedInvoice.balance ? (selectedInvoice.balance / 100).toFixed(2) : '0.00';
                    const invBalanceClass = selectedInvoice.balance < 0 ? 'text-danger' : '';
            html += `
                <div class="details-section">
                <h6 class="details-section-title">Информация о счёте</h6>
                    <div class="details-grid">
                    <div class="detail-item"><span class="detail-label">Название счёта</span><span class="detail-value">${selectedInvoice.nameInvoice || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Лицевой счёт</span><span class="detail-value">${selectedInvoice.payCode || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Дата создания</span><span class="detail-value">${invDateCreated}</span></div>
                    <div class="detail-item"><span class="detail-label">Периодичность</span><span class="detail-value">${selectedInvoice.periodicity || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Текущий баланс</span><span class="detail-value ${invBalanceClass}">${invBalance} сом</span></div>
                    <div class="detail-item"><span class="detail-label">Автопродление</span><span class="detail-value">${selectedInvoice.autoProlongation ? 'Да' : 'Нет'}</span></div>
                </div>
            </div>`;
                }
            }
        } catch (e) {
            console.warn('Не удалось загрузить информацию о счёте для карточки ребёнка', e);
        }

        // Дополнительные данные
        if (additionalFields.length > 0) {
            html += '<div class="details-section"><h6 class="details-section-title">Дополнительные данные</h6><div class="details-grid">';
            additionalFields.forEach(field => {
                html += `<div class="detail-item"><span class="detail-label">${field.fieldName || '-'}</span><span class="detail-value">${field.value || '-'}</span></div>`;
            });
            html += '</div></div>';
        }
        contentEl.innerHTML = html;
    } catch (err) {
        console.error('Error loading child info:', err);
        contentEl.innerHTML = '<div class="alert alert-danger" role="alert"><strong>Ошибка!</strong> Не удалось загрузить данные.</div>';
    }
}

// Обновить вкладку «Платежи» в модальном окне (при смене счета в select)
window.refreshInvoicesInDetailModal = function(invoiceId) {
    const content = document.getElementById('childDetailPaymentsContent');
    if (currentDetailClientId && content) loadPaymentsTab(currentDetailClientId, content, invoiceId || null);
};

// Загрузить вкладку «Платежи»: выбор счёта + платёжные периоды
async function loadPaymentsTab(clientId, contentEl, invoiceId) {
    if (!contentEl) return;
    contentEl.innerHTML = '<div class="loading-spinner"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Загрузка...</span></div></div>';
    try {
        const url = invoiceId 
            ? `/Detsad/Cabinet/GetInvoicesInfo?clientId=${clientId}&invoiceId=${encodeURIComponent(invoiceId)}`
            : `/Detsad/Cabinet/GetInvoicesInfo?clientId=${clientId}`;
        const response = await fetch(url);
        if (!response.ok) throw new Error('Ошибка загрузки данных');
        const data = await response.json();
        const invoices = data.invoices || [];
        const selectedInvoice = data.selectedInvoice;
        const invoicePayments = data.invoicePayments || [];
        if (!selectedInvoice) {
            contentEl.innerHTML = '<div class="alert alert-info" role="alert"><strong>Информация:</strong> У данного клиента нет счетов на оплату.</div>';
            return;
        }
        const dateCreated = selectedInvoice.dateCreated ? new Date(selectedInvoice.dateCreated).toLocaleDateString('ru-RU') : '-';
        const balance = selectedInvoice.balance ? (selectedInvoice.balance / 100).toFixed(2) : '0.00';
        const balanceClass = selectedInvoice.balance < 0 ? 'text-danger' : '';
        const downloadPdfUrl = '/Detsad/Invoices/DownloadPdf?id=' + encodeURIComponent(selectedInvoice.id);
        let html = `
            <div class="details-section">
                <div class="mb-3 d-flex flex-wrap align-items-center gap-2">
                    <div class="flex-grow-1">
                        <label for="invoiceSelectDetail" class="form-label fw-bold mb-0 me-2">Выберите счет:</label>
                        <select id="invoiceSelectDetail" class="form-select form-select-sm d-inline-block" style="max-width: 320px;" onchange="refreshInvoicesInDetailModal(this.value)">
        `;
        invoices.forEach(inv => {
            const sel = inv.id === selectedInvoice.id ? ' selected' : '';
            html += `<option value="${inv.id}"${sel}>${inv.name} (${inv.payCode || 'без кода'})</option>`;
        });
        html += `</select>
                </div>
                    <a href="${downloadPdfUrl}" class="btn btn-primary btn-sm" download title="Счет на оплату PDF">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" class="me-1"><path d="M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/></svg>
                        Скачать счет (PDF)
                    </a>
                </div></div>
            <div class="details-section">
                <h6 class="details-section-title">Платежные периоды</h6>
                <div class="table-responsive">
                    <table class="table table-hover table-striped">
                        <thead><tr><th>Период с</th><th>Период по</th><th>Сумма</th><th>Статус</th><th>Значение периода</th><th class="text-center">Чек</th></tr></thead>
                        <tbody>`;
        if (invoicePayments.length > 0) {
            invoicePayments.forEach(p => {
                const dateFrom = p.dateFrom ? new Date(p.dateFrom).toLocaleDateString('ru-RU') : '-';
                const dateTo = p.dateTo ? new Date(p.dateTo).toLocaleDateString('ru-RU') : '-';
                const status = p.paymentStatus === 'paid' ? 'Оплачено' : p.paymentStatus === 'non_paid' ? 'Не оплачено' : p.paymentStatus === 'anulated' ? 'Аннулировано' : p.paymentStatus || '-';
                const badge = p.paymentStatus === 'paid' ? 'bg-success' : p.paymentStatus === 'non_paid' ? 'bg-warning' : p.paymentStatus === 'anulated' ? 'bg-danger' : 'bg-secondary';
                const paySumDisplay = p.paymentSumm != null ? Number(p.paymentSumm).toFixed(2) + ' сом' : '—';
                const receiptLink = p.paymentStatus === 'paid' && p.id
                    ? '<a href="/Detsad/Invoices/DownloadReceiptPdf?id=' + encodeURIComponent(p.id) + '" class="btn btn-sm btn-outline-primary" download title="Скачать чек"><svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/></svg></a>'
                    : '—';
                html += `<tr><td>${dateFrom}</td><td>${dateTo}</td><td>${paySumDisplay}</td><td><span class="badge ${badge}">${status}</span></td><td>${p.periodValue || '-'}</td><td class="text-center">${receiptLink}</td></tr>`;
            });
        } else {
            html += '<tr><td colspan="6" class="text-center text-muted py-4">Платежных периодов не найдено</td></tr>';
        }
        html += '</tbody></table></div></div>';
        contentEl.innerHTML = html;
    } catch (err) {
        console.error('Error loading invoices/payments:', err);
        contentEl.innerHTML = '<div class="alert alert-danger" role="alert"><strong>Ошибка!</strong> Не удалось загрузить данные.</div>';
    }
}

// Функция для показа информации о ребенке (открывает модальное окно с вкладками на первой вкладке)
async function showChildInfo(clientId) {
    openChildDetailModal(clientId, 'Ребенок');
}

// Открыть модальное окно на вкладке «Платежи»
function showInvoicesInfo(clientId, invoiceId = null) {
    openChildDetailModal(clientId, 'Ребенок', function() {
        const tabPayments = document.getElementById('tab-payments');
        const paymentsContent = document.getElementById('childDetailPaymentsContent');
        if (tabPayments) bootstrap.Tab.getOrCreateInstance(tabPayments).show();
        if (paymentsContent) loadPaymentsTab(clientId, paymentsContent, invoiceId);
    });
}

// Экспортируем функции для глобального доступа
window.showChildInfo = showChildInfo;
window.showInvoicesInfo = showInvoicesInfo;

// Функции для модального окна добавления ребенка
function openAddChildModal() {
    const modal = new bootstrap.Modal(document.getElementById('addChildModal'));
    resetModal();
    
    modal.show();
    setTimeout(() => {
        initPhoneMask();
        initInnValidation();
        initWhatsAppSync();
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
    const nameEl = document.getElementById('addChildInvoiceName');
    const amountEl = document.getElementById('addChildInvoiceAmount');
    if (nameEl) nameEl.value = '';
    if (amountEl) amountEl.value = '';

    showAddChildStep(1);

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
    const step2 = document.getElementById('addChildStep2');
    const submitBtn = document.getElementById('submitBtn');
    const btnBack = document.getElementById('addChildBtnBack');
    const btnSkip = document.getElementById('addChildBtnSkip');
    const btnCancel = document.getElementById('addChildBtnCancel');
    if (!step1 || !step2 || !submitBtn) return;

    if (step === 1) {
        step1.style.display = 'block';
        step2.style.display = 'none';
        submitBtn.textContent = 'Далее';
        submitBtn.onclick = function() { goAddChildToStep2(); };
        if (btnBack) { btnBack.style.display = 'none'; }
        if (btnSkip) { btnSkip.style.display = 'none'; }
        if (btnCancel) { btnCancel.classList.remove('me-auto'); }
    } else {
        step1.style.display = 'none';
        step2.style.display = 'block';
        submitBtn.textContent = 'Зарегистрировать и создать счёт';
        submitBtn.onclick = function() { submitChildAndInvoice(); };
        if (btnBack) { btnBack.style.display = 'block'; }
        if (btnSkip) { btnSkip.style.display = 'inline-block'; }
        if (btnCancel) { btnCancel.classList.add('me-auto'); }
    }
}

function goAddChildToStep2() {
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
    if (btnBack) btnBack.addEventListener('click', function() { showAddChildStep(1); });
    const btnSkip = document.getElementById('addChildBtnSkip');
    if (btnSkip) btnSkip.addEventListener('click', function() { submitChildOnly(); });
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
    const response = await fetch('/Detsad/Cabinet/UploadChildPhoto', {
        method: 'POST',
        body: fd
    });
    return await response.json();
}

async function submitChild() {
    // На шаге 1 кнопка «Далее» — переходим на шаг 2
    const step2 = document.getElementById('addChildStep2');
    if (step2 && step2.style.display === 'none') {
        goAddChildToStep2();
        return;
    }
    submitChildOnly();
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
    if (phoneInput) phoneInput.classList.remove('is-invalid');
    if (!form.checkValidity()) { form.classList.add('was-validated'); form.reportValidity(); return; }
    form.classList.add('was-validated');

    const childData = getChildFormData();
    try {
        const submitBtn = document.getElementById('submitBtn');
        const btnSkip = document.getElementById('addChildBtnSkip');
        submitBtn.disabled = true;
        if (btnSkip) btnSkip.disabled = true;
        submitBtn.textContent = 'Регистрация...';

        const response = await fetch('/Detsad/Cabinet/CreateChild', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(childData)
        });
        const result = await response.json();

        if (result.success) {
            const clientId = result.clientId;
            const photoInput = document.getElementById('addChildPhoto');
            if (clientId && photoInput && photoInput.files && photoInput.files.length) {
                const up = await uploadChildPhoto(clientId, photoInput.files[0], false);
                if (!up.success) {
                    showError((result.message || 'Ребёнок создан.') + ' ' + (up.message || 'Не удалось загрузить фото.'));
                    submitBtn.disabled = false;
                    submitBtn.textContent = 'Зарегистрировать и создать счёт';
                    if (btnSkip) btnSkip.disabled = false;
        return;
                }
            }
            bootstrap.Modal.getInstance(document.getElementById('addChildModal')).hide();
            window.location.reload();
        } else {
            showError(result.message || 'Ошибка при добавлении ребенка');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Зарегистрировать и создать счёт';
            if (btnSkip) btnSkip.disabled = false;
        }
    } catch (error) {
        console.error('Error submitting child:', error);
        showError('Ошибка при отправке данных. Попробуйте позже.');
        const submitBtn = document.getElementById('submitBtn');
        const btnSkip = document.getElementById('addChildBtnSkip');
        submitBtn.disabled = false;
        submitBtn.textContent = 'Зарегистрировать и создать счёт';
        if (btnSkip) btnSkip.disabled = false;
    }
}

async function submitChildAndInvoice() {
    const errorDiv = document.getElementById('errorMessage');
    errorDiv.style.display = 'none';

    const nameEl = document.getElementById('addChildInvoiceName');
    const amountEl = document.getElementById('addChildInvoiceAmount');
    const name = (nameEl && nameEl.value.trim()) || '';
    const amount = amountEl && amountEl.value !== '' ? parseFloat(amountEl.value) : NaN;
    if (!name) {
        if (nameEl) nameEl.classList.add('is-invalid');
        errorDiv.textContent = 'Укажите название счёта.';
        errorDiv.style.display = 'block';
        return;
    }
    if (nameEl) nameEl.classList.remove('is-invalid');
    if (isNaN(amount) || amount < 0) {
        if (amountEl) amountEl.classList.add('is-invalid');
        errorDiv.textContent = 'Укажите корректную сумму (сом).';
        errorDiv.style.display = 'block';
        return;
    }
    if (amountEl) amountEl.classList.remove('is-invalid');

    const form = document.getElementById('childForm');
    const innInput = document.getElementById('clientInn');
    const phoneInput = document.getElementById('clientPhone');
    if ((innInput && innInput.value.replace(/\D/g, '').length !== 14) || !form.checkValidity()) {
        showAddChildStep(1);
        form.reportValidity();
        return;
    }

    const childData = getChildFormData();
        const submitBtn = document.getElementById('submitBtn');
    const btnSkip = document.getElementById('addChildBtnSkip');
        submitBtn.disabled = true;
    if (btnSkip) btnSkip.disabled = true;
        submitBtn.textContent = 'Регистрация...';

    try {
        const createChildRes = await fetch('/Detsad/Cabinet/CreateChild', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(childData)
        });
        const createChildResult = await createChildRes.json();
        if (!createChildResult.success) {
            showError(createChildResult.message || 'Ошибка при добавлении ребенка');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Зарегистрировать и создать счёт';
            if (btnSkip) btnSkip.disabled = false;
            return;
        }

        const clientId = createChildResult.clientId;
        if (!clientId) {
            showError('Не получен идентификатор ребёнка.');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Зарегистрировать и создать счёт';
            if (btnSkip) btnSkip.disabled = false;
            return;
        }

        const photoInput = document.getElementById('addChildPhoto');
        if (photoInput && photoInput.files && photoInput.files.length) {
            const up = await uploadChildPhoto(clientId, photoInput.files[0], false);
            if (!up.success) {
                showError('Ребёнок создан, но фото не загрузилось: ' + (up.message || ''));
                submitBtn.disabled = false;
                submitBtn.textContent = 'Зарегистрировать и создать счёт';
                if (btnSkip) btnSkip.disabled = false;
                return;
            }
        }

        const invoicePayload = {
            nameInvoice: name,
            manualServicePriceSom: amount,
            autoProlongation: true,
            periodicity: 'monthly',
            useCurrentDateTime: true,
            clientIds: [clientId],
            serviceItems: [],
            hassameaccount: true
        };

        const invoiceRes = await fetch('/Detsad/Cabinet/CreateInvoices', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(invoicePayload)
        });
        const invoiceResult = await invoiceRes.json();

        if (invoiceResult.success !== false) {
            bootstrap.Modal.getInstance(document.getElementById('addChildModal')).hide();
            window.location.reload();
        } else {
            showError(invoiceResult.message || 'Ребёнок добавлен, но не удалось создать счёт.');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Зарегистрировать и создать счёт';
            if (btnSkip) btnSkip.disabled = false;
        }
    } catch (error) {
        console.error('Error submitChildAndInvoice:', error);
        showError('Ошибка при отправке данных. Попробуйте позже.');
        submitBtn.disabled = false;
        submitBtn.textContent = 'Зарегистрировать и создать счёт';
        if (btnSkip) btnSkip.disabled = false;
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
        });
    }
});

// Экспортируем функции для глобального доступа
window.openAddChildModal = openAddChildModal;
window.submitChild = submitChild;
window.uploadChildPhoto = uploadChildPhoto;