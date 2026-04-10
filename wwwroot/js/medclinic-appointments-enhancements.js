(function () {
    const payload = window.medclinicSchedule || {};
    const patients = payload.patients || [];
    const saveUrl = payload.saveUrl || "";

    const modalElement = document.getElementById("appointmentEditorModal");
    const nameInput = document.getElementById("appointmentPatientName");
    const patientIdInput = document.getElementById("appointmentPatientId");
    const legacySelect = document.getElementById("appointmentPatient");
    const phoneInput = document.getElementById("appointmentPhone");
    const emailInput = document.getElementById("appointmentEmail");
    const usePhoneAsWhatsAppInput = document.getElementById("appointmentUsePhoneAsWhatsApp");
    const suggestionsBox = document.getElementById("appointmentPatientSuggestions");
    const stateLabel = document.getElementById("appointmentPatientLookupState");
    const saveButton = document.getElementById("appointmentSaveButton");

    if (!modalElement || !nameInput || !patientIdInput || !legacySelect || !saveButton) return;

    let selectedPatient = null;

    function normalizeText(value) {
        return (value || "").trim().toLowerCase();
    }

    function normalizePhone(value) {
        return (value || "").replace(/\D/g, "");
    }

    function setStateLabel(message, kind) {
        if (!stateLabel) return;
        stateLabel.textContent = message;
        stateLabel.classList.remove("text-muted", "text-warning", "text-success");
        stateLabel.classList.add(kind === "success" ? "text-success" : kind === "warning" ? "text-warning" : "text-muted");
    }

    function hideSuggestions() {
        if (!suggestionsBox) return;
        suggestionsBox.style.display = "none";
        suggestionsBox.innerHTML = "";
    }

    function syncLegacySelect(patientId) {
        legacySelect.value = patientId || "";
        patientIdInput.value = patientId || "";
    }

    function applyPatient(patient) {
        selectedPatient = patient;
        if (!patient) {
            syncLegacySelect("");
            setStateLabel("Если пациент уже есть в базе, выберите его из подсказок. Иначе он будет создан при сохранении записи.", "muted");
            hideSuggestions();
            return;
        }

        nameInput.value = patient.name || "";
        phoneInput.value = patient.phone || "";
        emailInput.value = patient.email || "";
        usePhoneAsWhatsAppInput.checked = Boolean(patient.phone) && normalizePhone(patient.phone) === normalizePhone(patient.whatsApp);
        syncLegacySelect(patient.id);
        setStateLabel("Выбран существующий клиент организации.", "success");
        hideSuggestions();
    }

    function renderSuggestions() {
        if (!suggestionsBox) return;

        const query = normalizeText(nameInput.value);
        if (query.length < 2) {
            hideSuggestions();
            if (!selectedPatient) {
                setStateLabel("Если пациент уже есть в базе, выберите его из подсказок. Иначе он будет создан при сохранении записи.", "muted");
            }
            return;
        }

        const matches = patients
            .filter((patient) => normalizeText(patient.name).includes(query))
            .slice(0, 6);

        if (!matches.length) {
            suggestionsBox.style.display = "block";
            suggestionsBox.innerHTML = '<div class="list-group-item text-muted small">Совпадений нет. Новый клиент будет создан при сохранении записи.</div>';
            setStateLabel("Клиент с таким ФИО не найден. Будет создан новый.", "warning");
            return;
        }

        suggestionsBox.style.display = "block";
        suggestionsBox.innerHTML = matches.map((patient) => `
            <button type="button" class="list-group-item list-group-item-action patient-suggestion" data-patient-id="${patient.id}">
                <div class="fw-semibold">${patient.name}</div>
                <div class="small text-muted">${patient.phone || "Телефон не указан"}${patient.email ? ` · ${patient.email}` : ""}</div>
            </button>
        `).join("");

        suggestionsBox.querySelectorAll(".patient-suggestion").forEach((button) => {
            button.addEventListener("click", () => {
                const patient = patients.find((item) => item.id === button.getAttribute("data-patient-id"));
                applyPatient(patient || null);
            });
        });
    }

    function clearPatientSelectionIfNeeded() {
        if (selectedPatient && normalizeText(selectedPatient.name) !== normalizeText(nameInput.value)) {
            selectedPatient = null;
            syncLegacySelect("");
            setStateLabel("Будет создан новый клиент, если вы не выберете существующего из списка.", "warning");
        }
    }

    function syncFormFromLegacySelection() {
        const patientId = legacySelect.value;
        if (!patientId) {
            if (!nameInput.value.trim()) {
                selectedPatient = null;
                syncLegacySelect("");
                setStateLabel("Если пациент уже есть в базе, выберите его из подсказок. Иначе он будет создан при сохранении записи.", "muted");
            }
            return;
        }

        const patient = patients.find((item) => item.id === patientId);
        if (!patient) return;

        selectedPatient = patient;
        patientIdInput.value = patient.id;
        nameInput.value = patient.name || "";
        if (!phoneInput.value) phoneInput.value = patient.phone || "";
        if (!emailInput.value) emailInput.value = patient.email || "";
        usePhoneAsWhatsAppInput.checked = Boolean(patient.phone) && normalizePhone(patient.phone) === normalizePhone(patient.whatsApp);
        setStateLabel("Выбран существующий клиент организации.", "success");
    }

    async function saveAppointment(event) {
        event.preventDefault();
        event.stopImmediatePropagation();

        const patientName = nameInput.value.trim();
        if (!patientName) {
            window.alert("Укажите ФИО пациента.");
            return;
        }

        if (!saveUrl) {
            window.alert("Маршрут сохранения appointments не настроен.");
            return;
        }

        const request = {
            appointmentId: document.getElementById("appointmentId")?.value || null,
            patientId: patientIdInput.value || null,
            patientName,
            doctorId: document.getElementById("appointmentDoctor")?.value || null,
            phone: phoneInput.value || null,
            usePhoneAsWhatsApp: usePhoneAsWhatsAppInput.checked,
            email: emailInput.value || null,
            comment: document.getElementById("appointmentComment")?.value || null,
            appointmentDate: document.getElementById("appointmentDate")?.value || null,
            startTime: document.getElementById("appointmentStart")?.value || null,
            endTime: document.getElementById("appointmentEnd")?.value || null,
            referralSource: document.getElementById("appointmentReferral")?.value || null,
            paymentType: document.getElementById("appointmentPaymentType")?.value || null,
            appointmentStatus: document.getElementById("appointmentStatus")?.value || null,
            isActive: document.getElementById("appointmentIsActive")?.checked ?? true,
            services: Array.from(document.querySelectorAll("#appointmentServicesTable tr[data-service-index]")).map((row) => {
                const index = Number(row.getAttribute("data-service-index"));
                const appState = window.medclinicScheduleState;
                const selected = appState?.selectedServices?.[index];
                return selected ? {
                    organizationServiceId: selected.organizationServiceId,
                    serviceName: selected.serviceName,
                    priceTyiyn: Number(selected.priceTyiyn || 0),
                    quantity: Number(selected.quantity || 1)
                } : null;
            }).filter(Boolean)
        };

        const originalText = saveButton.textContent;
        saveButton.disabled = true;
        saveButton.textContent = "Сохранение...";

        try {
            const response = await fetch(saveUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify(request)
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                throw new Error(result.message || "Не удалось сохранить запись.");
            }

            window.location.reload();
        } catch (error) {
            window.alert(error.message || "Не удалось сохранить запись.");
            saveButton.disabled = false;
            saveButton.textContent = originalText;
        }
    }

    nameInput.addEventListener("input", () => {
        clearPatientSelectionIfNeeded();
        renderSuggestions();
    });

    nameInput.addEventListener("focus", renderSuggestions);

    modalElement.addEventListener("shown.bs.modal", () => {
        syncFormFromLegacySelection();
        renderSuggestions();
    });

    document.addEventListener("click", (event) => {
        if (!event.target.closest(".patient-lookup")) {
            hideSuggestions();
        }
    });

    saveButton.addEventListener("click", saveAppointment, true);
})();
