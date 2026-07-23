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

    const ui = window.MedclinicUI || {};

    let selectedPatient = null;

    function normalizeText(value) {
        return (value || "").trim().toLowerCase();
    }

    function normalizePhone(value) {
        return (value || "").replace(/\D/g, "");
    }

    function formatPhone(value) {
        const digits = normalizePhone(value);
        if (!digits) return "";

        let local = digits;
        if (local.startsWith("996")) {
            local = local.slice(3);
        }

        local = local.slice(0, 9);
        const parts = [];
        if (local.length > 0) parts.push(local.slice(0, 3));
        if (local.length > 3) parts.push(local.slice(3, 6));
        if (local.length > 6) parts.push(local.slice(6, 9));

        return `+996${parts.length ? " " + parts.join(" ") : ""}`;
    }

    function getRequestVerificationToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    }

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

    function setEditorFormBusy(busy) {
        const form = document.getElementById("appointmentForm");
        if (!form) return;
        form.querySelectorAll("input, button, select, textarea").forEach((el) => {
            if (el.id === "appointmentSaveButton" || el.id === "appointmentSaveDraftButton") return;
            el.disabled = busy;
        });
        modalElement.querySelectorAll('[data-bs-dismiss="modal"]').forEach((el) => {
            el.disabled = busy;
        });
    }

    function setWhatsAppCheckbox(value) {
        if (usePhoneAsWhatsAppInput) {
            usePhoneAsWhatsAppInput.checked = value;
        }
    }

    function getWhatsAppCheckboxValue() {
        return usePhoneAsWhatsAppInput?.checked ?? true;
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
        if (phoneInput) phoneInput.value = formatPhone(patient.phone || "");
        if (emailInput) emailInput.value = patient.email || "";
        setWhatsAppCheckbox(Boolean(patient.phone) && normalizePhone(patient.phone) === normalizePhone(patient.whatsApp));
        syncLegacySelect(patient.id);
        setStateLabel("Выбран существующий пациент организации.", "success");
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
            suggestionsBox.innerHTML = '<div class="list-group-item text-muted small">Совпадений нет. Новый пациент будет создан при сохранении записи.</div>';
            setStateLabel("Пациент с таким ФИО не найден. Будет создан новый.", "warning");
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
            setStateLabel("Будет создан новый пациент, если вы не выберете существующего из списка.", "warning");
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
        if (phoneInput && !phoneInput.value) phoneInput.value = formatPhone(patient.phone || "");
        if (emailInput && !emailInput.value) emailInput.value = patient.email || "";
        setWhatsAppCheckbox(Boolean(patient.phone) && normalizePhone(patient.phone) === normalizePhone(patient.whatsApp));
        setStateLabel("Выбран существующий пациент организации.", "success");
    }

    function buildCommentPayload() {
        const plainComment = document.getElementById("appointmentComment")?.value?.trim() || "";
        const medicalSection = document.getElementById("appointmentMedicalSection");
        const medicalVisible = Boolean(medicalSection && medicalSection.offsetParent !== null);
        if (!medicalVisible) {
            return plainComment;
        }

        const complaints = document.getElementById("appointmentComplaints")?.value?.trim() || "";
        const diagnosis = document.getElementById("appointmentDiagnosis")?.value?.trim() || "";
        const recommendations = document.getElementById("appointmentRecommendations")?.value?.trim() || "";
        const researchReferral = document.getElementById("appointmentResearchReferral")?.value?.trim() || "";
        const medicalComment = document.getElementById("appointmentMedicalComment")?.value?.trim() || "";

        const hasMedicalData = Boolean(complaints || diagnosis || recommendations || researchReferral || medicalComment);
        if (!hasMedicalData) {
            return plainComment;
        }

        const medicalPayload = JSON.stringify({
            complaints,
            diagnosis,
            recommendations,
            researchReferral,
            comment: medicalComment || plainComment
        });

        return `__medjson__${medicalPayload}`;
    }

    function collectSelectedServices() {
        const appState = window.medclinicScheduleState;
        const services = appState?.selectedServices || [];
        return services.map((service) => ({
            organizationServiceId: service.organizationServiceId || null,
            serviceName: service.serviceName || null,
            priceTyiyn: Number(service.priceTyiyn || 0),
            quantity: Math.max(1, Number(service.quantity || 1))
        }));
    }

    function closeEditorModal() {
        const instance = bootstrap.Modal.getInstance(modalElement);
        if (instance) instance.hide();
    }

    async function saveAppointment(event) {
        event.preventDefault();
        event.stopImmediatePropagation();

        if (saveButton.disabled) return;

        const statusInput = document.getElementById("appointmentStatus");
        const forceDraft = saveButton.dataset.forceDraft === "1";
        saveButton.dataset.forceDraft = "0";
        if (statusInput) {
            statusInput.value = forceDraft ? "draft" : "active";
        }

        const patientName = nameInput.value.trim();
        const doctorId = document.getElementById("appointmentDoctor")?.value || payload.currentDoctorId || "";
        const appointmentDate = document.getElementById("appointmentDate")?.value || "";
        const startTime = document.getElementById("appointmentStart")?.value || "";
        const endTime = document.getElementById("appointmentEnd")?.value || "";
        const status = statusInput?.value || "active";
        const isDraft = status === "draft";
        const services = collectSelectedServices();
        const isCreate = !document.getElementById("appointmentId")?.value;

        if (!patientName) {
            ui.showToast?.("Укажите ФИО пациента.", "danger");
            return;
        }

        if (!doctorId) {
            ui.showToast?.("Не выбран врач для записи.", "danger");
            return;
        }

        if (!appointmentDate || !startTime || !endTime) {
            ui.showToast?.("Не задан слот записи.", "danger");
            return;
        }

        if (!isDraft && services.length === 0) {
            ui.showToast?.("Добавьте хотя бы одну услугу.", "danger");
            return;
        }

        const paymentType = document.getElementById("appointmentPaymentType")?.value || (isCreate ? "qr_secore" : "unpaid");
        if (isCreate && !paymentType) {
            ui.showToast?.("Выберите тип оплаты.", "danger");
            return;
        }

        if (!saveUrl) {
            ui.showToast?.("Маршрут сохранения appointments не настроен.", "danger");
            return;
        }

        const request = {
            appointmentId: document.getElementById("appointmentId")?.value || null,
            patientId: patientIdInput.value || null,
            patientName,
            doctorId,
            phone: phoneInput?.value || null,
            usePhoneAsWhatsApp: getWhatsAppCheckboxValue(),
            email: emailInput?.value || null,
            comment: buildCommentPayload(),
            appointmentDate,
            startTime,
            endTime,
            referralSource: document.getElementById("appointmentReferral")?.value || null,
            paymentType,
            appointmentStatus: status,
            isActive: document.getElementById("appointmentIsActive")?.checked ?? true,
            services
        };

        ui.setButtonLoading?.(saveButton, true, {
            label: saveButton.dataset.loadingText || (isCreate ? "Регистрация..." : "Сохранение...")
        });
        setEditorFormBusy(true);
        ui.showPageOverlay?.("Сохранение записи…");

        try {
            const csrfToken = getRequestVerificationToken();
            const response = await fetch(saveUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest",
                    ...(csrfToken ? { RequestVerificationToken: csrfToken } : {})
                },
                body: JSON.stringify(request)
            });

            const { result } = await readJsonResponse(response);
            if (!response.ok || !result.success) {
                throw new Error(result.message || "Не удалось зарегистрировать запись.");
            }

            closeEditorModal();

            if (result.eventItem && typeof window.medclinicUpsertEvent === "function") {
                window.medclinicUpsertEvent(result.eventItem);
                const scheduleState = window.medclinicScheduleState;
                if (scheduleState) {
                    scheduleState.selectedEvent = scheduleState.events.find((x) => x.id === result.eventItem.id) || result.eventItem;
                }
            }
            if (typeof window.medclinicRenderAll === "function") {
                window.medclinicRenderAll();
            }
            if (typeof window.medclinicSaveRegistryFilters === "function") {
                window.medclinicSaveRegistryFilters();
            }

            const toastKind =
                result.message &&
                (String(result.message).includes("не обновлён") || String(result.message).includes("не создан"))
                    ? "warning"
                    : "success";
            ui.showToast?.(result.message || "Запись зарегистрирована.", toastKind);
        } catch (error) {
            ui.showToast?.(error.message || "Не удалось зарегистрировать запись.", "danger");
        } finally {
            setEditorFormBusy(false);
            ui.hidePageOverlay?.();
            ui.setButtonLoading?.(saveButton, false);
        }
    }

    nameInput.addEventListener("input", () => {
        clearPatientSelectionIfNeeded();
        renderSuggestions();
        window.medclinicValidateAppointmentForm?.();
    });

    nameInput.addEventListener("focus", renderSuggestions);

    phoneInput?.addEventListener("input", () => {
        const cursorAtEnd = phoneInput.selectionStart === phoneInput.value.length;
        phoneInput.value = formatPhone(phoneInput.value);
        if (cursorAtEnd) {
            phoneInput.setSelectionRange(phoneInput.value.length, phoneInput.value.length);
        }
    });

    modalElement.addEventListener("shown.bs.modal", () => {
        syncFormFromLegacySelection();
        renderSuggestions();
        if (phoneInput) {
            phoneInput.value = formatPhone(phoneInput.value);
        }
        window.medclinicValidateAppointmentForm?.();
    });

    document.addEventListener("click", (event) => {
        if (!event.target.closest(".patient-lookup")) {
            hideSuggestions();
        }
    });

    saveButton.addEventListener("click", saveAppointment, true);
})();
