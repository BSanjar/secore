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
    
    console.log('Detsad children page loaded');
});

// Функция для показа информации о ребенке
async function showChildInfo(clientId) {
    const modal = new bootstrap.Modal(document.getElementById('childInfoModal'));
    const content = document.getElementById('childInfoContent');
    
    content.innerHTML = `
        <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Загрузка...</span>
            </div>
        </div>
    `;
    
    modal.show();
    
    try {
        const response = await fetch(`/Detsad/Cabinet/GetChildInfo?clientId=${clientId}`);
        if (!response.ok) {
            throw new Error('Ошибка загрузки данных');
        }
        
        const data = await response.json();
        const client = data.client;
        const additionalFields = data.additionalFields || [];
        
        // Определяем статус
        let statusText = 'Неизвестно';
        let statusClass = 'status-unknown';
        if (client.status == 1) {
            statusText = 'Активный';
            statusClass = 'status-active';
        } else if (client.status == 0) {
            statusText = 'Приостановлен';
            statusClass = 'status-suspended';
        } else {
            statusText = 'Удален';
            statusClass = 'status-deleted';
        }
        
        // Форматируем даты
        const createdDate = client.createdDate ? new Date(client.createdDate).toLocaleDateString('ru-RU') : '-';
        const updatedDate = client.updatedDate ? new Date(client.updatedDate).toLocaleDateString('ru-RU') : '-';
        
        // Форматируем баланс
        const balance = client.balance ? (client.balance / 100).toFixed(2) : '0.00';
        const balanceClass = client.balance < 0 ? 'text-danger' : '';
        
        // HTML для модального окна
        let html = `
            <!-- Основные данные из OrganizationClient -->
            <div class="details-section">
                <h6 class="details-section-title">Основные данные из OrganizationClient</h6>
                <div class="details-grid">
                    <div class="detail-item">
                        <span class="detail-label">ФИО</span>
                        <span class="detail-value">${client.name || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Телефон</span>
                        <span class="detail-value">${client.phone || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Email</span>
                        <span class="detail-value">${client.email || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Адрес</span>
                        <span class="detail-value">${client.address || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">ИНН</span>
                        <span class="detail-value">${client.inn || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Баланс</span>
                        <span class="detail-value ${balanceClass}">${balance} сом</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Статус</span>
                        <span class="detail-value"><span class="child-status-badge ${statusClass}">${statusText}</span></span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Дата регистрации</span>
                        <span class="detail-value">${createdDate}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Дата обновления</span>
                        <span class="detail-value">${updatedDate}</span>
                    </div>
                </div>
            </div>
        `;
        
        // Дополнительные поля
        if (additionalFields.length > 0) {
            html += `
                <div class="details-section">
                    <h6 class="details-section-title">Дополнительные данные</h6>
                    <div class="details-grid">
            `;
            
            additionalFields.forEach(field => {
                html += `
                    <div class="detail-item">
                        <span class="detail-label">${field.fieldName || '-'}</span>
                        <span class="detail-value">${field.value || '-'}</span>
                    </div>
                `;
            });
            
            html += `
                    </div>
                </div>
            `;
        }
        
        content.innerHTML = html;
        
    } catch (error) {
        console.error('Error loading child info:', error);
        content.innerHTML = `
            <div class="alert alert-danger" role="alert">
                <strong>Ошибка!</strong> Не удалось загрузить данные. Попробуйте позже.
            </div>
        `;
    }
}

