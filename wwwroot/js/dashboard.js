/**
 * ============================================================
 * 1. INIT: WERSJA KULOODPORNA
 * ============================================================
 */
(function initThemeAndAuth() {
    // 1. Sprawdź i WYCZYŚĆ (Nuclear Option)
    const savedTheme = localStorage.getItem('theme');
    const cachedUser = JSON.parse(localStorage.getItem("userInfo") || "null");
    const isAgroOrganization = (cachedUser && (cachedUser.accountType || cachedUser.account_type)) === 'AGROORGANIZATION';

    if (isAgroOrganization) {
        localStorage.setItem('theme', 'dark');
    }

    // Na wszelki wypadek czyścimy body i html z klasy, żeby nie było duplikatów
    document.documentElement.classList.remove('dark-theme');
    if (document.body) document.body.classList.remove('dark-theme');

    // Aplikujemy tylko jeśli trzeba
    if (savedTheme === 'dark' || isAgroOrganization) {
        document.documentElement.classList.add('dark-theme');
    }

    // 2. Auth Guard
    const token = localStorage.getItem("token");
    const isPublicPage = window.location.pathname.includes("login.html") ||
        window.location.pathname.includes("register.html");

    if (!token && !isPublicPage) {
        window.location.href = "login.html";
    }
})();

