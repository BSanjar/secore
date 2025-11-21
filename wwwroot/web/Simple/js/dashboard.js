// JavaScript для анимации статистики на главной странице детсада
document.addEventListener('DOMContentLoaded', function() {
    // Анимация чисел в карточках статистики
    const statCards = document.querySelectorAll('.stat-card');
    
    const animateValue = (element, start, end, duration, suffix = '') => {
        const startTime = performance.now();
        const isDecimal = end % 1 !== 0;
        
        const animate = (currentTime) => {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);
            
            // Используем easing функцию для плавной анимации
            const easeOutQuart = 1 - Math.pow(1 - progress, 4);
            const current = start + (end - start) * easeOutQuart;
            
            if (isDecimal) {
                element.textContent = current.toFixed(2) + suffix;
            } else {
                element.textContent = Math.floor(current) + suffix;
            }
            
            if (progress < 1) {
                requestAnimationFrame(animate);
            } else {
                // Устанавливаем финальное значение
                if (isDecimal) {
                    element.textContent = end.toFixed(2) + suffix;
                } else {
                    element.textContent = Math.floor(end) + suffix;
                }
            }
        };
        
        requestAnimationFrame(animate);
    };
    
    // Intersection Observer для запуска анимации при появлении карточки в viewport
    const observerOptions = {
        threshold: 0.3,
        rootMargin: '0px'
    };
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting && !entry.target.classList.contains('animated')) {
                entry.target.classList.add('animated');
                
                const card = entry.target;
                const valueElement = card.querySelector('.stat-value-number');
                const dataValue = parseFloat(card.getAttribute('data-value')) || 0;
                
                if (valueElement && dataValue > 0) {
                    // Определяем, является ли значение десятичным
                    const isDecimal = dataValue % 1 !== 0;
                    const duration = isDecimal ? 1500 : 1000;
                    
                    animateValue(valueElement, 0, dataValue, duration);
                }
            }
        });
    }, observerOptions);
    
    // Наблюдаем за всеми карточками статистики
    statCards.forEach(card => {
        observer.observe(card);
    });
    
    // Добавляем эффект пульсации при наведении на карточки
    statCards.forEach(card => {
        card.addEventListener('mouseenter', function() {
            this.style.transform = 'translateY(-4px) scale(1.02)';
        });
        
        card.addEventListener('mouseleave', function() {
            this.style.transform = 'translateY(0) scale(1)';
        });
    });
    
    console.log('Detsad dashboard loaded with animations');
});

