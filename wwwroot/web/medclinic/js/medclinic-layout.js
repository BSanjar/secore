(function () {
    const key = "medclinic-sidebar-collapsed";
    const body = document.body;
    const toggle = document.getElementById("medclinicSidebarToggle");

    if (body && toggle) {
        if (window.localStorage.getItem(key) === "1") {
            body.classList.add("medclinic-shell--collapsed");
        }

        toggle.addEventListener("click", () => {
            const collapsed = body.classList.toggle("medclinic-shell--collapsed");
            window.localStorage.setItem(key, collapsed ? "1" : "0");
            toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
        });

        toggle.setAttribute("aria-expanded", body.classList.contains("medclinic-shell--collapsed") ? "false" : "true");
    }

    const formatterDate = new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "long", year: "numeric" });
    const formatterTime = new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit" });

    function tickMedclinicClocks() {
        const now = new Date();
        const time = formatterTime.format(now);
        const dateFull = formatterDate.format(now);
        const dateShort = new Intl.DateTimeFormat("ru-RU", {
            day: "numeric",
            month: "short",
            year: "numeric"
        }).format(now);

        document.querySelectorAll("[data-medclinic-clock-time]").forEach((el) => {
            el.textContent = time;
        });
        document.querySelectorAll(".medclinic-sidebar__clock-date[data-medclinic-clock-date]").forEach((el) => {
            el.textContent = dateShort;
        });
        document.querySelectorAll(".medclinic-topbar__clock-date[data-medclinic-clock-date]").forEach((el) => {
            el.textContent = dateFull;
        });
    }

    tickMedclinicClocks();
    window.setInterval(tickMedclinicClocks, 30000);
})();