// Функция для показа информации о счетах
async function showInvoicesInfo(clientId, invoiceId = null) {
    const modal = new bootstrap.Modal(document.getElementById('invoicesInfoModal'));
    const content = document.getElementById('invoicesInfoContent');
    
    content.innerHTML = `
        <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Загрузка...</span>
            </div>
        </div>
    `;
    
    modal.show();
    
    try {
        const url = invoiceId 
            ? `/Detsad/Cabinet/GetInvoicesInfo?clientId=${clientId}&invoiceId=${invoiceId}`
            : `/Detsad/Cabinet/GetInvoicesInfo?clientId=${clientId}`;
        
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error('Ошибка загрузки данных');
        }
        
        const data = await response.json();
        const invoices = data.invoices || [];
        const selectedInvoice = data.selectedInvoice;
        const invoicePayments = data.invoicePayments || [];
        
        if (!selectedInvoice) {
            content.innerHTML = `
                <div class="alert alert-info" role="alert">
                    <strong>Информация:</strong> У данного клиента нет счетов на оплату.
                </div>
            `;
            return;
        }
        
        // Форматируем дату создания
        const dateCreated = selectedInvoice.dateCreated 
            ? new Date(selectedInvoice.dateCreated).toLocaleDateString('ru-RU') 
            : '-';
        
        // Форматируем баланс
        const balance = selectedInvoice.balance ? (selectedInvoice.balance / 100).toFixed(2) : '0.00';
        const balanceClass = selectedInvoice.balance < 0 ? 'text-danger' : '';
        
        // HTML для модального окна
        let html = `
            <!-- Выбор счета -->
            <div class="details-section">
                <div class="mb-3">
                    <label for="invoiceSelect" class="form-label fw-bold">Выберите счет:</label>
                    <select id="invoiceSelect" class="form-select" onchange="showInvoicesInfo('${clientId}', this.value)">
        `;
        
        invoices.forEach(invoice => {
            const isSelected = invoice.id === selectedInvoice.id;
            html += `
                <option value="${invoice.id}" ${isSelected ? 'selected' : ''}>
                    ${invoice.name} (${invoice.payCode || 'без кода'})
                </option>
            `;
        });
        
        html += `
                    </select>
                </div>
            </div>
            
            <!-- Информация о выбранном счете -->
            <div class="details-section">
                <h6 class="details-section-title">Информация о счете</h6>
                <div class="details-grid">
                    <div class="detail-item">
                        <span class="detail-label">Счет на оплату (pay_code)</span>
                        <span class="detail-value">${selectedInvoice.payCode || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">ФИО клиента (ребенка)</span>
                        <span class="detail-value">${data.client.name || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Дата создания</span>
                        <span class="detail-value">${dateCreated}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Создавший пользователь</span>
                        <span class="detail-value">${selectedInvoice.userCreater || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Тип периодичности оплаты</span>
                        <span class="detail-value">${selectedInvoice.periodicity || '-'}</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Текущий баланс</span>
                        <span class="detail-value ${balanceClass}">${balance} сом</span>
                    </div>
                    <div class="detail-item">
                        <span class="detail-label">Автоматически будет продлеваться контракт</span>
                        <span class="detail-value">${selectedInvoice.autoProlongation ? 'Да' : 'Нет'}</span>
                    </div>
                </div>
            </div>
        `;
        
        // Платежные периоды (invoice_payments)
        html += `
            <div class="details-section">
                <h6 class="details-section-title">Платежные периоды</h6>
                <div class="table-responsive">
                    <table class="table table-hover table-striped">
                        <thead>
                            <tr>
                                <th>Период с</th>
                                <th>Период по</th>
                                <th>Сумма оплаты</th>
                                <th>Статус</th>
                                <th>Значение периода</th>
                            </tr>
                        </thead>
                        <tbody>
        `;
        
        if (invoicePayments.length > 0) {
            invoicePayments.forEach(payment => {
                const dateFrom = payment.dateFrom ? new Date(payment.dateFrom).toLocaleDateString('ru-RU') : '-';
                const dateTo = payment.dateTo ? new Date(payment.dateTo).toLocaleDateString('ru-RU') : '-';
                const paymentSumm = payment.paymentSumm || '-';
                const status = payment.paymentStatus === 'paid' ? 'Оплачено' 
                    : payment.paymentStatus === 'non_paid' ? 'Не оплачено'
                    : payment.paymentStatus === 'anulated' ? 'Аннулировано'
                    : payment.paymentStatus || '-';
                const statusClass = payment.paymentStatus === 'paid' ? 'text-success' 
                    : payment.paymentStatus === 'non_paid' ? 'text-warning'
                    : payment.paymentStatus === 'anulated' ? 'text-danger' : '';
                const statusBadge = payment.paymentStatus === 'paid' ? 'bg-success' 
                    : payment.paymentStatus === 'non_paid' ? 'bg-warning'
                    : payment.paymentStatus === 'anulated' ? 'bg-danger' : 'bg-secondary';
                
                html += `
                    <tr>
                        <td><strong>${dateFrom}</strong></td>
                        <td><strong>${dateTo}</strong></td>
                        <td class="fw-bold">${paymentSumm}</td>
                        <td><span class="badge ${statusBadge}">${status}</span></td>
                        <td>${payment.periodValue || '-'}</td>
                    </tr>
                `;
            });
        } else {
            html += `
                <tr>
                    <td colspan="5" class="text-center text-muted py-4">Платежных периодов не найдено</td>
                </tr>
            `;
        }
        
        html += `
                        </tbody>
                    </table>
                </div>
            </div>
        `;
        
        content.innerHTML = html;
        
    } catch (error) {
        console.error('Error loading invoices info:', error);
        content.innerHTML = `
            <div class="alert alert-danger" role="alert">
                <strong>Ошибка!</strong> Не удалось загрузить данные. Попробуйте позже.
            </div>
        `;
    }
}

