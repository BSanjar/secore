(function () {
    const payload = window.medclinicSchedule || {};
    const catalog = payload.serviceCatalog || [];

    const grid = document.getElementById("appointmentServiceCatalog");
    const searchInput = document.getElementById("appointmentServiceSearch");
    const selectedBox = document.getElementById("appointmentSelectedServices");
    const paymentHidden = document.getElementById("appointmentPaymentType");

    if (!grid || !selectedBox) return;

    const fmt = (v) =>
        `${new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(v) || 0)} c`;

    function esc(v) {
        return String(v ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function syncPaymentHidden() {
        if (!paymentHidden) return;
        const checked = document.querySelector('input[name="appointmentPaymentChoice"]:checked');
        paymentHidden.value = checked?.value || "qr_secore";
        window.medclinicValidateAppointmentForm?.();
    }

    function selectedServiceIds() {
        const state = window.medclinicScheduleState;
        return new Set((state?.selectedServices || []).map((x) => String(x.organizationServiceId || "")));
    }

    function syncCatalogSelection() {
        const ids = selectedServiceIds();
        grid.querySelectorAll("[data-service-id]").forEach((btn) => {
            btn.classList.toggle("is-selected", ids.has(btn.getAttribute("data-service-id")));
        });
    }

    function renderCatalog() {
        const q = (searchInput?.value || "").trim().toLowerCase();
        const filtered = catalog.filter((s) => !q || String(s.name || "").toLowerCase().includes(q));
        if (!filtered.length) {
            grid.innerHTML = '<div class="text-muted small py-2">Услуги не найдены</div>';
            return;
        }
        grid.innerHTML = filtered
            .map(
                (s) =>
                    `<button type="button" class="appointment-create-service-card" data-service-id="${esc(s.id)}" title="${esc(s.name)}"><span class="appointment-create-service-card__name">${esc(s.name)}</span><span class="appointment-create-service-card__price">${fmt(Number(s.priceTyiyn || 0))}</span></button>`
            )
            .join("");
        grid.querySelectorAll("[data-service-id]").forEach((btn) => {
            btn.addEventListener("click", () => {
                const id = btn.getAttribute("data-service-id");
                const item = catalog.find((x) => String(x.id) === String(id));
                if (!item) return;
                const state = window.medclinicScheduleState;
                if (!state) return;
                const existing = state.selectedServices.find((x) => String(x.organizationServiceId || "") === String(id));
                if (existing) existing.quantity += 1;
                else {
                    state.selectedServices.push({
                        organizationServiceId: item.id,
                        serviceName: item.name,
                        priceTyiyn: Number(item.priceTyiyn || 0),
                        quantity: 1
                    });
                }
                if (typeof window.medclinicRenderSelectedServices === "function") {
                    window.medclinicRenderSelectedServices();
                }
            });
        });
        syncCatalogSelection();
    }

    window.medclinicRenderSelectedServices = function renderSelected() {
        const state = window.medclinicScheduleState;
        const services = state?.selectedServices || [];
        const totalNode = document.getElementById("appointmentServicesTotal");
        if (!services.length) {
            selectedBox.innerHTML = '<div class="appointment-create-selected__empty text-muted small">Выберите услуги из каталога</div>';
            if (totalNode) totalNode.textContent = "Итого: 0,00 c";
            syncCatalogSelection();
            if (typeof window.medclinicValidateAppointmentForm === "function") window.medclinicValidateAppointmentForm();
            return;
        }
        selectedBox.innerHTML =
            `<div class="appointment-create-selected__head">Выбрано: ${services.length}</div>` +
            services
                .map(
                    (s, i) => {
                        const lineTotal = Number(s.priceTyiyn || 0) * Math.max(1, Number(s.quantity || 1));
                        return `<div class="appointment-create-selected-row" data-index="${i}"><div class="appointment-create-selected-row__main"><span class="appointment-create-selected-row__name" title="${esc(s.serviceName)}">${esc(s.serviceName)}</span><span class="appointment-create-selected-row__unit">${fmt(Number(s.priceTyiyn || 0))} × </span></div><div class="appointment-create-selected-row__controls"><input type="number" min="1" class="form-control form-control-sm appointment-create-qty" data-index="${i}" value="${Math.max(1, Number(s.quantity || 1))}" aria-label="Количество" /><span class="appointment-create-selected-row__sum">${fmt(lineTotal)}</span><button type="button" class="appointment-create-selected-row__remove" data-index="${i}" aria-label="Удалить">×</button></div></div>`;
                    }
                )
                .join("");
        selectedBox.querySelectorAll(".appointment-create-qty").forEach((input) => {
            input.addEventListener("input", () => {
                const idx = Number(input.dataset.index);
                services[idx].quantity = Math.max(1, Number(input.value || 1));
                renderSelected();
            });
        });
        selectedBox.querySelectorAll(".appointment-create-selected-row__remove").forEach((btn) => {
            btn.addEventListener("click", () => {
                services.splice(Number(btn.dataset.index), 1);
                renderSelected();
            });
        });
        const total = services.reduce((sum, item) => sum + Number(item.priceTyiyn || 0) * Math.max(1, Number(item.quantity || 1)), 0);
        if (totalNode) totalNode.textContent = `Итого: ${fmt(total)}`;
        syncCatalogSelection();
        if (typeof window.medclinicValidateAppointmentForm === "function") window.medclinicValidateAppointmentForm();
    };

    searchInput?.addEventListener("input", renderCatalog);
    document.querySelectorAll('input[name="appointmentPaymentChoice"]').forEach((input) => {
        input.addEventListener("change", syncPaymentHidden);
    });

    syncPaymentHidden();
    renderCatalog();

    document.getElementById("appointmentEditorModal")?.addEventListener("shown.bs.modal", () => {
        syncPaymentHidden();
        renderCatalog();
        window.medclinicRenderSelectedServices?.();
    });

    window.medclinicAppointmentCreate = { refreshCatalog: renderCatalog, syncPayment: syncPaymentHidden };
})();
