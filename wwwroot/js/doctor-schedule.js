(function () {
    const payload = window.doctorScheduleCalendar || { events: [], schedules: [], overrides: [] };
    const state = {
        view: "week",
        anchor: new Date(),
        events: (payload.events || [])
            .map((item) => ({
                ...item,
                start: new Date(item.start),
                end: new Date(item.end)
            }))
            .filter((item) => !Number.isNaN(item.start.getTime()) && !Number.isNaN(item.end.getTime())),
        schedules: payload.schedules || [],
        overrides: payload.overrides || []
    };

    const grid = document.getElementById("doctorCalendarGrid");
    const label = document.getElementById("doctorCalendarPeriodLabel");
    if (!grid || !label) return;

    const weekdayNames = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
    const shortDateFormatter = new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "short" });
    const periodMonthFormatter = new Intl.DateTimeFormat("ru-RU", { month: "long", year: "numeric" });

    function formatDate(date, options) {
        return new Intl.DateTimeFormat("ru-RU", options).format(date);
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function sameDay(a, b) {
        return a.getFullYear() === b.getFullYear()
            && a.getMonth() === b.getMonth()
            && a.getDate() === b.getDate();
    }

    function normalizeDayOfWeek(date) {
        return date.getDay() === 0 ? 7 : date.getDay();
    }

    function toIsoDate(date) {
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${date.getFullYear()}-${month}-${day}`;
    }

    function shiftMonth(date, delta) {
        return new Date(date.getFullYear(), date.getMonth() + delta, date.getDate());
    }

    function getScheduleDayOfWeek(item) {
        return Number(item.dayOfWeek ?? item.DayOfWeek);
    }

    function getScheduleStartTime(item) {
        return item.startTime || item.StartTime || "";
    }

    function getScheduleEndTime(item) {
        return item.endTime || item.EndTime || "";
    }

    function getOverrideDate(item) {
        return item.workDate || item.WorkDate || "";
    }

    function getOverrideIsWorking(item) {
        return Boolean(item.isWorking ?? item.IsWorking);
    }

    function getOverrideStartTime(item) {
        return item.startTime || item.StartTime || "";
    }

    function getOverrideEndTime(item) {
        return item.endTime || item.EndTime || "";
    }

    function getApplicableSchedule(date) {
        const overrideItem = state.overrides.find((item) => getOverrideDate(item) === toIsoDate(date));
        if (overrideItem) {
            if (!getOverrideIsWorking(overrideItem)) {
                return { isWorking: false, isOverride: true, text: "Выходной" };
            }

            const start = getOverrideStartTime(overrideItem);
            const end = getOverrideEndTime(overrideItem);
            return {
                isWorking: true,
                isOverride: true,
                startTime: start,
                endTime: end,
                text: `${start} - ${end}`
            };
        }

        const weeklyItem = state.schedules.find((item) => getScheduleDayOfWeek(item) === normalizeDayOfWeek(date)) || null;
        if (!weeklyItem) return { isWorking: false, isOverride: false, text: "Нет смены" };

        const start = getScheduleStartTime(weeklyItem);
        const end = getScheduleEndTime(weeklyItem);
        return {
            isWorking: true,
            isOverride: false,
            startTime: start,
            endTime: end,
            text: `${start} - ${end}`
        };
    }

    function getDayEvents(date) {
        return state.events
            .filter((eventItem) => sameDay(eventItem.start, date))
            .sort((left, right) => left.start - right.start);
    }

    function renderScheduleBadge(schedule) {
        if (!schedule || !schedule.isWorking) {
            const offText = schedule?.isOverride ? "Выходной (особый день)" : (schedule?.text || "Выходной");
            return `<span class="doctor-day-cell__schedule-badge doctor-day-cell__schedule-badge--off">${escapeHtml(offText)}</span>`;
        }

        const overrideClass = schedule.isOverride ? " doctor-day-cell__schedule-badge--override" : "";
        return `<span class="doctor-day-cell__schedule-badge${overrideClass}">${escapeHtml(schedule.text)}</span>`;
    }

    function renderEventCard(eventItem) {
        const start = eventItem.start.toTimeString().slice(0, 5);
        const end = eventItem.end.toTimeString().slice(0, 5);
        const title = eventItem.title || "Прием";
        const patientName = eventItem.patientName || "Пациент не указан";

        return `
            <div class="doctor-day-event">
                <div class="doctor-day-event__time">${start} - ${end}</div>
                <div class="doctor-day-event__title">${escapeHtml(title)}</div>
                <div class="doctor-day-event__patient">${escapeHtml(patientName)}</div>
            </div>
        `;
    }

    function renderEventsList(events) {
        if (events.length === 0) {
            return `<div class="doctor-day-cell__empty">Нет приемов</div>`;
        }

        const visible = events.slice(0, 4).map(renderEventCard).join("");
        const extra = events.length > 4
            ? `<div class="doctor-day-cell__more">+${events.length - 4} еще</div>`
            : "";

        return visible + extra;
    }

    function renderDayCell(date, options) {
        const opts = options || {};
        const schedule = getApplicableSchedule(date);
        const events = getDayEvents(date);
        const isToday = sameDay(date, new Date());
        const isSelected = sameDay(date, state.anchor);
        const isMuted = Boolean(opts.muted);
        const weekday = formatDate(date, { weekday: "short" });

        const classes = [
            "doctor-day-cell",
            isToday ? "doctor-day-cell--today" : "",
            isSelected ? "doctor-day-cell--selected" : "",
            isMuted ? "doctor-day-cell--muted" : ""
        ].filter(Boolean).join(" ");

        return `
            <div class="${classes}">
                <div class="doctor-day-cell__head">
                    <div class="doctor-day-cell__day">${date.getDate()}</div>
                    <div class="doctor-day-cell__weekday">${escapeHtml(weekday)}</div>
                </div>
                <div class="doctor-day-cell__schedule">
                    ${renderScheduleBadge(schedule)}
                </div>
                <div class="doctor-day-cell__events">
                    ${renderEventsList(events)}
                </div>
            </div>
        `;
    }

    function renderToolbarDateControls() {
        const strip = document.getElementById("doctorAnchorStrip");
        const quickCards = document.getElementById("doctorQuickCards");
        if (!strip || !quickCards) return;

        const stripButtons = [];
        for (let offset = -2; offset <= 2; offset++) {
            const date = new Date(state.anchor);
            date.setDate(state.anchor.getDate() + offset);
            const isActive = sameDay(date, state.anchor);
            stripButtons.push(`
                <button type="button" class="doctor-anchor-strip__item${isActive ? " is-active" : ""}" data-anchor-date="${toIsoDate(date)}">
                    ${escapeHtml(shortDateFormatter.format(date))}
                </button>
            `);
        }
        strip.innerHTML = stripButtons.join("");

        const quickItems = [
            { label: "Вчера", delta: -1 },
            { label: "Сегодня", delta: 0 },
            { label: "Завтра", delta: 1 }
        ];

        quickCards.innerHTML = quickItems.map((item) => {
            const date = new Date();
            date.setDate(date.getDate() + item.delta);
            const isActive = sameDay(date, state.anchor);
            const eventsCount = getDayEvents(date).length;
            return `
                <button type="button" class="doctor-quick-cards__item${isActive ? " is-active" : ""}" data-anchor-date="${toIsoDate(date)}">
                    <span class="doctor-quick-cards__label">${item.label}</span>
                    <span class="doctor-quick-cards__date">${escapeHtml(shortDateFormatter.format(date))}</span>
                    <span class="doctor-quick-cards__meta">${eventsCount} прием(ов)</span>
                </button>
            `;
        }).join("");
    }

    function renderWeek() {
        grid.className = "schedule-calendar__grid doctor-day-grid doctor-day-grid--week";

        const start = new Date(state.anchor);
        start.setDate(state.anchor.getDate() - ((state.anchor.getDay() + 6) % 7));
        const end = new Date(start);
        end.setDate(start.getDate() + 6);

        label.textContent = `${formatDate(start, { day: "numeric", month: "long" })} - ${formatDate(end, { day: "numeric", month: "long", year: "numeric" })}`;

        const cells = Array.from({ length: 7 }, (_, index) => {
            const day = new Date(start);
            day.setDate(start.getDate() + index);
            return renderDayCell(day, { muted: false });
        }).join("");

        grid.innerHTML = cells;
    }

    function renderMonth() {
        grid.className = "schedule-calendar__grid doctor-day-grid doctor-day-grid--month";
        label.textContent = periodMonthFormatter.format(state.anchor);

        const monthStart = new Date(state.anchor.getFullYear(), state.anchor.getMonth(), 1);
        const monthEnd = new Date(state.anchor.getFullYear(), state.anchor.getMonth() + 1, 0);
        const start = new Date(monthStart);
        start.setDate(monthStart.getDate() - ((monthStart.getDay() + 6) % 7));

        const headers = weekdayNames.map((dayName) => `<div class="schedule-weekday-header">${dayName}</div>`).join("");
        const totalCells = 42;
        const cells = Array.from({ length: totalCells }, (_, index) => {
            const day = new Date(start);
            day.setDate(start.getDate() + index);
            const muted = day < monthStart || day > monthEnd;
            return renderDayCell(day, { muted });
        }).join("");

        grid.innerHTML = headers + cells;
    }

    function updateSwitcherButtons() {
        document.querySelectorAll(".doctor-calendar-switcher [data-view]").forEach((button) => {
            const value = button.getAttribute("data-view") || "week";
            const isActive = value === state.view;
            button.classList.toggle("active", isActive);
            button.classList.toggle("btn-primary", isActive);
            button.classList.toggle("btn-outline-primary", !isActive);
        });
    }

    function render() {
        if (state.view === "month") {
            renderMonth();
        } else {
            renderWeek();
        }
        updateSwitcherButtons();
        renderToolbarDateControls();
    }

    function buildToolbar() {
        const toolbar = document.querySelector(".medclinic-surface.schedule-board .p-4.border-bottom");
        if (!toolbar) return null;

        toolbar.classList.add("doctor-calendar-toolbar");
        toolbar.innerHTML = `
            <div class="doctor-calendar-toolbar__left">
                <div>
                    <h2 class="h5 mb-1">Календарь врача</h2>
                    <p class="text-muted mb-0">Рабочее время и приемы сгруппированы по дням.</p>
                </div>
                <div id="doctorAnchorStrip" class="doctor-anchor-strip"></div>
                <div id="doctorQuickCards" class="doctor-quick-cards"></div>
            </div>
            <div class="doctor-calendar-toolbar__right">
                <div class="btn-group" role="group">
                    <button class="btn btn-outline-secondary doctor-calendar-nav" data-direction="prev" type="button">Назад</button>
                    <button class="btn btn-outline-secondary doctor-calendar-nav" data-direction="today" type="button">Сегодня</button>
                    <button class="btn btn-outline-secondary doctor-calendar-nav" data-direction="next" type="button">Вперед</button>
                </div>
                <div class="btn-group doctor-calendar-switcher" role="group">
                    <button class="btn btn-primary active" data-view="week" type="button">Неделя</button>
                    <button class="btn btn-outline-primary" data-view="month" type="button">Месяц</button>
                </div>
            </div>
        `;

        return toolbar;
    }

    const toolbar = buildToolbar();
    if (toolbar) {
        toolbar.addEventListener("click", (event) => {
            const button = event.target.closest("button");
            if (!button) return;

            const view = button.getAttribute("data-view");
            if (view === "week" || view === "month") {
                state.view = view;
                render();
                return;
            }

            const direction = button.getAttribute("data-direction");
            if (direction) {
                if (direction === "today") {
                    state.anchor = new Date();
                } else if (direction === "prev") {
                    state.anchor = state.view === "month"
                        ? shiftMonth(state.anchor, -1)
                        : new Date(state.anchor.getFullYear(), state.anchor.getMonth(), state.anchor.getDate() - 7);
                } else if (direction === "next") {
                    state.anchor = state.view === "month"
                        ? shiftMonth(state.anchor, 1)
                        : new Date(state.anchor.getFullYear(), state.anchor.getMonth(), state.anchor.getDate() + 7);
                }
                render();
                return;
            }

            const anchorDate = button.getAttribute("data-anchor-date");
            if (anchorDate) {
                const nextAnchor = new Date(`${anchorDate}T00:00:00`);
                if (!Number.isNaN(nextAnchor.getTime())) {
                    state.anchor = nextAnchor;
                    render();
                }
            }
        });
    }

    const overrideForm = document.getElementById("doctorScheduleOverrideForm");
    if (overrideForm) {
        const modeInputs = overrideForm.querySelectorAll('input[name="Mode"]');
        const startInput = overrideForm.querySelector('input[name="StartTime"]');
        const endInput = overrideForm.querySelector('input[name="EndTime"]');

        function syncOverrideMode() {
            const mode = overrideForm.querySelector('input[name="Mode"]:checked')?.value || "default";
            const disabled = mode !== "custom";
            if (startInput) startInput.disabled = disabled;
            if (endInput) endInput.disabled = disabled;
        }

        modeInputs.forEach((input) => input.addEventListener("change", syncOverrideMode));
        syncOverrideMode();

        document.querySelectorAll(".doctor-override-prefill").forEach((button) => {
            button.addEventListener("click", () => {
                const dateInput = overrideForm.querySelector('input[name="WorkDate"]');
                const commentInput = overrideForm.querySelector('input[name="Comment"]');
                const mode = button.getAttribute("data-mode") || "default";

                if (dateInput) dateInput.value = button.getAttribute("data-date") || "";
                if (startInput) startInput.value = button.getAttribute("data-start") || "08:00";
                if (endInput) endInput.value = button.getAttribute("data-end") || "18:00";
                if (commentInput) commentInput.value = button.getAttribute("data-comment") || "";

                const targetMode = overrideForm.querySelector(`input[name="Mode"][value="${mode}"]`);
                if (targetMode) {
                    targetMode.checked = true;
                    syncOverrideMode();
                }
            });
        });
    }

    render();
})();
