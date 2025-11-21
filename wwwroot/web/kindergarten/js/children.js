/**
 * Children Page JavaScript
 * Handles interactive features for the children page
 */

(function() {
    'use strict';

    document.addEventListener('DOMContentLoaded', function() {
        initChildren();
    });

    function initChildren() {
        // Initialize table interactions
        const tableRows = document.querySelectorAll('.child-row');
        
        tableRows.forEach((row, index) => {
            row.style.opacity = '0';
            row.style.transform = 'translateX(-20px)';
            
            setTimeout(() => {
                row.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
                row.style.opacity = '1';
                row.style.transform = 'translateX(0)';
            }, index * 50);
        });
    }
})();

