(function () {
    const p = window.medclinicSchedule || {};
    const pageType = p.pageType || "registry";
    const canManageAppointments = p.canManageAppointments !== false;
    const canGenerateInvoice = p.canGenerateInvoice !== false;
    const $ = (id) => document.getElementById(id);

    const doctorGrid = $("scheduleCalendarGrid");
    const registryGrid = $("gridView");
    if (pageType === "doctor" && !doctorGrid) return;
    if (pageType === "registry" && !registryGrid) return;

    const state = {
        pageType,
        view: "day",
        anchor: new Date(),
        activeTab: "grid",
        selectedEvent: null,
        selectedDate: null,
        selectedServices: [],
        doctorSchedulesReady: !!p.doctorSchedulesReady,
        events: (p.events || []).map((e) => ({
            ...e,
            start: new Date(e.start),
            end: new Date(e.end),
            services: (e.services || []).map((x) => ({ ...x, priceTyiyn: Number(x.priceTyiyn || 0), quantity: Number(x.quantity || 1) }))
        })),
        serviceCatalog: p.serviceCatalog || [],
        appointmentDurations: p.appointmentDurations || p.doctorAppointmentDurations || [],
        doctorSchedules: p.doctorSchedules || [],
        doctorScheduleOverrides: p.doctorScheduleOverrides || [],
        doctorBreaks: p.doctorBreaks || [],
        doctorBlocks: p.doctorBlocks || [],
        medicalTemplates: p.medicalTemplates || [],
        departments: p.departments || [],
        doctorFilters: p.doctorFilters || [],
        selectedDepartmentIds: new Set((p.departments || []).map((x) => x.value)),
        selectedDoctorIds: new Set((p.doctorFilters || []).map((x) => x.value)),
        paymentFilter: "all"
    };

    window.medclinicScheduleState = state;

    const periodModal = $("appointmentPeriodModal") ? new bootstrap.Modal($("appointmentPeriodModal")) : null;
    const detailsModal = $("appointmentDetailsModal") ? new bootstrap.Modal($("appointmentDetailsModal")) : null;
    const editorModal = $("appointmentEditorModal") ? new bootstrap.Modal($("appointmentEditorModal")) : null;

    const esc = (v) => String(v ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    const ft = (d) => new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit" }).format(d);
    const fd = (d, o) => new Intl.DateTimeFormat("ru-RU", o).format(d);
    const fm = (v) => `${new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(v) || 0)} c`;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    const weekdays = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

    function addDays(date, days) { const d = new Date(date); d.setDate(d.getDate() + days); return d; }
    function monthAnchor(date) { return new Date(date.getFullYear(), date.getMonth(), 1); }
    function sameDay(a, b) { return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate(); }
    function startOfWeek(date) { const d = new Date(date); const shift = (d.getDay() + 6) % 7; d.setDate(d.getDate() - shift); d.setHours(0, 0, 0, 0); return d; }
    function toIsoDate(value) { const d = new Date(value); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; }
    function timeToMin(v) { if (!v || !String(v).includes(":")) return null; const [h, m] = String(v).split(":").map(Number); return h * 60 + m; }
    function minutesToTime(v) { return `${String(Math.floor(v / 60)).padStart(2, "0")}:${String(v % 60).padStart(2, "0")}`; }
    function paymentClass(ev) {
        if (ev.hasPaidInvoice) return "paid";
        const t = (ev.paymentType || "").toLowerCase();
        return (!t || t === "unpaid" || t === "none") ? "unpaid" : "paid";
    }
    function paymentText(ev) { return paymentClass(ev) === "paid" ? "Оплачено" : "Не оплачено"; }
    function serviceText(ev) { return (ev.services || []).map((x) => x.name).filter(Boolean).join(", ") || (ev.title || "Прием"); }
    function eventTitle(ev) { return ev.patientName || ev.title || "Прием"; }
    function doctorMeta(doctorId) {
        const item = state.doctorFilters.find((x) => x.value === doctorId) || {};
        const deptIds = item.departmentIds || [];
        const deptNames = deptIds.map((id) => state.departments.find((d) => d.value === id)?.label).filter(Boolean);
        return { id: item.value || doctorId, name: item.label || "Не назначен", subtitle: deptNames.join(" · ") || "Без отделения" };
    }
    function getDoctorDefaultReferral(doctorId) {
        const item = state.doctorFilters.find((x) => x.value === doctorId) || {};
        return item.defaultReferral || item.referral || item.cabinet || "";
    }
    function resolveDoctorIdForEditor() {
        const selected = $("appointmentDoctor")?.value || "";
        if (selected) return selected;
        if (pageType === "doctor") return p.currentDoctorId || "";
        return "";
    }
    function durationForDoctor(doctorId) {
        const item = state.appointmentDurations.find((x) => x.doctorId === doctorId);
        const value = Number(item?.durationMinutes || 30);
        return value > 0 ? value : 30;
    }
    function applyDoctorDefaults(doctorId) {
        const referral = $("appointmentReferral");
        if (!doctorId || !referral) return;
        if ((referral.value || "").trim().length > 0) return;
        const defaultReferral = getDoctorDefaultReferral(doctorId);
        if (defaultReferral) referral.value = defaultReferral;
    }
    function selectedDoctors() {
        if (pageType === "doctor") return state.doctorFilters;
        return state.doctorFilters
            .filter((doc) => state.selectedDoctorIds.has(doc.value))
            .filter((doc) => !doc.departmentIds?.length || doc.departmentIds.some((id) => state.selectedDepartmentIds.has(id)));
    }
    function eventMatchesPeriod(ev) {
        if (state.view === "day") return sameDay(ev.start, state.anchor);
        if (state.view === "week") { const s = startOfWeek(state.anchor); const e = addDays(s, 7); return ev.start >= s && ev.start < e; }
        return ev.start.getFullYear() === state.anchor.getFullYear() && ev.start.getMonth() === state.anchor.getMonth();
    }
    function filteredEvents() {
        let items = state.events.filter(eventMatchesPeriod);
        if (pageType === "registry") {
            const doctorIds = new Set(selectedDoctors().map((x) => x.value));
            items = items.filter((x) => !x.doctorId || doctorIds.has(x.doctorId));
            if (state.paymentFilter !== "all") items = items.filter((x) => paymentClass(x) === state.paymentFilter);
        }
        return items.sort((a, b) => a.start - b.start);
    }
    function eventsForDate(date) { return filteredEvents().filter((x) => sameDay(x.start, date)).sort((a, b) => a.start - b.start); }
    function allHours() { return Array.from({ length: 10 }, (_, i) => 8 + i); }
    function setPeriodLabel() {
        const label = $("schedulePeriodLabel");
        if (!label) return;
        if (state.view === "day") { label.textContent = fd(state.anchor, { weekday: "long", day: "numeric", month: "long" }); return; }
        if (state.view === "week") {
            const s = startOfWeek(state.anchor); const e = addDays(s, 6);
            label.textContent = `${fd(s, { day: "numeric", month: "long" })} — ${fd(e, { day: "numeric", month: "long" })}`;
            return;
        }
        label.textContent = fd(state.anchor, { month: "long", year: "numeric" });
    }
    function syncDatePicker() { const picker = $("scheduleDatePicker"); if (picker) picker.value = toIsoDate(state.anchor); }
    function setHint(message) { const hint = $("appointmentScheduleHint"); if (hint) { hint.style.display = message ? "" : "none"; hint.textContent = message || ""; } }
    function applyEditorMode(mode) {
        const isVisit = mode === "visit";
        document.querySelectorAll("[data-editor-section]").forEach((node) => {
            const sectionMode = node.getAttribute("data-editor-section") || "";
            if (sectionMode.includes("visit")) {
                node.style.display = isVisit ? "" : "none";
            }
        });

        const editorBody = $("appointmentForm");
        const sideColumn = document.querySelector("#appointmentEditorModal .appointment-editor-side");
        const hasVisibleSideContent = sideColumn
            ? Array.from(sideColumn.children).some((child) => window.getComputedStyle(child).display !== "none")
            : false;
        if (editorBody) {
            editorBody.classList.toggle("appointment-editor-body--single", !hasVisibleSideContent);
        }
    }
    function syncPaymentStatusControl(eventItem) {
        const select = $("appointmentPaymentType");
        const hint = $("appointmentPaymentTypeHint");
        if (!select) return;

        const hasPaidInvoice = !!eventItem?.hasPaidInvoice;
        if (hasPaidInvoice) {
            select.value = "invoice_paid";
            select.disabled = true;
            if (hint) hint.textContent = "Статус оплаты определяется по привязанному оплаченному счету.";
            return;
        }

        if (p.canMarkAsPaidManually === false) {
            select.value = "unpaid";
            select.disabled = true;
            if (hint) hint.textContent = "Статус «Оплачено» доступен только после оплаты счета.";
            return;
        }

        select.disabled = false;
        if (hint) hint.textContent = "Статус оплаты можно менять вручную до создания оплаченного счета.";
    }

    function doctorSchedule(doctorId, dateValue) {
        if (!doctorId || !dateValue || !state.doctorSchedulesReady) return null;
        const ov = state.doctorScheduleOverrides.find((x) => (x.userId || x.UserId) === doctorId && (x.workDate || x.WorkDate) === dateValue);
        if (ov) {
            const isWorking = !!(ov.isWorking ?? ov.IsWorking);
            if (!isWorking) return { isWorking: false };
            return { isWorking: true, startTime: ov.startTime || ov.StartTime, endTime: ov.endTime || ov.EndTime };
        }
        const jsDay = new Date(dateValue).getDay();
        const day = jsDay === 0 ? 7 : jsDay;
        const base = state.doctorSchedules.find((x) => (x.doctorId || x.DoctorId) === doctorId && Number(x.dayOfWeek ?? x.DayOfWeek) === day);
        if (!base) return null;
        return { isWorking: true, startTime: base.startTime || base.StartTime, endTime: base.endTime || base.EndTime };
    }

    function busyIntervalsForDoctorDate(doctorId, dateValue, excludeAppointmentId) {
        if (!doctorId || !dateValue) return [];
        return state.events
            .filter((eventItem) => eventItem.doctorId === doctorId)
            .filter((eventItem) => toIsoDate(eventItem.start) === dateValue)
            .filter((eventItem) => eventItem.id !== excludeAppointmentId)
            .filter((eventItem) => eventItem.isActive !== false)
            .filter((eventItem) => (eventItem.status || "").toLowerCase() !== "cancelled")
            .map((eventItem) => ({
                start: eventItem.start.getHours() * 60 + eventItem.start.getMinutes(),
                end: eventItem.end.getHours() * 60 + eventItem.end.getMinutes()
            }));
    }
    function fixedIntervalsForDoctorDate(source, doctorId, dateValue) {
        if (!doctorId || !dateValue || !Array.isArray(source)) return [];
        return source
            .filter((item) => (item.doctorId || item.userId || item.DoctorId || item.UserId) === doctorId)
            .filter((item) => (item.workDate || item.date || item.WorkDate || item.Date) === dateValue)
            .map((item) => ({
                start: timeToMin(item.startTime || item.StartTime || ""),
                end: timeToMin(item.endTime || item.EndTime || "")
            }))
            .filter((range) => range.start != null && range.end != null && range.end > range.start);
    }
    function unavailableIntervalsForDoctorDate(doctorId, dateValue, excludeAppointmentId) {
        return [
            ...busyIntervalsForDoctorDate(doctorId, dateValue, excludeAppointmentId),
            ...fixedIntervalsForDoctorDate(state.doctorBreaks, doctorId, dateValue),
            ...fixedIntervalsForDoctorDate(state.doctorBlocks, doctorId, dateValue)
        ];
    }

    function buildSlots(schedule) {
        const box = $("appointmentSlotButtons");
        if (!box) return;
        if (!schedule) { box.innerHTML = '<span class="text-muted small">Выберите врача и дату для записи.</span>'; return; }
        if (!schedule.isWorking) { box.innerHTML = '<span class="text-muted small">На выбранный день у врача выходной.</span>'; return; }

        const doctorId = resolveDoctorIdForEditor();
        const dateValue = $("appointmentDate")?.value || "";
        const currentAppointmentId = $("appointmentId")?.value || "";
        const duration = durationForDoctor(doctorId);
        const start = timeToMin(schedule.startTime);
        const end = timeToMin(schedule.endTime);
        if (start == null || end == null || end <= start) { box.innerHTML = '<span class="text-muted small">Нет доступных слотов.</span>'; return; }

        const unavailable = unavailableIntervalsForDoctorDate(doctorId, dateValue, currentAppointmentId);
        const html = [];
        for (let m = start; m + duration <= end; m += duration) {
            const overlapsBusy = unavailable.some((interval) => m < interval.end && (m + duration) > interval.start);
            if (overlapsBusy) continue;

            const s = minutesToTime(m); const e = minutesToTime(m + duration);
            html.push(`<button type="button" class="btn btn-outline-secondary btn-sm slot-button" data-start="${s}" data-end="${e}">${s}</button>`);
        }

        if (!html.length) {
            box.innerHTML = '<span class="text-muted small">Свободных слотов на выбранный день нет.</span>';
            return;
        }

        box.innerHTML = html.join("");
        box.querySelectorAll(".slot-button").forEach((b) => b.addEventListener("click", () => {
            $("appointmentStart").value = b.dataset.start || "09:00";
            $("appointmentEnd").value = b.dataset.end || "09:30";
            validateSchedule();
        }));
    }

    function renderServices() {
        const tb = $("appointmentServicesTable");
        const totalNode = $("appointmentServicesTotal");
        if (!tb) return;
        if (!state.selectedServices.length) {
            tb.innerHTML = '<tr class="appointment-services-empty"><td colspan="4" class="text-center text-muted py-4">Пока услуги не выбраны.</td></tr>';
            if (totalNode) totalNode.textContent = "Итого: 0.00 c";
            return;
        }
        tb.innerHTML = state.selectedServices.map((x, i) => `<tr data-service-index="${i}"><td>${esc(x.serviceName)}</td><td>${fm(x.priceTyiyn)}</td><td><input type="number" min="1" class="form-control form-control-sm appointment-service-qty" value="${x.quantity}" data-service-index="${i}" /></td><td class="text-end"><button type="button" class="btn btn-sm btn-outline-danger appointment-service-remove" data-service-index="${i}">Удалить</button></td></tr>`).join("");
        tb.querySelectorAll(".appointment-service-qty").forEach((i) => i.addEventListener("input", () => { state.selectedServices[Number(i.dataset.serviceIndex)].quantity = Math.max(1, Number(i.value || 1)); }));
        tb.querySelectorAll(".appointment-service-remove").forEach((b) => b.addEventListener("click", () => { state.selectedServices.splice(Number(b.dataset.serviceIndex), 1); renderServices(); }));
        const total = state.selectedServices.reduce((sum, item) => sum + (Number(item.priceTyiyn || 0) * Math.max(1, Number(item.quantity || 1))), 0);
        if (totalNode) totalNode.textContent = `Итого: ${fm(total)}`;
    }

    function parseMedicalNotes(value) { if (!value) return {}; if (!value.startsWith("__medjson__")) return { comment: value }; try { return JSON.parse(value.slice("__medjson__".length)); } catch { return { comment: value }; } }
    function fillMedicalFields(notes) {
        const model = parseMedicalNotes(notes || "");
        ["Complaints", "Diagnosis", "Recommendations", "ResearchReferral"].forEach((suffix) => {
            const key = suffix.charAt(0).toLowerCase() + suffix.slice(1);
            const field = $(`appointment${suffix}`);
            if (field) field.value = model[key] || "";
        });
        const medicalComment = $("appointmentMedicalComment");
        if (medicalComment) medicalComment.value = model.comment || "";
    }
    function normalizeTemplateCategory(type) {
        const key = (type || "").toLowerCase();
        if (key === "diagnosis") return "diagnosis";
        if (key === "recommendation" || key === "recommendations") return "recommendation";
        if (key === "research" || key === "referral") return "research";
        if (key === "comment" || key === "comments") return "comment";
        return "";
    }
    function categoryTargetField(category) {
        if (category === "diagnosis") return $("appointmentDiagnosis");
        if (category === "recommendation") return $("appointmentRecommendations");
        if (category === "research") return $("appointmentResearchReferral");
        if (category === "comment") return $("appointmentMedicalComment");
        return null;
    }
    function insertTemplateText(category, text) {
        const field = categoryTargetField(category);
        if (!field) return;
        field.value = field.value.trim() ? `${field.value}\n${text}` : text;
        field.dispatchEvent(new Event("input", { bubbles: true }));
        field.focus();
    }
    function renderTemplates() {
        const tabs = document.querySelectorAll("[data-template-tab]");
        const searchInput = $("appointmentTemplateSearch");
        const list = $("appointmentTemplateItems");
        const empty = $("appointmentTemplateEmpty");
        const clearCurrentFieldButton = $("appointmentTemplateClearField");
        if (!tabs.length || !searchInput || !list || !empty) return;
        let activeCategory = "diagnosis";
        function drawList() {
            const search = (searchInput.value || "").trim().toLowerCase();
            const items = state.medicalTemplates
                .map((x) => ({ id: x.id, category: normalizeTemplateCategory(x.type), title: x.title || "Шаблон", content: x.content || "", sortOrder: Number(x.sortOrder || 0) }))
                .filter((x) => x.category === activeCategory)
                .filter((x) => !search || x.title.toLowerCase().includes(search) || x.content.toLowerCase().includes(search))
                .sort((a, b) => a.sortOrder - b.sortOrder || a.title.localeCompare(b.title, "ru-RU"));
            if (!items.length) { list.innerHTML = ""; empty.classList.remove("d-none"); return; }
            empty.classList.add("d-none");
            list.innerHTML = items.map((item) => `<article class="appointment-template-row"><div><div class="appointment-template-row__title">${esc(item.title)}</div><div class="appointment-template-row__preview">${esc(item.content.replace(/\s+/g, " ").trim().slice(0, 120))}</div></div><button type="button" class="btn appointment-template-row__insert" data-template-category="${esc(item.category)}" data-template-content="${encodeURIComponent(item.content)}">Вставить</button></article>`).join("");
            list.querySelectorAll("[data-template-content]").forEach((btn) => btn.addEventListener("click", () => insertTemplateText(btn.dataset.templateCategory || activeCategory, decodeURIComponent(btn.dataset.templateContent || ""))));
        }
        tabs.forEach((tab) => tab.addEventListener("click", () => { activeCategory = tab.dataset.templateTab || "diagnosis"; tabs.forEach((x) => x.classList.toggle("active", x === tab)); drawList(); }));
        searchInput.addEventListener("input", drawList);
        clearCurrentFieldButton?.addEventListener("click", () => { const field = categoryTargetField(activeCategory); if (field) { field.value = ""; field.focus(); } });
        drawList();
    }

    function openDetails(eventItem) {
        state.selectedEvent = eventItem;
        if ($("appointmentDetailsTitle")) $("appointmentDetailsTitle").textContent = eventTitle(eventItem);
        if ($("appointmentDetailsSubtitle")) $("appointmentDetailsSubtitle").textContent = `${fd(eventItem.start, { day: "numeric", month: "long", year: "numeric" })} · ${ft(eventItem.start)} - ${ft(eventItem.end)}`;
        const set = (id, value, fallback = "—") => { const node = $(id); if (node) node.textContent = value || fallback; };
        set("appointmentDetailsPatient", eventItem.patientName);
        set("appointmentDetailsPhone", eventItem.phone);
        set("appointmentDetailsEmail", eventItem.email);
        set("appointmentDetailsDoctor", eventItem.doctor || doctorMeta(eventItem.doctorId).name);
        set("appointmentDetailsDateTime", `${fd(eventItem.start, { day: "numeric", month: "long", year: "numeric" })}, ${ft(eventItem.start)} - ${ft(eventItem.end)}`);
        set("appointmentDetailsPaymentType", paymentText(eventItem));
        set("appointmentDetailsStatus", eventItem.status || "active");
        const historyText = state.events
            .filter((x) => x.id !== eventItem.id && eventItem.patientId && x.patientId === eventItem.patientId)
            .sort((a, b) => b.start - a.start)
            .slice(0, 5)
            .map((x) => `${fd(x.start, { day: "2-digit", month: "2-digit", year: "numeric" })} ${ft(x.start)} - ${paymentText(x)}`)
            .join("\n");
        set("appointmentDetailsHistory", historyText, "Предыдущих приемов не найдено.");
        set("appointmentDetailsReferral", eventItem.referralSource);
        const noteModel = parseMedicalNotes(eventItem.notes || "");
        const summary = [noteModel.complaints && `Жалобы: ${noteModel.complaints}`, noteModel.diagnosis && `Диагноз: ${noteModel.diagnosis}`, noteModel.recommendations && `Рекомендации: ${noteModel.recommendations}`, noteModel.researchReferral && `Направления: ${noteModel.researchReferral}`, noteModel.comment && `Комментарий: ${noteModel.comment}`].filter(Boolean).join("\n\n");
        set("appointmentDetailsNotes", summary || eventItem.notes || "—");
        const serviceTable = $("appointmentDetailsServices");
        if (serviceTable) serviceTable.innerHTML = (eventItem.services || []).length ? eventItem.services.map((x) => `<tr><td>${esc(x.name)}</td><td>${fm(x.priceTyiyn)}</td><td>${x.quantity}</td></tr>`).join("") : '<tr><td colspan="3" class="text-center text-muted py-4">Услуги не выбраны.</td></tr>';
        detailsModal?.show();
    }

    function openPeriod(date) {
        state.selectedDate = new Date(date);
        if ($("appointmentPeriodModalTitle")) $("appointmentPeriodModalTitle").textContent = "Все записи за день";
        if ($("appointmentPeriodModalSubtitle")) $("appointmentPeriodModalSubtitle").textContent = fd(state.selectedDate, { weekday: "long", day: "numeric", month: "long", year: "numeric" });
        const list = filteredEvents().filter((x) => sameDay(x.start, state.selectedDate));
        const container = $("appointmentPeriodList");
        if (container) {
            container.innerHTML = list.length ? list.map((ev) => `<button type="button" class="period-appointment-card ${paymentClass(ev)}" data-event-id="${esc(ev.id)}"><div class="period-appointment-card__time">${ft(ev.start)} - ${ft(ev.end)}</div><div class="period-appointment-card__body"><div class="period-appointment-card__title">${esc(eventTitle(ev))}</div><div class="period-appointment-card__meta">${esc(doctorMeta(ev.doctorId).name)} · ${paymentText(ev)}</div></div></button>`).join("") : '<div class="period-list-empty"><div class="period-list-empty__title">Записей пока нет</div></div>';
            container.querySelectorAll("[data-event-id]").forEach((b) => b.addEventListener("click", () => {
                const item = state.events.find((x) => x.id === b.dataset.eventId);
                if (item) openDetails(item);
            }));
        }
        periodModal?.show();
    }

    function openEditor(ev, slotDate) {
        if (!canManageAppointments) return;
        const isExistingEvent = !!ev?.id;
        applyEditorMode(isExistingEvent ? "visit" : "create");
        if ($("appointmentEditorTitle")) $("appointmentEditorTitle").textContent = isExistingEvent ? "Проведение приема" : "Создание записи";
        if ($("appointmentEditorSubtitle")) $("appointmentEditorSubtitle").textContent = isExistingEvent ? "Заполните данные и медицинскую часть" : "Выберите время, услуги и сохраните запись";
        if ($("appointmentSaveButton")) $("appointmentSaveButton").textContent = isExistingEvent ? "Сохранить прием" : "Сохранить запись";
        if (!ev || !ev.id) {
            ["appointmentId", "appointmentPatientId", "appointmentPatientName", "appointmentPhone", "appointmentEmail", "appointmentReferral", "appointmentComment"].forEach((id) => { if ($(id)) $(id).value = ""; });
            if ($("appointmentPatient")) $("appointmentPatient").selectedIndex = 0;
            const date = slotDate || new Date();
            if ($("appointmentDoctor")) $("appointmentDoctor").value = ev?.doctorId || p.currentDoctorId || "";
            if ($("appointmentDate")) $("appointmentDate").value = date.toISOString().slice(0, 10);
            if ($("appointmentStart")) $("appointmentStart").value = `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
            const end = new Date(date);
            end.setMinutes(end.getMinutes() + durationForDoctor(resolveDoctorIdForEditor() || ev?.doctorId));
            if ($("appointmentEnd")) $("appointmentEnd").value = `${String(end.getHours()).padStart(2, "0")}:${String(end.getMinutes()).padStart(2, "0")}`;
            if ($("appointmentPaymentType")) $("appointmentPaymentType").value = "unpaid";
            if ($("appointmentStatus")) $("appointmentStatus").value = "active";
            if ($("appointmentIsActive")) $("appointmentIsActive").checked = true;
            fillMedicalFields("");
            state.selectedServices = [];
            renderServices();
            applyDoctorDefaults(resolveDoctorIdForEditor());
        } else {
            if ($("appointmentId")) $("appointmentId").value = ev.id || "";
            if ($("appointmentPatient")) $("appointmentPatient").value = ev.patientId || "";
            if ($("appointmentPatientId")) $("appointmentPatientId").value = ev.patientId || "";
            if ($("appointmentPatientName")) $("appointmentPatientName").value = ev.patientName || "";
            if ($("appointmentDoctor")) $("appointmentDoctor").value = ev.doctorId || "";
            if ($("appointmentPhone")) $("appointmentPhone").value = ev.phone || "";
            if ($("appointmentEmail")) $("appointmentEmail").value = ev.email || "";
            if ($("appointmentDate")) $("appointmentDate").value = ev.start.toISOString().slice(0, 10);
            if ($("appointmentStart")) $("appointmentStart").value = ev.start.toTimeString().slice(0, 5);
            if ($("appointmentEnd")) $("appointmentEnd").value = ev.end.toTimeString().slice(0, 5);
            if ($("appointmentReferral")) $("appointmentReferral").value = ev.referralSource || "";
            if ($("appointmentPaymentType")) $("appointmentPaymentType").value = ev.paymentType || "unpaid";
            const notesModel = parseMedicalNotes(ev.notes || "");
            if ($("appointmentComment")) $("appointmentComment").value = notesModel.comment || ev.notes || "";
            fillMedicalFields(ev.notes || "");
            state.selectedServices = (ev.services || []).map((x) => ({ organizationServiceId: x.organizationServiceId || null, serviceName: x.name || "Услуга", priceTyiyn: Number(x.priceTyiyn || 0), quantity: Number(x.quantity || 1) }));
            renderServices();
        }
        syncPaymentStatusControl(ev);
        validateSchedule();
        detailsModal?.hide();
        periodModal?.hide();
        editorModal?.show();
    }

    async function cancelSelected() {
        if (!state.selectedEvent?.id || !p.cancelUrl) return;
        if (!confirm("Отменить выбранную запись?")) return;
        const btn = $("appointmentDetailsCancelButton");
        const text = btn?.textContent || "Отменить запись";
        if (btn) { btn.disabled = true; btn.textContent = "Отмена..."; }
        try {
            const response = await fetch(p.cancelUrl, { method: "POST", headers: { "Content-Type": "application/json", "X-Requested-With": "XMLHttpRequest", ...(token() ? { RequestVerificationToken: token() } : {}) }, body: JSON.stringify({ appointmentId: state.selectedEvent.id }) });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || "Не удалось отменить запись.");
            window.location.reload();
        } catch (error) {
            window.alert(error.message || "Не удалось отменить запись.");
            if (btn) { btn.disabled = false; btn.textContent = text; }
        }
    }

    async function generateInvoice() {
        if (!canGenerateInvoice || !p.generateInvoiceUrl || !state.selectedEvent?.id) return;
        const btn = $("appointmentGenerateInvoiceButton");
        const text = btn?.textContent || "Счет на оплату";
        if (btn) { btn.disabled = true; btn.textContent = "Формирование..."; }
        try {
            const response = await fetch(p.generateInvoiceUrl, { method: "POST", headers: { "Content-Type": "application/json", "X-Requested-With": "XMLHttpRequest", ...(token() ? { RequestVerificationToken: token() } : {}) }, body: JSON.stringify({ appointmentId: state.selectedEvent.id }) });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || "Не удалось сформировать счет.");
            if (result.url) window.location.href = result.url;
        } catch (error) {
            window.alert(error.message || "Не удалось сформировать счет.");
        } finally {
            if (btn) { btn.disabled = false; btn.textContent = text; }
        }
    }

    function validateSchedule() {
        if (!$("appointmentSaveButton")) return;
        if (!p.storageReady) { setHint("Сначала примените SQL-скрипт appointments."); $("appointmentSaveButton").disabled = true; return; }
        if (!state.doctorSchedulesReady) { setHint("Таблица смен врачей еще не создана."); $("appointmentSaveButton").disabled = false; return; }
        const doctorId = resolveDoctorIdForEditor();
        const dateValue = $("appointmentDate")?.value || "";
        const startValue = $("appointmentStart")?.value || "";
        const endValue = $("appointmentEnd")?.value || "";
        if (!doctorId || !dateValue) { buildSlots(null); setHint("Выберите врача и дату приема."); $("appointmentSaveButton").disabled = true; return; }
        const schedule = doctorSchedule(doctorId, dateValue);
        buildSlots(schedule);
        if (!schedule || !schedule.isWorking) { setHint("У врача нет активной смены на выбранный день."); $("appointmentSaveButton").disabled = true; return; }
        const a = timeToMin(startValue); const b = timeToMin(endValue); const c = timeToMin(schedule.startTime); const d = timeToMin(schedule.endTime);
        if (a == null || b == null || c == null || d == null || b <= a) { setHint("Укажите корректное время приема."); $("appointmentSaveButton").disabled = true; return; }
        if (a < c || b > d) { setHint(`Прием должен быть в пределах смены ${schedule.startTime} - ${schedule.endTime}.`); $("appointmentSaveButton").disabled = true; return; }
        const busy = unavailableIntervalsForDoctorDate(doctorId, dateValue, $("appointmentId")?.value || "");
        const hasOverlap = busy.some((interval) => a < interval.end && b > interval.start);
        if (hasOverlap) { setHint("Выбранное время уже занято. Укажите другой слот."); $("appointmentSaveButton").disabled = true; return; }
        setHint(`Смена врача: ${schedule.startTime} - ${schedule.endTime}.`);
        $("appointmentSaveButton").disabled = false;
    }
    function alignEndTimeToDoctorDuration() {
        const doctorId = resolveDoctorIdForEditor();
        const startValue = $("appointmentStart")?.value || "";
        const startMin = timeToMin(startValue);
        if (!doctorId || startMin == null) return;
        const endValue = minutesToTime(startMin + durationForDoctor(doctorId));
        if ($("appointmentEnd")) $("appointmentEnd").value = endValue;
    }

    function bindEventButtons(root) {
        root.querySelectorAll("[data-event-id]").forEach((b) => b.addEventListener("click", () => {
            const item = state.events.find((x) => x.id === b.dataset.eventId);
            if (item) openDetails(item);
        }));
    }

    function doctorEventCard(ev, compact) {
        return `<button type="button" class="schedule-event-chip appointment-card ${paymentClass(ev)}${compact ? " compact" : ""}" data-event-id="${esc(ev.id)}"><div class="appointment-head"><div><div class="appointment-title">${esc(eventTitle(ev))}</div><div class="appointment-service">${esc(serviceText(ev))}</div></div><span class="badge ${paymentClass(ev)}">${paymentText(ev)}</span></div><div class="appointment-meta"><span>${ft(ev.start)} - ${ft(ev.end)}</span><span>${esc(ev.referralSource || "кабинет не указан")}</span></div></button>`;
    }

    function renderDoctorDay() {
        doctorGrid.className = "schedule-calendar__grid schedule-grid--doctor-day";
        const dayEvents = eventsForDate(state.anchor);
        const rows = allHours().map((h) => {
            const items = dayEvents.filter((x) => x.start.getHours() === h);
            return `<div class="row" data-hour="${h}"><div class="time-cell">${String(h).padStart(2, "0")}:00</div><div class="content-cell">${items.length ? `<div class="appointment-list">${items.map((x) => doctorEventCard(x, false)).join("")}</div>` : '<div class="empty-text">Нет записи</div>'}</div></div>`;
        }).join("");
        doctorGrid.innerHTML = `<div class="day-table"><div class="head"><div class="time-cell">Время</div><div class="content-cell day-head-title">${fd(state.anchor, { weekday: "long", day: "numeric", month: "long" })}</div></div>${rows}</div>`;
        bindEventButtons(doctorGrid);
        doctorGrid.querySelectorAll(".day-table .row").forEach((row) => row.addEventListener("click", (e) => {
            if (!canManageAppointments || e.target.closest("[data-event-id]")) return;
            const dt = new Date(state.anchor); dt.setHours(Number(row.dataset.hour), 0, 0, 0); openEditor(null, dt);
        }));
    }

    function renderDoctorWeek() {
        doctorGrid.className = "schedule-calendar__grid schedule-grid--doctor-week";
        const weekStart = startOfWeek(state.anchor); const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
        let html = '<div class="week-wrap"><div class="week-grid"><div class="week-head"><div class="time-cell">Время</div>';
        html += days.map((d, i) => `<div class="week-day-cell"><div class="week-day-name">${weekdays[i]}</div><div class="week-day-date">${fd(d, { day: "numeric", month: "short" })}</div></div>`).join("") + "</div>";
        allHours().forEach((h) => {
            html += `<div class="week-row"><div class="time-cell">${String(h).padStart(2, "0")}:00</div>`;
            days.forEach((d) => { html += `<div class="week-slot">${eventsForDate(d).filter((x) => x.start.getHours() === h).map((x) => doctorEventCard(x, true)).join("")}</div>`; });
            html += "</div>";
        });
        doctorGrid.innerHTML = `${html}</div></div>`;
        bindEventButtons(doctorGrid);
    }

    function renderDoctorMonth() {
        doctorGrid.className = "schedule-calendar__grid schedule-grid--doctor-month";
        const monthStart = monthAnchor(state.anchor); const monthEnd = new Date(state.anchor.getFullYear(), state.anchor.getMonth() + 1, 0); const start = startOfWeek(monthStart);
        const heads = weekdays.map((w) => `<div>${w}</div>`).join("");
        const cells = Array.from({ length: 42 }, (_, i) => {
            const day = addDays(start, i); const muted = day < monthStart || day > monthEnd; const items = filteredEvents().filter((x) => sameDay(x.start, day));
            return `<div class="month-cell ${muted ? "outside" : ""}"><div class="month-cell-head"><div class="month-date">${day.getDate()}</div><div class="month-mini">${fd(day, { weekday: "short" })}</div></div><div class="month-items">${items.slice(0, 3).map((x) => `<button type="button" class="month-item ${paymentClass(x)}" data-event-id="${esc(x.id)}"><span class="month-time">${ft(x.start)}</span><span class="month-patient">${esc(eventTitle(x))}</span></button>`).join("") || '<div class="month-empty">Нет записей</div>'}${items.length > 3 ? `<button type="button" class="month-more" data-open-day="${toIsoDate(day)}">Посмотреть все (${items.length})</button>` : ""}</div></div>`;
        }).join("");
        doctorGrid.innerHTML = `<div class="month-grid-wrap"><div class="month-head">${heads}</div><div class="month-grid">${cells}</div></div>`;
        bindEventButtons(doctorGrid);
        doctorGrid.querySelectorAll("[data-open-day]").forEach((btn) => btn.addEventListener("click", () => openPeriod(new Date(`${btn.dataset.openDay}T00:00:00`))));
    }

    function selectedDoctorPills() {
        const box = $("selectedDoctors");
        if (!box) return;
        const docs = selectedDoctors();
        box.innerHTML = docs.length ? docs.map((doc) => `<div class="doctor-pill">${esc(doc.label)} · ${esc(doctorMeta(doc.value).subtitle)}</div>`).join("") : '<div class="empty">Нет сотрудников по выбранным фильтрам</div>';
    }
    function populateAvailabilityDoctors() {
        const select = $("availabilityDoctor");
        if (!select) return;
        const docs = selectedDoctors();
        select.innerHTML = `<option value="all">Все сотрудники</option>${docs.map((doc) => `<option value="${esc(doc.value)}">${esc(doc.label)}</option>`).join("")}`;
    }
    function doctorAppointmentsOnDate(doctorId, date) {
        return state.events
            .filter((a) => a.doctorId === doctorId && sameDay(a.start, date) && a.isActive !== false)
            .map((a) => ({ start: a.start.getHours() * 60 + a.start.getMinutes(), end: a.end.getHours() * 60 + a.end.getMinutes() }));
    }
    function findNearestSlots() {
        const duration = Number($("availabilityDuration")?.value || 30);
        const doctorValue = $("availabilityDoctor")?.value || "all";
        const docs = doctorValue === "all" ? selectedDoctors() : selectedDoctors().filter((x) => x.value === doctorValue);
        const results = [];
        for (let dayOffset = 0; dayOffset < 14 && results.length < 8; dayOffset += 1) {
            const date = addDays(state.anchor, dayOffset);
            for (const doc of docs) {
                const schedule = doctorSchedule(doc.value, toIsoDate(date));
                if (!schedule || !schedule.isWorking) continue;
                const busy = doctorAppointmentsOnDate(doc.value, date);
                const start = timeToMin(schedule.startTime); const end = timeToMin(schedule.endTime);
                if (start == null || end == null) continue;
                for (let minute = start; minute + duration <= end; minute += 10) {
                    const hasConflict = busy.some((x) => minute < x.end && minute + duration > x.start);
                    if (!hasConflict) { results.push({ doctor: doc, date: new Date(date), start: minute, end: minute + duration }); break; }
                }
                if (results.length >= 8) break;
            }
        }
        return results;
    }
    function renderAvailability() {
        const box = $("availabilityList");
        if (!box) return;
        populateAvailabilityDoctors();
        const results = findNearestSlots();
        if (!results.length) { box.innerHTML = '<div class="empty">Свободные окна не найдены в ближайшие 14 дней.</div>'; return; }
        box.innerHTML = results.map((r, index) => `<button class="availability-card" data-book-date="${toIsoDate(r.date)}" data-doctor-id="${esc(r.doctor.value)}" data-start="${minutesToTime(r.start)}"><span class="availability-rank">${index + 1}</span><div class="availability-date">${fd(r.date, { day: "numeric", month: "short" })}</div><div class="availability-time">${minutesToTime(r.start)}-${minutesToTime(r.end)}</div><div class="availability-meta">${esc(r.doctor.label)}<br>${esc(doctorMeta(r.doctor.value).subtitle)}</div></button>`).join("");
        box.querySelectorAll("[data-book-date]").forEach((btn) => btn.addEventListener("click", () => {
            const [hours, minutes] = (btn.dataset.start || "09:00").split(":").map(Number);
            const date = new Date(`${btn.dataset.bookDate}T00:00:00`);
            date.setHours(hours, minutes, 0, 0);
            state.anchor = new Date(date);
            renderAll();
            if (canManageAppointments) {
                openEditor({ doctorId: btn.dataset.doctorId, start: date }, date);
                if ($("appointmentDoctor")) $("appointmentDoctor").value = btn.dataset.doctorId || "";
                validateSchedule();
            }
        }));
    }

    function registryCard(ev, meta) {
        return `<button class="slot-card ${paymentClass(ev)}" data-event-id="${esc(ev.id)}"><div class="slot-top"><div><div class="slot-patient">${esc(eventTitle(ev))}</div><div class="slot-service">${esc(serviceText(ev))}</div></div><span class="status-tag status-${paymentClass(ev)}">${paymentText(ev)}</span></div><div class="slot-meta"><span>${ft(ev.start)}-${ft(ev.end)}</span><span>${esc(meta.subtitle)}</span></div></button>`;
    }
    function renderRegistryDay() {
        const docs = selectedDoctors();
        registryGrid.style.setProperty("--registry-doctor-columns", Math.max(docs.length, 1));
        let html = '<div class="grid-wrap"><div class="doctor-grid-head"><div class="time-head">Время</div>';
        html += docs.map((doc) => { const meta = doctorMeta(doc.value); return `<div class="doctor-head"><div class="doctor-name">${esc(meta.name)}</div><div class="doctor-sub">${esc(meta.subtitle)}</div></div>`; }).join("");
        html += '</div><div class="doctor-grid">';
        allHours().forEach((h) => {
            html += `<div class="time-col">${String(h).padStart(2, "0")}:00</div>`;
            docs.forEach((doc) => {
                const meta = doctorMeta(doc.value);
                const items = filteredEvents().filter((ev) => ev.doctorId === doc.value && sameDay(ev.start, state.anchor) && ev.start.getHours() === h);
                html += `<div class="slot-col">${items.length ? `<div class="slot-list">${items.map((ev) => registryCard(ev, meta)).join("")}</div>` : ""}</div>`;
            });
        });
        registryGrid.innerHTML = `${html}</div></div>`;
        bindEventButtons(registryGrid);
    }
    function renderRegistryWeek() {
        const weekStart = startOfWeek(state.anchor); const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
        registryGrid.innerHTML = `<div class="week-board">${days.map((day, index) => { const items = filteredEvents().filter((ev) => sameDay(ev.start, day)); return `<div class="week-day-card"><div class="week-day-head"><div class="week-day-name">${weekdays[index]}</div><div class="week-day-date">${fd(day, { day: "numeric", month: "short" })}</div></div><div class="week-day-body">${items.length ? items.map((ev) => `<button class="week-appointment ${paymentClass(ev)}" data-event-id="${esc(ev.id)}"><strong>${ft(ev.start)} · ${esc(eventTitle(ev))}</strong><small>${esc(doctorMeta(ev.doctorId).name)} · ${esc(serviceText(ev))}</small><small>${esc(doctorMeta(ev.doctorId).subtitle)} · ${paymentText(ev)}</small></button>`).join("") : '<div class="empty">Нет приемов</div>'}</div></div>`; }).join("")}</div>`;
        bindEventButtons(registryGrid);
    }
    function renderRegistryMonth() {
        const docs = selectedDoctors();
        registryGrid.innerHTML = `<div class="month-doctor-grid">${docs.map((doc) => { const meta = doctorMeta(doc.value); const items = filteredEvents().filter((ev) => ev.doctorId === doc.value); return `<div class="doctor-month-card"><div class="doctor-month-title">${esc(meta.name)}</div><div class="doctor-month-sub">${esc(meta.subtitle)}</div><div class="doctor-month-list">${items.length ? items.map((ev) => `<button class="week-appointment ${paymentClass(ev)}" data-event-id="${esc(ev.id)}"><span class="month-date-badge">${fd(ev.start, { day: "numeric", month: "short" })}</span><strong>${ft(ev.start)} · ${esc(eventTitle(ev))}</strong><small>${esc(serviceText(ev))}</small><small>${paymentText(ev)}</small></button>`).join("") : '<div class="empty">За месяц записей нет</div>'}</div></div>`; }).join("")}</div>`;
        bindEventButtons(registryGrid);
    }
    function renderRegistryList() {
        const list = $("listView");
        if (!list) return;
        const items = filteredEvents();
        list.innerHTML = items.length ? `<div class="list-table"><div class="list-head"><div>Дата</div><div>Время</div><div>Пациент</div><div>Врач</div><div>Услуги</div><div>Статус</div></div>${items.map((ev) => `<div class="list-row" data-event-id="${esc(ev.id)}"><div>${fd(ev.start, { day: "2-digit", month: "2-digit", year: "numeric" })}</div><div>${ft(ev.start)}</div><div><strong>${esc(eventTitle(ev))}</strong><br><span class="text-muted">${esc(ev.phone || "Телефон не указан")}</span></div><div>${esc(doctorMeta(ev.doctorId).name)}<br><span class="text-muted">${esc(doctorMeta(ev.doctorId).subtitle)}</span></div><div>${esc(serviceText(ev))}</div><div><span class="status-tag status-${paymentClass(ev)}">${paymentText(ev)}</span></div></div>`).join("")}</div>` : '<div class="empty">Нет приемов по выбранным фильтрам</div>';
        bindEventButtons(list);
    }
    function renderRegistryLoad() {
        const load = $("loadView");
        if (!load) return;
        const cards = selectedDoctors().map((doc) => {
            const meta = doctorMeta(doc.value); const items = filteredEvents().filter((ev) => ev.doctorId === doc.value); const paid = items.filter((x) => paymentClass(x) === "paid").length; const unpaid = items.length - paid; const busyPercent = Math.min(100, items.length * (state.view === "month" ? 6 : state.view === "week" ? 14 : 24));
            return `<div class="load-card"><div class="load-name">${esc(meta.name)}</div><div class="load-sub">${esc(meta.subtitle)}</div><div class="load-row"><span>Всего приемов</span><strong>${items.length}</strong></div><div class="load-row"><span>Оплачено</span><strong>${paid}</strong></div><div class="load-row"><span>Не оплачено</span><strong>${unpaid}</strong></div><div class="load-row"><span>Оценка загрузки</span><strong>${busyPercent}%</strong></div><div class="progress"><div class="progress-bar" style="width:${busyPercent}%"></div></div></div>`;
        }).join("");
        load.innerHTML = cards ? `<div class="load-grid">${cards}</div>` : '<div class="empty">Нет данных по врачам</div>';
    }
    function setRegistryTab(tab) {
        state.activeTab = tab;
        document.querySelectorAll("#registryTabs .tab-btn").forEach((btn) => btn.classList.toggle("active", btn.dataset.tab === tab));
        if ($("gridView")) $("gridView").classList.toggle("hidden", tab !== "grid");
        if ($("listView")) $("listView").classList.toggle("hidden", tab !== "list");
        if ($("loadView")) $("loadView").classList.toggle("hidden", tab !== "load");
    }
    function setupMultiselect(kind, items, rootIds) {
        const list = $(rootIds.list); const trigger = $(rootIds.trigger); const summary = $(rootIds.summary); const menu = $(rootIds.menu); const search = $(rootIds.search); const selectAll = $(rootIds.selectAll);
        if (!list || !trigger || !summary || !menu) return;
        const set = kind === "departments" ? state.selectedDepartmentIds : state.selectedDoctorIds;
        function syncSummary() { const count = Array.from(set).length; summary.textContent = count === items.length ? "Выбраны все" : count === 0 ? "Не выбрано" : `Выбрано: ${count}`; }
        function draw() {
            const query = (search?.value || "").trim().toLowerCase();
            const filtered = items.filter((item) => !query || item.label.toLowerCase().includes(query));
            list.innerHTML = filtered.length ? filtered.map((item) => `<label class="filter-option"><input class="form-check-input filter-option__checkbox" type="checkbox" data-value="${esc(item.value)}" ${set.has(item.value) ? "checked" : ""} /><span class="filter-option__label">${esc(item.label)}</span></label>`).join("") : '<div class="filter-empty">Ничего не найдено.</div>';
            list.querySelectorAll("[data-value]").forEach((input) => input.addEventListener("change", () => {
                if (input.checked) set.add(input.dataset.value); else set.delete(input.dataset.value);
                if (kind === "departments") syncDoctorSelection();
                syncSummary(); renderAll(); draw();
            }));
            syncSummary();
        }
        function openMenu(open) { menu.hidden = !open; trigger.setAttribute("aria-expanded", open ? "true" : "false"); trigger.closest(".appointments-multiselect")?.classList.toggle("is-open", open); }
        trigger.addEventListener("click", () => openMenu(menu.hidden));
        search?.addEventListener("input", draw);
        selectAll?.addEventListener("click", () => { items.forEach((item) => set.add(item.value)); if (kind === "departments") syncDoctorSelection(); draw(); renderAll(); });
        document.addEventListener("click", (event) => { if (!event.target.closest(`[data-filter-root="${kind}"]`)) openMenu(false); });
        draw();
    }
    function syncDoctorSelection() {
        const allowed = new Set(state.doctorFilters.filter((doc) => !doc.departmentIds?.length || doc.departmentIds.some((id) => state.selectedDepartmentIds.has(id))).map((doc) => doc.value));
        Array.from(state.selectedDoctorIds).forEach((id) => { if (!allowed.has(id)) state.selectedDoctorIds.delete(id); });
        if (!state.selectedDoctorIds.size) allowed.forEach((id) => state.selectedDoctorIds.add(id));
    }

    function renderDoctorPage() { setPeriodLabel(); syncDatePicker(); if (state.view === "day") renderDoctorDay(); else if (state.view === "week") renderDoctorWeek(); else renderDoctorMonth(); }
    function renderRegistryPage() { setPeriodLabel(); syncDatePicker(); selectedDoctorPills(); renderAvailability(); renderRegistryList(); renderRegistryLoad(); setRegistryTab(state.activeTab); if (state.view === "day") renderRegistryDay(); else if (state.view === "week") renderRegistryWeek(); else renderRegistryMonth(); }
    function renderAll() { if (pageType === "doctor") renderDoctorPage(); else renderRegistryPage(); }
    function shiftByNav(direction) {
        if (direction === "today") { state.anchor = state.view === "month" ? monthAnchor(new Date()) : new Date(); renderAll(); return; }
        const k = direction === "prev" ? -1 : 1;
        if (state.view === "day") state.anchor = addDays(state.anchor, k);
        else if (state.view === "week") state.anchor = addDays(state.anchor, k * 7);
        else state.anchor = new Date(state.anchor.getFullYear(), state.anchor.getMonth() + k, 1);
        renderAll();
    }

    document.querySelectorAll(".schedule-view-switcher [data-view]").forEach((btn) => btn.addEventListener("click", () => {
        state.view = btn.dataset.view || "day";
        document.querySelectorAll(".schedule-view-switcher [data-view]").forEach((x) => { x.classList.remove("active", "btn-primary"); x.classList.add("btn-outline-primary"); });
        btn.classList.add("active", "btn-primary");
        btn.classList.remove("btn-outline-primary");
        if (state.view === "month") state.anchor = monthAnchor(state.anchor);
        renderAll();
    }));
    document.querySelectorAll(".schedule-nav").forEach((btn) => btn.addEventListener("click", () => shiftByNav(btn.dataset.direction)));
    $("scheduleDatePicker")?.addEventListener("change", (e) => {
        const date = new Date(`${e.target.value}T00:00:00`);
        if (!Number.isNaN(date.getTime())) { state.anchor = state.view === "month" ? monthAnchor(date) : date; renderAll(); }
    });
    $("appointmentCreateButton")?.addEventListener("click", () => { const dt = new Date(state.anchor); dt.setHours(9, 0, 0, 0); openEditor(null, dt); });
    $("appointmentPeriodAddButton")?.addEventListener("click", () => { const dt = state.selectedDate ? new Date(state.selectedDate) : new Date(); dt.setHours(9, 0, 0, 0); openEditor(null, dt); });
    $("appointmentDetailsOpenVisitButton")?.addEventListener("click", () => { if (state.selectedEvent) openEditor(state.selectedEvent, state.selectedEvent.start); });
    $("appointmentDetailsEditButton")?.addEventListener("click", () => { if (state.selectedEvent) openEditor(state.selectedEvent, state.selectedEvent.start); });
    $("appointmentDetailsCancelButton")?.addEventListener("click", cancelSelected);
    $("appointmentGenerateInvoiceButton")?.addEventListener("click", generateInvoice);
    $("appointmentSaveDraftButton")?.addEventListener("click", () => {
        const saveButton = $("appointmentSaveButton");
        if ($("appointmentStatus")) $("appointmentStatus").value = "draft";
        if (saveButton) saveButton.dataset.forceDraft = "1";
        saveButton?.click();
    });
    $("appointmentDoctor")?.addEventListener("change", () => {
        const doctorId = resolveDoctorIdForEditor();
        applyDoctorDefaults(doctorId);
        alignEndTimeToDoctorDuration();
        validateSchedule();
    });
    $("appointmentStart")?.addEventListener("change", () => {
        alignEndTimeToDoctorDuration();
        validateSchedule();
    });
    $("appointmentServiceSelector")?.addEventListener("change", (e) => {
        const serviceId = String(e.target.value || "");
        if (!serviceId) return;
        const existing = state.selectedServices.find((x) => String(x.organizationServiceId || "") === serviceId);
        if (existing) existing.quantity += 1;
        else {
            const item = state.serviceCatalog.find((x) => String(x.id || "") === serviceId);
            if (item) state.selectedServices.push({ organizationServiceId: item.id, serviceName: item.name, priceTyiyn: Number(item.priceTyiyn || 0), quantity: 1 });
        }
        e.target.value = "";
        renderServices();
    });
    ["appointmentDate", "appointmentEnd"].forEach((id) => { $(id)?.addEventListener("change", validateSchedule); $(id)?.addEventListener("input", validateSchedule); });

    if (pageType === "registry") {
        setupMultiselect("departments", state.departments, { list: "appointmentsDepartmentsList", trigger: "appointmentsDepartmentsTrigger", summary: "appointmentsDepartmentsSummary", menu: "appointmentsDepartmentsMenu", search: "appointmentsDepartmentSearch", selectAll: "appointmentsSelectAllDepartments" });
        setupMultiselect("doctors", state.doctorFilters, { list: "appointmentsDoctorsList", trigger: "appointmentsDoctorsTrigger", summary: "appointmentsDoctorsSummary", menu: "appointmentsDoctorsMenu", search: "appointmentsDoctorSearch", selectAll: "appointmentsSelectAllDoctors" });
        $("appointmentPaymentFilter")?.addEventListener("change", (e) => { state.paymentFilter = e.target.value || "all"; renderAll(); });
        $("registryQuickViewFilter")?.addEventListener("change", (e) => { setRegistryTab(e.target.value || "grid"); });
        document.querySelectorAll("#registryTabs .tab-btn").forEach((btn) => btn.addEventListener("click", () => { if ($("registryQuickViewFilter")) $("registryQuickViewFilter").value = btn.dataset.tab || "grid"; setRegistryTab(btn.dataset.tab || "grid"); }));
        $("availabilityDoctor")?.addEventListener("change", renderAvailability);
        $("availabilityDuration")?.addEventListener("change", renderAvailability);
    }

    renderTemplates();
    renderServices();
    validateSchedule();
    renderAll();
})();
