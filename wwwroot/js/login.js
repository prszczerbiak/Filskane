document.addEventListener("DOMContentLoaded", () => {
    // ==========================================
    // 1. KONFIGURACJA
    // ==========================================
    const API_BASE_URL = "https://localhost:7273/api";

    const ui = {
        form: document.getElementById("loginForm"),
        usernameInput: document.getElementById("username"),
        passwordInput: document.getElementById("password"),
        submitBtn: document.querySelector(".log-in"),
        errorMsg: document.getElementById("errorMessage"),
        registerBtn: document.getElementById("registerBtn")
    };

    // ==========================================
    // 2. LOGIKA BIZNESOWA
    // ==========================================

    async function handleLogin(e) {
        e.preventDefault();

        // Reset widoku
        ui.errorMsg.style.display = "none";
        setLoadingState(true);

        const credentials = {
            username: ui.usernameInput.value.trim(),
            password: ui.passwordInput.value.trim()
        };

        try {
            // KROK 1: Autentykacja (Pobranie tokena)
            const token = await authenticateUser(credentials);
            localStorage.setItem("token", token);

            // KROK 2: Pobranie danych z serwera
            const apiData = await fetchUserSettings(token);

            // ========================================================
            // KROK 3: SEGREGACJA DANYCH (KLUCZOWY MOMENT)
            // ========================================================

            // A. Wygląd i Jednostki -> Zapisujemy jako PROSTE KLUCZE (szybki dostęp)

            // Motyw
            
            const isDark = (apiData.darkMode === 1 || apiData.isDarkMode === 1);
            //console.log(isDark);
            //debugger;
            localStorage.setItem("theme", isDark ? 'dark' : 'light');

            // Jednostki (0=ha, 1=a, 2=ac)
            const surf = apiData.surface ?? apiData.surfaceUnit ?? 0;
            let unitVal = 'ha';
            if (surf === 1) unitVal = 'a';
            if (surf === 2) unitVal = 'ac';
            localStorage.setItem("unitType", unitVal);

            // B. Dane Użytkownika i Lokalizacja -> Zapisujemy w OBIEKCIE userInfo
            const cleanUserInfo = {
                name: apiData.name || apiData.firstName || credentials.username,
                farmX: apiData.farmX, // Longitude (Długość)
                farmY: apiData.farmY  // Latitude (Szerokość)
            };

            localStorage.setItem("userInfo", JSON.stringify(cleanUserInfo));

            // ========================================================

            // KROK 4: Przekierowanie
            window.location.href = "dashboard.html";

        } catch (error) {
            console.error("Login error:", error);
            ui.errorMsg.textContent = error.message || "Wystąpił błąd logowania.";
            ui.errorMsg.style.display = "block";
            localStorage.clear(); // W razie błędu czyścimy częściowo zapisane dane
        } finally {
            setLoadingState(false);
        }
    }

    async function authenticateUser(credentials) {
        const response = await fetch(`${API_BASE_URL}/auth/login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(credentials)
        });

        if (!response.ok) {
            if (response.status === 401) throw new Error("Nieprawidłowy login lub hasło.");
            throw new Error("Błąd serwera. Spróbuj ponownie później.");
        }

        const data = await response.json();
        return data.token;
    }

    async function fetchUserSettings(token) {
        const response = await fetch(`${API_BASE_URL}/settings/getShortInfo`, {
            method: "GET",
            headers: { "Authorization": `Bearer ${token}` }
        });

        if (!response.ok) throw new Error("Nie udało się pobrać profilu użytkownika.");
        return await response.json();
    }

    function setLoadingState(isLoading) {
        if (isLoading) {
            ui.submitBtn.textContent = "Logowanie...";
            ui.submitBtn.disabled = true;
        } else {
            ui.submitBtn.textContent = "Zaloguj się";
            ui.submitBtn.disabled = false;
        }
    }

    // ==========================================
    // 3. LISTENERY
    // ==========================================
    ui.form.addEventListener("submit", handleLogin);
    ui.registerBtn.addEventListener("click", () => {
        window.location.href = "register.html";
    });
});