/**
 * ============================================================
 * 1. INIT: ATOMOWY START (Nuclear Option)
 * ============================================================
 */
(function initThemeAndAuth() {
    // 1. MOTYW - Czyścimy wszystko i nakładamy na czysto
    const savedTheme = localStorage.getItem('theme');

    document.documentElement.classList.remove('dark-theme');
    if (document.body) document.body.classList.remove('dark-theme');

    if (savedTheme === 'dark') {
        document.documentElement.classList.add('dark-theme');
    }

    // 2. AUTH GUARD
    const token = localStorage.getItem("token");
    if (!token) {
        window.location.href = "login.html";
    }
})();

document.addEventListener("DOMContentLoaded", () => {
    // ==========================================
    // 2. KONFIGURACJA
    // ==========================================
    const CONFIG = {
        API_URL: '/api/fieldsList'
    };

    const UI = {
        container: document.getElementById('fields-container')
    };

    // ==========================================
    // 3. SYNCHRONIZACJA MOTYWU (ATOMOWA)
    // ==========================================
    function syncThemeNuclear() {
        const savedTheme = localStorage.getItem('theme');

        // Miotacz ognia: usuwamy klasę zewsząd
        document.documentElement.classList.remove('dark-theme');
        document.body.classList.remove('dark-theme');

        // Nakładamy tylko tam gdzie trzeba
        if (savedTheme === 'dark') {
            document.documentElement.classList.add('dark-theme');
        }
    }

    // ==========================================
    // 4. API & SECURITY
    // ==========================================
    function handleLogout() {
        localStorage.clear();
        sessionStorage.clear();
        window.location.href = "login.html";
    }

    const originalFetch = window.fetch;
    window.fetch = async (...args) => {
        try {
            const response = await originalFetch(...args);
            if (response.status === 401) {
                alert("Twoja sesja wygasła. Zaloguj się ponownie.");
                handleLogout();
                throw new Error("Unauthorized");
            }
            return response;
        } catch (error) {
            throw error;
        }
    };

    async function getFields() {
        const token = localStorage.getItem("token");
        if (!token) { handleLogout(); return []; }

        try {
            const response = await fetch(CONFIG.API_URL, {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (!response.ok) throw new Error(`Błąd HTTP: ${response.status}`);
            return await response.json();
        } catch (err) {
            console.error(err);
            UI.container.innerHTML = `<p style="color:var(--danger-color); font-weight:bold;">Nie udało się pobrać listy pól.</p>`;
            return [];
        }
    }

    // ==========================================
    // 5. LOGIKA UI
    // ==========================================
    function renderFields(fields) {
        UI.container.innerHTML = '';

        if (!fields || fields.length === 0) {
            UI.container.innerHTML = `<p>Nie masz jeszcze żadnych pól. <a href="yourFarm.html" style="color:var(--primary-color)">Przejdź do mapy</a>, aby je dodać.</p>`;
            return;
        }

        fields.forEach(field => {
            const tile = document.createElement('div');
            tile.className = 'field-tile';

            // Opcjonalnie: Dodaj formatowanie powierzchni jeśli API zwraca 'area'
            // const areaText = formatArea(field.area); 

            tile.innerHTML = `
                <h3>${field.name}</h3>
                
            `;

            tile.addEventListener('click', () => {
                window.location.href = `fieldDashboard.html?fieldId=${field.id}`;
            });

            UI.container.appendChild(tile);
        });
    }

    // Helper do jednostek (zgodny z settings.js)
    function formatArea(areaM2) {
        if (!areaM2) return "";
        const unit = localStorage.getItem('unitType') || 'ha';

        switch (unit) {
            case 'a': return (areaM2 / 100).toFixed(2) + " a";
            case 'ac': return (areaM2 / 4046.86).toFixed(3) + " ac";
            default: return (areaM2 / 10000).toFixed(4) + " ha";
        }
    }

    // ==========================================
    // 6. LISTENERY & START (Fix na powrót)
    // ==========================================

    window.addEventListener('pageshow', (event) => {
        syncThemeNuclear(); // Zawsze czyść i nakładaj motyw
        if (event.persisted) {
            init(); // Odśwież listę jeśli z cache
        }
    });

    window.addEventListener('storage', (e) => {
        if (e.key === 'theme') syncThemeNuclear();
    });

    async function init() {
        // Najpierw wygląd
        syncThemeNuclear();
        const fields = await getFields();
        renderFields(fields);
    }

    init();
});