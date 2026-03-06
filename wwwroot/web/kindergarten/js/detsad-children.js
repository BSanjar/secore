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

    // Функция для применения фильтров
    function applyFilters() {
        const search = searchInput.value.toLowerCase().trim();
        const selectedGroupIds = getSelectedGroupIds();
        const filterByGroups = selectedGroupIds.length > 0;

        const cards = childrenCards.querySelectorAll('.child-card');
        let visibleCount = 0;

        cards.forEach(card => {
            const searchText = card.getAttribute('data-search-text')?.toLowerCase() || '';
            const groupId = card.getAttribute('data-org-client-group-id') || '';

            let matchesSearch = !search || searchText.includes(search);
            let matchesGroups = !filterByGroups || selectedGroupIds.includes(groupId);

            if (matchesSearch && matchesGroups) {
                card.style.display = 'block';
                visibleCount++;
                card.style.animation = 'cardSlideIn 0.3s ease forwards';
            } else {
                card.style.display = 'none';
            }
        });
        
        // Показываем пустое состояние если нет карточек
        const emptyState = childrenCards.querySelector('.empty-state');
        if (visibleCount === 0 && cards.length > 0) {
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

    // Анимация карточек при загрузке
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
    
    console.log('Detsad children page loaded');
});

// Текущий клиент в модальном окне с вкладками (для загрузки Счета/Платежи при переключении вкладок)
let currentDetailClientId = null;

