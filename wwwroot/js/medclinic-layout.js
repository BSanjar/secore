(function () {
    const key = "medclinic-sidebar-collapsed";
    const body = document.body;
    const toggle = document.getElementById("medclinicSidebarToggle");

    if (!body || !toggle) return;

    if (window.localStorage.getItem(key) === "1") {
        body.classList.add("medclinic-shell--collapsed");
    }

    toggle.addEventListener("click", () => {
        const collapsed = body.classList.toggle("medclinic-shell--collapsed");
        window.localStorage.setItem(key, collapsed ? "1" : "0");
    });
})();