document.addEventListener("DOMContentLoaded", () => {
    // ==========================================
    // 2. KONFIGURACJA
    // ==========================================
    const CONFIG = {
        apiBase: "https://localhost:7273/api",
        endpoints: { shortInfo: "/settings/getShortInfo" },
        selectors: { welcomeMsg: "welcome" }
    };

    const ui = {
        welcome: document.getElementById(CONFIG.selectors.welcomeMsg),
        tileGrid: document.querySelector('.tile-grid')
    };

    // ==========================================
    // 3. SYNCHRONIZACJA MOTYWU (Miotacz Ognia)
    // ==========================================

    function syncThemeSimple() {
        const savedTheme = localStorage.getItem('theme');
        const cachedUser = JSON.parse(localStorage.getItem("userInfo") || "null");
        const isAgroOrganization = (cachedUser && (cachedUser.accountType || cachedUser.account_type)) === 'AGROORGANIZATION';
        console.log('🔄 Dashboard Theme Sync. Odczytano:', savedTheme);

        // KROK 1: Usuwamy klasę ZAWSZE z obu miejsc (HTML i BODY)
        // To eliminuje "klasy zombie" z poprzednich wersji kodu
        document.documentElement.classList.remove('dark-theme');
        document.body.classList.remove('dark-theme');

        // KROK 2: Dodajemy TYLKO na HTML i TYLKO jeśli dark
        if (savedTheme === 'dark' || isAgroOrganization) {
            document.documentElement.classList.add('dark-theme');
        }
    }

    // ==========================================
    // 4. API & AUTH
    // ==========================================
    function handleLogout() {
        localStorage.clear();
        sessionStorage.clear();
        window.location.href = "login.html";
    }

    async function fetchWithAuth(endpoint) {
        const token = localStorage.getItem("token");
        if (!token) { handleLogout(); return Promise.reject("No token"); }

        const res = await fetch(`${CONFIG.apiBase}${endpoint}`, {
            headers: { "Authorization": `Bearer ${token}` }
        });

        if (res.status === 401) {
            alert("Sesja wygasła.");
            handleLogout();
            throw new Error("Unauthorized");
        }
        return res;
    }

    // ==========================================
    // 5. LOGIKA DANYCH
    // ==========================================
    async function initDashboard() {
        // A) Najpierw wygląd (priorytet)
        syncThemeSimple();

        // B) Wyświetl imię z cache
        const cachedUser = JSON.parse(localStorage.getItem("userInfo") || "null");
        const accountType = (cachedUser && (cachedUser.accountType || cachedUser.account_type)) || 'USER';

        if (accountType === 'AGROORGANIZATION') {
            renderAgroOrganizationDashboard(cachedUser);
            return;
        }

        if (cachedUser && cachedUser.name) {
            ui.welcome.innerText = `Witaj, ${cachedUser.name} 😊`;
        }

        // C) Pobierz dane w tle (dla imienia/jednostek), ale BEZ psucia motywu
        try {
            await refreshUserData();
        } catch (err) {
            console.error("Błąd odświeżania danych:", err);
        }
    }

    async function refreshUserData() {
        const res = await fetchWithAuth(CONFIG.endpoints.shortInfo);
        if (!res.ok) throw new Error("API Error");

        const data = await res.json();

        // 1. Aktualizacja userInfo (dane analityczne)
        const isDarkInt = (data.darkMode === 1 || data.isDarkMode === 1) ? 1 : 0;
        const normalizedUser = {
            ...data,
            name: data.name || data.firstName,
            darkMode: isDarkInt,
            accountType: data.accountType || data.account_type || 'USER'
        };
        localStorage.setItem("userInfo", JSON.stringify(normalizedUser));

        // 2. UI - Powitanie
        if (ui.welcome) {
            ui.welcome.innerText = `Witaj, ${normalizedUser.name || 'Użytkowniku'} 😊`;
        }

        // WAŻNE: Nie ruszamy tu localStorage.setItem('theme'), 
        // żeby nie nadpisać "wyścigu" z settings.js
    }

    // ==========================================
    // 6. OBSŁUGA ZDARZEŃ (FIX NA COFANIE)
    // ==========================================

    window.addEventListener('pageshow', (event) => {
        console.log('📺 PageShow Event (Powrót na stronę)');
        syncThemeSimple();

        if (event.persisted) {
            // Jeśli z cache, możemy bezpiecznie pobrać dane w tle
            refreshUserData();
        }
    });

    window.addEventListener('storage', (e) => {
        if (e.key === 'theme') {
            console.log('💾 Wykryto zmianę w innej karcie');
            syncThemeSimple();
        }
    });

    function renderAgroOrganizationDashboard(user) {
        if (ui.welcome) {
            ui.welcome.remove();
        }

        if (ui.tileGrid) {
            ui.tileGrid.classList.add('org-view');
            ui.tileGrid.innerHTML = `
                <div class="org-panel">
                    <div class="org-panel-header">
                        <h3>Pending applications</h3>
                        <div class="org-summary" id="orgSummary"></div>
                    </div>
                    <div id="pendingReportsList" class="pending-reports-list">
                        <p>Loading applications...</p>
                    </div>
                </div>
            `;
            loadPendingReports();
        }

    }

    async function loadPendingReports() {
        const container = document.getElementById('pendingReportsList');
        const summary = document.getElementById('orgSummary');
        if (!container) return;

        try {
            const res = await fetchWithAuth('/field/pending-reports');
            if (!res.ok) throw new Error('Nie udało się pobrać wniosków');

            const pending = await res.json();

            if (summary) {
                summary.innerHTML = `
                    <div class="summary-pill">
                        <span class="summary-value">${pending.length}</span>
                        <span class="summary-label">pending</span>
                    </div>
                    <div class="summary-bars">
                        ${pending.slice(0, 6).map((r, idx) => `<span class="summary-bar bar-${(idx % 5) + 1}" title="${r.fieldName || 'Nieznane pole'}"></span>`).join('')}
                    </div>
                `;
            }

            if (pending.length === 0) {
                container.innerHTML = '<p>No pending applications.</p>';
                return;
            }

            container.innerHTML = pending.map(r => `
                <div class="pending-report-card">
                    <div class="pending-report-visual">
                        <div class="report-dot"></div>
                        <div class="pending-report-main">
                            <strong>${r.fieldName || 'Nieznane pole'}</strong>
                            <div class="report-subtitle">${r.username || 'Unknown user'}</div>
                        </div>
                    </div>
                    <div class="pending-report-meta">ID: ${r.reportId}</div>
                    <div class="pending-report-meta">Date: ${r.generatedAt ? new Date(r.generatedAt).toLocaleString('en-US') : 'No data'}</div>
                    <div class="status-badge status-${(r.validation || 'pending').toLowerCase()}">${translateValidation(r.validation)}</div>
                    <div class="pending-report-actions">
                        <button type="button" class="btn-download-report" data-report-id="${r.reportId}">Download</button>
                        <button type="button" class="btn-validate-report" data-report-id="${r.reportId}">Approve</button>
                        <button type="button" class="btn-reject-report" data-report-id="${r.reportId}">Reject</button>
                    </div>
                </div>
            `).join('');

            container.querySelectorAll('.btn-download-report').forEach(btn => {
                btn.addEventListener('click', () => downloadReport(Number(btn.dataset.reportId)));
            });
            container.querySelectorAll('.btn-validate-report').forEach(btn => {
                btn.addEventListener('click', () => validateReport(Number(btn.dataset.reportId)));
            });
            container.querySelectorAll('.btn-reject-report').forEach(btn => {
                btn.addEventListener('click', () => rejectReport(Number(btn.dataset.reportId)));
            });
        } catch (err) {
            container.innerHTML = `<p>Błąd pobierania wniosków: ${err.message}</p>`;
        }
    }

    function translateValidation(validation) {
        switch ((validation || 'pending').toLowerCase()) {
            case 'confirmed': return 'Approved';
            case 'rejected': return 'Rejected';
            default: return 'Pending review';
        }
    }

    async function downloadReport(reportId) {
        try {
            const res = await fetchWithAuth(`/report/${reportId}`);
            if (!res.ok) throw new Error('Nie udało się pobrać raportu');

            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `raport-${reportId}.pdf`;
            document.body.appendChild(link);
            link.click();
            link.remove();
            URL.revokeObjectURL(url);
        } catch (err) {
            alert(err.message);
        }
    }

    async function validateReport(reportId) {
        try {
            const res = await fetchWithAuth(`/field/overallAnalysisReport/validate/${reportId}`);
            if (!res.ok) throw new Error('Nie udało się zatwierdzić raportu');
            await loadPendingReports();
        } catch (err) {
            alert(err.message);
        }
    }

    async function rejectReport(reportId) {
        try {
            const res = await fetchWithAuth(`/field/overallAnalysisReport/reject/${reportId}`);
            if (!res.ok) throw new Error('Nie udało się odrzucić raportu');
            await loadPendingReports();
        } catch (err) {
            alert(err.message);
        }
    }

    // Start
    initDashboard();
});