// Открыть модальное окно с вкладками по клику на карточку. Опционально: onShown() вызовется после открытия (для переключения на нужную вкладку).
function openChildDetailModal(clientId, clientName, onShown) {
    const modalEl = document.getElementById('childDetailModal');
    const modalTitle = document.getElementById('childDetailModalLabel');
    const infoContent = document.getElementById('childDetailInfoContent');
    const invoicesContent = document.getElementById('childDetailInvoicesContent');
    const paymentsContent = document.getElementById('childDetailPaymentsContent');
    const tabInvoices = document.getElementById('tab-invoices');
    const tabPayments = document.getElementById('tab-payments');

    if (!modalEl || !modalTitle) return;

    currentDetailClientId = clientId;
    modalTitle.textContent = clientName || 'Ребенок';

    if (infoContent) infoContent.innerHTML = '<div class="loading-spinner"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Загрузка...</span></div></div>';
    if (invoicesContent) invoicesContent.innerHTML = '<p class="text-muted mb-0">Выберите вкладку «Счета» для загрузки данных.</p>';
    if (paymentsContent) paymentsContent.innerHTML = '<p class="text-muted mb-0">Выберите вкладку «Платежи» для загрузки данных.</p>';

    const modal = new bootstrap.Modal(modalEl);
    modal.show();

    loadChildInfoInto(clientId, infoContent);

    modalEl.addEventListener('shown.bs.modal', function onceShown() {
        modalEl.removeEventListener('shown.bs.modal', onceShown);
        var tabChildInfo = document.getElementById('tab-child-info');
        if (tabChildInfo) bootstrap.Tab.getOrCreateInstance(tabChildInfo).show();
        if (tabInvoices) {
            tabInvoices.addEventListener('shown.bs.tab', function onInvoices() {
                if (currentDetailClientId === clientId && invoicesContent && invoicesContent.innerHTML.includes('Выберите вкладку')) {
                    loadInvoicesInto(clientId, invoicesContent, null);
                }
            });
        }
        if (tabPayments) {
            tabPayments.addEventListener('shown.bs.tab', function onPayments() {
                if (currentDetailClientId === clientId && paymentsContent && paymentsContent.innerHTML.includes('Выберите вкладку')) {
                    loadPaymentsInto(clientId, paymentsContent);
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
        let statusText = 'Неизвестно', statusClass = 'status-unknown';
        if (client.status == 1) { statusText = 'Активный'; statusClass = 'status-active'; }
        else if (client.status == 0) { statusText = 'Приостановлен'; statusClass = 'status-suspended'; }
        else { statusText = 'Удален'; statusClass = 'status-deleted'; }
        const createdDate = client.createdDate ? new Date(client.createdDate).toLocaleDateString('ru-RU') : '-';
        const updatedDate = client.updatedDate ? new Date(client.updatedDate).toLocaleDateString('ru-RU') : '-';
        const balance = client.balance ? (client.balance / 100).toFixed(2) : '0.00';
        const balanceClass = client.balance < 0 ? 'text-danger' : '';
        let html = `
            <div class="details-section">
                <h6 class="details-section-title">Основные данные</h6>
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

// Обновить вкладку «Счета» в модальном окне (при смене счета в select)
window.refreshInvoicesInDetailModal = function(invoiceId) {
    const content = document.getElementById('childDetailInvoicesContent');
    if (currentDetailClientId && content) loadInvoicesInto(currentDetailClientId, content, invoiceId || null);
};

// Загрузить контент "Счета" в указанный элемент
async function loadInvoicesInto(clientId, contentEl, invoiceId) {
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
        let html = `
            <div class="details-section">
                <div class="mb-3">
                    <label for="invoiceSelectDetail" class="form-label fw-bold">Выберите счет:</label>
                    <select id="invoiceSelectDetail" class="form-select" onchange="refreshInvoicesInDetailModal(this.value)">
        `;
        invoices.forEach(inv => {
            const sel = inv.id === selectedInvoice.id ? ' selected' : '';
            html += `<option value="${inv.id}"${sel}>${inv.name} (${inv.payCode || 'без кода'})</option>`;
        });
        html += `</select></div></div>
            <div class="details-section">
                <h6 class="details-section-title">Информация о счете</h6>
                <div class="details-grid">
                    <div class="detail-item"><span class="detail-label">Название счёта</span><span class="detail-value">${selectedInvoice.nameInvoice || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Лицевой счёт</span><span class="detail-value">${selectedInvoice.payCode || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">ФИО клиента</span><span class="detail-value">${data.client.name || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Дата создания</span><span class="detail-value">${dateCreated}</span></div>
                    <div class="detail-item"><span class="detail-label">Создавший пользователь</span><span class="detail-value">${selectedInvoice.userCreater || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Периодичность</span><span class="detail-value">${selectedInvoice.periodicity || '-'}</span></div>
                    <div class="detail-item"><span class="detail-label">Текущий баланс</span><span class="detail-value ${balanceClass}">${balance} сом</span></div>
                    <div class="detail-item"><span class="detail-label">Автопродление</span><span class="detail-value">${selectedInvoice.autoProlongation ? 'Да' : 'Нет'}</span></div>
                </div>
            </div>
            <div class="details-section">
                <h6 class="details-section-title">Платежные периоды</h6>
                <div class="table-responsive">
                    <table class="table table-hover table-striped">
                        <thead><tr><th>Период с</th><th>Период по</th><th>Сумма</th><th>Статус</th><th>Значение периода</th></tr></thead>
                        <tbody>`;
        if (invoicePayments.length > 0) {
            invoicePayments.forEach(p => {
                const dateFrom = p.dateFrom ? new Date(p.dateFrom).toLocaleDateString('ru-RU') : '-';
                const dateTo = p.dateTo ? new Date(p.dateTo).toLocaleDateString('ru-RU') : '-';
                const status = p.paymentStatus === 'paid' ? 'Оплачено' : p.paymentStatus === 'non_paid' ? 'Не оплачено' : p.paymentStatus === 'anulated' ? 'Аннулировано' : p.paymentStatus || '-';
                const badge = p.paymentStatus === 'paid' ? 'bg-success' : p.paymentStatus === 'non_paid' ? 'bg-warning' : p.paymentStatus === 'anulated' ? 'bg-danger' : 'bg-secondary';
                const paySumDisplay = p.paymentSumm != null ? Number(p.paymentSumm).toFixed(2) + ' сом' : '—';
                html += `<tr><td>${dateFrom}</td><td>${dateTo}</td><td>${paySumDisplay}</td><td><span class="badge ${badge}">${status}</span></td><td>${p.periodValue || '-'}</td></tr>`;
            });
        } else {
            html += '<tr><td colspan="5" class="text-center text-muted py-4">Платежных периодов не найдено</td></tr>';
        }
        html += '</tbody></table></div></div>';
        contentEl.innerHTML = html;
    } catch (err) {
        console.error('Error loading invoices:', err);
        contentEl.innerHTML = '<div class="alert alert-danger" role="alert"><strong>Ошибка!</strong> Не удалось загрузить данные.</div>';
    }
}

// Загрузить контент "Платежи" в указанный элемент (agentIds — массив id агентов или пустая строка для всех)
async function loadPaymentsInto(clientId, contentEl, agentIds) {
    if (!contentEl) return;
    contentEl.innerHTML = '<div class="loading-spinner"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Загрузка...</span></div></div>';
    try {
        let url = `/Detsad/Cabinet/GetClientTransactions?clientId=${encodeURIComponent(clientId)}`;
        if (agentIds && agentIds.length > 0) {
            url += '&agentIds=' + agentIds.map(function(id) { return encodeURIComponent(id); }).join('&agentIds=');
        }
        const response = await fetch(url);
        if (!response.ok) throw new Error('Ошибка загрузки данных');
        const data = await response.json();
        const transactions = data.transactions || [];
        const agents = data.agents || [];
        const selectedIds = agentIds && agentIds.length > 0 ? agentIds : [];
        let filterHtml = '';
        if (agents.length > 0) {
            filterHtml = `
                <div class="mb-3 d-flex align-items-center flex-wrap gap-2">
                    <label class="form-label small text-muted mb-0 me-2">Агент:</label>
                    <select id="childPaymentsAgentFilter" class="form-select form-select-sm" style="max-width: 220px;">
                        <option value=""${selectedIds.length === 0 ? ' selected' : ''}>Все агенты</option>
                        ${agents.map(function(a) {
                            var isSel = selectedIds.length > 0 && selectedIds.indexOf(a.id) >= 0;
                            return '<option value="' + (a.id || '') + '"' + (isSel ? ' selected' : '') + '>' + (a.name || a.id || '-') + '</option>';
                        }).join('')}
                    </select>
                </div>`;
        }
        let html = `
            <div class="details-section">
                <h6 class="details-section-title">Транзакции</h6>
                <p class="text-muted mb-2">Клиент: <strong>${data.client.name || '-'}</strong></p>
                ${filterHtml}
                <div class="table-responsive">
                    <table class="table table-hover table-striped">
                        <thead><tr><th>Дата</th><th>Агент</th><th>Сумма</th><th>С комиссией</th><th>Тип</th><th>Статус</th></tr></thead>
                        <tbody>`;
        if (transactions.length > 0) {
            transactions.forEach(t => {
                const date = t.transactionDate ? new Date(t.transactionDate).toLocaleString('ru-RU') : '-';
                const agentName = t.agentName || t.agentId || '-';
                const summ = t.summ ? (t.summ / 100).toFixed(2) : '0.00';
                const summFee = t.transactionSumm ? (t.transactionSumm / 100).toFixed(2) : '0.00';
                const type = t.transactionType === 'debit' ? 'Приход' : t.transactionType === 'credit' ? 'Расход' : t.transactionType || '-';
                const typeClass = t.transactionType === 'debit' ? 'text-success' : t.transactionType === 'credit' ? 'text-danger' : '';
                const status = t.transactionStatus === 'success' ? 'Успешно' : t.transactionStatus === 'error' ? 'Ошибка' : t.transactionStatus || '-';
                const statusBadge = t.transactionStatus === 'success' ? 'bg-success' : 'bg-danger';
                html += '<tr><td>' + date + '</td><td>' + agentName + '</td><td>' + summ + ' сом</td><td>' + summFee + ' сом</td><td class="' + typeClass + '">' + type + '</td><td><span class="badge ' + statusBadge + '">' + status + '</span></td></tr>';
            });
        } else {
            html += '<tr><td colspan="6" class="text-center text-muted py-4">Транзакций не найдено</td></tr>';
        }
        html += '</tbody></table></div></div>';
        contentEl.innerHTML = html;
        if (agents.length > 0) {
            var selEl = contentEl.querySelector('#childPaymentsAgentFilter');
            if (selEl) {
                selEl.addEventListener('change', function() {
                    var val = selEl.value;
                    var ids = val ? [val] : [];
                    loadPaymentsInto(clientId, contentEl, ids);
                });
            }
        }
    } catch (err) {
        console.error('Error loading payments:', err);
        contentEl.innerHTML = '<div class="alert alert-danger" role="alert"><strong>Ошибка!</strong> Не удалось загрузить данные.</div>';
    }
}

// Функция для показа информации о ребенке (открывает модальное окно с вкладками на первой вкладке)
async function showChildInfo(clientId) {
    openChildDetailModal(clientId, 'Ребенок');
}

// Открыть модальное окно на вкладке «Счета»
function showInvoicesInfo(clientId, invoiceId = null) {
    openChildDetailModal(clientId, 'Ребенок', function() {
        const tabInvoices = document.getElementById('tab-invoices');
        const invoicesContent = document.getElementById('childDetailInvoicesContent');
        if (tabInvoices) bootstrap.Tab.getOrCreateInstance(tabInvoices).show();
        if (invoicesContent) loadInvoicesInto(clientId, invoicesContent, invoiceId);
    });
}

// Открыть модальное окно на вкладке «Платежи»
function showClientTransactions(clientId) {
    openChildDetailModal(clientId, 'Ребенок', function() {
        const tabPayments = document.getElementById('tab-payments');
        const paymentsContent = document.getElementById('childDetailPaymentsContent');
        if (tabPayments) bootstrap.Tab.getOrCreateInstance(tabPayments).show();
        if (paymentsContent) loadPaymentsInto(clientId, paymentsContent);
    });
}

// Экспортируем функции для глобального доступа
window.showChildInfo = showChildInfo;
window.showInvoicesInfo = showInvoicesInfo;
window.showClientTransactions = showClientTransactions;

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
    document.getElementById('errorMessage').style.display = 'none';
    
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

async function submitChild() {
    const errorDiv = document.getElementById('errorMessage');
    errorDiv.style.display = 'none';

    const form = document.getElementById('childForm');
    
    // Валидация ИНН
    const innInput = document.getElementById('clientInn');
    const innValue = innInput.value.replace(/\D/g, '');
    if (innValue.length !== 14) {
        innInput.classList.add('is-invalid');
        innInput.classList.remove('is-valid');
        form.classList.add('was-validated');
        return;
    } else {
        innInput.classList.remove('is-invalid');
        innInput.classList.add('is-valid');
    }
    
    // Валидация телефона
    const phoneInput = document.getElementById('clientPhone');
    const phoneValue = phoneInput.value;
    const phoneRegex = /^\+996\(\d{3}\) \d{3} \d{3}$/;
    if (!phoneRegex.test(phoneValue)) {
        phoneInput.classList.add('is-invalid');
        phoneInput.classList.remove('is-valid');
        form.classList.add('was-validated');
        return;
    } else {
        phoneInput.classList.remove('is-invalid');
        phoneInput.classList.add('is-valid');
    }
    
    if (!form.checkValidity()) {
        form.classList.add('was-validated');
        form.reportValidity();
        return;
    }
    
    form.classList.add('was-validated');

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
    if (groupSelect && groupSelect.value) {
        childData.orgClientGroupId = groupSelect.value;
    }

    const additionalFieldsSection = document.getElementById('additionalFieldsSection');
    if (additionalFieldsSection) {
        const fieldInputs = additionalFieldsSection.querySelectorAll('[data-field-id]');
        const additionalFields = {};
        fieldInputs.forEach(el => {
            const fieldId = el.getAttribute('data-field-id');
            const value = (el.value || '').trim();
            if (fieldId && value) {
                additionalFields[fieldId] = value;
            }
        });
        if (Object.keys(additionalFields).length > 0) {
            childData.additionalFields = additionalFields;
        }
    }

    // Отправляем данные
    try {
        const submitBtn = document.getElementById('submitBtn');
        submitBtn.disabled = true;
        submitBtn.textContent = 'Регистрация...';

        const response = await fetch('/Detsad/Cabinet/CreateChild', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(childData)
        });

        const result = await response.json();

        if (result.success) {
            // Закрываем модальное окно
            const modal = bootstrap.Modal.getInstance(document.getElementById('addChildModal'));
            modal.hide();

            // Обновляем страницу
            window.location.reload();
        } else {
            showError(result.message || 'Ошибка при добавлении ребенка');
            submitBtn.disabled = false;
            submitBtn.textContent = 'Зарегистрировать';
        }
    } catch (error) {
        console.error('Error submitting child:', error);
        showError('Ошибка при отправке данных. Попробуйте позже.');
        const submitBtn = document.getElementById('submitBtn');
        submitBtn.disabled = false;
        submitBtn.textContent = 'Зарегистрировать';
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