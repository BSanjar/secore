(function () {
    const body = document.body;
    if (!body) return;

    const sidebar = document.querySelector("[data-shell-sidebar]");
    if (!sidebar) return;

    const overlay = document.querySelector("[data-shell-overlay]");
    const toggles = Array.from(document.querySelectorAll("[data-shell-toggle]"));
    const closes = Array.from(document.querySelectorAll("[data-shell-close]"));
    const mobileQuery = window.matchMedia("(max-width: 1100px)");
    const focusableSelector = "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";
    let lastTrigger = null;

    if (!sidebar.id) {
        sidebar.id = "app-shell-sidebar";
    }
    if (!sidebar.hasAttribute("tabindex")) {
        sidebar.setAttribute("tabindex", "-1");
    }

    toggles.forEach((btn) => {
        btn.setAttribute("aria-controls", sidebar.id);
    });

    function getFocusableElements() {
        return Array.from(sidebar.querySelectorAll(focusableSelector))
            .filter((el) => el.offsetParent !== null);
    }

    function setExpanded(expanded) {
        toggles.forEach((btn) => btn.setAttribute("aria-expanded", String(expanded)));
        if (overlay) overlay.setAttribute("aria-hidden", String(!expanded));
        if (mobileQuery.matches) {
            sidebar.setAttribute("aria-hidden", String(!expanded));
        } else {
            sidebar.removeAttribute("aria-hidden");
        }
    }

    function openSidebar(triggerButton) {
        if (!mobileQuery.matches) return;
        lastTrigger = triggerButton || document.activeElement;
        body.classList.add("shell-sidebar-open");
        setExpanded(true);

        const focusables = getFocusableElements();
        if (focusables.length > 0) {
            focusables[0].focus();
        } else {
            sidebar.focus();
        }
    }

    function closeSidebar() {
        const wasOpen = body.classList.contains("shell-sidebar-open");
        body.classList.remove("shell-sidebar-open");
        setExpanded(false);

        if (wasOpen && lastTrigger && typeof lastTrigger.focus === "function") {
            lastTrigger.focus();
        }
    }

    toggles.forEach((btn) => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            if (body.classList.contains("shell-sidebar-open")) {
                closeSidebar();
            } else {
                openSidebar(btn);
            }
        });
    });

    closes.forEach((btn) => {
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            closeSidebar();
        });
    });

    if (overlay) {
        overlay.addEventListener("click", function () {
            closeSidebar();
        });
    }

    sidebar.addEventListener("click", function (e) {
        if (!mobileQuery.matches) return;
        if (e.target.closest("a[href]")) {
            closeSidebar();
        }
    });

    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && body.classList.contains("shell-sidebar-open")) {
            closeSidebar();
            return;
        }

        if (!body.classList.contains("shell-sidebar-open") || e.key !== "Tab") {
            return;
        }

        const focusables = getFocusableElements();
        if (focusables.length === 0) {
            e.preventDefault();
            return;
        }

        const first = focusables[0];
        const last = focusables[focusables.length - 1];
        const active = document.activeElement;

        if (e.shiftKey && active === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && active === last) {
            e.preventDefault();
            first.focus();
        }
    });

    mobileQuery.addEventListener("change", function (e) {
        if (!e.matches) {
            closeSidebar();
        } else {
            setExpanded(false);
        }
    });

    body.classList.remove("shell-sidebar-open");
    setExpanded(false);
})();
