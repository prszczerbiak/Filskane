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
}
const options = document.querySelectorAll('.menu-option');
const description = document.querySelector('.description');

const content = {
    'Twoje dane': {
        text: `
            <form>
                <div class="form-group">
                    <label for="name">Imię:</label>
                    <div class="input-with-icon">
                        <input type="text" id="name" name="name" readonly>
                        <span class="edit-icon" onclick="enableEdit('name')">✏️</span>
                    </div>
                </div>

                <div class="form-group">
                    <label for="email">Email:</label>
                    <div class="input-with-icon">
                        <input type="email" id="email" name="email" readonly>
                        <span class="edit-icon" onclick="enableEdit('email')">✏️</span>
                    </div>
                </div>

                <div class="form-group">
                    <label for="phone">Telefon:</label>
                    <div class="input-with-icon">
                        <input type="tel" id="phone" name="phone" readonly>
                        <span class="edit-icon" onclick="enableEdit('phone')">✏️</span>
                    </div>
                </div>
            </form>
        `
    },
    'Personalizacja': {
        text: `
        <h3>Jednostki powierzchni:</h3>
        <div class="unit-switch">
            <div class="switch-option active" data-unit="ha">ha</div>
            <div class="switch-option" data-unit="a">a</div>
            <div class="switch-option" data-unit="ac">akr</div>
            <div class="switch-highlight"></div>
        </div>

        <h3 style="margin-top: 30px;">Tryb wyświetlania</h3>
        <div class="theme-switch">
            <div class="theme-option" data-theme="light"><i class="fa-regular fa-sun"></i></div>
            <div class="theme-option" data-theme="dark"><i class="fa-regular fa-moon"></i></div>
            <div class="theme-highlight"></div>
        </div>
    `,
    },
    'Zaawansowane': {
        text: `<br><br><br><div style="
            margin-top: 25px;
            color: red;
            cursor: pointer;
            font-size: 18px;
        " 
        onclick="deleteAccount()">
            Usuń konto
        </div>`
    }
};

async function loadUserShortInfo(token) {
    try {
        const response = await fetch("https://localhost:7273/api/settings/getShortInfo", {
            method: "GET",
            headers: {
                "Authorization": "Bearer " + token
            }
        });

        if (!response.ok) throw new Error("Błąd pobierania shortInfo");

        const userInfo = await response.json();

        // Zapis do localStorage
        localStorage.setItem("userInfo", JSON.stringify(userInfo));

    } catch (error) {
        console.error("loadUserShortInfo error:", error);
    }
}

options.forEach(option => {
    option.addEventListener('click', () => {
        options.forEach(opt => opt.classList.remove('active'));
        option.classList.add('active');

        const key = option.textContent.trim();

        // Wstawiamy HTML bez owinięcia w <p> — tak żeby zawarty HTML był poprawny
        description.innerHTML = `
            ${content[key].title ? `<h2>${content[key].title}</h2>` : ''}
            ${content[key].text}
        `;

        // jeśli wybrano "Twoje dane" – pobierz dane użytkownika
        if (key === "Twoje dane") {
            loadUserProfile();
        }

        // jeśli wybrano "Personalizacja" — zainicjalizuj suwak
        if (key === 'Personalizacja') {
            // Upewnij się, że initUnitSwitch jest wywołane PO wstawieniu HTML do DOM
            initUnitSwitch();
            initThemeSwitch();
        }
    });
});

document.addEventListener('DOMContentLoaded', () => {
    const activeOption = document.querySelector('.menu-option.active');
    if (activeOption) activeOption.click();
});

