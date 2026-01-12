/**
 * ============================================================
 * 1. INIT: SZYBKI START MOTYWU (Prosty klucz)
 * ============================================================
 */
(function initThemeAndAuth() {
    // Prosty odczyt - zero JSON-a
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark') {
        document.documentElement.classList.add('dark-theme');
    } else {
        document.documentElement.classList.remove('dark-theme');
    }

    const token = localStorage.getItem("token");
    if (!token) {
        window.location.href = "login.html";
    }
})();

document.addEventListener("DOMContentLoaded", () => {
    const API_BASE_URL = "https://localhost:7273/api";

    // UI Selectors
    const ui = {
        tabs: document.querySelectorAll(".menu-option[data-tab]"),
        contents: document.querySelectorAll(".tab-content"),
        displayName: document.getElementById("display-name"),
        displayEmail: document.getElementById("display-email"),
        displayPhone: document.getElementById("display-phone"),
        editModal: document.getElementById("editModal"),
        editForm: document.getElementById("editForm"),
        editInput: document.getElementById("editInput"),
        modalTitle: document.getElementById("modalTitle"),
        validationMsg: document.getElementById("validationMessage"),
        btnSave: document.querySelector(".btn-save"),
        deleteModal: document.getElementById("deleteAccountModal"),
        deleteForm: document.getElementById("deleteForm"),
        deletePasswordInput: document.getElementById("deletePasswordInput"),
        btnOpenDelete: document.getElementById("btnOpenDeleteModal"),
        unitSwitch: document.getElementById("unitSwitch"),
        themeSwitch: document.getElementById("themeSwitch"),
        editButtons: document.querySelectorAll(".edit-icon"),
        closeButtons: document.querySelectorAll(".btn-close")
    };

    let currentEditType = null;

    // --- Helpers ---
    function handleLogout() {
        localStorage.clear();
        sessionStorage.clear();
        window.location.href = "login.html";
    }

    async function fetchWithAuth(endpoint, method = "GET", body = null) {
        const token = localStorage.getItem("token");
        if (!token) { handleLogout(); return null; }

        const headers = { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" };
        const config = { method, headers };
        if (body) config.body = JSON.stringify(body);

        try {
            const response = await fetch(`${API_BASE_URL}${endpoint}`, config);
            if (response.status === 401) { alert("Sesja wygasła."); handleLogout(); return null; }
            return response;
        } catch (error) {
            console.error("API Error:", error);
            return null;
        }
    }

    // --- LOGIKA MOTYWU I JEDNOSTEK (Proste zmienne) ---

    function updateSwitches(themeStr, unitStr) {
        initSwitchUI(ui.unitSwitch, unitStr);
        initSwitchUI(ui.themeSwitch, themeStr);
    }

    async function loadInitialData() {
        // 1. Najpierw ustaw to co jest w pamięci (szybko)
        const localTheme = localStorage.getItem('theme') || 'light';
        const localUnit = localStorage.getItem('unitType') || 'ha';
        updateSwitches(localTheme, localUnit);

        // 2. Pobierz dane z API
        const resShort = await fetchWithAuth("/settings/getShortInfo");
        if (resShort && resShort.ok) {
            const data = await resShort.json();

            // Tłumaczenie API -> Proste Zmienne
            const isDark = (data.darkMode === 1 || data.isDarkMode === 1);
            const serverTheme = isDark ? 'dark' : 'light';

            const surf = data.surface ?? data.surfaceUnit ?? 0;
            let serverUnit = 'ha';
            if (surf === 1) serverUnit = 'a';
            if (surf === 2) serverUnit = 'ac';

            // Zapisujemy PROSTE zmienne
            localStorage.setItem('theme', serverTheme);
            localStorage.setItem('unitType', serverUnit);

            // Aktualizujemy suwaki
            updateSwitches(serverTheme, serverUnit);

            // Aplikujemy motyw
            if (serverTheme === 'dark') document.documentElement.classList.add('dark-theme');
            else document.documentElement.classList.remove('dark-theme');
        }

        // Pobierz dane osobowe (LongInfo)
        const resLong = await fetchWithAuth("/settings/getLongInfo");
        if (resLong && resLong.ok) {
            const data = await resLong.json();
            ui.displayName.value = data.name || "";
            ui.displayEmail.value = data.email || "";
            ui.displayPhone.value = data.phone || "";
        }
    }

    function initSwitchUI(container, activeValue) {
        if (!container) return;
        const options = Array.from(container.querySelectorAll(container.id === "themeSwitch" ? ".theme-option" : ".switch-option"));
        const highlight = container.querySelector(container.id === "themeSwitch" ? ".theme-highlight" : ".switch-highlight");

        let activeIdx = options.findIndex(opt => {
            const val = opt.dataset.val || opt.dataset.theme || opt.dataset.unit;
            return val === activeValue;
        });
        if (activeIdx === -1) activeIdx = 0;

        options.forEach(o => o.classList.remove("active"));
        options[activeIdx].classList.add("active");
        highlight.style.left = `calc(${activeIdx} * 100% / ${options.length})`;
    }

    function handleSwitch(e, type) {
        const optionClass = type === 'theme' ? '.theme-option' : '.switch-option';
        const option = e.target.closest(optionClass);
        if (!option) return;

        const container = type === 'theme' ? ui.themeSwitch : ui.unitSwitch;
        const val = option.dataset.val || option.dataset.theme || option.dataset.unit; // to jest np. 'dark' albo 'ha'

        // Przesuń suwak
        initSwitchUI(container, val);

        if (type === 'theme') {
            // 1. Zapisz prostą zmienną
            localStorage.setItem('theme', val);

            // 2. Zmień wygląd natychmiast
            if (val === 'dark') document.documentElement.classList.add('dark-theme');
            else document.documentElement.classList.remove('dark-theme');

            // 3. Wyślij do API
            fetchWithAuth("/settings/updateTheme", "POST", { darkMode: (val === 'dark' ? 1 : 0) });

        } else {
            // 1. Zapisz prostą zmienną
            localStorage.setItem('unitType', val);

            // 2. Wyślij do API
            const surfaceInt = val === 'a' ? 1 : (val === 'ac' ? 2 : 0);
            fetchWithAuth("/settings/updateSurface", "POST", { surface: surfaceInt });
        }
    }

    // --- Reszta obsługi (Modale, Edycja) bez zmian ---
    function switchTab(clickedTab) {
        ui.tabs.forEach(t => t.classList.remove("active"));
        ui.contents.forEach(c => c.classList.remove("active"));
        clickedTab.classList.add("active");
        document.getElementById(clickedTab.dataset.tab).classList.add("active");
    }

    function openEditModal(actionType) {
        ui.validationMsg.textContent = ""; ui.btnSave.disabled = false;
        if (actionType === 'edit-name') { currentEditType = 'name'; ui.modalTitle.textContent = "Zmień imię"; ui.editInput.type = "text"; ui.editInput.value = ui.displayName.value; }
        else if (actionType === 'edit-email') { currentEditType = 'email'; ui.modalTitle.textContent = "Zmień email"; ui.editInput.type = "email"; ui.editInput.value = ui.displayEmail.value; }
        else if (actionType === 'edit-phone') { currentEditType = 'phone'; ui.modalTitle.textContent = "Zmień telefon"; ui.editInput.type = "tel"; ui.editInput.value = ui.displayPhone.value; }
        ui.editModal.style.display = "block"; ui.editInput.focus();
    }

    async function handleSaveEdit(e) {
        e.preventDefault();
        const newValue = ui.editInput.value.trim();
        if (!validateInput()) return;

        let endpoint = "", body = {};
        if (currentEditType === 'name') { endpoint = "/settings/updateName"; body = { name: newValue }; }
        else if (currentEditType === 'email') { endpoint = "/settings/updateEmail"; body = { email: newValue }; }
        else if (currentEditType === 'phone') { endpoint = "/settings/updatePhone"; body = { phone: newValue }; }

        const res = await fetchWithAuth(endpoint, "POST", body);
        if (res && res.ok) {
            if (currentEditType === 'name') ui.displayName.value = newValue;
            if (currentEditType === 'email') ui.displayEmail.value = newValue;
            if (currentEditType === 'phone') ui.displayPhone.value = newValue;
            closeAllModals(); alert("Dane zaktualizowane.");
        } else alert("Błąd zapisu.");
    }

    function validateInput() {
        const val = ui.editInput.value.trim();
        if (!val) return false;
        ui.btnSave.disabled = false; return true;
    }

    async function handleDeleteAccount(e) {
        e.preventDefault();
        const password = ui.deletePasswordInput.value;
        const res = await fetchWithAuth("/auth/deleteAccount", "POST", { password });
        if (res && res.ok) { alert("Konto usunięte."); handleLogout(); }
        else alert("Błąd usuwania.");
    }

    function closeAllModals() { document.querySelectorAll(".modal").forEach(m => m.style.display = "none"); }

    // Listenery
    loadInitialData();
    ui.tabs.forEach(tab => tab.addEventListener("click", (e) => switchTab(e.currentTarget)));
    ui.editButtons.forEach(btn => btn.addEventListener("click", (e) => openEditModal(e.target.dataset.action)));
    ui.editForm.addEventListener("submit", handleSaveEdit);
    ui.editInput.addEventListener("input", validateInput);
    ui.unitSwitch.addEventListener("click", (e) => handleSwitch(e, 'unit'));
    ui.themeSwitch.addEventListener("click", (e) => handleSwitch(e, 'theme'));
    ui.btnOpenDelete.addEventListener("click", () => ui.deleteModal.style.display = "block");
    ui.deleteForm.addEventListener("submit", handleDeleteAccount);
    ui.closeButtons.forEach(btn => btn.addEventListener("click", closeAllModals));
    window.addEventListener("click", (e) => { if (e.target.classList.contains("modal")) closeAllModals(); });
});