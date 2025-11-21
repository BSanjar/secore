// JavaScript для страницы "Дети" детсада
document.addEventListener('DOMContentLoaded', function() {
    const searchInput = document.getElementById('searchInput');
    const statusFilter = document.getElementById('statusFilter');
    const debtorsFilter = document.getElementById('debtorsFilter');
    const childrenCards = document.getElementById('childrenCards');
    
    // Функция для применения фильтров
    function applyFilters() {
        const search = searchInput.value.toLowerCase().trim();
        const status = statusFilter.value;
        const debtorsOnly = debtorsFilter.checked;
        
        const cards = childrenCards.querySelectorAll('.child-card');
        let visibleCount = 0;
        
        cards.forEach(card => {
            const searchText = card.getAttribute('data-search-text')?.toLowerCase() || '';
            const clientId = card.getAttribute('data-client-id');
            
            // Поиск
            let matchesSearch = !search || searchText.includes(search);
            
            if (matchesSearch) {
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
