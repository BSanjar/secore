/**
 * Страница "Группы" (OrgClientGroups) — модальные окна добавления, редактирования и удаления
 */
(function () {
    'use strict';

    let deleteGroupId = null;

    function getCreateUrl() {
        return document.getElementById('groupsCards')?.dataset?.createUrl || '/Detsad/OrgClientGroups/Create';
    }
    function getEditUrl(id) {
        const template = document.getElementById('groupsCards')?.dataset?.editUrlTemplate;
        return template ? template.replace('__ID__', encodeURIComponent(id)) : '/Detsad/OrgClientGroups/Edit/' + encodeURIComponent(id);
    }
    function getDeleteUrl(id) {
        const template = document.getElementById('groupsCards')?.dataset?.deleteUrlTemplate;
        return template ? template.replace('__ID__', encodeURIComponent(id)) : '/Detsad/OrgClientGroups/Delete/' + encodeURIComponent(id);
    }

    function getRequestVerificationToken() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    window.openAddGroupModal = function () {
        deleteGroupId = null;
        const modal = document.getElementById('addEditGroupModal');
        const title = document.getElementById('addEditGroupModalLabel');
        const form = document.getElementById('groupForm');
        if (!modal || !form) return;

        title.textContent = 'Добавить группу';
        form.reset();
        document.getElementById('groupFormId').value = '';
        document.getElementById('groupFormParentId').value = '';
        document.getElementById('groupFormError').style.display = 'none';

        const parentSelect = document.getElementById('groupFormParentId');
        if (parentSelect) {
            Array.from(parentSelect.options).forEach(function (opt) {
                opt.disabled = false;
            });
        }

        const bsModal = bootstrap.Modal.getOrCreateInstance(modal);
        bsModal.show();
    };

    window.openEditGroupModal = function (id, name, parentId) {
        deleteGroupId = null;
        const modal = document.getElementById('addEditGroupModal');
        const title = document.getElementById('addEditGroupModalLabel');
        const form = document.getElementById('groupForm');
        if (!modal || !form) return;

        title.textContent = 'Редактировать группу';
        document.getElementById('groupFormId').value = id || '';
        document.getElementById('groupFormName').value = name || '';
        document.getElementById('groupFormParentId').value = parentId || '';
        document.getElementById('groupFormError').style.display = 'none';

        const parentSelect = document.getElementById('groupFormParentId');
        if (parentSelect && id) {
            Array.from(parentSelect.options).forEach(function (opt) {
                opt.disabled = opt.value === id;
            });
        }

        const bsModal = bootstrap.Modal.getOrCreateInstance(modal);
        bsModal.show();
    };

    window.openDeleteGroupModal = function (id, name) {
        deleteGroupId = id;
        const modal = document.getElementById('deleteGroupModal');
        const nameEl = document.getElementById('deleteGroupName');
        const errEl = document.getElementById('deleteGroupError');
        if (!modal || !nameEl) return;

        nameEl.textContent = name || '(без названия)';
        if (errEl) errEl.style.display = 'none';

        const bsModal = bootstrap.Modal.getOrCreateInstance(modal);
        bsModal.show();
    };

    function submitGroupForm() {
        const form = document.getElementById('groupForm');
        const submitBtn = document.getElementById('groupFormSubmitBtn');
        const errEl = document.getElementById('groupFormError');
        if (!form || !submitBtn) return;

        const nameInput = document.getElementById('groupFormName');
        const name = nameInput && nameInput.value ? nameInput.value.trim() : '';
        if (!name) {
            if (nameInput) {
                nameInput.classList.add('is-invalid');
            }
            return;
        }
        if (nameInput) nameInput.classList.remove('is-invalid');

        submitBtn.disabled = true;
        if (errEl) errEl.style.display = 'none';

        const id = document.getElementById('groupFormId').value;
        const isEdit = !!id;
        const url = isEdit ? getEditUrl(id) : getCreateUrl();
        const formData = new FormData(form);

        fetch(url, {
            method: 'POST',
            body: formData
        })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (data.success) {
                    const modal = document.getElementById('addEditGroupModal');
                    if (modal) {
                        const bsModal = bootstrap.Modal.getInstance(modal);
                        if (bsModal) bsModal.hide();
                    }
                    window.location.reload();
                } else {
                    if (errEl) {
                        errEl.textContent = data.message || 'Ошибка сохранения';
                        errEl.style.display = 'block';
                    }
                }
            })
            .catch(function () {
                if (errEl) {
                    errEl.textContent = 'Ошибка связи с сервером';
                    errEl.style.display = 'block';
                }
            })
            .finally(function () {
                submitBtn.disabled = false;
            });
    }

    window.confirmDeleteGroup = function () {
        if (!deleteGroupId) return;
        const btn = document.getElementById('deleteGroupConfirmBtn');
        const errEl = document.getElementById('deleteGroupError');
        if (btn) btn.disabled = true;
        if (errEl) errEl.style.display = 'none';

        const formData = new FormData();
        formData.append('__RequestVerificationToken', getRequestVerificationToken());

        fetch(getDeleteUrl(deleteGroupId), {
            method: 'POST',
            body: formData
        })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (data.success) {
                    const modal = document.getElementById('deleteGroupModal');
                    if (modal) {
                        const bsModal = bootstrap.Modal.getInstance(modal);
                        if (bsModal) bsModal.hide();
                    }
                    deleteGroupId = null;
                    window.location.reload();
                } else {
                    if (errEl) {
                        errEl.textContent = data.message || 'Ошибка удаления';
                        errEl.style.display = 'block';
                    }
                }
            })
            .catch(function () {
                if (errEl) {
                    errEl.textContent = 'Ошибка связи с сервером';
                    errEl.style.display = 'block';
                }
            })
            .finally(function () {
                if (btn) btn.disabled = false;
            });
    };

    function applyFiltersRedirect() {
        const searchInput = document.getElementById('searchInput');
        const isDeletedFilter = document.getElementById('isDeletedFilter');
        if (!searchInput || !isDeletedFilter) return;
        const params = new URLSearchParams(window.location.search);
        params.set('search', searchInput.value.trim());
        params.set('isDeletedFilter', isDeletedFilter.value);
        params.set('page', '1');
        window.location.search = params.toString();
    }

    function applySearchLocal() {
        const searchInput = document.getElementById('searchInput');
        const container = document.getElementById('groupsTree');
        if (!searchInput || !container) return;
        const term = searchInput.value.toLowerCase().trim();
        const nodes = Array.from(container.querySelectorAll('.tree-node'));
        if (!term) {
            nodes.forEach(function (n) { n.classList.remove('search-hidden'); });
            return;
        }
        var visible = new Set();
        for (var i = nodes.length - 1; i >= 0; i--) {
            var node = nodes[i];
            var name = (node.getAttribute('data-group-name') || '').toLowerCase();
            var matches = name.indexOf(term) !== -1;
            var childNodes = node.querySelectorAll(':scope > .tree-children > .tree-node');
            var childVisible = Array.from(childNodes).some(function (c) { return visible.has(c); });
            if (matches || childVisible) visible.add(node);
        }
        nodes.forEach(function (n) {
            if (visible.has(n)) n.classList.remove('search-hidden');
            else n.classList.add('search-hidden');
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        const searchInput = document.getElementById('searchInput');
        const isDeletedFilter = document.getElementById('isDeletedFilter');
        if (searchInput) {
            var searchTimeout;
            searchInput.addEventListener('input', function () {
                clearTimeout(searchTimeout);
                searchTimeout = setTimeout(applySearchLocal, 300);
            });
        }
        if (isDeletedFilter) {
            isDeletedFilter.addEventListener('change', applyFiltersRedirect);
        }
        applySearchLocal();

        const submitBtn = document.getElementById('groupFormSubmitBtn');
        if (submitBtn) {
            submitBtn.addEventListener('click', function () { submitGroupForm(); });
        }

        const deleteBtn = document.getElementById('deleteGroupConfirmBtn');
        if (deleteBtn) {
            deleteBtn.addEventListener('click', function () { confirmDeleteGroup(); });
        }

        const groupForm = document.getElementById('groupForm');
        if (groupForm) {
            groupForm.addEventListener('submit', function (e) {
                e.preventDefault();
                submitGroupForm();
            });
        }

        const groupsContainer = document.getElementById('groupsCards');
        if (groupsContainer) {
            groupsContainer.addEventListener('click', function (e) {
                const toggle = e.target.closest('.tree-toggle');
                if (toggle) {
                    const node = toggle.closest('.tree-node');
                    if (node && node.classList.contains('has-children')) {
                        e.preventDefault();
                        node.classList.toggle('collapsed');
                    }
                    return;
                }
                const editBtn = e.target.closest('.btn-edit-group');
                if (editBtn) {
                    e.preventDefault();
                    openEditGroupModal(
                        editBtn.getAttribute('data-group-id'),
                        editBtn.getAttribute('data-group-name'),
                        editBtn.getAttribute('data-group-parent-id')
                    );
                    return;
                }
                const delBtn = e.target.closest('.btn-delete-group');
                if (delBtn) {
                    e.preventDefault();
                    openDeleteGroupModal(
                        delBtn.getAttribute('data-group-id'),
                        delBtn.getAttribute('data-group-name')
                    );
                }
            });
        }
    });
})();