async function loadUserProfile() {
    const token = localStorage.getItem("token");
    if (!token) {
        window.location.href = "/index.html";
        return;
    }

    try {
        const response = await fetch("/api/settings/getLongInfo", {
            method: "GET",
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (!response.ok) throw new Error("Błąd pobierania danych");

        const data = await response.json();

        // Wypełnij pola — ale musisz poczekać aż formularz zostanie wstawiony do DOM!
        requestAnimationFrame(() => {
            document.getElementById("name").value = data.name ?? "";
            document.getElementById("email").value = data.email ?? "";
            document.getElementById("phone").value = data.phone ?? "";
        });

    } catch (err) {
        console.error("Błąd podczas pobierania profilu:", err);
    }
}




function initUnitSwitch() {
    const switchEl = description.querySelector('.unit-switch'); // ograniczone do .description
    if (!switchEl) return;

    const currentUnit = localStorage.getItem('unitType') || 'ha';
    const highlight = switchEl.querySelector('.switch-highlight');
    const options = Array.from(switchEl.querySelectorAll('.switch-option'));

    options.forEach((opt, idx) => {
        if (opt.dataset.unit === currentUnit) {
            opt.classList.add('active');
            // ustaw left (bez błędów)
            highlight.style.left = `calc(${idx} * 100% / ${options.length})`;
        } else {
            opt.classList.remove('active');
        }
    });
}

// delegacja kliknięć ograniczona do .description (mniej kolizji globalnych)
description.addEventListener('click', (e) => {
    const option = e.target.closest('.switch-option');
    if (!option) return;

    const parent = option.closest('.unit-switch');
    const highlight = parent.querySelector('.switch-highlight');

    // Usuń poprzednią aktywną klasę
    parent.querySelectorAll('.switch-option').forEach(opt => opt.classList.remove('active'));

    // Dodaj nową aktywną
    option.classList.add('active');

    // Oblicz index tylko spośród .switch-option (ignoruj highlight)
    const options = Array.from(parent.querySelectorAll('.switch-option'));
    const index = options.indexOf(option);
    highlight.style.left = `calc(${index} * 100% / ${options.length})`;

    // Zapisz wybraną jednostkę do localStorage
    const unit = option.dataset.unit;

    // zapisz lokalnie w prostym formacie
    localStorage.setItem('unitType', unit);

    // przelicz wartość do bazy danych (0=ha, 1=a, 2=akr)
    const surfaceValue = unit === 'a' ? 1 : unit === 'ac' ? 2 : 0;

    // zaktualizuj zapisany obiekt użytkownika
    const userInfo = JSON.parse(localStorage.getItem('userInfo') || "{}");
    userInfo.surface = surfaceValue;
    localStorage.setItem('userInfo', JSON.stringify(userInfo));

    // wyślij aktualizację tylko dla surface
    const token = localStorage.getItem("token");
    fetch("https://localhost:7273/api/settings/updateSurface", {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${token}`
        },
        body: JSON.stringify({ surface: surfaceValue })
    })
        .then(res => {
            if (!res.ok) throw new Error("Błąd aktualizacji jednostki");
            return res.json();
        })
        .then(() => console.log("✅ Jednostka zaktualizowana w bazie"))
        .catch(err => console.error("❌ Błąd przy zapisie jednostki:", err));

    // Odśwież wyświetlanie wartości powierzchni
    if (window.lastLoadedFieldData) {
        const areaEl = document.querySelector("#area");
        if (areaEl) {
            areaEl.textContent = formatArea(window.lastLoadedFieldData.area);
        }
    }
});

function initThemeSwitch() {
    const switchEl = document.querySelector('.theme-switch');
    if (!switchEl) return;

    const currentTheme = localStorage.getItem('theme') || 'light';
    const highlight = switchEl.querySelector('.theme-highlight');
    const options = Array.from(switchEl.querySelectorAll('.theme-option'));

    options.forEach((opt, idx) => {
        if (opt.dataset.theme === currentTheme) {
            opt.classList.add('active');
            highlight.style.left = `calc(${idx} * 100% / ${options.length})`;
        } else {
            opt.classList.remove('active');
        }
    });

    // Ustaw motyw od razu po załadowaniu
    applyTheme(currentTheme);
}

description.addEventListener('click', (e) => {
    const themeOption = e.target.closest('.theme-option');
    if (!themeOption) return;

    const parent = themeOption.closest('.theme-switch');
    const highlight = parent.querySelector('.theme-highlight');
    const options = Array.from(parent.querySelectorAll('.theme-option'));

    options.forEach(opt => opt.classList.remove('active'));
    themeOption.classList.add('active');

    const index = options.indexOf(themeOption);
    highlight.style.left = `calc(${index} * 100% / ${options.length})`;

    const theme = themeOption.dataset.theme;
    const themeInt = (theme === 'dark') ? 1 : 0;
    localStorage.setItem('theme', theme);
    applyTheme(theme);

    // --- NOWOŚĆ: Zapis do bazy danych ---
    const token = localStorage.getItem("token");

    // Opcjonalnie: aktualizujemy też obiekt userInfo w localStorage, żeby był spójny
    const userInfo = JSON.parse(localStorage.getItem('userInfo') || "{}");
    userInfo.theme = theme;
    localStorage.setItem('userInfo', JSON.stringify(userInfo));

    // Wysyłamy do backendu
    fetch("/api/settings/updateTheme", { // Upewnij się, że ta ścieżka pasuje do C# [Route]
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${token}`
        },
        body: JSON.stringify({ theme: themeInt })
    })
        .then(res => {
            if (!res.ok) throw new Error("Błąd zapisu motywu");
            console.log(`✅ Motyw ${theme} zapisany w bazie.`);
        })
        .catch(err => {
            console.error("❌ Błąd przy zapisie motywu:", err);
            // Opcjonalnie: Pokaż użytkownikowi dymek z błędem
        });
});

function applyTheme(theme) {
    if (theme === 'dark') {
        document.documentElement.classList.add('dark-theme');
    } else {
        document.documentElement.classList.remove('dark-theme');
    }
}

function enableEdit(type) {
    const map = {
        name: { modal: "changeNameModal", input: "nameInput", source: "name" },
        email: { modal: "changeEmailModal", input: "emailInput", source: "email" },
        phone: { modal: "changeTelModal", input: "telInput", source: "phone" }
    };

    const config = map[type];
    if (!config) return;

    const modal = document.getElementById(config.modal);
    const input = document.getElementById(config.input);
    const source = document.getElementById(config.source);

    if (input && source) {
        input.value = source.value;       // ← aktualne dane
        input.placeholder = source.value; // ← fallback, jeśli chcesz
    }

    modal.style.display = "block";
}

function deleteAccount() {
    const modal = document.getElementById("deleteAccountModal");
    if (modal) {
        modal.style.display = "block";
    }
}

// Zamknięcie modali przy kliknięciu "Anuluj"
document.querySelectorAll(".btn-close").forEach(btn => {
    btn.addEventListener("click", () => {
        closeAllModals();
    });
});

function closeAllModals() {
    document.querySelectorAll(".modal").forEach(m => {
        m.style.display = "none";
    });
}

// Obsługa formularza ZMIANA IMIENIA
document.getElementById("dateForm").addEventListener("submit", async function (e) {
    e.preventDefault();

    const newName = document.getElementById("nameInput").value;

    const response = await fetch("/api/settings/updateName", {
        method: "POST",
        headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ name: newName })
    });

    if (response.ok) {
        document.getElementById("name").value = newName;
        closeAllModals();
        loadUserShortInfo(token);
        alert("Pomyślnie zmieniono imię");
    } else {
        alert("Błąd podczas zapisywania imienia.");
    }
});

