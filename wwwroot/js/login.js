document.addEventListener("DOMContentLoaded", function () {
    const form = document.querySelector("form");
    const errorMessage = document.getElementById("errorMessage");

    form.addEventListener("submit", async function (event) {
        event.preventDefault();

        const username = form.elements[0].value;
        const password = form.elements[1].value;

        try {
            const response = await fetch("https://localhost:7273/api/auth/login", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ username, password })
            });

            if (!response.ok) throw new Error("Błędne dane logowania");

            const data = await response.json();
            localStorage.setItem("token", data.token);

            // 🔹 Od razu pobierz dane użytkownika po zalogowaniu
            const userResponse = await fetch("https://localhost:7273/api/settings/getShortInfo", {
                method: "GET",
                headers: {
                    "Authorization": "Bearer " + data.token
                }
            });

            if (!userResponse.ok) throw new Error("Błąd pobierania danych użytkownika");

            const userInfo = await userResponse.json();

            // 🔹 Zapisz dane użytkownika w localStorage
            localStorage.setItem("userInfo", JSON.stringify(userInfo));

            // 🔹 Możesz też od razu zastosować dark mode itp.
            if (userInfo.darkMode === 1) {
                document.documentElement.classList.add("dark-mode");
            }

            // 🔹 Następnie przekieruj do dashboarda
            window.location.href = "/dashboard.html";

        } catch (error) {
            console.error(error);
            errorMessage.style.display = "block";
        }
    });
});

// 🔹 Funkcja do stosowania motywu ciemnego/jasnego
function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
}
