/**
 * ============================================================
 * 1. INIT: WERSJA KULOODPORNA
 * ============================================================
 */
(function initThemeAndAuth() {
    // 1. Sprawdź i WYCZYŚĆ (Nuclear Option)
    const savedTheme = localStorage.getItem('theme');

    // Na wszelki wypadek czyścimy body i html z klasy, żeby nie było duplikatów
    document.documentElement.classList.remove('dark-theme');
    if (document.body) document.body.classList.remove('dark-theme');

    // Aplikujemy tylko jeśli trzeba
    if (savedTheme === 'dark') {
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
        selectors: { welcomeMsg: "welcome", logoutBtn: "logoutBtn" }
    };

    const ui = {
        welcome: document.getElementById(CONFIG.selectors.welcomeMsg),
        logoutBtn: document.getElementById(CONFIG.selectors.logoutBtn)
    };

    // ==========================================
    // 3. SYNCHRONIZACJA MOTYWU (Miotacz Ognia)
    // ==========================================

    function syncThemeSimple() {
        const savedTheme = localStorage.getItem('theme');
        console.log('🔄 Dashboard Theme Sync. Odczytano:', savedTheme);

        // KROK 1: Usuwamy klasę ZAWSZE z obu miejsc (HTML i BODY)
        // To eliminuje "klasy zombie" z poprzednich wersji kodu
        document.documentElement.classList.remove('dark-theme');
        document.body.classList.remove('dark-theme');

        // KROK 2: Dodajemy TYLKO na HTML i TYLKO jeśli dark
        if (savedTheme === 'dark') {
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
            darkMode: isDarkInt
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

    if (ui.logoutBtn) ui.logoutBtn.addEventListener('click', handleLogout);

    // Start
    initDashboard();
});