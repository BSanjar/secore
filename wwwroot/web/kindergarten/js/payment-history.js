/**
 * Payment History Page JavaScript
 * Handles interactive features for the payment history page
 */

(function() {
    'use strict';

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        initPaymentHistory();
    });

    /**
     * Initialize payment history page features
     */
    function initPaymentHistory() {
        initTableInteractions();
        initRowAnimations();
    }

    /**
     * Initialize table interactions
     */
    function initTableInteractions() {
        const tableRows = document.querySelectorAll('.transaction-row');
        
        tableRows.forEach((row, index) => {
            // Stagger animation on load
            row.style.opacity = '0';
            row.style.transform = 'translateX(-20px)';
            
            setTimeout(() => {
                row.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
                row.style.opacity = '1';
                row.style.transform = 'translateX(0)';
            }, index * 50);

            // Add click interaction
            row.addEventListener('click', function() {
                // Toggle row highlight
                this.classList.toggle('row-selected');
            });
        });
    }

    /**
     * Initialize row animations
     */
    function initRowAnimations() {
        const table = document.querySelector('.transactions-table');
        if (!table) return;

        // Add hover effect to table
        table.addEventListener('mouseover', function(e) {
            const row = e.target.closest('.transaction-row');
            if (row) {
                row.style.transform = 'scale(1.01)';
            }
        });

        table.addEventListener('mouseout', function(e) {
            const row = e.target.closest('.transaction-row');
            if (row) {
                row.style.transform = 'scale(1)';
            }
        });
    }

    /**
     * Format date for display
     */
    function formatDate(dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString('ru-RU', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    /**
     * Format currency amount
     */
    function formatAmount(amount) {
        return new Intl.NumberFormat('ru-RU', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount);
    }
})();