// Функция для показа транзакций клиента
async function showClientTransactions(clientId) {
    const modal = new bootstrap.Modal(document.getElementById('clientTransactionsModal'));
    const content = document.getElementById('clientTransactionsContent');
    
    content.innerHTML = `
        <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Загрузка...</span>
            </div>
        </div>
    `;
    
    modal.show();
    
    try {
        const response = await fetch(`/Detsad/Cabinet/GetClientTransactions?clientId=${clientId}`);
        if (!response.ok) {
            throw new Error('Ошибка загрузки данных');
        }
        
        const data = await response.json();
        const transactions = data.transactions || [];
        
        let html = `
            <div class="details-section">
                <h6 class="details-section-title">Транзакции по всем счетам</h6>
                <p class="text-muted mb-3">Клиент: <strong>${data.client.name || '-'}</strong></p>
                <div class="table-responsive">
                    <table class="table table-hover table-striped">
                        <thead>
                            <tr>
                                <th>Дата транзакции</th>
                                <th>Сумма</th>
                                <th>Сумма с комиссией</th>
                                <th>Тип</th>
                                <th>Статус</th>
                            </tr>
                        </thead>
                        <tbody>
        `;
        
        if (transactions.length > 0) {
            transactions.forEach(transaction => {
                const transactionDate = transaction.transactionDate 
                    ? new Date(transaction.transactionDate).toLocaleString('ru-RU') 
                    : '-';
                const summ = transaction.summ ? (transaction.summ / 100).toFixed(2) : '0.00';
                const transactionSumm = transaction.transactionSumm ? (transaction.transactionSumm / 100).toFixed(2) : '0.00';
                const type = transaction.transactionType === 'debit' ? 'Приход' 
                    : transaction.transactionType === 'credit' ? 'Расход'
                    : transaction.transactionType || '-';
                const status = transaction.transactionStatus === 'success' ? 'Успешно' 
                    : transaction.transactionStatus === 'error' ? 'Ошибка'
                    : transaction.transactionStatus || '-';
                const statusClass = transaction.transactionStatus === 'success' ? 'text-success' 
                    : transaction.transactionStatus === 'error' ? 'text-danger' : '';
                const typeClass = transaction.transactionType === 'debit' ? 'text-success' 
                    : transaction.transactionType === 'credit' ? 'text-danger' : '';
                
                html += `
                    <tr>
                        <td>${transactionDate}</td>
                        <td class="fw-bold">${summ} сом</td>
                        <td>${transactionSumm} сом</td>
                        <td class="${typeClass}">${type}</td>
                        <td class="${statusClass}"><span class="badge ${statusClass === 'text-success' ? 'bg-success' : 'bg-danger'}">${status}</span></td>
                    </tr>
                `;
            });
        } else {
            html += `
                <tr>
                    <td colspan="5" class="text-center text-muted py-4">Транзакций не найдено</td>
                </tr>
            `;
        }
        
        html += `
                        </tbody>
                    </table>
                </div>
            </div>
        `;
        
        content.innerHTML = html;
        
    } catch (error) {
        console.error('Error loading client transactions:', error);
        content.innerHTML = `
            <div class="alert alert-danger" role="alert">
                <strong>Ошибка!</strong> Не удалось загрузить данные. Попробуйте позже.
            </div>
        `;
    }
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