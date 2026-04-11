(function () {
    const p = window.medclinicSchedule || {};
    const s = {
        view: "week",
        anchor: new Date(),
        events: (p.events || []).map((e) => ({
            ...e,
            start: new Date(e.start),
            end: new Date(e.end),
            services: (e.services || []).map((x) => ({ ...x, priceTyiyn: Number(x.priceTyiyn || 0), quantity: Number(x.quantity || 1) }))
        })),
        serviceCatalog: p.serviceCatalog || [],
        doctorSchedules: p.doctorSchedules || [],
        doctorScheduleOverrides: p.doctorScheduleOverrides || [],
        departments: p.departments || [],
        doctorFilters: (p.doctorFilters || []).map((x) => ({ ...x, departmentIds: Array.isArray(x.departmentIds) ? x.departmentIds : [] })),
        selectedDepartmentIds: new Set((p.departments || []).map((x) => x.value)),
        selectedDoctorIds: new Set((p.doctorFilters || []).map((x) => x.value)),
        selectedServices: [],
        doctorSchedulesReady: !!p.doctorSchedulesReady,
        scheduleValid: true,
        selectedDate: null,
        selectedEvent: null
    };

    const canManageAppointments = p.canManageAppointments !== false;
    const canGenerateInvoice = p.canGenerateInvoice !== false;
    window.medclinicScheduleState = s;

    const $ = (id) => document.getElementById(id);
    const grid = $("scheduleCalendarGrid");
    const label = $("schedulePeriodLabel");
    if (!grid || !label) return;

    const editor = $("appointmentEditorModal") ? new bootstrap.Modal($("appointmentEditorModal")) : null;
    const periodModal = $("appointmentPeriodModal") ? new bootstrap.Modal($("appointmentPeriodModal")) : null;
    const detailsModal = $("appointmentDetailsModal") ? new bootstrap.Modal($("appointmentDetailsModal")) : null;

    const esc = (v) => String(v ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    const fm = (v) => `${new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format((Number(v) || 0) / 100)} c`;
    const fd = (d, o) => new Intl.DateTimeFormat("ru-RU", o).format(d);
    const ft = (d) => new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit" }).format(d);
    const sameDay = (a, b) => a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
    const monthAnchor = (d) => new Date(d.getFullYear(), d.getMonth(), 1);
    const timeM = (v) => v && v.includes(":") ? (Number(v.split(":")[0]) * 60 + Number(v.split(":")[1])) : null;
    const timeS = (m) => `${Math.floor(m / 60).toString().padStart(2, "0")}:${Math.floor(m % 60).toString().padStart(2, "0")}`;
    const weekdayNames = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

    function toIsoDate(value) {
        const d = new Date(value);
        const month = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${d.getFullYear()}-${month}-${day}`;
    }

    function dayOfWeek(dateString) { const d = new Date(dateString).getDay(); return d === 0 ? 7 : d; }
    function scheduleDoctorId(x) { return x?.doctorId || x?.DoctorId || ""; }
    function scheduleDay(x) { return Number(x?.dayOfWeek ?? x?.DayOfWeek); }
    function scheduleStart(x) { return x?.startTime || x?.StartTime || ""; }
    function scheduleEnd(x) { return x?.endTime || x?.EndTime || ""; }
    function overrideDoctorId(x) { return x?.doctorId || x?.DoctorId || x?.userId || x?.UserId || ""; }
    function overrideDate(x) { return x?.workDate || x?.WorkDate || ""; }
    function overrideWorking(x) { return !!(x?.isWorking ?? x?.IsWorking); }
    function overrideStart(x) { return x?.startTime || x?.StartTime || ""; }
    function overrideEnd(x) { return x?.endTime || x?.EndTime || ""; }
    function doctorDeps(id) { return s.doctorFilters.find((x) => x.value === id)?.departmentIds || []; }
    function visibleDeps() { const q = (($("appointmentsDepartmentSearch")?.value) || "").trim().toLowerCase(); return s.departments.filter((x) => !q || (x.label || "").toLowerCase().includes(q)); }
    function visibleDoctors() { const q = (($("appointmentsDoctorSearch")?.value) || "").trim().toLowerCase(); return s.doctorFilters.filter((x) => (!q || (x.label || "").toLowerCase().includes(q)) && (!s.selectedDepartmentIds.size || x.departmentIds.some((id) => s.selectedDepartmentIds.has(id)))); }
    function filteredEvents() { return s.events.filter((x) => (!s.selectedDoctorIds.size || s.selectedDoctorIds.has(x.doctorId)) && (!s.selectedDepartmentIds.size || doctorDeps(x.doctorId).some((id) => s.selectedDepartmentIds.has(id)))); }
    function eventsForDate(date) { return filteredEvents().filter((x) => sameDay(x.start, date)).sort((a, b) => a.start - b.start); }

    function setMenuState(name, open) {
        const root = document.querySelector(`[data-filter-root="${name}"]`);
        const menu = $(`appointments${name === "departments" ? "Departments" : "Doctors"}Menu`);
        const trigger = $(`appointments${name === "departments" ? "Departments" : "Doctors"}Trigger`);
        if (!root || !menu || !trigger) return;
        root.classList.toggle("is-open", open);
        menu.hidden = !open;
        trigger.setAttribute("aria-expanded", open ? "true" : "false");
    }

    function closeMenus() { setMenuState("departments", false); setMenuState("doctors", false); }

    function updateSummaries() {
        const depSummary = $("appointmentsDepartmentsSummary");
        const docSummary = $("appointmentsDoctorsSummary");
        if (depSummary) depSummary.textContent = s.selectedDepartmentIds.size === s.departments.length ? "Выбраны все" : s.selectedDepartmentIds.size ? `Выбрано: ${s.selectedDepartmentIds.size}` : "Не выбрано";
        if (docSummary) docSummary.textContent = s.selectedDoctorIds.size === s.doctorFilters.length ? "Выбраны все" : s.selectedDoctorIds.size ? `Выбрано: ${s.selectedDoctorIds.size}` : "Не выбрано";
    }

    function renderToolbarDateControls() {
        const strip = $("appointmentsAnchorStrip");
        const quickCards = $("appointmentsQuickCards");
        if (!strip || !quickCards) return;

        const shortDate = new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "short" });
        const stripButtons = [];
        for (let offset = -2; offset <= 2; offset++) {
            const date = new Date(s.anchor);
            date.setDate(s.anchor.getDate() + offset);
            const isActive = sameDay(date, s.anchor);
            stripButtons.push(`<button type="button" class="appointments-anchor-strip__item${isActive ? " is-active" : ""}" data-anchor-date="${toIsoDate(date)}">${esc(shortDate.format(date))}</button>`);
        }
        strip.innerHTML = stripButtons.join("");

        const quickItems = [{ label: "Вчера", delta: -1 }, { label: "Сегодня", delta: 0 }, { label: "Завтра", delta: 1 }];
        quickCards.innerHTML = quickItems.map((item) => {
            const date = new Date();
            date.setDate(date.getDate() + item.delta);
            const isActive = sameDay(date, s.anchor);
            const count = eventsForDate(date).length;
            return `<button type="button" class="appointments-quick-cards__item${isActive ? " is-active" : ""}" data-anchor-date="${toIsoDate(date)}"><span class="appointments-quick-cards__label">${item.label}</span><span class="appointments-quick-cards__date">${esc(shortDate.format(date))}</span><span class="appointments-quick-cards__meta">${count} прием(ов)</span></button>`;
        }).join("");
    }

    function doctorSchedule(doctorId, dateValue) {
        if (!doctorId || !dateValue || !s.doctorSchedulesReady) return null;
        const ov = s.doctorScheduleOverrides.find((x) => overrideDoctorId(x) === doctorId && overrideDate(x) === dateValue);
        if (ov) return overrideWorking(ov) ? { isWorking: true, startTime: overrideStart(ov), endTime: overrideEnd(ov), isOverride: true } : { isWorking: false, isOverride: true };
        const base = s.doctorSchedules.find((x) => scheduleDoctorId(x) === doctorId && scheduleDay(x) === dayOfWeek(dateValue));
        return base ? { isWorking: true, startTime: scheduleStart(base), endTime: scheduleEnd(base), isOverride: false } : null;
    }

    function setHint(msg) {
        const hint = $("appointmentScheduleHint");
        if (!hint) return;
        hint.style.display = msg ? "" : "none";
        hint.textContent = msg || "";
    }

    function buildSlots(sc) {
        const box = $("appointmentSlotButtons");
        if (!box) return;
        if (!sc) { box.innerHTML = '<span class="text-muted small">Выберите врача и дату.</span>'; return; }
        if (!sc.isWorking) { box.innerHTML = '<span class="text-muted small">На выбранную дату у врача выходной.</span>'; return; }
        const start = timeM(sc.startTime), end = timeM(sc.endTime);
        if (start == null || end == null || end <= start) { box.innerHTML = '<span class="text-muted small">Нет доступных слотов.</span>'; return; }
        const html = [];
        for (let m = start; m + 30 <= end; m += 30) html.push(`<button type="button" class="btn btn-outline-secondary btn-sm slot-button" data-start="${timeS(m)}" data-end="${timeS(m + 30)}">${timeS(m)}</button>`);
        box.innerHTML = html.join("");
        box.querySelectorAll(".slot-button").forEach((b) => b.addEventListener("click", () => {
            $("appointmentStart").value = b.dataset.start || "09:00";
            $("appointmentEnd").value = b.dataset.end || "09:30";
            syncScheduleState();
        }));
    }

    function syncScheduleState() {
        if (!p.storageReady) { s.scheduleValid = false; setHint("Сначала примените SQL-скрипт appointments."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        if (!s.doctorSchedulesReady) { s.scheduleValid = true; setHint("Таблица смен еще не создана."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = false; return; }
        const doctorId = $("appointmentDoctor")?.value || "", dateValue = $("appointmentDate")?.value || "", startValue = $("appointmentStart")?.value || "", endValue = $("appointmentEnd")?.value || "";
        if (!doctorId) { s.scheduleValid = false; buildSlots(null); setHint("Сначала выберите врача."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        if (!dateValue) { s.scheduleValid = false; buildSlots(null); setHint("Укажите дату приема."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        const sc = doctorSchedule(doctorId, dateValue); buildSlots(sc);
        if (!sc) { s.scheduleValid = false; setHint("У выбранного врача на этот день нет активной смены. Настройте ее на странице врача."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        if (!sc.isWorking) { s.scheduleValid = false; setHint("На выбранную дату у врача отмечен выходной."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        const a = timeM(startValue), b = timeM(endValue), c = timeM(sc.startTime), d = timeM(sc.endTime);
        if (a == null || b == null || c == null || d == null) { s.scheduleValid = false; setHint("Укажите корректное время приема."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        if (b <= a) { s.scheduleValid = false; setHint("Время окончания должно быть позже времени начала."); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        if (a < c || b > d) { s.scheduleValid = false; setHint(`Запись можно создать только в рамках смены ${sc.startTime} - ${sc.endTime}.`); if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = true; return; }
        s.scheduleValid = true;
        setHint(`Смена врача: ${sc.startTime} - ${sc.endTime}.`);
        if ($("appointmentSaveButton")) $("appointmentSaveButton").disabled = false;
    }

    function renderServices() {
        const tb = $("appointmentServicesTable");
        if (!tb) return;
        if (!s.selectedServices.length) { tb.innerHTML = '<tr class="appointment-services-empty"><td colspan="4" class="text-center text-muted py-4">Пока услуги не выбраны.</td></tr>'; return; }
        tb.innerHTML = s.selectedServices.map((x, i) => `<tr data-service-index="${i}"><td>${esc(x.serviceName)}</td><td>${fm(x.priceTyiyn)}</td><td><input type="number" min="1" class="form-control form-control-sm appointment-service-qty" value="${x.quantity}" data-service-index="${i}" /></td><td class="text-end"><button type="button" class="btn btn-sm btn-outline-danger appointment-service-remove" data-service-index="${i}">Удалить</button></td></tr>`).join("");
        tb.querySelectorAll(".appointment-service-qty").forEach((i) => i.addEventListener("input", () => { const idx = Number(i.dataset.serviceIndex); s.selectedServices[idx].quantity = Math.max(1, Number(i.value || 1)); }));
        tb.querySelectorAll(".appointment-service-remove").forEach((b) => b.addEventListener("click", () => { s.selectedServices.splice(Number(b.dataset.serviceIndex), 1); renderServices(); }));
    }

    function addService(serviceId) {
        if (!serviceId) return;
        const existing = s.selectedServices.find((x) => x.organizationServiceId === serviceId);
        if (existing) { existing.quantity += 1; renderServices(); return; }
        const item = s.serviceCatalog.find((x) => x.id === serviceId);
        if (!item) return;
        s.selectedServices.push({ organizationServiceId: item.id, serviceName: item.name, priceTyiyn: Number(item.priceTyiyn || 0), quantity: 1 });
        renderServices();
    }

    function defaultDoctor() { return s.selectedDoctorIds.size === 1 ? Array.from(s.selectedDoctorIds)[0] : ""; }

    function resetForm(slotDate, doctorId) {
        ["appointmentId", "appointmentPatientId", "appointmentPatientName", "appointmentPhone", "appointmentEmail", "appointmentComment", "appointmentReferral"].forEach((id) => { const n = $(id); if (n) n.value = ""; });
        if ($("appointmentPatient")) $("appointmentPatient").selectedIndex = 0;
        $("appointmentDoctor").value = doctorId || "";
        $("appointmentPaymentType").value = "cash";
        $("appointmentStatus").value = "active";
        $("appointmentIsActive").checked = true;
        $("appointmentServiceSelector").value = "";
        const dt = slotDate || new Date();
        $("appointmentDate").value = dt.toISOString().slice(0, 10);
        $("appointmentStart").value = `${dt.getHours().toString().padStart(2, "0")}:${dt.getMinutes().toString().padStart(2, "0")}`;
        const end = new Date(dt); end.setMinutes(end.getMinutes() + 30);
        $("appointmentEnd").value = `${end.getHours().toString().padStart(2, "0")}:${end.getMinutes().toString().padStart(2, "0")}`;
        s.selectedServices = [];
        renderServices();
        syncScheduleState();
    }

    function fillForm(ev) {
        $("appointmentId").value = ev.id || "";
        $("appointmentPatient").value = ev.patientId || "";
        $("appointmentPatientId").value = ev.patientId || "";
        $("appointmentPatientName").value = ev.patientName || "";
        $("appointmentDoctor").value = ev.doctorId || "";
        $("appointmentPhone").value = ev.phone || "";
        $("appointmentEmail").value = ev.email || "";
        $("appointmentComment").value = ev.notes || "";
        $("appointmentDate").value = ev.start.toISOString().slice(0, 10);
        $("appointmentStart").value = ev.start.toTimeString().slice(0, 5);
        $("appointmentEnd").value = ev.end.toTimeString().slice(0, 5);
        $("appointmentReferral").value = ev.referralSource || "";
        $("appointmentPaymentType").value = ev.paymentType || "cash";
        $("appointmentStatus").value = ev.status || "active";
        $("appointmentIsActive").checked = ev.isActive !== false;
        $("appointmentServiceSelector").value = "";
        s.selectedServices = (ev.services || []).map((x) => ({ organizationServiceId: x.organizationServiceId || null, serviceName: x.name || "Услуга", priceTyiyn: Number(x.priceTyiyn || 0), quantity: Number(x.quantity || 1) }));
        renderServices();
        syncScheduleState();
    }

    function openEditor(ev, slotDate, doctorId) {
        if (!canManageAppointments) return;
        ev ? fillForm(ev) : resetForm(slotDate, doctorId || defaultDoctor());
        periodModal?.hide();
        detailsModal?.hide();
        editor?.show();
    }

    function renderFilterLists() {
        const dl = $("appointmentsDepartmentsList"), ul = $("appointmentsDoctorsList");
        if (dl) {
            const items = visibleDeps();
            dl.innerHTML = items.length ? items.map((x) => `<label class="filter-option"><input type="checkbox" class="filter-option__checkbox" value="${esc(x.value)}" ${s.selectedDepartmentIds.has(x.value) ? "checked" : ""}><span class="filter-option__label">${esc(x.label)}</span></label>`).join("") : '<div class="filter-empty">Ничего не найдено.</div>';
            dl.querySelectorAll(".filter-option__checkbox").forEach((i) => i.addEventListener("change", () => { i.checked ? s.selectedDepartmentIds.add(i.value) : s.selectedDepartmentIds.delete(i.value); updateSummaries(); renderFilterLists(); render(); }));
        }
        if (ul) {
            const items = visibleDoctors();
            ul.innerHTML = items.length ? items.map((x) => `<label class="filter-option"><input type="checkbox" class="filter-option__checkbox" value="${esc(x.value)}" ${s.selectedDoctorIds.has(x.value) ? "checked" : ""}><span class="filter-option__label">${esc(x.label)}</span></label>`).join("") : '<div class="filter-empty">Нет сотрудников для выбранных отделений.</div>';
            ul.querySelectorAll(".filter-option__checkbox").forEach((i) => i.addEventListener("change", () => { i.checked ? s.selectedDoctorIds.add(i.value) : s.selectedDoctorIds.delete(i.value); updateSummaries(); render(); }));
        }
    }

    function openPeriod(date) {
        s.selectedDate = new Date(date);
        const list = eventsForDate(s.selectedDate);
        const pl = $("appointmentPeriodList");
        $("appointmentPeriodModalTitle").textContent = `Приемы на ${fd(s.selectedDate, { day: "numeric", month: "long", year: "numeric" })}`;
        $("appointmentPeriodModalSubtitle").textContent = list.length ? `Найдено записей: ${list.length}` : "На выбранную дату записей пока нет.";
        if (pl) {
            pl.innerHTML = list.length ? list.map((ev) => `<button type="button" class="period-appointment-card" data-event-id="${esc(ev.id)}"><div class="period-appointment-card__time">${ft(ev.start)} - ${ft(ev.end)}</div><div class="period-appointment-card__body"><div class="period-appointment-card__title">${esc(ev.patientName || ev.title)}</div><div class="period-appointment-card__meta">${esc(ev.doctor || "Без врача")} · ${esc(ev.status || "active")}</div></div></button>`).join("") : '<div class="period-list-empty"><div class="period-list-empty__title">Записей пока нет</div><div class="text-muted small">Вы можете сразу добавить новый прием на выбранную дату.</div></div>';
            pl.querySelectorAll(".period-appointment-card").forEach((b) => b.addEventListener("click", () => { const ev = s.events.find((x) => x.id === b.dataset.eventId); if (ev) openDetails(ev); }));
        }
        periodModal?.show();
    }

    function openDetails(ev) {
        s.selectedEvent = ev;
        $("appointmentDetailsTitle").textContent = ev.patientName || "Просмотр приема";
        $("appointmentDetailsSubtitle").textContent = `${fd(ev.start, { day: "numeric", month: "long", year: "numeric" })} · ${ft(ev.start)} - ${ft(ev.end)}`;
        const set = (id, v, fb = "—") => { const n = $(id); if (n) n.textContent = v || fb; };
        set("appointmentDetailsPatient", ev.patientName);
        set("appointmentDetailsPhone", ev.phone);
        set("appointmentDetailsEmail", ev.email);
        set("appointmentDetailsDoctor", ev.doctor);
        set("appointmentDetailsDateTime", `${fd(ev.start, { day: "numeric", month: "long", year: "numeric" })}, ${ft(ev.start)} - ${ft(ev.end)}`);
        set("appointmentDetailsStatus", ev.status);
        set("appointmentDetailsPaymentType", ev.paymentType);
        set("appointmentDetailsReferral", ev.referralSource);
        set("appointmentDetailsNotes", ev.notes);
        const tb = $("appointmentDetailsServices");
        if (tb) tb.innerHTML = (ev.services || []).length ? ev.services.map((x) => `<tr><td>${esc(x.name)}</td><td>${fm(x.priceTyiyn)}</td><td>${x.quantity}</td></tr>`).join("") : '<tr><td colspan="3" class="text-center text-muted py-4">Услуги не выбраны.</td></tr>';
        detailsModal?.show();
    }

    async function generateInvoice() {
        if (!p.generateInvoiceUrl || !s.selectedEvent?.id) { window.alert("Маршрут генерации счета не настроен."); return; }
        const btn = $("appointmentGenerateInvoiceButton"), text = btn?.textContent || "Счет на оплату";
        if (btn) { btn.disabled = true; btn.textContent = "Формирование..."; }
        try {
            const r = await fetch(p.generateInvoiceUrl, { method: "POST", headers: { "Content-Type": "application/json", "X-Requested-With": "XMLHttpRequest" }, body: JSON.stringify({ appointmentId: s.selectedEvent.id }) });
            const j = await r.json();
            if (!r.ok || !j.success) throw new Error(j.message || "Не удалось сформировать счет.");
            if (j.url) { window.location.href = j.url; return; }
            window.alert(j.message || "Счет сформирован.");
        } catch (e) { window.alert(e.message || "Не удалось сформировать счет."); }
        finally { if (btn) { btn.disabled = false; btn.textContent = text; } }
    }

    function renderDay() {
        grid.className = "schedule-calendar__grid schedule-grid--day";
        label.textContent = fd(s.anchor, { weekday: "long", day: "numeric", month: "long", year: "numeric" });
        const list = eventsForDate(s.anchor);
        grid.innerHTML = Array.from({ length: 10 }, (_, i) => 8 + i).map((h) => {
            const items = list.filter((x) => x.start.getHours() === h);
            return `<div class="schedule-time-label">${String(h).padStart(2, "0")}:00</div><div class="schedule-slot-card schedule-slot-card--stack ${items.length ? "schedule-slot-card--busy" : "schedule-slot-card--available"}" data-hour="${h}">${items.length ? items.map((ev) => `<button type="button" class="schedule-event-chip schedule-event-chip--${esc(ev.status)}" data-event-id="${esc(ev.id)}"><strong>${ft(ev.start)}</strong> ${esc(ev.patientName || ev.title)}</button>`).join("") : '<div class="schedule-slot-card__meta">Свободное окно</div>'}</div>`;
        }).join("");
        grid.querySelectorAll(".schedule-event-chip").forEach((b) => b.addEventListener("click", (e) => {
            e.stopPropagation();
            const ev = s.events.find((x) => x.id === b.dataset.eventId);
            if (ev) openDetails(ev);
        }));
        grid.querySelectorAll(".schedule-slot-card").forEach((card) => card.addEventListener("click", () => {
            if (!canManageAppointments) return;
            const hour = Number(card.dataset.hour);
            const slotDate = new Date(s.anchor);
            if (!Number.isNaN(hour)) slotDate.setHours(hour, 0, 0, 0); else slotDate.setHours(9, 0, 0, 0);
            openEditor(null, slotDate, defaultDoctor());
        }));
    }

    function renderCalendarDayCell(date, items, options) {
        const opts = options || {};
        const isToday = sameDay(date, new Date());
        const isMuted = !!opts.muted;
        const maxItems = opts.maxItems || 4;
        const visible = items.slice(0, maxItems);
        const more = items.length > maxItems ? `<div class="appointments-day-cell__more">+${items.length - maxItems} еще</div>` : "";
        const weekday = fd(date, { weekday: "short" });
        const cellClass = `appointments-day-cell schedule-day-card--clickable${isToday ? " appointments-day-cell--today" : ""}${isMuted ? " appointments-day-cell--muted" : ""}`;

        return `
            <button type="button" class="${cellClass}" data-date="${date.toISOString()}">
                <div class="appointments-day-cell__head">
                    <div class="appointments-day-cell__day">${date.getDate()}</div>
                    <div class="appointments-day-cell__weekday">${esc(weekday)}</div>
                </div>
                <div class="appointments-day-cell__meta">
                    <span class="appointments-day-cell__count-badge">${items.length} прием(ов)</span>
                </div>
                <div class="appointments-day-cell__events">
                    ${visible.length
                        ? visible.map((ev) => `<div class="appointments-day-event"><div class="appointments-day-event__time">${ft(ev.start)} - ${ft(ev.end)}</div><div class="appointments-day-event__title">${esc(ev.title || "Прием")}</div><div class="appointments-day-event__patient">${esc(ev.patientName || "Пациент не указан")}</div></div>`).join("")
                        : '<div class="appointments-day-cell__empty">Нет записей</div>'}
                    ${more}
                </div>
            </button>
        `;
    }

    function renderWeek() {
        grid.className = "schedule-calendar__grid schedule-grid--week";
        const start = new Date(s.anchor); start.setDate(s.anchor.getDate() - ((s.anchor.getDay() + 6) % 7));
        const end = new Date(start); end.setDate(start.getDate() + 6);
        label.textContent = `${fd(start, { day: "numeric", month: "long" })} - ${fd(end, { day: "numeric", month: "long", year: "numeric" })}`;
        grid.innerHTML = Array.from({ length: 7 }, (_, i) => { const d = new Date(start); d.setDate(start.getDate() + i); return renderCalendarDayCell(d, eventsForDate(d), { maxItems: 5 }); }).join("");
        grid.querySelectorAll(".appointments-day-cell").forEach((c) => c.addEventListener("click", () => { const d = new Date(c.dataset.date); d.setHours(9, 0, 0, 0); openPeriod(d); }));
    }

    function renderMonth() {
        grid.className = "schedule-calendar__grid schedule-grid--month";
        label.textContent = fd(s.anchor, { month: "long", year: "numeric" });
        const ms = monthAnchor(s.anchor), me = new Date(s.anchor.getFullYear(), s.anchor.getMonth() + 1, 0), start = new Date(ms);
        start.setDate(ms.getDate() - ((ms.getDay() + 6) % 7));
        const heads = weekdayNames.map((x) => `<div class="schedule-weekday-header">${x}</div>`).join("");
        const cells = Array.from({ length: 42 }, (_, i) => { const d = new Date(start); d.setDate(start.getDate() + i); return renderCalendarDayCell(d, eventsForDate(d), { maxItems: 3, muted: d < ms || d > me }); }).join("");
        grid.innerHTML = heads + cells;
        grid.querySelectorAll(".appointments-day-cell").forEach((c) => c.addEventListener("click", () => { const d = new Date(c.dataset.date); d.setHours(9, 0, 0, 0); openPeriod(d); }));
    }

    function render() {
        if (s.view === "month") renderMonth();
        else if (s.view === "week") renderWeek();
        else renderDay();
        renderToolbarDateControls();
    }

    document.querySelectorAll(".schedule-view-switcher [data-view]").forEach((b) => b.addEventListener("click", () => {
        s.view = b.dataset.view || "week";
        document.querySelectorAll(".schedule-view-switcher [data-view]").forEach((x) => { x.classList.remove("active", "btn-primary"); x.classList.add("btn-outline-primary"); });
        b.classList.add("active", "btn-primary");
        b.classList.remove("btn-outline-primary");
        if (s.view === "month") s.anchor = monthAnchor(s.anchor);
        render();
    }));

    document.querySelectorAll(".schedule-nav").forEach((b) => b.addEventListener("click", () => {
        const d = b.dataset.direction;
        if (d === "today") s.anchor = s.view === "month" ? monthAnchor(new Date()) : new Date();
        else if (d === "prev") s.anchor = s.view === "month" ? new Date(s.anchor.getFullYear(), s.anchor.getMonth() - 1, 1) : new Date(s.anchor.getFullYear(), s.anchor.getMonth(), s.anchor.getDate() - 7);
        else if (d === "next") s.anchor = s.view === "month" ? new Date(s.anchor.getFullYear(), s.anchor.getMonth() + 1, 1) : new Date(s.anchor.getFullYear(), s.anchor.getMonth(), s.anchor.getDate() + 7);
        render();
    }));

    document.addEventListener("click", (e) => {
        const btn = e.target.closest("[data-anchor-date]");
        if (!btn) return;
        const v = btn.getAttribute("data-anchor-date");
        if (!v) return;
        const d = new Date(`${v}T00:00:00`);
        if (Number.isNaN(d.getTime())) return;
        s.anchor = d;
        render();
    });

    $("appointmentServiceSelector")?.addEventListener("change", (e) => { addService(e.target.value); e.target.value = ""; });
    ["appointmentDoctor", "appointmentDate", "appointmentStart", "appointmentEnd"].forEach((id) => { $(id)?.addEventListener("change", syncScheduleState); $(id)?.addEventListener("input", syncScheduleState); });
    $("appointmentsDepartmentSearch")?.addEventListener("input", renderFilterLists);
    $("appointmentsDoctorSearch")?.addEventListener("input", renderFilterLists);
    $("appointmentsSelectAllDepartments")?.addEventListener("click", () => { s.selectedDepartmentIds = new Set(s.departments.map((x) => x.value)); updateSummaries(); renderFilterLists(); render(); });
    $("appointmentsSelectAllDoctors")?.addEventListener("click", () => { s.selectedDoctorIds = new Set(s.doctorFilters.map((x) => x.value)); updateSummaries(); renderFilterLists(); render(); });
    $("appointmentsDepartmentsTrigger")?.addEventListener("click", (e) => { e.stopPropagation(); const open = $("appointmentsDepartmentsMenu")?.hidden !== false; closeMenus(); setMenuState("departments", open); });
    $("appointmentsDoctorsTrigger")?.addEventListener("click", (e) => { e.stopPropagation(); const open = $("appointmentsDoctorsMenu")?.hidden !== false; closeMenus(); setMenuState("doctors", open); });
    document.addEventListener("click", (e) => { if (!e.target.closest(".appointments-multiselect")) closeMenus(); });
    $("appointmentPeriodAddButton")?.addEventListener("click", () => { const d = s.selectedDate ? new Date(s.selectedDate) : new Date(); d.setHours(9, 0, 0, 0); openEditor(null, d, defaultDoctor()); });
    $("appointmentDetailsEditButton")?.addEventListener("click", () => { if (s.selectedEvent) openEditor(s.selectedEvent, s.selectedEvent.start); });
    if (canGenerateInvoice) $("appointmentGenerateInvoiceButton")?.addEventListener("click", generateInvoice);

    buildSlots(null);
    syncScheduleState();
    updateSummaries();
    renderFilterLists();
    render();
})();