// Obsługa ZMIANY EMAILA
document.getElementById("emailForm").addEventListener("submit", async function (e) {
    e.preventDefault();

    const newEmail = document.getElementById("emailInput").value;

    const response = await fetch("/api/settings/updateEmail", {
        method: "POST",
        headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ email: newEmail })
    });

    if (response.ok) {
        document.getElementById("email").value = newEmail;
        closeAllModals();
        loadUserShortInfo(token);
        alert("Pomyślnie zmieniono email");
    } else {
        alert("Błąd podczas zapisywania emaila.");
    }
});

// Obsługa ZMIANY TELEFONU
document.getElementById("telForm").addEventListener("submit", async function (e) {
    e.preventDefault();

    const newPhone = document.getElementById("telInput").value;

    const response = await fetch("/api/settings/updatePhone", {
        method: "POST",
        headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ phone: newPhone })
    });

    if (response.ok) {
        document.getElementById("phone").value = newPhone;
        closeAllModals();
        loadUserShortInfo(token);
        alert("Pomyślnie zmieniono numer telefonu");
    } else {
        alert("Błąd podczas zapisywania telefonu.");
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const modal = document.getElementById("changeEmailModal");
    const form = document.getElementById("emailForm");
    const input = document.getElementById("emailInput");
    const msg = document.getElementById("emailMessage");
    const btnSave = form.querySelector(".btn-save");
    const btnClose = form.querySelector(".btn-close");

    // --- funkcja walidacji emaila ---
    async function validateEmail() {
        const email = input.value.trim();
        const pattern = /^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$/;

        if (!email) {
            msg.textContent = "";
            btnSave.disabled = true;
            return false;
        }

        if (!pattern.test(email)) {
            msg.textContent = "Nieprawidłowy format adresu e-mail ❌";
            msg.style.color = "red";
            btnSave.disabled = true;
            return false;
        }

        try {
            const res = await fetch("/api/auth/check-email", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ email })
            });
            const data = await res.json();

            if (data.exists) {
                msg.textContent = "Email jest już zajęty ❌";
                msg.style.color = "red";
                btnSave.disabled = true;
                return false;
            } else {
                msg.textContent = "Email jest dostępny ✅";
                msg.style.color = "green";
                btnSave.disabled = false;
                return true;
            }
        } catch {
            msg.textContent = "Błąd połączenia z serwerem ❌";
            msg.style.color = "red";
            btnSave.disabled = true;
            return false;
        }
    }

    // --- Walidacja na żywo podczas pisania ---
    input.addEventListener("input", validateEmail);

    // --- Obsługa przycisku Zapisz ---
    form.addEventListener("submit", async (e) => {
        e.preventDefault();

        if (!(await validateEmail())) return; // blokada jeśli niepoprawny email

        try {
            const token = localStorage.getItem("token"); // jeśli używasz JWT
            const response = await fetch("/api/settings/updateEmail", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": "Bearer " + token
                },
                body: JSON.stringify({ email: input.value.trim() })
            });

            if (!response.ok) throw new Error("Błąd zapisu emaila");

            // opcjonalnie: odśwież dane w UI lub localStorage
            msg.textContent = "Email zapisany ✅";
            msg.style.color = "green";
            setTimeout(() => modal.style.display = "none", 800);

        } catch (err) {
            console.error(err);
            msg.textContent = "Błąd zapisu emaila ❌";
            msg.style.color = "red";
        }
    });

    // --- Obsługa przycisku Anuluj ---
    btnClose.addEventListener("click", () => {
        modal.style.display = "none";
    });
});

