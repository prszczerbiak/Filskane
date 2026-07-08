const savedTheme = localStorage.getItem('theme') || 'light';
if (savedTheme === 'dark') {
    document.documentElement.classList.add('dark-theme');
}

function handleUnauthorized() {
    alert("Twoja sesja wygasła. Zaloguj się ponownie.");
    localStorage.removeItem("token"); // usuń stary token
    window.location.href = "index.html"; // przekierowanie do strony logowania
}

// Interceptor dla fetch, żeby złapać 401
const originalFetch = window.fetch;
window.fetch = async (...args) => {
    const response = await originalFetch(...args);
    if (response.status === 401) {
        handleUnauthorized(); // pokaz alert i wróć do logowania
        throw new Error("Unauthorized");
    }
    return response;
};

// Sprawdzenie od razu przy ładowaniu strony
const token = localStorage.getItem("token");
if (!token) {
    handleUnauthorized();
    throw new Error("Brak tokena – zatrzymano dalsze wykonywanie skryptu");
}

async function getCityName(lat, lon) {
    try {
        const res = await fetch(`https://geocoding-api.open-meteo.com/v1/reverse?latitude=${lat}&longitude=${lon}`);
        const data = await res.json();
        // Pierwszy wynik z listy miejscowości
        if (data && data.results && data.results.length > 0) {
            return data.results[0].name;
        }
    } catch (err) {
        console.warn("Nie udało się pobrać nazwy miasta", err);
    }
    return "Nieznane miasto";
}

async function loadWeather() {
    let lat = 52.2297;   // domyślna Warszawa
    let lon = 21.0122;

    // Spróbuj pobrać userInfo z localStorage
    let userInfo = JSON.parse(localStorage.getItem("userInfo"));

    // Jeśli userInfo nie istnieje lub współrzędne są null, fetch z API
    if (!userInfo || userInfo.farmX == null || userInfo.farmY == null) {
        try {
            const token = localStorage.getItem("token");
            const res = await fetch("https://localhost:7273/api/userinfo/getShortInfo", {
                method: "GET",
                headers: { "Authorization": "Bearer " + token }
            });

            if (!res.ok) throw new Error("Błąd pobierania danych użytkownika");

            userInfo = await res.json();
            localStorage.setItem("userInfo", JSON.stringify(userInfo)); // zapisujemy do localStorage
        } catch (err) {
            console.warn("Nie udało się pobrać danych użytkownika, użyto domyślnych współrzędnych", err);
            userInfo = null;
        }
    }

    // Jeśli mamy poprawne współrzędne, użyj ich
    if (userInfo && userInfo.farmX != null && userInfo.farmY != null) {
        lon = userInfo.farmX;
        lat = userInfo.farmY;
    }

    const resCity = await fetch(`https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lon}&format=json`);
    const dataCity = await resCity.json();
    const cityName = dataCity.address.city || dataCity.address.town || dataCity.address.village || "Nieznane miasto";
    document.querySelector(".weather-header h3").textContent = `Pogoda dla ${cityName} - prognoza na 24h`;

    const url = `https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lon}&hourly=temperature_2m,weathercode,windspeed_10m&forecast_days=2&timezone=auto`;

    const res = await fetch(url);
    const data = await res.json();

    const hoursDiv = document.getElementById("weather-hours");
    hoursDiv.innerHTML = "";

    const now = new Date();
    const currentHour = now.getHours();

    // Ikony Open-Meteo (prosty mapping)
    const icons = {
        0: "fa-sun",          // ☀️
        1: "fa-sun-cloud",    // 🌤️
        2: "fa-cloud-sun",    // ⛅
        3: "fa-cloud",        // ☁️
        45: "fa-smog",        // 🌫️
        48: "fa-smog",        // 🌫️
        51: "fa-cloud-rain",  // 🌦️
        53: "fa-cloud-showers-heavy", // 🌧️
        55: "fa-cloud-showers-heavy", // 🌧️
        61: "fa-cloud-showers-heavy", // 🌧️
        63: "fa-cloud-showers-heavy", // 🌧️
        65: "fa-cloud-showers-heavy", // 🌧️
        71: "fa-snowflake",   // ❄️
        73: "fa-snowflake",   // ❄️
        75: "fa-snowflake",   // ❄️
        95: "fa-bolt-cloud",  // ⛈️
        96: "fa-bolt-cloud",  // ⛈️
        99: "fa-bolt-cloud"   // ⛈️
    };

    for (let i = 0; i < 24; i++) {
        const hourIndex = currentHour + i;

        if (hourIndex >= data.hourly.time.length) break;

        const time = new Date(data.hourly.time[hourIndex]);
        const temp = data.hourly.temperature_2m[hourIndex];
        const wcode = data.hourly.weathercode[hourIndex];
        const wind = data.hourly.windspeed_10m[hourIndex];

        const hourBox = document.createElement("div");
        hourBox.classList.add("weather-hour");

        hourBox.innerHTML = `
            <div>${time.getHours()}:00</div>
            <div style="font-size: 26px;"><i class="fas ${icons[wcode] ?? 'fa-question'}"></i></div>
            <div>${temp}°C</div>
            <div style="font-size: 12px; color: #555;">💨 ${wind} m/s</div>
        `;

        hoursDiv.appendChild(hourBox);
    }
}

async function loadUserReports() {
    const list = document.getElementById("userReportsList");
    if (!list) return;

    list.innerHTML = "<p>Ładowanie raportów...</p>";

    try {
        const token = localStorage.getItem("token");
        const res = await fetch("https://localhost:7273/api/field/reports", {
            method: "GET",
            headers: { "Authorization": "Bearer " + token }
        });

        if (!res.ok) throw new Error("Nie udało się pobrać raportów");

        const reports = await res.json();
        if (!reports || reports.length === 0) {
            list.innerHTML = "<p>Brak raportów do wyświetlenia.</p>";
            return;
        }

        list.innerHTML = reports.map(r => `
            <div class="report-item">
                <div class="report-title">Pole: ${r.fieldName || 'Nieznane pole'}</div>
                <div class="report-meta">ID raportu: ${r.reportId}</div>
                <div class="report-meta">Data: ${r.generatedAt ? new Date(r.generatedAt).toLocaleString('pl-PL') : 'Brak danych'}</div>
                <div class="report-status ${`status-${(r.validation || 'pending').toLowerCase()}`}">
                    ${r.validation === 'confirmed' ? 'Zatwierdzony' : (r.validation === 'rejected' ? 'Odrzucony' : 'Oczekuje na walidację')}
                </div>
            </div>
        `).join('');
    } catch (err) {
        list.innerHTML = `<p>Błąd pobierania raportów: ${err.message}</p>`;
    }
}

function setupNewsTabs() {
    const tabs = document.querySelectorAll('.news-tab');
    const panels = {
        agroNews: document.getElementById('agroNewsPanel'),
        userReports: document.getElementById('userReportsPanel')
    };

    tabs.forEach(tab => {
        tab.addEventListener('click', async () => {
            tabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');

            Object.values(panels).forEach(panel => {
                if (panel) panel.style.display = 'none';
            });

            const target = tab.getAttribute('data-tab');
            if (target === 'agroNews' && panels.agroNews) panels.agroNews.style.display = 'block';
            if (target === 'userReports' && panels.userReports) {
                panels.userReports.style.display = 'block';
                await loadUserReports();
            }
        });
    });
}

setupNewsTabs();
loadWeather();
