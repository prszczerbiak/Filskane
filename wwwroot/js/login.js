document.addEventListener("DOMContentLoaded", function () {
    const form = document.querySelector("form");
    const errorMessage = document.getElementById("errorMessage");

    form.addEventListener("submit", async function (event) {
        event.preventDefault(); // Zapobiega przeładowaniu strony

        const username = form.elements[0].value; // Pobranie nazwy użytkownika
        const password = form.elements[1].value; // Pobranie hasła

        // Wysłanie zapytania do API

        /*
        if (username !== "admin" || password !== "1234") {
            errorMessage.style.display = "block"; // Pokaż komunikat błędu
        } else {
            errorMessage.style.display = "none";  // Ukryj komunikat błędu
            alert("Zalogowano pomyślnie!");
        }
        */
        
        try {
            const response = await fetch("https://localhost:7273/api/auth/login", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({ username, password }),
            });

            if (!response.ok) {
                throw new Error("Błędne dane logowania");
            }

            const data = await response.json();
            localStorage.setItem("token", data.token); // Zapisz token w localStorage
            window.location.href = "/dashboard.html"; // Przekierowanie po zalogowaniu

        } catch (error) {
            errorMessage.style.display = "block"; // Pokaż komunikat błędu
        }
        
    });
});