document.addEventListener("DOMContentLoaded", () => {
    const modal = document.getElementById("changeTelModal");
    const form = document.getElementById("telForm");
    const input = document.getElementById("telInput");
    const msg = document.getElementById("telMessage");
    const btnSave = form.querySelector(".btn-save");
    const btnClose = form.querySelector(".btn-close");

    if (!msg) {
        console.error("Nie znaleziono elementu #telMessage w DOM!");
        return;
    }

    // --- funkcja walidacji telefonu ---
    function validatePhone() {
        const phone = input.value.trim();
        const pattern = /^\+48\d{9}$/; // +48 + 9 cyfr

        if (!phone) {
            msg.textContent = "";
            btnSave.disabled = true;
            return false;
        }

        if (!pattern.test(phone)) {
            msg.textContent = "Nieprawidłowy numer telefonu (format: +48xxxXXXxxx) ❌";
            msg.style.color = "red";
            btnSave.disabled = true;
            return false;
        }

        msg.textContent = "Numer telefonu poprawny ✅";
        msg.style.color = "green";
        btnSave.disabled = false;
        return true;
    }

    // --- walidacja na żywo ---
    input.addEventListener("input", validatePhone);

    // --- obsługa Zapisz ---
    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        if (!validatePhone()) return;

        try {
            const token = localStorage.getItem("token");
            const response = await fetch("/api/settings/updatePhone", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": "Bearer " + token
                },
                body: JSON.stringify({ phone: input.value.trim() })
            });

            if (!response.ok) throw new Error("Błąd zapisu telefonu");

            msg.textContent = "Telefon zapisany ✅";
            msg.style.color = "green";
            setTimeout(() => modal.style.display = "none", 800);

        } catch (err) {
            console.error(err);
            msg.textContent = "Błąd zapisu telefonu ❌";
            msg.style.color = "red";
        }
    });

    // --- obsługa Anuluj ---
    btnClose.addEventListener("click", () => {
        modal.style.display = "none";
    });
});

document.addEventListener("DOMContentLoaded", () => {

    const modal = document.getElementById("deleteAccountModal");
    const form = document.getElementById("deleteForm");
    const input = document.getElementById("deleteInput");
    const btnClose = form.querySelector(".btn-close");

    // --- Obsługa zamknięcia ---
    btnClose.addEventListener("click", () => {
        modal.style.display = "none";
        input.value = "";
    });

    // --- Obsługa potwierdzenia usunięcia ---
    form.addEventListener("submit", async (e) => {
        e.preventDefault();

        const password = input.value.trim();
        if (!password) return;

        try {
            const token = localStorage.getItem("token");

            const response = await fetch("/api/auth/deleteAccount", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": "Bearer " + token
                },
                body: JSON.stringify({ password })
            });

            const data = await response.json();

            if (!response.ok) {
                alert(data.message || "Hasło niepoprawne");
                return;
            }

            // --- Konto usunięte ---
            alert("Twoje konto zostało usunięte.");

            // wyczyść dane lokalne
            localStorage.removeItem("token");
            localStorage.removeItem("userInfo");

            // przekierowanie do logowania
            window.location.href = "index.html";

        } catch (err) {
            console.error("Błąd usuwania konta:", err);
            alert("Wystąpił błąd podczas usuwania konta.");
        }
    });

});
