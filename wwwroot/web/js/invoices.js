/**
 * Invoices Page JavaScript
 * Handles interactive features for the invoices page
 */

(function() {
    'use strict';

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        initInvoices();
    });

    /**
     * Initialize invoices page features
     */
    function initInvoices() {
        initTableInteractions();
        initRowAnimations();
    }

    /**
     * Initialize table interactions
     */
    function initTableInteractions() {
        const tableRows = document.querySelectorAll('.invoice-row');
        
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
        const table = document.querySelector('.invoices-table');
        if (!table) return;

        // Add hover effect to table
        table.addEventListener('mouseover', function(e) {
            const row = e.target.closest('.invoice-row');
            if (row) {
                row.style.transform = 'scale(1.01)';
            }
        });

        table.addEventListener('mouseout', function(e) {
            const row = e.target.closest('.invoice-row');
            if (row) {
                row.style.transform = 'scale(1)';
            }
        });
    }
})();

