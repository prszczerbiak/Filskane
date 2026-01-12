document.addEventListener("DOMContentLoaded", () => {
    // ==========================================
    // 1. SELEKTORY DOM I KONFIGURACJA
    // ==========================================
    const form = document.getElementById("registerForm");
    
    const fields = {
        name: document.getElementById("name"),
        username: document.getElementById("username"),
        email: document.getElementById("email"),
        password: document.getElementById("password"),
        againPassword: document.getElementById("againPassword"),
        terms: document.getElementById("acceptTerms")
    };

    const ui = {
        errorName: document.getElementById("errorName"),
        errorUsername: document.getElementById("errorUsername"),
        errorEmail: document.getElementById("errorEmail"),
        emailMessage: document.getElementById("emailMessage"),
        errorPassword: document.getElementById("errorPassword"),
        passwordInfo: document.getElementById("passwordInfo"),
        errorAgainPassword: document.getElementById("errorAgainPassword"),
        successAgainPassword: document.getElementById("successAgainPassword"),
        errorTerms: document.getElementById("errorTerms")
    };

    const API_ENDPOINTS = {
        REGISTER: "/api/auth/register",
        CHECK_EMAIL: "/api/auth/check-email"
    };

    // ==========================================
    // 2. FUNKCJE POMOCNICZE (VALIDATORS)
    // ==========================================

    /**
     * Sprawdza czy pole nie jest puste.
     */
    function validateRequired(field, errorElement) {
        if (!field.value.trim()) {
            errorElement.style.display = "block";
            return false;
        }
        errorElement.style.display = "none";
        return true;
    }

    /**
     * Sprawdza złożoność hasła (Regex).
     */
    function isStrongPassword(password) {
        return (
            password.length >= 7 &&
            /[A-Z]/.test(password) &&
            /[a-z]/.test(password) &&
            /\d/.test(password) &&
            /[!@#$%^&*(),.?":{}|<>_\-+=/\\[\]]/.test(password)
        );
    }

    /**
     * Sprawdza poprawność formatu e-mail.
     */
    function isValidEmailFormat(email) {
        const pattern = /^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$/;
        return pattern.test(email);
    }

    // ==========================================
    // 3. LOGIKA BIZNESOWA
    // ==========================================

    /**
     * Porównuje oba hasła i wyświetla odpowiedni komunikat.
     */
    function checkPasswordsMatch() {
        const pass = fields.password.value;
        const again = fields.againPassword.value;

        // Jeśli pole powtórzenia jest puste, ukrywamy komunikaty
        if (!again) {
            ui.errorAgainPassword.style.display = "none";
            ui.successAgainPassword.style.display = "none";
            return;
        }

        if (pass === again) {
            ui.errorAgainPassword.style.display = "none";
            ui.successAgainPassword.style.display = "block";
        } else {
            ui.errorAgainPassword.style.display = "block";
            ui.successAgainPassword.style.display = "none";
        }
    }

    /**
     * Asynchroniczne sprawdzanie dostępności adresu e-mail w bazie.
     */
    async function checkEmailAvailability() {
        const email = fields.email.value.trim();
        const msg = ui.emailMessage;

        // Reset komunikatów
        msg.textContent = "";
        ui.errorEmail.style.display = "none";

        if (!email) return;

        if (!isValidEmailFormat(email)) {
            msg.textContent = "Nieprawidłowy format adresu e-mail ❌";
            msg.style.color = "#dc3545"; // Czerwony
            return;
        }

        try {
            const res = await fetch(API_ENDPOINTS.CHECK_EMAIL, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ email })
            });

            if (!res.ok) throw new Error("Server error");

            const data = await res.json();
            
            if (data.exists) {
                msg.textContent = "Ten email jest już zajęty ❌";
                msg.style.color = "#dc3545";
            } else {
                msg.textContent = "Email jest dostępny ✅";
                msg.style.color = "#28a745"; // Zielony
            }
        } catch (error) {
            console.error(error);
            msg.textContent = "Błąd weryfikacji e-maila ⚠️";
            msg.style.color = "orange";
        }
    }

    /**
     * Walidacja całego formularza przed wysyłką.
     */
    function validateForm() {
        let isValid = true;

        isValid &= validateRequired(fields.name, ui.errorName);
        isValid &= validateRequired(fields.username, ui.errorUsername);
        isValid &= validateRequired(fields.email, ui.errorEmail);
        isValid &= validateRequired(fields.password, ui.errorPassword);

        // Walidacja siły hasła
        if (!isStrongPassword(fields.password.value)) {
            ui.passwordInfo.classList.add("invalid-text"); // Możesz dodać klasę CSS dla koloru czerwonego
            isValid = false;
        } else {
            ui.passwordInfo.classList.remove("invalid-text");
        }

        // Walidacja zgodności haseł
        if (fields.password.value !== fields.againPassword.value) {
            ui.errorAgainPassword.style.display = "block";
            isValid = false;
        }

        // Walidacja regulaminu
        if (!fields.terms.checked) {
            ui.errorTerms.style.display = "block";
            isValid = false;
        } else {
            ui.errorTerms.style.display = "none";
        }

        return !!isValid;
    }

    /**
     * Wysłanie danych do API.
     */
    async function submitRegistration() {
        const payload = {
            name: fields.name.value.trim(),
            username: fields.username.value.trim(),
            email: fields.email.value.trim(),
            password: fields.password.value.trim()
        };

        const submitBtn = form.querySelector("button[type='submit']");
        const originalBtnText = submitBtn.textContent;
        
        // Blokada przycisku na czas requestu
        submitBtn.disabled = true;
        submitBtn.textContent = "Przetwarzanie...";

        try {
            const response = await fetch(API_ENDPOINTS.REGISTER, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            const data = await response.json();

            if (response.ok) {
                // Sukces - przekierowanie na stronę potwierdzenia
                window.location.href = "verifyRegistration.html";
            } else {
                alert(`❌ Błąd rejestracji: ${data.message || "Spróbuj ponownie później."}`);
            }
        } catch (error) {
            console.error("Critical error:", error);
            alert("❌ Wystąpił błąd połączenia z serwerem.");
        } finally {
            // Odblokowanie przycisku
            submitBtn.disabled = false;
            submitBtn.textContent = originalBtnText;
        }
    }

    // ==========================================
    // 4. OBSŁUGA ZDARZEŃ (EVENT LISTENERS)
    // ==========================================

    // Dynamiczna walidacja hasła
    fields.password.addEventListener("input", () => {
        const strong = isStrongPassword(fields.password.value);
        // Opcjonalnie: zmiana koloru tekstu z wymaganiami
        ui.passwordInfo.style.color = strong ? "green" : ""; 
        
        // Sprawdź od razu czy pasują do siebie, jeśli użytkownik poprawia pierwsze hasło
        if (fields.againPassword.value) checkPasswordsMatch();
    });

    // Sprawdzanie zgodności haseł
    fields.againPassword.addEventListener("input", checkPasswordsMatch);

    // Sprawdzanie e-maila po opuszczeniu pola (blur)
    fields.email.addEventListener("blur", checkEmailAvailability);

    // Obsługa wysyłki formularza
    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        if (validateForm()) {
            await submitRegistration();
        }
    });
});