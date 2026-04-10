(function () {
    const payload = window.doctorScheduleCalendar || { events: [], schedules: [], overrides: [] };
    const state = {
        view: "week",
        anchor: new Date(),
        events: (payload.events || []).map((item) => ({
            ...item,
            start: new Date(item.start),
            end: new Date(item.end)
        })),
        schedules: payload.schedules || [],
        overrides: payload.overrides || []
    };

    const grid = document.getElementById("doctorCalendarGrid");
    const label = document.getElementById("doctorCalendarPeriodLabel");
    if (!grid || !label) return;

    const weekdayNames = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
    const calendarStartMinutes = 7 * 60;
    const calendarEndMinutes = 22 * 60;
    const calendarDuration = calendarEndMinutes - calendarStartMinutes;
    const hourMarks = Array.from({ length: 16 }, (_, index) => 7 + index);

    const formatDate = (date, options) => new Intl.DateTimeFormat("ru-RU", options).format(date);
    const sameDay = (a, b) => a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
    const normalizeDayOfWeek = (date) => date.getDay() === 0 ? 7 : date.getDay();
    const toIsoDate = (date) => date.toISOString().slice(0, 10);

    function parseMinutes(value) {
        if (!value || !value.includes(":")) return null;
        const [hours, minutes] = value.split(":").map(Number);
        if (Number.isNaN(hours) || Number.isNaN(minutes)) return null;
        return hours * 60 + minutes;
    }

    function clampToCalendar(minutes) {
        return Math.min(calendarEndMinutes, Math.max(calendarStartMinutes, minutes));
    }

    function minutesToPercent(minutes) {
        return ((minutes - calendarStartMinutes) / calendarDuration) * 100;
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
                return { isWorking: false, isOverride: true };
            }

            return {
                isWorking: true,
                isOverride: true,
                startTime: getOverrideStartTime(overrideItem),
                endTime: getOverrideEndTime(overrideItem)
            };
        }

        const weeklyItem = state.schedules.find((item) => getScheduleDayOfWeek(item) === normalizeDayOfWeek(date)) || null;
        if (!weeklyItem) return null;

        return {
            isWorking: true,
            isOverride: false,
            startTime: getScheduleStartTime(weeklyItem),
            endTime: getScheduleEndTime(weeklyItem)
        };
    }

    function shiftMonth(date, delta) {
        return new Date(date.getFullYear(), date.getMonth() + delta, 1);
    }

    function renderShiftTrack(schedule, compact) {
        if (!schedule || !schedule.isWorking) {
            return `
                <div class="doctor-shift-track doctor-shift-track--off">
                    <div class="doctor-shift-track__empty">${schedule?.isOverride ? "Индивидуальный выходной" : "Выходной"}</div>
                </div>
            `;
        }

        const startMinutes = parseMinutes(schedule.startTime);
        const endMinutes = parseMinutes(schedule.endTime);

        if (startMinutes == null || endMinutes == null || endMinutes <= startMinutes) {
            return `
                <div class="doctor-shift-track doctor-shift-track--off">
                    <div class="doctor-shift-track__empty">Смена не настроена</div>
                </div>
            `;
        }

        const left = Math.max(0, minutesToPercent(clampToCalendar(startMinutes)));
        const width = Math.max(6, minutesToPercent(clampToCalendar(endMinutes)) - left);
        const sizeClass = compact ? "doctor-shift-track--compact" : "";
        const overrideClass = schedule.isOverride ? "doctor-shift-track--override" : "";
        const suffix = schedule.isOverride ? " (особый день)" : "";

        return `
            <div class="doctor-shift-track ${sizeClass} ${overrideClass}">
                <div class="doctor-shift-track__fill" style="left:${left}%; width:${width}%;">
                    <span class="doctor-shift-track__label">${schedule.startTime} - ${schedule.endTime}${suffix}</span>
                </div>
            </div>
        `;
    } 

    function renderWeek() {
        grid.className = "doctor-week-timeline";

        const start = new Date(state.anchor);
        start.setDate(state.anchor.getDate() - ((state.anchor.getDay() + 6) % 7));
        const end = new Date(start);
        end.setDate(start.getDate() + 6);
        label.textContent = `${formatDate(start, { day: "numeric", month: "long" })} - ${formatDate(end, { day: "numeric", month: "long", year: "numeric" })}`;

        const days = Array.from({ length: 7 }, (_, index) => {
            const day = new Date(start);
            day.setDate(start.getDate() + index);
            return day;
        });

        const headerCells = [
            '<div class="doctor-week-timeline__corner"></div>',
            ...days.map((day) => `
                <div class="doctor-week-timeline__day-header">
                    <div class="doctor-week-timeline__day-name">${formatDate(day, { weekday: "short" })}</div>
                    <div class="doctor-week-timeline__day-date">${formatDate(day, { day: "numeric", month: "short" })}</div>
                </div>
            `)
        ].join("");

        const timeLabels = `
            <div class="doctor-week-timeline__time-column">
                ${hourMarks.map((hour) => `
                    <div class="doctor-week-timeline__time-label" style="top:${minutesToPercent(hour * 60)}%;">
                        ${hour.toString().padStart(2, "0")}:00
                    </div>
                `).join("")}
            </div>
        `;

        const dayColumns = days.map((day) => {
            const schedule = getApplicableSchedule(day);
            const dayEvents = state.events
                .filter((eventItem) => sameDay(eventItem.start, day))
                .sort((left, right) => left.start - right.start);

            let scheduleBlock = '<div class="doctor-week-timeline__day-off">Выходной</div>';
            if (schedule && schedule.isWorking) {
                const shiftStartRaw = parseMinutes(schedule.startTime);
                const shiftEndRaw = parseMinutes(schedule.endTime);
                if (shiftStartRaw != null && shiftEndRaw != null && shiftEndRaw > shiftStartRaw) {
                    const shiftStart = clampToCalendar(shiftStartRaw);
                    const shiftEnd = clampToCalendar(shiftEndRaw);
                    const shiftClass = schedule.isOverride
                        ? "doctor-week-timeline__shift doctor-week-timeline__shift--override"
                        : "doctor-week-timeline__shift";
                    const shiftLabel = schedule.isOverride
                        ? `${schedule.startTime} - ${schedule.endTime} • особый день`
                        : `${schedule.startTime} - ${schedule.endTime}`;

                    scheduleBlock = `
                        <div class="${shiftClass}" style="top:${minutesToPercent(shiftStart)}%; height:${Math.max(18, minutesToPercent(shiftEnd) - minutesToPercent(shiftStart))}%;">
                            <div class="doctor-week-timeline__shift-label">${shiftLabel}</div>
                        </div>
                    `;
                }
            } else if (schedule?.isOverride) {
                scheduleBlock = '<div class="doctor-week-timeline__day-off doctor-week-timeline__day-off--override">Индивидуальный выходной</div>';
            }

            const eventBlocks = dayEvents.map((eventItem) => {
                const startMinutes = clampToCalendar(eventItem.start.getHours() * 60 + eventItem.start.getMinutes());
                const endMinutes = clampToCalendar(eventItem.end.getHours() * 60 + eventItem.end.getMinutes());
                const top = minutesToPercent(startMinutes);
                const height = Math.max(20, minutesToPercent(endMinutes) - top);
                const patient = eventItem.patientName || "Без пациента";

                return `
                    <div class="doctor-week-timeline__event" style="top:${top}%; height:${height}%;">
                        <div class="doctor-week-timeline__event-time">${eventItem.start.toTimeString().slice(0, 5)} - ${eventItem.end.toTimeString().slice(0, 5)}</div>
                        <div class="doctor-week-timeline__event-title">${eventItem.title}</div>
                        <div class="doctor-week-timeline__event-patient">${patient}</div>
                    </div>
                `;
            }).join("");

            return `
                <div class="doctor-week-timeline__day-column">
                    <div class="doctor-week-timeline__hour-lines">
                        ${hourMarks.map((hour) => `
                            <div class="doctor-week-timeline__hour-line" style="top:${minutesToPercent(hour * 60)}%;"></div>
                        `).join("")}
                    </div>
                    ${scheduleBlock}
                    ${eventBlocks}
                </div>
            `;
        }).join("");

        grid.innerHTML = `${headerCells}${timeLabels}${dayColumns}`;
    }

    function renderMonth() {
        grid.className = "schedule-calendar__grid schedule-grid--month";
        label.textContent = formatDate(state.anchor, { month: "long", year: "numeric" });

        const monthStart = new Date(state.anchor.getFullYear(), state.anchor.getMonth(), 1);
        const start = new Date(monthStart);
        start.setDate(monthStart.getDate() - ((monthStart.getDay() + 6) % 7));

        const headers = weekdayNames.map((dayName) => `<div class="schedule-weekday-header">${dayName}</div>`).join("");
        const cells = Array.from({ length: 35 }, (_, index) => {
            const day = new Date(start);
            day.setDate(start.getDate() + index);
            const dayEvents = state.events.filter((eventItem) => sameDay(eventItem.start, day));
            const schedule = getApplicableSchedule(day);

            return `
                <div class="schedule-month-cell doctor-schedule-card doctor-schedule-card--month">
                    <div class="schedule-month-cell__title">${day.getDate()}</div>
                    <div class="doctor-schedule-background doctor-schedule-background--compact">
                        ${renderShiftTrack(schedule, true)}
                    </div>
                    ${dayEvents.slice(0, 3).map((eventItem) => `
                        <div class="schedule-event-chip schedule-event-chip--busy">
                            ${eventItem.start.toTimeString().slice(0, 5)} ${eventItem.title}
                        </div>
                    `).join("")}
                </div>
            `;
        }).join("");

        grid.innerHTML = headers + cells;
    }

    function render() {
        if (state.view === "month") {
            renderMonth();
            return;
        }

        renderWeek();
    }

    document.querySelectorAll(".doctor-calendar-switcher [data-view]").forEach((button) => {
        button.addEventListener("click", () => {
            state.view = button.getAttribute("data-view") || "week";
            document.querySelectorAll(".doctor-calendar-switcher [data-view]").forEach((candidate) => {
                candidate.classList.remove("active", "btn-primary");
                candidate.classList.add("btn-outline-primary");
            });
            button.classList.add("active", "btn-primary");
            button.classList.remove("btn-outline-primary");

            if (state.view === "month") {
                state.anchor = new Date(state.anchor.getFullYear(), state.anchor.getMonth(), 1);
            }

            render();
        });
    });

    document.querySelectorAll(".doctor-calendar-nav").forEach((button) => {
        button.addEventListener("click", () => {
            const direction = button.getAttribute("data-direction");
            if (direction === "today") {
                const today = new Date();
                state.anchor = state.view === "month"
                    ? new Date(today.getFullYear(), today.getMonth(), 1)
                    : today;
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
        });
    });

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
