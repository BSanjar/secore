(function () {
    const p = window.medclinicSchedule || {};
    const pageType = p.pageType || "registry";
    const canManageAppointments = p.canManageAppointments !== false;
    const canGenerateInvoice = p.canGenerateInvoice !== false;
    const canMarkAsPaidManually = p.canMarkAsPaidManually === true;
    const markPaidUrl = p.markPaidUrl || "";
    const paymentQrUrl = p.paymentQrUrl || "";
    const appointmentInvoiceUrl = p.appointmentInvoiceUrl || "";
    const sendInvoiceWhatsAppUrl = p.sendInvoiceWhatsAppUrl || "";
    const $ = (id) => document.getElementById(id);

    async function readJsonResponse(response) {
        const text = await response.text();
        if (!text) {
            return { response, result: {} };
        }
        try {
            return { response, result: JSON.parse(text) };
        } catch {
            const snippet = text.replace(/\s+/g, " ").trim().slice(0, 240);
            throw new Error(snippet || `Ошибка сервера (${response.status})`);
        }
    }

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
        doctorFilters: (p.doctorFilters || []).map((doc) => ({
            ...doc,
            departmentIds: doc.departmentIds || doc.DepartmentIds || []
        })),
        selectedDepartmentIds: new Set((p.departments || []).map((x) => x.value)),
        selectedDoctorIds: new Set((p.doctorFilters || []).map((x) => x.value)),
        paymentFilter: "all",
        selectedPatientId: null,
        patientCatalog: p.patients || [],
        returnToDetails: false,
        detailsQrInvoiceId: null
    };

    window.medclinicScheduleState = state;

    const periodModal = $("appointmentPeriodModal") ? new bootstrap.Modal($("appointmentPeriodModal")) : null;
    const detailsModal = $("appointmentDetailsModal") ? new bootstrap.Modal($("appointmentDetailsModal")) : null;
    const editorModal = $("appointmentEditorModal") ? new bootstrap.Modal($("appointmentEditorModal")) : null;
    const rescheduleModal = $("appointmentRescheduleModal") ? new bootstrap.Modal($("appointmentRescheduleModal")) : null;

    const esc = (v) => String(v ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    const ft = (d) => new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit" }).format(d);
    const fd = (d, o) => new Intl.DateTimeFormat("ru-RU", o).format(d);
    const fm = (tyiyn) => {
        const ui = window.MedclinicUI;
        const text = ui && ui.formatTyiynAsSom ? ui.formatTyiynAsSom(tyiyn) : new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format((Number(tyiyn) || 0) / 100);
        return `${text} c`;
    };
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    const weekdays = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
    const REGISTRY_FILTERS_KEY = "medclinicRegistryFiltersV1";
    const OWN_SCHEDULE_FILTERS_KEY = "medclinicOwnScheduleFiltersV1";
    const isOwnSchedule = p.isOwnSchedule === true;

    function saveRegistryFilters() {
        if (pageType !== "registry" || !$("registryDoctorSelect")) return;
        try {
            if (isOwnSchedule) {
                sessionStorage.setItem(
                    OWN_SCHEDULE_FILTERS_KEY,
                    JSON.stringify({ anchor: toIsoDate(state.anchor) })
                );
                return;
            }
            sessionStorage.setItem(
                REGISTRY_FILTERS_KEY,
                JSON.stringify({
                    anchor: toIsoDate(state.anchor),
                    departmentId: $("registryDepartmentSelect")?.value || "",
                    doctorId: $("registryDoctorSelect")?.value || "",
                    patientId: state.selectedPatientId || ""
                })
            );
        } catch {
            /* ignore quota / private mode */
        }
    }

    function restoreRegistryFilters() {
        if (pageType !== "registry" || !$("registryDoctorSelect")) return;
        if (isOwnSchedule) {
            try {
                const raw = sessionStorage.getItem(OWN_SCHEDULE_FILTERS_KEY);
                if (raw) {
                    const data = JSON.parse(raw);
                    if (data.anchor) {
                        const restored = new Date(`${data.anchor}T12:00:00`);
                        if (!Number.isNaN(restored.getTime())) state.anchor = restored;
                    }
                }
            } catch {
                /* ignore corrupt storage */
            }
            ensureOwnScheduleDoctorSelect();
            return;
        }
        let preservedDoctor = "";
        let preservedPatientId = "";
        let preservedDepartmentId = "";
        try {
            const raw = sessionStorage.getItem(REGISTRY_FILTERS_KEY);
            if (raw) {
                const data = JSON.parse(raw);
                if (data.anchor) {
                    const restored = new Date(`${data.anchor}T12:00:00`);
                    if (!Number.isNaN(restored.getTime())) state.anchor = restored;
                }
                preservedDepartmentId = data.departmentId ?? "";
                preservedDoctor = data.doctorId ?? "";
                preservedPatientId = data.patientId || "";
            }
        } catch {
            /* ignore corrupt storage */
        }

        const urlPatientId = (p.initialPatientId || "").trim();
        if (urlPatientId) {
            const deptSelect = $("registryDepartmentSelect");
            if (deptSelect) deptSelect.value = "";
            refreshRegistryDoctorSelect("");
            setRegistryPatientFilter(urlPatientId, { save: false, render: false, adjustAnchor: true });
            return;
        }

        const deptSelect = $("registryDepartmentSelect");
        if (deptSelect) {
            const hasDept = preservedDepartmentId && state.departments.some((d) => String(d.value) === String(preservedDepartmentId));
            deptSelect.value = hasDept ? preservedDepartmentId : "";
        }
        ensureRegistryDefaultDepartment();
        refreshRegistryDoctorSelect(preservedDoctor);
        setRegistryPatientFilter(preservedPatientId, { save: false, render: false, adjustAnchor: false });
    }

    function resolveAnchorForPatient(patientId) {
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        const patientEvents = state.events
            .filter((ev) => String(ev.patientId || "") === String(patientId) && ev.isActive !== false)
            .sort((a, b) => a.start - b.start);
        if (!patientEvents.length) return new Date();

        const upcoming = patientEvents.find((ev) => {
            const day = new Date(ev.start);
            day.setHours(0, 0, 0, 0);
            return day >= today;
        });
        if (upcoming) return new Date(upcoming.start);

        return new Date(patientEvents[patientEvents.length - 1].start);
    }

    function normalizePatientSearchText(value) {
        return String(value ?? "").trim().toLowerCase();
    }

    function findPatientById(patientId) {
        if (!patientId) return null;
        return state.patientCatalog.find((item) => String(item.id) === String(patientId)) || null;
    }

    function applyRegistryPatientFilterUi(patient) {
        const input = $("registryPatientFilterInput");
        const hidden = $("registryPatientFilterId");
        const clearBtn = $("registryPatientFilterClear");
        if (!input || !hidden) return;
        if (patient) {
            input.value = patient.name || "";
            hidden.value = patient.id || "";
            if (clearBtn) clearBtn.hidden = false;
        } else {
            input.value = "";
            hidden.value = "";
            if (clearBtn) clearBtn.hidden = true;
        }
    }

    function setRegistryPatientFilter(patientId, options = {}) {
        const normalizedId = (patientId || "").trim();
        const patient = findPatientById(normalizedId);
        state.selectedPatientId = patient ? patient.id : null;
        applyRegistryPatientFilterUi(patient);
        hideRegistryPatientSuggestions();
        if (patient && options.adjustAnchor !== false) {
            state.anchor = resolveAnchorForPatient(patient.id);
        }
        if (options.save !== false) saveRegistryFilters();
        if (options.render !== false) renderAll();
    }

    function hideRegistryPatientSuggestions() {
        const box = $("registryPatientFilterSuggestions");
        const input = $("registryPatientFilterInput");
        if (box) box.hidden = true;
        if (input) input.setAttribute("aria-expanded", "false");
    }

    function renderRegistryPatientSuggestions(query) {
        const box = $("registryPatientFilterSuggestions");
        const input = $("registryPatientFilterInput");
        if (!box || !input) return;
        const normalized = normalizePatientSearchText(query);
        const matches = state.patientCatalog
            .filter((patient) => {
                if (!normalized) return true;
                const haystack = normalizePatientSearchText(`${patient.name || ""} ${patient.phone || ""} ${patient.email || ""}`);
                return haystack.includes(normalized);
            })
            .slice(0, 12);

        if (!matches.length) {
            box.hidden = true;
            input.setAttribute("aria-expanded", "false");
            return;
        }

        box.innerHTML = matches.map((patient) => `
            <button type="button" class="registry-v2-patient-suggestion" role="option" data-patient-id="${esc(patient.id)}">
                <span class="registry-v2-patient-suggestion__name">${esc(patient.name || "Без имени")}</span>
                <span class="registry-v2-patient-suggestion__meta">${esc(patient.phone || "Телефон не указан")}${patient.email ? ` · ${esc(patient.email)}` : ""}</span>
            </button>
        `).join("");
        box.hidden = false;
        input.setAttribute("aria-expanded", "true");
        box.querySelectorAll(".registry-v2-patient-suggestion").forEach((button) => {
            button.addEventListener("mousedown", (event) => event.preventDefault());
            button.addEventListener("click", () => setRegistryPatientFilter(button.dataset.patientId || ""));
        });
    }

    function setupRegistryPatientFilter() {
        const root = $("registryPatientFilter");
        const input = $("registryPatientFilterInput");
        const clearBtn = $("registryPatientFilterClear");
        if (!root || !input) return;

        input.addEventListener("focus", () => renderRegistryPatientSuggestions(input.value));
        input.addEventListener("input", () => {
            if ($("registryPatientFilterId")) $("registryPatientFilterId").value = "";
            state.selectedPatientId = null;
            if (clearBtn) clearBtn.hidden = !input.value.trim();
            renderRegistryPatientSuggestions(input.value);
        });
        input.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                hideRegistryPatientSuggestions();
                input.blur();
            }
        });
        clearBtn?.addEventListener("click", () => setRegistryPatientFilter(""));
        document.addEventListener("click", (event) => {
            if (!event.target.closest("#registryPatientFilter")) hideRegistryPatientSuggestions();
        });
    }

    function eventMatchesPatientFilter(ev) {
        if (!state.selectedPatientId) return true;
        return String(ev.patientId || "") === String(state.selectedPatientId);
    }

    function doctorDayEventsAll(doctorId, date = state.anchor) {
        return state.events
            .filter((ev) => ev.doctorId === doctorId && sameDay(ev.start, date) && ev.isActive !== false)
            .sort((a, b) => a.start - b.start);
    }

    function slotOccupiedByOtherPatient(minute, duration, doctorId, date = state.anchor) {
        if (!state.selectedPatientId) return false;
        return doctorDayEventsAll(doctorId, date).some(
            (ev) => String(ev.patientId || "") !== String(state.selectedPatientId) && appointmentOverlapsMinutes(ev, minute, duration)
        );
    }

    function addDays(date, days) { const d = new Date(date); d.setDate(d.getDate() + days); return d; }
    function monthAnchor(date) { return new Date(date.getFullYear(), date.getMonth(), 1); }
    function sameDay(a, b) { return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate(); }
    function startOfWeek(date) { const d = new Date(date); const shift = (d.getDay() + 6) % 7; d.setDate(d.getDate() - shift); d.setHours(0, 0, 0, 0); return d; }
    function toIsoDate(value) { const d = new Date(value); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; }
    function timeToMin(v) { if (!v || !String(v).includes(":")) return null; const [h, m] = String(v).split(":").map(Number); return h * 60 + m; }
    function minutesToTime(v) { return `${String(Math.floor(v / 60)).padStart(2, "0")}:${String(v % 60).padStart(2, "0")}`; }
    function combineDateAndMinutes(dateIso, minute) {
        if (!dateIso || minute == null) return null;
        const dt = new Date(`${dateIso}T00:00:00`);
        dt.setHours(Math.floor(minute / 60), minute % 60, 0, 0);
        return dt;
    }
    function combineDateAndTime(dateIso, timeValue) {
        if (!dateIso || !timeValue) return null;
        const minute = typeof timeValue === "number" ? timeValue : timeToMin(timeValue);
        if (minute == null) return null;
        return combineDateAndMinutes(dateIso, minute);
    }
    function isPastSlot(dateIso, timeValue) {
        const dt = combineDateAndTime(dateIso, timeValue);
        return dt ? dt.getTime() < Date.now() : false;
    }
    function clampToUpcomingSlot(date, doctorId) {
        const now = new Date();
        if (date.getTime() >= now.getTime()) return date;
        const duration = Math.max(5, durationForDoctor(doctorId) || 30);
        const next = new Date(now);
        const remainder = next.getMinutes() % duration;
        if (remainder !== 0) next.setMinutes(next.getMinutes() + (duration - remainder));
        next.setSeconds(0, 0);
        return next;
    }
    window.medclinicIsPastAppointmentSlot = isPastSlot;
    function paymentClass(ev) {
        if (ev.hasPaidInvoice) return "paid";
        const t = (ev.paymentType || "").toLowerCase();
        if (t === "qr_secore_paid" || t === "invoice_paid" || t === "paid") return "paid";
        return "unpaid";
    }
    function paymentText(ev) {
        const t = (ev.paymentType || "").toLowerCase();
        if (t === "qr_secore_paid") return "Оплачено через QR SECORE";
        return paymentClass(ev) === "paid" ? "Оплачено" : "Не оплачено";
    }
    function paymentTypeLabel(ev) {
        const t = (ev.paymentType || "").toLowerCase();
        if (t === "qr_secore" || t === "qr_secore_paid") return "QR SECORE";
        if (t === "qr_external") return "QR внешняя";
        if (t === "card") return "Карта";
        if (t === "cash") return "Наличные";
        if (t === "online") return "Онлайн";
        if (t === "insurance") return "Страховка";
        if (t === "invoice_paid" || t === "paid") return "По счёту";
        return "Не указан";
    }

    function syncDetailsPaymentBadges(eventItem) {
        const payLabel = paymentText(eventItem);
        const payCls = paymentClass(eventItem);
        const badge = $("appointmentDetailsPaymentBadge");
        if (badge) {
            badge.textContent = payLabel;
            badge.className = `appointment-details-modal__pay-badge status-${payCls}`;
        }
        const chip = $("appointmentDetailsPaymentChip");
        if (!chip) return;

        const methodLabel = paymentTypeLabel(eventItem);
        const hideChip =
            !methodLabel ||
            methodLabel === "Не указан" ||
            methodLabel === payLabel ||
            (payCls === "paid" && methodLabel === "Оплачено");

        chip.classList.toggle("d-none", hideChip);
        chip.textContent = hideChip ? "" : methodLabel;
    }
    function isQrSecoreType(ev) {
        const t = (ev?.paymentType || "").toLowerCase();
        return t === "qr_secore";
    }
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
        if (pageType === "doctor" || isOwnSchedule) return p.currentDoctorId || getRegistryDoctorId() || "";
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
    function getRegistryDoctorId() {
        if (isOwnSchedule && p.currentDoctorId) return p.currentDoctorId;
        return $("registryDoctorSelect")?.value || "";
    }
    function ensureOwnScheduleDoctorSelect() {
        const select = $("registryDoctorSelect");
        const doctorId = p.currentDoctorId || "";
        if (!select || !doctorId) return;
        if (Array.from(select.options).some((option) => option.value === doctorId)) {
            select.value = doctorId;
            return;
        }
        const doc = state.doctorFilters.find((x) => x.value === doctorId);
        if (!doc) return;
        select.innerHTML = `<option value="${esc(doc.value)}">${esc(doc.label)}</option>`;
        select.value = doc.value;
        const deptSelect = $("registryDepartmentSelect");
        const deptId = (doc.departmentIds || [])[0];
        if (deptSelect && deptId && Array.from(deptSelect.options).some((option) => option.value === deptId)) {
            deptSelect.value = deptId;
        }
    }
    function registryDoctorsFiltered() {
        const deptId = ($("registryDepartmentSelect")?.value || "").trim();
        return state.doctorFilters.filter((doc) => {
            const deptIds = doc.departmentIds || [];
            if (!deptId) return true;
            return deptIds.some((id) => String(id) === String(deptId));
        });
    }
    function ensureRegistryDefaultDepartment() {
        const deptSelect = $("registryDepartmentSelect");
        if (!deptSelect) return;
        const val = (deptSelect.value || "").trim();
        if (val === "") return;
        const ok = state.departments.some((d) => String(d.value) === String(val));
        if (!ok) deptSelect.value = "";
    }
    function refreshRegistryDoctorSelect(preserveDoctorId) {
        const select = $("registryDoctorSelect");
        if (!select) return;
        const docs = registryDoctorsFiltered();
        const current = preserveDoctorId !== undefined ? preserveDoctorId : select.value;

        select.disabled = false;
        select.innerHTML = `<option value="">Все врачи</option>${docs.map((doc) => `<option value="${esc(doc.value)}">${esc(doc.label)}</option>`).join("")}`;

        if (current === "" || current === "all") {
            select.value = "";
        } else if (docs.some((doc) => String(doc.value) === String(current))) {
            select.value = current;
        } else {
            select.value = "";
        }
    }
    function setupRegistryFilters() {
        restoreRegistryFilters();
        setupRegistryPatientFilter();
        if (isOwnSchedule) return;
        $("registryDepartmentSelect")?.addEventListener("change", () => {
            refreshRegistryDoctorSelect();
            saveRegistryFilters();
            renderAll();
        });
        $("registryDoctorSelect")?.addEventListener("change", () => {
            saveRegistryFilters();
            renderAll();
        });
    }
    function updateRegistryScheduleCaption() {
        const cap = $("registryScheduleDoctorCaption");
        const countBadge = $("registryScheduleDayCount");
        if (!cap) return;
        const doctorId = getRegistryDoctorId();
        const dateStr = fd(state.anchor, { weekday: "long", day: "numeric", month: "long" });
        const patient = findPatientById(state.selectedPatientId);
        const patientSuffix = patient ? ` · ${patient.name}` : "";
        const dayItems = filteredEvents().filter((ev) => sameDay(ev.start, state.anchor));

        if (!doctorId) {
            cap.textContent = `Все врачи · ${dateStr}${patientSuffix} · расписание и свободные слоты`;
            if (countBadge) {
                countBadge.hidden = false;
                const count = dayItems.length;
                countBadge.textContent = count ? `${count} ${count === 1 ? "запись" : count < 5 ? "записи" : "записей"}` : "Нет записей";
            }
            return;
        }

        const meta = doctorMeta(doctorId);
        cap.textContent = meta.subtitle
            ? `${dateStr} · ${meta.name} · ${meta.subtitle}${patientSuffix}`
            : `${dateStr} · ${meta.name}${patientSuffix}`;
        if (countBadge) {
            const count = doctorDayEventsAll(doctorId).filter(eventMatchesPatientFilter).length;
            countBadge.hidden = false;
            countBadge.textContent = count ? `${count} ${count === 1 ? "запись" : count < 5 ? "записи" : "записей"}` : "Нет записей";
        }
    }
    function selectedDoctors() {
        if (pageType === "doctor") return state.doctorFilters;
        if ($("registryDoctorSelect")) {
            const doctorId = getRegistryDoctorId();
            if (!doctorId) return registryDoctorsFiltered();
            const doc = state.doctorFilters.find((x) => x.value === doctorId);
            return doc ? [doc] : [];
        }
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
            if (state.selectedPatientId) items = items.filter(eventMatchesPatientFilter);
            if (state.paymentFilter !== "all") items = items.filter((x) => paymentClass(x) === state.paymentFilter);
        }
        return items.sort((a, b) => a.start - b.start);
    }
    function eventsForDate(date) { return filteredEvents().filter((x) => sameDay(x.start, date)).sort((a, b) => a.start - b.start); }
    function allHours() { return Array.from({ length: 10 }, (_, i) => 8 + i); }
    function setPeriodLabel() {
        const label = $("schedulePeriodLabel");
        if (label) {
            if (state.view === "day") { label.textContent = fd(state.anchor, { weekday: "long", day: "numeric", month: "long" }); }
            else if (state.view === "week") {
                const s = startOfWeek(state.anchor); const e = addDays(s, 6);
                label.textContent = `${fd(s, { day: "numeric", month: "long" })} — ${fd(e, { day: "numeric", month: "long" })}`;
            } else {
                label.textContent = fd(state.anchor, { month: "long", year: "numeric" });
            }
        }
        syncRegistryFilterDate();
    }
    function syncRegistryFilterDate() {
        const weekday = $("registryFilterWeekday");
        const dateLine = $("registryFilterDate");
        if (!weekday && !dateLine) return;
        const isToday = sameDay(state.anchor, new Date());
        if (weekday) {
            weekday.textContent = isToday
                ? `Сегодня, ${fd(state.anchor, { weekday: "long" })}`
                : fd(state.anchor, { weekday: "long" });
        }
        if (dateLine) dateLine.textContent = fd(state.anchor, { day: "numeric", month: "long" });
    }
    function syncDatePicker() {
        const picker = $("scheduleDatePicker");
        if (picker && picker.type === "date") picker.value = toIsoDate(state.anchor);
        syncRegistryMonthUi();
    }

    const MONTH_NAMES_SHORT = ["Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек"];
    let registryPickerYear = null;
    let registryPickerMonth = null;

    function setAnchorDate(year, monthIndex, day) {
        const lastDay = new Date(year, monthIndex + 1, 0).getDate();
        const safeDay = Math.min(Math.max(1, day), lastDay);
        state.anchor = new Date(year, monthIndex, safeDay);
        saveRegistryFilters();
        renderAll();
    }

    function shiftAnchorMonth(delta) {
        const d = new Date(state.anchor);
        d.setDate(1);
        d.setMonth(d.getMonth() + delta);
        const day = state.anchor.getDate();
        const lastDay = new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
        d.setDate(Math.min(day, lastDay));
        state.anchor = d;
        saveRegistryFilters();
        renderAll();
    }

    function syncRegistryMonthUi() {
        const label = $("scheduleMonthLabel");
        if (label) {
            label.textContent = fd(state.anchor, { month: "long", year: "numeric" });
        }
        const panel = $("scheduleDatePanel");
        if (panel && !panel.hidden && registryPickerYear != null && registryPickerMonth != null) {
            drawRegistryDatePanel();
        }
    }

    function drawRegistryDatePanel() {
        if (registryPickerYear == null || registryPickerMonth == null) return;
        drawRegistryMonthGrid(registryPickerYear);
        drawRegistryDayGrid(registryPickerYear, registryPickerMonth);
    }

    function drawRegistryMonthGrid(year) {
        const grid = $("scheduleMonthGrid");
        const yearLabel = $("scheduleYearLabel");
        if (!grid) return;
        registryPickerYear = year;
        if (yearLabel) yearLabel.textContent = String(year);
        const now = new Date();
        grid.innerHTML = MONTH_NAMES_SHORT.map((name, index) => {
            const isActive = registryPickerYear === year && registryPickerMonth === index;
            const isNow = now.getFullYear() === year && now.getMonth() === index;
            const classes = ["registry-v2-month-chip"];
            if (isActive) classes.push("is-active");
            if (isNow) classes.push("is-now");
            return `<button type="button" class="${classes.join(" ")}" data-month-index="${index}" role="option" aria-selected="${isActive}">${esc(name)}</button>`;
        }).join("");
        grid.querySelectorAll("[data-month-index]").forEach((btn) => {
            btn.addEventListener("click", (e) => {
                e.stopPropagation();
                const monthIndex = Number(btn.dataset.monthIndex);
                if (Number.isNaN(monthIndex)) return;
                registryPickerMonth = monthIndex;
                drawRegistryDatePanel();
            });
        });
    }

    function drawRegistryDayGrid(year, monthIndex) {
        const grid = $("scheduleDayGrid");
        if (!grid) return;
        const first = new Date(year, monthIndex, 1);
        const lastDay = new Date(year, monthIndex + 1, 0).getDate();
        const startOffset = (first.getDay() + 6) % 7;
        const today = new Date();
        const selectedDay = state.anchor.getDate();
        const selectedSameMonth =
            state.anchor.getFullYear() === year && state.anchor.getMonth() === monthIndex;

        let html = "";
        for (let i = 0; i < startOffset; i += 1) {
            html += '<span class="registry-v2-day-chip registry-v2-day-chip--empty" aria-hidden="true"></span>';
        }
        for (let day = 1; day <= lastDay; day += 1) {
            const isActive = selectedSameMonth && selectedDay === day;
            const isToday =
                today.getFullYear() === year &&
                today.getMonth() === monthIndex &&
                today.getDate() === day;
            const classes = ["registry-v2-day-chip"];
            if (isActive) classes.push("is-active");
            if (isToday) classes.push("is-today");
            html += `<button type="button" class="${classes.join(" ")}" data-day="${day}">${day}</button>`;
        }
        grid.innerHTML = html;
        grid.querySelectorAll("[data-day]").forEach((btn) => {
            btn.addEventListener("click", (e) => {
                e.stopPropagation();
                const day = Number(btn.dataset.day);
                if (Number.isNaN(day)) return;
                setAnchorDate(year, monthIndex, day);
                closeRegistryDatePanel();
            });
        });
    }

    function registryDatePanelTrigger() {
        return $("scheduleDateCore");
    }

    function openRegistryDatePanel() {
        const panel = $("scheduleDatePanel");
        const toggle = registryDatePanelTrigger();
        if (!panel || !toggle) return;
        registryPickerYear = state.anchor.getFullYear();
        registryPickerMonth = state.anchor.getMonth();
        drawRegistryDatePanel();
        panel.hidden = false;
        toggle.setAttribute("aria-expanded", "true");
        toggle.closest(".registry-v2-month-popover-wrap")?.classList.add("is-open");
    }

    function closeRegistryDatePanel() {
        const panel = $("scheduleDatePanel");
        const toggle = registryDatePanelTrigger();
        if (!panel || !toggle) return;
        panel.hidden = true;
        toggle.setAttribute("aria-expanded", "false");
        toggle.closest(".registry-v2-month-popover-wrap")?.classList.remove("is-open");
    }

    function toggleRegistryDatePanel() {
        const panel = $("scheduleDatePanel");
        if (!panel) return;
        if (panel.hidden) openRegistryDatePanel();
        else closeRegistryDatePanel();
    }

    function removeRegistryMonthNavButtons() {
        $("scheduleMonthPrev")?.remove();
        $("scheduleMonthNext")?.remove();
        document
            .querySelectorAll(
                ".registry-v2-period__shell button.registry-v2-period__icon-btn--sm, " +
                    ".registry-v2-period__shell button[title*='месяц'], " +
                    ".registry-v2-period__shell button[aria-label*='месяц']"
            )
            .forEach((btn) => btn.remove());
        document.querySelectorAll(".registry-v2-period__shell .registry-v2-period__sep").forEach((el) => el.remove());
    }

    function setupRegistryMonthPicker() {
        removeRegistryMonthNavButtons();
        const core = $("scheduleDateCore");
        if (!core) return;
        core.addEventListener("click", (e) => {
            e.preventDefault();
            e.stopPropagation();
            toggleRegistryDatePanel();
        });
        $("scheduleYearPrev")?.addEventListener("click", (e) => {
            e.stopPropagation();
            if (registryPickerYear == null) registryPickerYear = state.anchor.getFullYear();
            if (registryPickerMonth == null) registryPickerMonth = state.anchor.getMonth();
            registryPickerYear -= 1;
            drawRegistryDatePanel();
        });
        $("scheduleYearNext")?.addEventListener("click", (e) => {
            e.stopPropagation();
            if (registryPickerYear == null) registryPickerYear = state.anchor.getFullYear();
            if (registryPickerMonth == null) registryPickerMonth = state.anchor.getMonth();
            registryPickerYear += 1;
            drawRegistryDatePanel();
        });
        document.addEventListener("click", (e) => {
            if (!e.target.closest(".registry-v2-month-popover-wrap")) closeRegistryDatePanel();
        });
        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") closeRegistryDatePanel();
        });
        syncRegistryMonthUi();
    }
    function setHint(message) { const hint = $("appointmentScheduleHint"); if (hint) { hint.classList.toggle("d-none", !message); hint.textContent = message || ""; } }
    function applyEditorMode(mode) {
        const isVisit = mode === "visit";
        const createShell = $("appointmentEditorCreateShell");
        const visitShell = $("appointmentEditorVisitShell");
        if (createShell) createShell.classList.toggle("d-none", isVisit);
        if (visitShell) visitShell.classList.toggle("d-none", !isVisit);
        $("appointmentSaveDraftButton")?.classList.toggle("d-none", !isVisit);
        document.querySelectorAll("[data-editor-section]").forEach((node) => {
            const sectionMode = node.getAttribute("data-editor-section") || "";
            if (sectionMode.includes("visit")) {
                node.style.display = isVisit ? "" : "none";
            }
        });

        const editorBody = $("appointmentForm");
        if (editorBody) {
            editorBody.classList.toggle("appointment-editor-body--single", !isVisit);
        }
    }
    function updateCreateContext(doctorId, slotDate, endDate) {
        const nameEl = $("appointmentCreateDoctorName");
        const slotEl = $("appointmentCreateSlotSummary");
        const meta = doctorMeta(doctorId);
        if (nameEl) nameEl.textContent = meta.subtitle ? `${meta.name} · ${meta.subtitle}` : meta.name;
        if (slotEl && slotDate) {
            const end = endDate || slotDate;
            slotEl.textContent = `${fd(slotDate, { weekday: "long", day: "numeric", month: "long" })} · ${ft(slotDate)} – ${ft(end)}`;
        }
    }
    function syncPaymentStatusControl(eventItem) {
        const select = $("appointmentPaymentType");
        const hint = $("appointmentPaymentTypeHint");
        if (!select || select.type === "hidden") return;

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
            if (isPastSlot(dateValue, m)) continue;

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
        if ($("appointmentSelectedServices") && typeof window.medclinicRenderSelectedServices === "function") {
            window.medclinicRenderSelectedServices();
            return;
        }
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

    function formatAppointmentStatus(status) {
        const value = (status || "").toLowerCase();
        if (value === "busy" || value === "active") return "Активна";
        if (value === "draft") return "Черновик";
        if (value === "cancelled") return "Отменена";
        return status || "—";
    }

    function formatPatientBirthLine(ev) {
        const birthDate = ev?.patientBirthDate;
        if (!birthDate) return "—";
        const parts = String(birthDate).split("-");
        if (parts.length === 3) {
            const label = `${parts[2]}.${parts[1]}.${parts[0]}`;
            const age = ev.patientAge;
            return age != null && !Number.isNaN(Number(age)) ? `${label} (${age} лет)` : label;
        }
        return birthDate;
    }

    function openDetails(eventItem) {
        showDetailsMainView();
        state.selectedEvent = eventItem;
        state.detailsQrInvoiceId = null;
        showDetailsMainView();
        if ($("appointmentDetailsTitle")) $("appointmentDetailsTitle").textContent = eventTitle(eventItem);
        if ($("appointmentDetailsSubtitle")) {
            $("appointmentDetailsSubtitle").textContent = `${fd(eventItem.start, { weekday: "long", day: "numeric", month: "long", year: "numeric" })} · ${ft(eventItem.start)} – ${ft(eventItem.end)}`;
        }
        syncDetailsPaymentBadges(eventItem);
        const set = (id, value, fallback = "—") => {
            const node = $(id);
            if (!node) return;
            node.textContent = value || fallback;
            if (id === "appointmentDetailsNotes" || id === "appointmentDetailsHistory") {
                node.classList.toggle("is-empty", !(value || "").trim() || value === fallback);
            }
        };
        const patientName = eventItem.patientName || "—";
        set("appointmentDetailsPatient", patientName);
        const avatar = $("appointmentDetailsPatientAvatar");
        if (avatar) {
            const letter = (patientName || "?").trim().charAt(0).toUpperCase() || "?";
            avatar.textContent = letter;
        }
        set("appointmentDetailsPhone", eventItem.phone);
        set("appointmentDetailsGender", eventItem.patientGender);
        set("appointmentDetailsBirthDate", formatPatientBirthLine(eventItem));
        set("appointmentDetailsDoctor", eventItem.doctor || doctorMeta(eventItem.doctorId).name);
        set("appointmentDetailsDateTime", `${fd(eventItem.start, { day: "numeric", month: "long", year: "numeric" })}, ${ft(eventItem.start)} – ${ft(eventItem.end)}`);
        set("appointmentDetailsStatus", formatAppointmentStatus(eventItem.status));
        const historyItems = state.events
            .filter((x) => x.id !== eventItem.id && eventItem.patientId && x.patientId === eventItem.patientId)
            .sort((a, b) => b.start - a.start)
            .slice(0, 5);
        const historyText = historyItems.length
            ? historyItems
                .map((x) => `${fd(x.start, { day: "2-digit", month: "2-digit", year: "numeric" })} ${ft(x.start)} · ${paymentText(x)}`)
                .join("\n")
            : "";
        set("appointmentDetailsHistory", historyText, "Предыдущих приёмов не найдено.");
        set("appointmentDetailsReferral", eventItem.referralSource);
        const noteModel = parseMedicalNotes(eventItem.notes || "");
        const summary = [
            noteModel.complaints && `Жалобы: ${noteModel.complaints}`,
            noteModel.diagnosis && `Диагноз: ${noteModel.diagnosis}`,
            noteModel.recommendations && `Рекомендации: ${noteModel.recommendations}`,
            noteModel.researchReferral && `Направления: ${noteModel.researchReferral}`,
            noteModel.comment && `Комментарий: ${noteModel.comment}`
        ]
            .filter(Boolean)
            .join("\n\n");
        set("appointmentDetailsNotes", summary || eventItem.notes || "—", "Комментарий не указан.");
        const serviceBox = $("appointmentDetailsServices");
        const services = eventItem.services || [];
        let servicesTotal = 0;
        if (serviceBox) {
            if (!services.length) {
                serviceBox.innerHTML = '<div class="ad-services__empty">Услуги не выбраны</div>';
            } else {
                serviceBox.innerHTML = services
                    .map((x) => {
                        const qty = Math.max(1, Number(x.quantity || 1));
                        const line = Number(x.priceTyiyn || 0) * qty;
                        servicesTotal += line;
                        const name = esc(x.name || "Услуга");
                        return `<div class="ad-service-row" role="listitem"><span class="ad-service-row__name" title="${name}">${name}</span><span class="ad-service-row__qty">× ${qty}</span><span class="ad-service-row__price">${fm(line)}</span></div>`;
                    })
                    .join("");
            }
        }
        const totalNode = $("appointmentDetailsServicesTotal");
        if (totalNode) {
            totalNode.textContent = services.length ? `Итого: ${fm(servicesTotal)}` : "";
        }
        syncDetailsActionButtons(eventItem);
        detailsModal?.show();
    }

    function syncDetailsActionButtons(ev) {
        const qrBtn = $("appointmentDetailsShowQrButton");
        const paidBtn = $("appointmentDetailsMarkPaidButton");
        const downloadBtn = $("appointmentDetailsDownloadInvoiceButton");
        const showQr = !!ev && isQrSecoreType(ev) && paymentClass(ev) !== "paid";
        const showPaid = !!ev && canShowMarkPaid(ev);
        const showDownload = !!ev && paymentClass(ev) !== "paid";
        if (qrBtn) qrBtn.classList.toggle("d-none", !showQr);
        if (paidBtn) paidBtn.classList.toggle("d-none", !showPaid);
        if (downloadBtn) downloadBtn.classList.toggle("d-none", !showDownload);
    }

    async function downloadAppointmentInvoicePdf() {
        const ev = state.selectedEvent;
        if (!ev?.id) return;
        if (!appointmentInvoiceUrl) {
            window.MedclinicUI?.showToast("Скачивание счёта недоступно.", "warning");
            return;
        }
        const btn = $("appointmentDetailsDownloadInvoiceButton");
        window.MedclinicUI?.setButtonLoading?.(btn, true, { label: "Подготовка…" });
        try {
            const response = await fetch(`${appointmentInvoiceUrl}?appointmentId=${encodeURIComponent(ev.id)}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const { result } = await readJsonResponse(response);
            if (!response.ok || !result.success || !result.invoiceId) {
                throw new Error(result.message || "Не удалось получить счёт.");
            }
            window.open(`/Invoices/DownloadPdf?id=${encodeURIComponent(result.invoiceId)}`, "_blank", "noopener,noreferrer");
        } catch (error) {
            window.MedclinicUI?.showToast(error.message || "Не удалось скачать счёт.", "danger");
        } finally {
            window.MedclinicUI?.setButtonLoading?.(btn, false);
        }
    }

    function showDetailsMainView() {
        const main = $("appointmentDetailsMainView");
        const qr = $("appointmentDetailsQrView");
        if (main) main.classList.remove("d-none");
        if (qr) {
            qr.classList.add("d-none");
            qr.setAttribute("aria-hidden", "true");
        }
    }

    function showDetailsQrView() {
        const main = $("appointmentDetailsMainView");
        const qr = $("appointmentDetailsQrView");
        if (main) main.classList.add("d-none");
        if (qr) {
            qr.classList.remove("d-none");
            qr.setAttribute("aria-hidden", "false");
        }
    }

    function renderAppointmentQr(bodyEl, payload) {
        if (!bodyEl) return;
        const link = payload?.qrLink;
        const b64 = payload?.qrCodeBase64;
        const payCode = payload?.payCode || "";
        const amount = Number(payload?.amountTyiyn || 0);
        const logoSrc = "/web/kindergarten/img/secore-logo.png";
        const amountHtml = amount > 0 ? `<div class="ad-qr-amount">${esc(fm(amount))}</div>` : "";
        const payCodeHtml = payCode ? `<span class="ad-qr-paycode">Лицевой счёт: ${esc(payCode)}</span>` : "";
        const hint = `<p class="ad-qr-hint">Отсканируйте код в приложении банка для оплаты${payCodeHtml}</p>${amountHtml}`;

        if (b64) {
            bodyEl.innerHTML =
                `<div class="ad-qr-frame"><img src="data:image/png;base64,${b64}" alt="QR для оплаты" width="280" height="280" /><img class="ad-qr-logo" src="${logoSrc}" alt="SECORE" width="52" height="52" /></div>${hint}`;
            return;
        }

        if (link && typeof QRCode !== "undefined") {
            bodyEl.innerHTML =
                `<div class="ad-qr-frame"><canvas id="appointmentPayQrCanvas" width="280" height="280" aria-label="QR для оплаты"></canvas><img class="ad-qr-logo" src="${logoSrc}" alt="SECORE" width="52" height="52" /></div>${hint}`;
            const canvas = document.getElementById("appointmentPayQrCanvas");
            QRCode.toCanvas(canvas, link, {
                width: 280,
                margin: 1,
                color: { dark: "#0c4a6e", light: "#ffffff" }
            }, (err) => {
                if (err) bodyEl.innerHTML = '<div class="ad-qr-error">Не удалось построить QR</div>';
            });
            return;
        }

        bodyEl.innerHTML = '<div class="ad-qr-error">QR ещё не сформирован для этой записи.</div>';
    }

    async function openAppointmentQrScreen() {
        const ev = state.selectedEvent;
        if (!ev?.id || !paymentQrUrl) {
            window.MedclinicUI?.showToast("QR недоступен для этой записи.", "warning");
            return;
        }
        showDetailsQrView();
        const body = $("appointmentDetailsQrBody");
        const sub = $("appointmentDetailsQrSubtitle");
        const download = $("appointmentDetailsQrDownload");
        const waBtn = $("appointmentDetailsQrWhatsApp");
        if (sub) sub.textContent = eventTitle(ev);
        if (body) body.innerHTML = '<div class="ad-qr-loading">Загрузка QR…</div>';
        if (download) {
            download.href = "#";
            download.classList.add("is-disabled");
        }
        if (waBtn) waBtn.disabled = true;
        state.detailsQrInvoiceId = null;

        window.MedclinicUI?.showPageOverlay?.("Загрузка QR…");
        try {
            const response = await fetch(`${paymentQrUrl}?appointmentId=${encodeURIComponent(ev.id)}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const { result } = await readJsonResponse(response);
            if (!response.ok || !result.success) {
                throw new Error(result.message || "Не удалось загрузить QR.");
            }
            state.detailsQrInvoiceId = result.invoiceId || null;
            if (sub) {
                const bits = [eventTitle(ev)];
                if (result.payCode) bits.push(`Л/с ${result.payCode}`);
                sub.textContent = bits.join(" · ");
            }
            renderAppointmentQr(body, result);
            if (download && result.invoiceId) {
                download.href = `/Invoices/DownloadPdf?id=${encodeURIComponent(result.invoiceId)}`;
                download.classList.remove("is-disabled");
            }
            if (waBtn) waBtn.disabled = !result.invoiceId;
        } catch (error) {
            if (body) body.innerHTML = `<div class="ad-qr-error">${esc(error.message || "Ошибка загрузки QR")}</div>`;
        } finally {
            window.MedclinicUI?.hidePageOverlay?.();
        }
    }

    async function sendAppointmentInvoiceWhatsApp() {
        const ev = state.selectedEvent;
        const invoiceId = state.detailsQrInvoiceId;
        if (!ev?.id || !invoiceId || !sendInvoiceWhatsAppUrl) return;
        const btn = $("appointmentDetailsQrWhatsApp");
        window.MedclinicUI?.showPageOverlay?.("Отправка в WhatsApp…");
        try {
            const response = await fetch(sendInvoiceWhatsAppUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    ...(token() ? { RequestVerificationToken: token() } : {})
                },
                body: JSON.stringify({ appointmentId: ev.id, invoiceId })
            });
            const { result } = await readJsonResponse(response);
            if (!response.ok || !result.success) {
                throw new Error(result.message || "Не удалось отправить в WhatsApp.");
            }
            window.MedclinicUI?.showToast(result.message || "Счёт поставлен в очередь WhatsApp.", "success");
        } catch (error) {
            window.MedclinicUI?.showToast(error.message || "Не удалось отправить в WhatsApp.", "danger");
        } finally {
            window.MedclinicUI?.hidePageOverlay?.();
            window.MedclinicUI?.setButtonLoading?.(btn, false);
        }
    }

    function openVisitFromDetails() {
        if (!state.selectedEvent) return;
        if (!canManageAppointments) {
            window.MedclinicUI?.showToast("Недостаточно прав для проведения приёма.", "danger");
            return;
        }
        state.returnToDetails = true;
        $("appointmentEditorBackToDetails")?.classList.remove("d-none");
        openEditor(state.selectedEvent, state.selectedEvent.start);
    }

    function setRescheduleHint(message) {
        const hint = $("appointmentRescheduleHint");
        if (!hint) return;
        hint.classList.toggle("d-none", !message);
        hint.textContent = message || "";
    }

    function setRescheduleFormBusy(busy) {
        const fieldset = $("appointmentRescheduleFieldset");
        if (fieldset) fieldset.disabled = busy;
        $("appointmentRescheduleModal")?.querySelectorAll('[data-bs-dismiss="modal"], .appointment-reschedule-back').forEach((el) => {
            el.disabled = busy;
        });
    }

    function renderRescheduleSlots() {
        const box = $("appointmentRescheduleSlots");
        const doctorId = $("appointmentRescheduleDoctorId")?.value || "";
        const dateValue = $("appointmentRescheduleDate")?.value || "";
        const appointmentId = $("appointmentRescheduleId")?.value || "";
        const summary = $("appointmentRescheduleSlotSummary");
        if (!box) return;
        if (!doctorId || !dateValue) {
            box.innerHTML = '<span class="text-muted small">Выберите дату приёма.</span>';
            return;
        }
        const schedule = doctorSchedule(doctorId, dateValue);
        if (!schedule || !schedule.isWorking) {
            box.innerHTML = '<span class="text-muted small">Врач не принимает в этот день.</span>';
            return;
        }
        const duration = durationForDoctor(doctorId);
        const start = timeToMin(schedule.startTime);
        const end = timeToMin(schedule.endTime);
        if (start == null || end == null || end <= start) {
            box.innerHTML = '<span class="text-muted small">Нет доступных слотов.</span>';
            return;
        }
        const unavailable = unavailableIntervalsForDoctorDate(doctorId, dateValue, appointmentId);
        const selectedStart = $("appointmentRescheduleStart")?.value || "";
        const html = [];
        for (let m = start; m + duration <= end; m += duration) {
            const overlapsBusy = unavailable.some((interval) => m < interval.end && m + duration > interval.start);
            if (overlapsBusy) continue;
            if (isPastSlot(dateValue, m)) continue;
            const s = minutesToTime(m);
            const e = minutesToTime(m + duration);
            const active = selectedStart === s ? " is-active" : "";
            html.push(`<button type="button" class="appointment-reschedule-slot${active}" data-start="${s}" data-end="${e}">${s}</button>`);
        }
        if (!html.length) {
            box.innerHTML = '<span class="text-muted small">Свободных слотов на этот день нет.</span>';
            return;
        }
        box.innerHTML = html.join("");
        box.querySelectorAll(".appointment-reschedule-slot").forEach((btn) => {
            btn.addEventListener("click", () => {
                box.querySelectorAll(".appointment-reschedule-slot").forEach((b) => b.classList.remove("is-active"));
                btn.classList.add("is-active");
                if ($("appointmentRescheduleStart")) $("appointmentRescheduleStart").value = btn.dataset.start || "";
                if ($("appointmentRescheduleEnd")) $("appointmentRescheduleEnd").value = btn.dataset.end || "";
                if (summary) summary.textContent = `Выбрано: ${btn.dataset.start} – ${btn.dataset.end}`;
                validateRescheduleForm();
            });
        });
    }

    function validateRescheduleForm() {
        const btn = $("appointmentRescheduleSaveButton");
        if (!btn) return;
        const name = ($("appointmentReschedulePatientName")?.value || "").trim();
        const dateValue = $("appointmentRescheduleDate")?.value || "";
        const start = $("appointmentRescheduleStart")?.value || "";
        const end = $("appointmentRescheduleEnd")?.value || "";
        const hasServices = (state.selectedServices?.length || 0) > 0;
        const isPast = dateValue && start ? isPastSlot(dateValue, start) : false;
        setRescheduleHint(isPast ? "Нельзя перенести запись на прошедшее время." : "");
        btn.disabled = !name || !dateValue || !start || !end || !hasServices || isPast;
    }
    window.medclinicValidateRescheduleForm = validateRescheduleForm;

    function openRescheduleModal(ev) {
        if (!ev?.id || !canManageAppointments) return;
        state.selectedEvent = ev;
        state.returnToDetails = true;
        const notesModel = parseMedicalNotes(ev.notes || "");
        if ($("appointmentRescheduleId")) $("appointmentRescheduleId").value = ev.id;
        if ($("appointmentRescheduleDoctorId")) $("appointmentRescheduleDoctorId").value = ev.doctorId || "";
        if ($("appointmentReschedulePatientId")) $("appointmentReschedulePatientId").value = ev.patientId || "";
        if ($("appointmentReschedulePaymentType")) $("appointmentReschedulePaymentType").value = ev.paymentType || "unpaid";
        if ($("appointmentReschedulePatientName")) $("appointmentReschedulePatientName").value = ev.patientName || "";
        if ($("appointmentReschedulePhone")) $("appointmentReschedulePhone").value = ev.phone || "";
        if ($("appointmentRescheduleEmail")) $("appointmentRescheduleEmail").value = ev.email || "";
        if ($("appointmentRescheduleComment")) $("appointmentRescheduleComment").value = notesModel.comment || (ev.notes && !String(ev.notes).startsWith("__medjson__") ? ev.notes : "") || "";
        if ($("appointmentRescheduleDate")) {
            $("appointmentRescheduleDate").min = toIsoDate(new Date());
            $("appointmentRescheduleDate").value = toIsoDate(ev.start);
        }
        if ($("appointmentRescheduleStart")) $("appointmentRescheduleStart").value = ft(ev.start);
        if ($("appointmentRescheduleEnd")) $("appointmentRescheduleEnd").value = ft(ev.end);
        const meta = doctorMeta(ev.doctorId);
        const ctx = $("appointmentRescheduleContext");
        if (ctx) {
            ctx.innerHTML = `<strong>${esc(meta.name)}</strong><span>${esc(meta.subtitle)}</span><span>${esc(paymentTypeLabel(ev))} · ${esc(paymentText(ev))}</span>`;
        }
        if ($("appointmentRescheduleSubtitle")) {
            $("appointmentRescheduleSubtitle").textContent = `${fd(ev.start, { weekday: "long", day: "numeric", month: "long" })} · ${ft(ev.start)} – ${ft(ev.end)}`;
        }
        state.selectedServices = (ev.services || []).map((x) => ({
            organizationServiceId: x.organizationServiceId || null,
            serviceName: x.name || "Услуга",
            priceTyiyn: Number(x.priceTyiyn || 0),
            quantity: Number(x.quantity || 1)
        }));
        window.medclinicRenderRescheduleServices?.();
        window.medclinicRefreshRescheduleCatalog?.();
        renderRescheduleSlots();
        const slotSummary = $("appointmentRescheduleSlotSummary");
        if (slotSummary) slotSummary.textContent = `Текущий слот: ${ft(ev.start)} – ${ft(ev.end)}`;
        validateRescheduleForm();
        setRescheduleHint("");
        detailsModal?.hide();
        rescheduleModal?.show();
    }

    async function saveReschedule() {
        const btn = $("appointmentRescheduleSaveButton");
        if (btn?.disabled || !p.saveUrl) return;
        const appointmentDate = $("appointmentRescheduleDate")?.value || "";
        const startTime = $("appointmentRescheduleStart")?.value || "";
        if (isPastSlot(appointmentDate, startTime)) {
            setRescheduleHint("Нельзя перенести запись на прошедшее время.");
            window.MedclinicUI?.showToast?.("Нельзя перенести запись на прошедшее время.", "danger");
            return;
        }
        const appointmentId = $("appointmentRescheduleId")?.value || "";
        const patientName = ($("appointmentReschedulePatientName")?.value || "").trim();
        const services = (state.selectedServices || []).map((s) => ({
            organizationServiceId: s.organizationServiceId || null,
            serviceName: s.serviceName || null,
            priceTyiyn: Number(s.priceTyiyn || 0),
            quantity: Math.max(1, Number(s.quantity || 1))
        }));
        const request = {
            appointmentId,
            patientId: $("appointmentReschedulePatientId")?.value || null,
            patientName,
            doctorId: $("appointmentRescheduleDoctorId")?.value || "",
            phone: $("appointmentReschedulePhone")?.value || null,
            email: $("appointmentRescheduleEmail")?.value || null,
            comment: (() => {
                const plain = ($("appointmentRescheduleComment")?.value || "").trim();
                const evNotes = state.selectedEvent?.notes || "";
                if (String(evNotes).startsWith("__medjson__")) {
                    const model = parseMedicalNotes(evNotes);
                    return `__medjson__${JSON.stringify({ ...model, comment: plain || model.comment || "" })}`;
                }
                return plain || null;
            })(),
            appointmentDate: $("appointmentRescheduleDate")?.value || "",
            startTime: $("appointmentRescheduleStart")?.value || "",
            endTime: $("appointmentRescheduleEnd")?.value || "",
            paymentType: $("appointmentReschedulePaymentType")?.value || "unpaid",
            appointmentStatus: "active",
            isActive: true,
            usePhoneAsWhatsApp: true,
            services
        };
        window.MedclinicUI?.setButtonLoading?.(btn, true, { label: "Сохранение..." });
        setRescheduleFormBusy(true);
        window.MedclinicUI?.showPageOverlay?.("Сохранение изменений…");
        try {
            const response = await fetch(p.saveUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    ...(token() ? { RequestVerificationToken: token() } : {})
                },
                body: JSON.stringify(request)
            });
            const { result } = await readJsonResponse(response);
            if (!response.ok || !result.success) throw new Error(result.message || "Не удалось сохранить изменения.");
            if (result.eventItem) upsertEventFromServer(result.eventItem);
            state.returnToDetails = false;
            state.selectedEvent = null;
            rescheduleModal?.hide();
            detailsModal?.hide();
            saveRegistryFilters();
            window.medclinicRenderAll?.();
            const toastKind =
                result.message &&
                (String(result.message).includes("не обновлён") || String(result.message).includes("не создан"))
                    ? "warning"
                    : "success";
            window.MedclinicUI?.showToast(result.message || "Запись обновлена.", toastKind);
        } catch (error) {
            window.MedclinicUI?.showToast(error.message || "Не удалось сохранить изменения.", "danger");
        } finally {
            setRescheduleFormBusy(false);
            window.MedclinicUI?.hidePageOverlay?.();
            window.MedclinicUI?.setButtonLoading?.(btn, false);
        }
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
        if (!isExistingEvent) {
            state.returnToDetails = false;
            $("appointmentEditorBackToDetails")?.classList.add("d-none");
        }
        applyEditorMode(isExistingEvent ? "visit" : "create");
        if ($("appointmentEditorTitle")) $("appointmentEditorTitle").textContent = isExistingEvent ? "Проведение приёма" : "Новая запись";
        if ($("appointmentEditorSubtitle")) {
            $("appointmentEditorSubtitle").textContent = isExistingEvent
                ? `${eventTitle(ev)} · ${fd(ev.start, { day: "numeric", month: "long" })} ${ft(ev.start)} – ${ft(ev.end)}`
                : "";
        }
        const saveBtn = $("appointmentSaveButton");
        if (saveBtn) {
            saveBtn.textContent = isExistingEvent ? "Сохранить приём" : "Зарегистрировать запись";
            saveBtn.dataset.loadingText = isExistingEvent ? "Сохранение..." : "Регистрация...";
        }
        if (!ev || !ev.id) {
            ["appointmentId", "appointmentPatientId", "appointmentPatientName", "appointmentPhone", "appointmentPatientGender", "appointmentPatientBirthDate"].forEach((id) => { if ($(id)) $(id).value = ""; });
            if ($("appointmentReferral")) $("appointmentReferral").value = "";
            if ($("appointmentPatient")) $("appointmentPatient").selectedIndex = 0;
            const doctorId = ev?.doctorId || (pageType === "registry" ? getRegistryDoctorId() : "") || p.currentDoctorId || "";
            let date = slotDate || new Date();
            date = clampToUpcomingSlot(date, doctorId);
            if ($("appointmentDoctor")) $("appointmentDoctor").value = doctorId;
            if ($("appointmentDate")) $("appointmentDate").value = toIsoDate(date);
            if ($("appointmentStart")) $("appointmentStart").value = `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
            const end = new Date(date);
            end.setMinutes(end.getMinutes() + durationForDoctor(doctorId));
            if ($("appointmentEnd")) $("appointmentEnd").value = `${String(end.getHours()).padStart(2, "0")}:${String(end.getMinutes()).padStart(2, "0")}`;
            if ($("appointmentPaymentType")) $("appointmentPaymentType").value = "qr_secore";
            const defaultPay = document.querySelector('input[name="appointmentPaymentChoice"][value="qr_secore"]');
            if (defaultPay) defaultPay.checked = true;
            window.medclinicAppointmentCreate?.syncPayment?.();
            if ($("appointmentStatus")) $("appointmentStatus").value = "active";
            if ($("appointmentIsActive")) $("appointmentIsActive").checked = true;
            fillMedicalFields("");
            state.selectedServices = [];
            renderServices();
            updateCreateContext(doctorId, date, end);
            applyDoctorDefaults(doctorId);
        } else {
            if ($("appointmentId")) $("appointmentId").value = ev.id || "";
            if ($("appointmentPatient")) $("appointmentPatient").value = ev.patientId || "";
            if ($("appointmentPatientId")) $("appointmentPatientId").value = ev.patientId || "";
            if ($("appointmentPatientName")) $("appointmentPatientName").value = ev.patientName || "";
            if ($("appointmentDoctor")) $("appointmentDoctor").value = ev.doctorId || "";
            if ($("appointmentPhone")) $("appointmentPhone").value = ev.phone || "";
            if ($("appointmentDate")) $("appointmentDate").value = toIsoDate(ev.start);
            if ($("appointmentStart")) $("appointmentStart").value = ft(ev.start);
            if ($("appointmentEnd")) $("appointmentEnd").value = ft(ev.end);
            if ($("appointmentVisitPatientName")) $("appointmentVisitPatientName").value = ev.patientName || eventTitle(ev);
            if ($("appointmentVisitDateTime")) $("appointmentVisitDateTime").value = `${fd(ev.start, { day: "numeric", month: "long", year: "numeric" })} · ${ft(ev.start)} – ${ft(ev.end)}`;
            if ($("appointmentReferral")) $("appointmentReferral").value = ev.referralSource || "";
            if ($("appointmentPaymentType")) $("appointmentPaymentType").value = ev.paymentType || "unpaid";
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

    function isAppointmentPaid(ev) {
        if (!ev) return false;
        return !!ev.hasPaidInvoice || paymentClass(ev) === "paid";
    }

    async function cancelSelected() {
        if (!state.selectedEvent?.id || !p.cancelUrl) return;
        const ui = window.MedclinicUI || {};
        const ev = state.selectedEvent;
        const details = `<strong>${esc(eventTitle(ev))}</strong><br><span class="text-muted">${esc(fd(ev.start, { weekday: "long", day: "numeric", month: "long" }))} · ${esc(ft(ev.start))}–${esc(ft(ev.end))}</span>`;

        let refundPayment = false;
        if (isAppointmentPaid(ev)) {
            const choice = await (ui.confirmPaidCancel
                ? ui.confirmPaidCancel({
                      title: "Отменить оплаченную запись?",
                      message: "По записи уже была оплата. Отменить с возвратом суммы или без возврата?",
                      details
                  })
                : Promise.resolve(null));
            if (choice === null || choice === undefined) return;
            refundPayment = choice === "refund";
        } else {
            const confirmed = await (ui.confirm
                ? ui.confirm({
                      title: "Отменить запись?",
                      message: "Запись будет снята с расписания.",
                      details,
                      confirmLabel: "Да, отменить",
                      cancelLabel: "Нет"
                  })
                : Promise.resolve(window.confirm("Отменить выбранную запись?")));
            if (!confirmed) return;
        }

        ui.showPageOverlay?.("Отмена записи…");
        try {
            const response = await fetch(p.cancelUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    ...(token() ? { RequestVerificationToken: token() } : {})
                },
                body: JSON.stringify({ appointmentId: ev.id, refundPayment })
            });
            const { result } = await readJsonResponse(response);
            if (!response.ok || !result.success) throw new Error(result.message || "Не удалось отменить запись.");
            const cancelledId = ev.id;
            state.events = state.events.filter((x) => x.id !== cancelledId);
            state.selectedEvent = null;
            detailsModal?.hide();
            rescheduleModal?.hide();
            saveRegistryFilters();
            renderAll();
            ui.showToast?.(result.message || "Запись отменена.", "success");
        } catch (error) {
            ui.showToast?.(error.message || "Не удалось отменить запись.", "danger");
        } finally {
            ui.hidePageOverlay?.();
        }
    }

    function validateSchedule() {
        if (!$("appointmentSaveButton")) return;
        const createShell = $("appointmentEditorCreateShell");
        const isCreateMode = createShell && !createShell.classList.contains("d-none");
        if (isCreateMode) {
            setHint("");
            const doctorId = resolveDoctorIdForEditor();
            const hasServices = (state.selectedServices?.length || 0) > 0;
            const patientName = ($("appointmentPatientName")?.value || "").trim();
            const dateValue = $("appointmentDate")?.value || "";
            const startValue = $("appointmentStart")?.value || "";
            const endValue = $("appointmentEnd")?.value || "";
            if (!p.storageReady) {
                setHint("Сначала примените SQL-скрипт appointments.");
                $("appointmentSaveButton").disabled = true;
                return;
            }
            if (dateValue && startValue && isPastSlot(dateValue, startValue)) {
                setHint("Нельзя записать на прошедшее время.");
                $("appointmentSaveButton").disabled = true;
                return;
            }
            $("appointmentSaveButton").disabled =
                !doctorId || !hasServices || !patientName || !dateValue || !startValue || !endValue;
            return;
        }
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
        bindMarkPaidButtons(root);
    }

    function appointmentTotalTyiyn(ev) {
        return (ev?.services || []).reduce((sum, x) => {
            const qty = Math.max(1, Number(x.quantity || 1));
            return sum + Number(x.priceTyiyn || 0) * qty;
        }, 0);
    }

    async function markAppointmentPaid(appointmentId) {
        if (!appointmentId || !markPaidUrl) return;
        const ev = state.events.find((x) => x.id === appointmentId);
        const ui = window.MedclinicUI || {};
        const total = ev ? appointmentTotalTyiyn(ev) : 0;
        const amountLine = ev
            ? `<div class="mt-2"><span class="text-muted">Сумма к принятию:</span> <strong style="font-size:1.15em">${esc(fm(total))}</strong></div>`
            : "";
        const confirmed = await (ui.confirm
            ? ui.confirm({
                title: "Отметить оплаченным?",
                message: "Статус записи и связанного счёта будет изменён на «Оплачено».",
                details: ev
                    ? `<strong>${esc(eventTitle(ev))}</strong><br><span class="text-muted">${esc(fd(ev.start, { day: "numeric", month: "long" }))} · ${esc(ft(ev.start))}–${esc(ft(ev.end))}</span>${amountLine}`
                    : "",
                confirmLabel: "Да, оплачено",
                cancelLabel: "Отмена"
            })
            : Promise.resolve(window.confirm(
                ev
                    ? `Отметить запись и связанный счёт как оплаченные?\n\n${eventTitle(ev)}\nСумма к принятию: ${fm(total)}`
                    : "Отметить запись и связанный счёт как оплаченные?"
            )));
        if (!confirmed) return;

        window.MedclinicUI?.showPageOverlay?.("Обновление оплаты…");
        try {
            const response = await fetch(markPaidUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    ...(token() ? { RequestVerificationToken: token() } : {})
                },
                body: JSON.stringify({ appointmentId })
            });
            const { result } = await readJsonResponse(response);
            if (!response.ok || !result.success) {
                throw new Error(result.message || "Не удалось сменить статус оплаты.");
            }
            if (result.eventItem) upsertEventFromServer(result.eventItem);
            saveRegistryFilters();
            renderAll();
            if (state.selectedEvent?.id === appointmentId && result.eventItem) {
                const updated = state.events.find((x) => x.id === appointmentId);
                if (updated) {
                    state.selectedEvent = updated;
                    syncDetailsActionButtons(updated);
                    syncDetailsPaymentBadges(updated);
                }
            }
            window.MedclinicUI?.showToast(result.message || "Статус оплаты обновлён.", "success");
        } catch (error) {
            window.MedclinicUI?.showToast(error.message || "Не удалось сменить статус оплаты.", "danger");
        } finally {
            window.MedclinicUI?.hidePageOverlay?.();
        }
    }

    function bindMarkPaidButtons(root) {
        root.querySelectorAll("[data-mark-paid-id]").forEach((btn) => {
            btn.addEventListener("click", (e) => {
                e.preventDefault();
                e.stopPropagation();
                markAppointmentPaid(btn.getAttribute("data-mark-paid-id"));
            });
        });
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
            const dt = new Date(state.anchor); dt.setHours(Number(row.dataset.hour), 0, 0, 0);
            if (dt.getTime() < Date.now()) {
                window.MedclinicUI?.showToast?.("Нельзя записать на прошедшее время.", "warning");
                return;
            }
            openEditor(null, dt);
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

    function upsertEventFromServer(item) {
        if (!item) return;
        const ev = {
            ...item,
            start: new Date(item.start),
            end: new Date(item.end),
            services: (item.services || []).map((x) => ({
                ...x,
                priceTyiyn: Number(x.priceTyiyn || 0),
                quantity: Number(x.quantity || 1)
            }))
        };
        const idx = state.events.findIndex((x) => x.id === ev.id);
        if (idx >= 0) state.events[idx] = ev;
        else state.events.push(ev);
    }

    function canShowMarkPaid(ev) {
        if (!canMarkAsPaidManually || !markPaidUrl) return false;
        if (paymentClass(ev) === "paid") return false;
        const t = (ev.paymentType || "").toLowerCase();
        if (t === "qr_secore" || t === "qr_secore_paid") return false;
        return t === "qr_external" || t === "card" || t === "cash" || t === "unpaid";
    }

    function canShowMarkPaidOnCard(ev) {
        return pageType === "registry" && canShowMarkPaid(ev);
    }

    function registryCardServiceLine(ev) {
        return (ev.services || []).map((x) => x.name).filter(Boolean).join(", ");
    }

    function registryCardMetaItems(ev, meta, options = {}) {
        const hideTime = options.hideTime === true;
        const hideDoctor = options.hideDoctor === true;
        const doctorName = meta?.name || ev.doctor || "Врач не указан";
        const deptLine = (meta?.subtitle || "").trim();
        const phone = (ev.phone || "").trim();
        const items = [];

        if (!hideTime) {
            items.push(`<span class="slot-card__meta-item slot-card__meta-item--time">${esc(ft(ev.start))}–${esc(ft(ev.end))}</span>`);
        }
        if (!hideDoctor) {
            items.push(`<span class="slot-card__meta-item slot-card__meta-item--doctor">${esc(doctorName)}</span>`);
        }
        if (deptLine) items.push(`<span class="slot-card__meta-item">${esc(deptLine)}</span>`);
        if (phone) items.push(`<span class="slot-card__meta-item slot-card__meta-item--phone">${esc(phone)}</span>`);

        return items.join('<span class="slot-card__meta-sep" aria-hidden="true">·</span>');
    }

    function registryCardBody(ev, meta, options = {}) {
        const patientName = eventTitle(ev);
        const services = registryCardServiceLine(ev);
        const serviceHtml = services ? `<div class="slot-card__service">${esc(services)}</div>` : "";

        return `<div class="slot-card__head"><div class="slot-card__identity"><div class="slot-patient">${esc(patientName)}</div>${serviceHtml}</div><span class="status-tag status-${paymentClass(ev)}">${paymentText(ev)}</span></div><div class="slot-card__meta">${registryCardMetaItems(ev, meta, options)}</div>`;
    }

    function registryCard(ev, meta, options = {}) {
        const markBtn = canShowMarkPaidOnCard(ev)
            ? `<div class="slot-card__pay-action"><button type="button" class="registry-mark-paid-btn" data-mark-paid-id="${esc(ev.id)}">Отметить оплаченным</button></div>`
            : "";
        return `<div class="slot-card slot-card--panel ${paymentClass(ev)}"><button type="button" class="slot-card__main" data-event-id="${esc(ev.id)}">${registryCardBody(ev, meta, options)}</button>${markBtn}</div>`;
    }
    function appointmentOverlapsMinutes(ev, startMin, duration) {
        const a = ev.start.getHours() * 60 + ev.start.getMinutes();
        const b = ev.end.getHours() * 60 + ev.end.getMinutes();
        return startMin < b && startMin + duration > a;
    }
    function eventStartMinutes(ev) {
        return ev.start.getHours() * 60 + ev.start.getMinutes();
    }
    function displaySlotMinuteForEvent(ev, gridStart, gridEnd, slotDuration) {
        const evStart = eventStartMinutes(ev);
        for (let minute = gridStart; minute + slotDuration <= gridEnd; minute += slotDuration) {
            if (minute <= evStart && evStart < minute + slotDuration) {
                return minute;
            }
        }
        for (let minute = gridStart; minute + slotDuration <= gridEnd; minute += slotDuration) {
            if (appointmentOverlapsMinutes(ev, minute, slotDuration)) {
                return minute;
            }
        }
        return null;
    }
    function registryCardContinued(ev, meta, options = {}) {
        return `<button type="button" class="slot-card slot-card--panel slot-card--continued ${paymentClass(ev)}" data-event-id="${esc(ev.id)}">${registryCardBody(ev, meta, options)}</button>`;
    }
    function buildDoctorScheduleRowsHtml(doctorId) {
        const dateIso = toIsoDate(state.anchor);
        const meta = doctorMeta(doctorId);
        const schedule = doctorSchedule(doctorId, dateIso);
        if (!schedule || !schedule.isWorking) {
            return { status: "off", meta, rows: "" };
        }

        const duration = durationForDoctor(doctorId);
        const start = timeToMin(schedule.startTime);
        const end = timeToMin(schedule.endTime);
        if (start == null || end == null || end <= start) {
            return { status: "invalid", meta, rows: "" };
        }

        const unavailable = unavailableIntervalsForDoctorDate(doctorId, dateIso, "");
        const allDayEvents = doctorDayEventsAll(doctorId);
        const dayEvents = state.selectedPatientId
            ? allDayEvents.filter(eventMatchesPatientFilter)
            : allDayEvents;
        const displaySlotByEventId = new Map(
            dayEvents.map((ev) => [ev.id, displaySlotMinuteForEvent(ev, start, end, duration)])
        );
        const cardOptions = { hideTime: true, hideDoctor: true };
        const rows = [];

        for (let minute = start; minute + duration <= end; minute += duration) {
            const slotStart = minutesToTime(minute);
            const slotEnd = minutesToTime(minute + duration);
            const primaryEvents = dayEvents.filter((ev) => displaySlotByEventId.get(ev.id) === minute);
            const overlappingEvents = dayEvents.filter((ev) => appointmentOverlapsMinutes(ev, minute, duration));
            const continuedEvents = overlappingEvents.filter((ev) => displaySlotByEventId.get(ev.id) !== minute);
            const isBlocked = unavailable.some((interval) => minute < interval.end && minute + duration > interval.start);
            const occupiedByOtherPatient = slotOccupiedByOtherPatient(minute, duration, doctorId);

            if (primaryEvents.length > 0) {
                const body = primaryEvents.map((ev) => registryCard(ev, meta, cardOptions)).join("");
                rows.push(`<div class="registry-v2-row registry-v2-row--busy"><div class="registry-v2-row__time">${esc(slotStart)}<span class="registry-v2-row__time-end">${esc(slotEnd)}</span></div><div class="registry-v2-row__body registry-v2-row__body--stack">${body}</div></div>`);
            } else if (continuedEvents.length > 0) {
                const body = continuedEvents.map((ev) => registryCardContinued(ev, meta, cardOptions)).join("");
                rows.push(`<div class="registry-v2-row registry-v2-row--busy registry-v2-row--continued"><div class="registry-v2-row__time">${esc(slotStart)}<span class="registry-v2-row__time-end">${esc(slotEnd)}</span></div><div class="registry-v2-row__body registry-v2-row__body--stack">${body}</div></div>`);
            } else if (isBlocked || occupiedByOtherPatient) {
                rows.push(`<div class="registry-v2-row registry-v2-row--blocked"><div class="registry-v2-row__time">${esc(slotStart)}</div><div class="registry-v2-row__body"><span class="registry-v2-blocked">${occupiedByOtherPatient ? "Занято" : "Недоступно"}</span></div></div>`);
            } else if (isPastSlot(dateIso, minute)) {
                rows.push(`<div class="registry-v2-row registry-v2-row--blocked registry-v2-row--past"><div class="registry-v2-row__time">${esc(slotStart)}<span class="registry-v2-row__time-end">${esc(slotEnd)}</span></div><div class="registry-v2-row__body"><span class="registry-v2-blocked">Прошло</span></div></div>`);
            } else {
                rows.push(`<button type="button" class="registry-v2-row registry-v2-row--free" data-slot-minute="${minute}" data-doctor-id="${esc(doctorId)}"><div class="registry-v2-row__time">${esc(slotStart)}<span class="registry-v2-row__time-end">${esc(slotEnd)}</span></div><div class="registry-v2-row__body"><span class="registry-v2-free-label">Свободно — записать</span></div></button>`);
            }
        }

        return { status: "ok", meta, rows: rows.join("") };
    }
    function wrapDoctorScheduleTimeline(rowsHtml, options = {}) {
        if (!rowsHtml) return "";
        const head = options.showHead !== false
            ? '<div class="registry-v2-timeline__head"><div>Время</div><div>Приём</div></div>'
            : "";
        return `<div class="registry-v2-timeline">${head}${rowsHtml}</div>`;
    }
    function buildDoctorScheduleSectionHtml(doctorId, options = {}) {
        const built = buildDoctorScheduleRowsHtml(doctorId);
        const meta = built.meta;
        if (built.status === "off") {
            return `<section class="registry-v2-doctor-section registry-v2-doctor-section--off"><div class="registry-v2-doctor-section__head"><div><strong class="registry-v2-doctor-section__name">${esc(meta.name)}</strong>${meta.subtitle ? `<span class="registry-v2-doctor-section__sub">${esc(meta.subtitle)}</span>` : ""}</div><span class="registry-v2-doctor-section__badge">Не принимает</span></div></section>`;
        }
        if (built.status === "invalid") {
            return `<section class="registry-v2-doctor-section registry-v2-doctor-section--off"><div class="registry-v2-doctor-section__head"><div><strong class="registry-v2-doctor-section__name">${esc(meta.name)}</strong>${meta.subtitle ? `<span class="registry-v2-doctor-section__sub">${esc(meta.subtitle)}</span>` : ""}</div><span class="registry-v2-doctor-section__badge">Смена не задана</span></div></section>`;
        }
        if (!built.rows) {
            return `<section class="registry-v2-doctor-section registry-v2-doctor-section--off"><div class="registry-v2-doctor-section__head"><div><strong class="registry-v2-doctor-section__name">${esc(meta.name)}</strong>${meta.subtitle ? `<span class="registry-v2-doctor-section__sub">${esc(meta.subtitle)}</span>` : ""}</div><span class="registry-v2-doctor-section__badge">Нет слотов</span></div></section>`;
        }

        const headBlock = options.showDoctorHead
            ? `<div class="registry-v2-doctor-section__head"><div><strong class="registry-v2-doctor-section__name">${esc(meta.name)}</strong>${meta.subtitle ? `<span class="registry-v2-doctor-section__sub">${esc(meta.subtitle)}</span>` : ""}</div></div>`
            : "";
        return `<section class="registry-v2-doctor-section">${headBlock}${wrapDoctorScheduleTimeline(built.rows)}</section>`;
    }
    function bindRegistryScheduleSlots(root) {
        bindEventButtons(root);
        root.querySelectorAll("[data-slot-minute]").forEach((btn) => btn.addEventListener("click", () => {
            if (!canManageAppointments) return;
            const minute = Number(btn.dataset.slotMinute);
            const doctorId = btn.dataset.doctorId || getRegistryDoctorId();
            if (!doctorId) return;
            const dateIso = toIsoDate(state.anchor);
            if (isPastSlot(dateIso, minute)) {
                window.MedclinicUI?.showToast?.("Нельзя записать на прошедшее время.", "warning");
                return;
            }
            const dt = new Date(`${dateIso}T00:00:00`);
            dt.setHours(Math.floor(minute / 60), minute % 60, 0, 0);
            openEditor({ doctorId }, dt);
        }));
    }
    function renderRegistryAllDoctorsDay() {
        registryGrid.className = "registry-v2-grid registry-v2-grid--multi";
        const docs = registryDoctorsFiltered();

        if (!docs.length) {
            registryGrid.innerHTML = '<div class="registry-v2-empty">Нет врачей по текущим фильтрам.</div>';
            return;
        }
        if (!state.doctorSchedulesReady) {
            const items = filteredEvents()
                .filter((ev) => sameDay(ev.start, state.anchor) && ev.isActive !== false)
                .sort((a, b) => a.start - b.start);
            if (!items.length) {
                registryGrid.innerHTML = '<div class="registry-v2-empty">Таблица смен врачей ещё не настроена.</div>';
                return;
            }
            const rows = items.map((ev) => {
                const meta = doctorMeta(ev.doctorId);
                return `<div class="registry-v2-row registry-v2-row--busy"><div class="registry-v2-row__time">${esc(ft(ev.start))}<span class="registry-v2-row__time-end">${esc(ft(ev.end))}</span></div><div class="registry-v2-row__body registry-v2-row__body--stack">${registryCard(ev, meta, { hideTime: true })}</div></div>`;
            }).join("");
            registryGrid.innerHTML = wrapDoctorScheduleTimeline(rows);
            bindEventButtons(registryGrid);
            return;
        }

        const sections = docs.map((doc) => buildDoctorScheduleSectionHtml(doc.value, { showDoctorHead: true }));
        registryGrid.innerHTML = sections.join("") || '<div class="registry-v2-empty">Нет расписания на выбранный день.</div>';
        bindRegistryScheduleSlots(registryGrid);
    }
    function renderRegistrySingleDoctorSchedule() {
        registryGrid.className = "registry-v2-grid";
        const doctorId = getRegistryDoctorId();

        if (!doctorId) {
            renderRegistryAllDoctorsDay();
            return;
        }
        if (!state.doctorSchedulesReady) {
            registryGrid.innerHTML = '<div class="registry-v2-empty">Таблица смен врачей ещё не настроена.</div>';
            return;
        }

        const built = buildDoctorScheduleRowsHtml(doctorId);
        const meta = built.meta;
        if (built.status === "off") {
            registryGrid.innerHTML = `<div class="registry-v2-empty"><strong>${esc(meta.name)}</strong> не принимает в этот день.</div>`;
            return;
        }
        if (built.status === "invalid") {
            registryGrid.innerHTML = '<div class="registry-v2-empty">Смена врача на этот день не задана.</div>';
            return;
        }

        registryGrid.innerHTML = built.rows
            ? wrapDoctorScheduleTimeline(built.rows)
            : '<div class="registry-v2-empty">Нет слотов в рамках смены врача.</div>';
        bindRegistryScheduleSlots(registryGrid);
    }
    function renderRegistryDay() {
        if ($("registryDoctorSelect")) {
            renderRegistrySingleDoctorSchedule();
            return;
        }
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
                html += `<div class="slot-col">${items.length ? `<div class="slot-list">${items.map((ev) => registryCard(ev, meta, { hideDoctor: true })).join("")}</div>` : ""}</div>`;
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
    function renderRegistryPage() {
        if (pageType === "registry") state.view = "day";
        setPeriodLabel();
        syncDatePicker();
        updateRegistryScheduleCaption();
        if ($("registryDoctorSelect")) {
            renderRegistrySingleDoctorSchedule();
            return;
        }
        selectedDoctorPills();
        renderAvailability();
        renderRegistryList();
        renderRegistryLoad();
        setRegistryTab(state.activeTab);
        if (state.view === "day") renderRegistryDay();
        else if (state.view === "week") renderRegistryWeek();
        else renderRegistryMonth();
    }
    function renderAll() { if (pageType === "doctor") renderDoctorPage(); else renderRegistryPage(); }
    function shiftByNav(direction) {
        if (pageType === "registry") state.view = "day";
        if (direction === "today") { state.anchor = state.view === "month" ? monthAnchor(new Date()) : new Date(); saveRegistryFilters(); renderAll(); return; }
        const k = direction === "prev" ? -1 : 1;
        if (state.view === "day") state.anchor = addDays(state.anchor, k);
        else if (state.view === "week") state.anchor = addDays(state.anchor, k * 7);
        else state.anchor = new Date(state.anchor.getFullYear(), state.anchor.getMonth() + k, 1);
        saveRegistryFilters();
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
    const legacyDatePicker = $("scheduleDatePicker");
    if (legacyDatePicker) {
        legacyDatePicker.addEventListener("change", (e) => {
            const date = new Date(`${e.target.value}T00:00:00`);
            if (!Number.isNaN(date.getTime())) {
                state.anchor = state.view === "month" ? monthAnchor(date) : date;
                saveRegistryFilters();
                renderAll();
            }
        });
    }
    $("appointmentCreateButton")?.addEventListener("click", () => {
        const dt = new Date(state.anchor);
        const doctorId = pageType === "registry" ? getRegistryDoctorId() : "";
        const schedule = doctorId ? doctorSchedule(doctorId, toIsoDate(state.anchor)) : null;
        const startMin = schedule?.isWorking ? timeToMin(schedule.startTime) : 9 * 60;
        const safeStart = startMin == null ? 9 * 60 : startMin;
        dt.setHours(Math.floor(safeStart / 60), safeStart % 60, 0, 0);
        openEditor(doctorId ? { doctorId } : null, dt);
    });
    $("appointmentPeriodAddButton")?.addEventListener("click", () => { const dt = state.selectedDate ? new Date(state.selectedDate) : new Date(); dt.setHours(9, 0, 0, 0); openEditor(null, dt); });
    $("appointmentDetailsOpenVisitButton")?.addEventListener("click", openVisitFromDetails);
    $("appointmentDetailsEditButton")?.addEventListener("click", () => { if (state.selectedEvent) openRescheduleModal(state.selectedEvent); });
    $("appointmentDetailsCancelButton")?.addEventListener("click", cancelSelected);
    $("appointmentDetailsShowQrButton")?.addEventListener("click", openAppointmentQrScreen);
    $("appointmentDetailsDownloadInvoiceButton")?.addEventListener("click", downloadAppointmentInvoicePdf);
    $("appointmentDetailsMarkPaidButton")?.addEventListener("click", () => {
        if (state.selectedEvent?.id) markAppointmentPaid(state.selectedEvent.id);
    });
    $("appointmentDetailsQrBackButton")?.addEventListener("click", showDetailsMainView);
    $("appointmentDetailsQrBackFooter")?.addEventListener("click", showDetailsMainView);
    $("appointmentDetailsQrWhatsApp")?.addEventListener("click", sendAppointmentInvoiceWhatsApp);
    $("appointmentEditorBackToDetails")?.addEventListener("click", () => {
        state.returnToDetails = false;
        editorModal?.hide();
        if (state.selectedEvent) {
            const fresh = state.events.find((x) => x.id === state.selectedEvent.id) || state.selectedEvent;
            openDetails(fresh);
        }
    });
    $("appointmentRescheduleBackButton")?.addEventListener("click", () => {
        state.returnToDetails = false;
        rescheduleModal?.hide();
        if (state.selectedEvent) openDetails(state.selectedEvent);
    });
    $("appointmentRescheduleDate")?.addEventListener("change", () => {
        if ($("appointmentRescheduleStart")) $("appointmentRescheduleStart").value = "";
        if ($("appointmentRescheduleEnd")) $("appointmentRescheduleEnd").value = "";
        renderRescheduleSlots();
        validateRescheduleForm();
    });
    $("appointmentReschedulePatientName")?.addEventListener("input", validateRescheduleForm);
    $("appointmentRescheduleSaveButton")?.addEventListener("click", saveReschedule);
    $("appointmentDetailsModal")?.addEventListener("hidden.bs.modal", showDetailsMainView);
    $("appointmentEditorModal")?.addEventListener("hidden.bs.modal", () => {
        $("appointmentEditorBackToDetails")?.classList.add("d-none");
        if (!state.returnToDetails) return;
        state.returnToDetails = false;
        const id = state.selectedEvent?.id;
        if (!id || !detailsModal) return;
        const fresh = state.events.find((x) => x.id === id) || state.selectedEvent;
        openDetails(fresh);
    });
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
        if ($("registryDoctorSelect")) {
            setupRegistryFilters();
            setupRegistryMonthPicker();
        } else {
            setupMultiselect("departments", state.departments, { list: "appointmentsDepartmentsList", trigger: "appointmentsDepartmentsTrigger", summary: "appointmentsDepartmentsSummary", menu: "appointmentsDepartmentsMenu", search: "appointmentsDepartmentSearch", selectAll: "appointmentsSelectAllDepartments" });
            setupMultiselect("doctors", state.doctorFilters, { list: "appointmentsDoctorsList", trigger: "appointmentsDoctorsTrigger", summary: "appointmentsDoctorsSummary", menu: "appointmentsDoctorsMenu", search: "appointmentsDoctorSearch", selectAll: "appointmentsSelectAllDoctors" });
            $("appointmentPaymentFilter")?.addEventListener("change", (e) => { state.paymentFilter = e.target.value || "all"; renderAll(); });
            $("registryQuickViewFilter")?.addEventListener("change", (e) => { setRegistryTab(e.target.value || "grid"); });
            document.querySelectorAll("#registryTabs .tab-btn").forEach((btn) => btn.addEventListener("click", () => { if ($("registryQuickViewFilter")) $("registryQuickViewFilter").value = btn.dataset.tab || "grid"; setRegistryTab(btn.dataset.tab || "grid"); }));
            $("availabilityDoctor")?.addEventListener("change", renderAvailability);
            $("availabilityDuration")?.addEventListener("change", renderAvailability);
        }
    }

    renderTemplates();
    renderServices();
    validateSchedule();
    window.medclinicValidateAppointmentForm = validateSchedule;
    window.medclinicUpsertEvent = upsertEventFromServer;
    window.medclinicRenderAll = renderAll;
    window.medclinicSaveRegistryFilters = saveRegistryFilters;
    renderAll();

    const deepLinkAppointmentId = (p.openAppointmentId || "").trim();
    if (deepLinkAppointmentId) {
        const deepLinkEvent = state.events.find((x) => x.id === deepLinkAppointmentId);
        if (deepLinkEvent) {
            state.anchor = new Date(deepLinkEvent.start);
            state.selectedEvent = deepLinkEvent;
            renderAll();
            openDetails(deepLinkEvent);
        }
    }
})();
