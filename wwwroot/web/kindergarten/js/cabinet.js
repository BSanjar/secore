/**
 * Cabinet Dashboard JavaScript
 * Handles interactive features for the cabinet dashboard
 */

(function() {
    'use strict';

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        initDashboard();
    });

    /**
     * Initialize dashboard features
     */
    function initDashboard() {
        initMenuCards();
        initStatCards();
        initAnimations();
    }

    /**
     * Initialize menu card interactions
     */
    function initMenuCards() {
        const menuCards = document.querySelectorAll('.menu-card:not(.disabled)');
        
        menuCards.forEach(card => {
            card.addEventListener('click', function(e) {
                // Add click animation
                this.style.transform = 'scale(0.98)';
                setTimeout(() => {
                    this.style.transform = '';
                }, 150);
            });

            // Add keyboard support
            card.addEventListener('keypress', function(e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    this.click();
                }
            });
        });
    }

    /**
     * Initialize statistics card animations
     */
    function initStatCards() {
        const statCards = document.querySelectorAll('.stat-card');
        
        // Animate numbers on scroll into view
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    animateValue(entry.target);
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.5
        });

        statCards.forEach(card => {
            observer.observe(card);
        });
    }

    /**
     * Animate value in stat card
     */
    function animateValue(card) {
        const valueElement = card.querySelector('.stat-card-value');
        if (!valueElement) return;

        const text = valueElement.textContent.trim();
        const numberMatch = text.match(/[\d,]+\.?\d*/);
        
        if (!numberMatch) return;

        const finalValue = parseFloat(numberMatch[0].replace(/,/g, ''));
        if (isNaN(finalValue)) return;

        const suffix = text.replace(numberMatch[0], '').trim();
        const duration = 1000;
        const startTime = performance.now();
        const startValue = 0;

        function updateValue(currentTime) {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);
            
            // Easing function (ease-out)
            const easeProgress = 1 - Math.pow(1 - progress, 3);
            const currentValue = startValue + (finalValue - startValue) * easeProgress;
            
            valueElement.textContent = formatNumber(currentValue) + (suffix ? ' ' + suffix : '');
            
            if (progress < 1) {
                requestAnimationFrame(updateValue);
            } else {
                valueElement.textContent = formatNumber(finalValue) + (suffix ? ' ' + suffix : '');
            }
        }

        requestAnimationFrame(updateValue);
    }

    /**
     * Format number with thousand separators
     */
    function formatNumber(num) {
        if (num >= 1000) {
            return num.toLocaleString('ru-RU', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            });
        }
        return num.toFixed(2);
    }

    /**
     * Initialize page animations
     */
    function initAnimations() {
        // Fade in elements on page load
        const elements = document.querySelectorAll('.menu-card, .stat-card');
        
        elements.forEach((element, index) => {
            element.style.opacity = '0';
            element.style.transform = 'translateY(20px)';
            
            setTimeout(() => {
                element.style.transition = 'opacity 0.5s ease, transform 0.5s ease';
                element.style.opacity = '1';
                element.style.transform = 'translateY(0)';
            }, index * 100);
        });
    }

    /**
     * Format currency (in KGS - Kyrgyzstani Som)
     */
    function formatCurrency(amount) {
        return new Intl.NumberFormat('ru-RU', {
            style: 'currency',
            currency: 'KGS',
            minimumFractionDigits: 2
        }).format(amount);
    }
})();

