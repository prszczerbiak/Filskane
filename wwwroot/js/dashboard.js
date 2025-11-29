
const savedTheme = localStorage.getItem('theme') || 'light';
if (savedTheme === 'dark') {
    document.documentElement.classList.add('dark-theme');
}

function handleUnauthorized() {
    alert("Twoja sesja wygasła. Zaloguj się ponownie.");
    localStorage.removeItem("token"); // usuń stary token
    window.location.href = "index.html"; // przekierowanie do strony logowania
}

// Sprawdzenie od razu przy ładowaniu strony
const token = localStorage.getItem("token");
//if (!token) {
//    handleUnauthorized();
//}

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



const cachedUser = localStorage.getItem("userInfo");

if (cachedUser) {
    const data = JSON.parse(cachedUser);
    document.getElementById("welcome").innerText = `Witaj, ${data.name} 😊`;
} else if (token) {
    // Jeśli nie ma danych w pamięci, pobierz z API
    fetch("/api/userinfo/getShortInfo", {
        headers: { "Authorization": "Bearer " + token }
    })
        .then(res => {
            if (!res.ok) throw new Error("Błąd autoryzacji");
            return res.json();
        })
        .then(data => {
            // Zapisz dane w localStorage, żeby przy następnym wejściu nie fetchować ponownie
            localStorage.setItem("userInfo", JSON.stringify(data));
            document.getElementById("welcome").innerText = `Witaj, ${data.name} 😊`;
        })
        .catch(err => {
            console.error("Nie udało się pobrać danych użytkownika:", err);
            localStorage.removeItem("token");
            localStorage.removeItem("userInfo");
            window.location.href = "index.html";
        });
} else {
    // Brak tokena — przekierowanie do logowania
    window.location.href = "index.html";
}

document.addEventListener("DOMContentLoaded", () => {
    const logoutBtn = document.getElementById('logoutBtn');

    logoutBtn?.addEventListener('click', () => {
        console.log("Kliknięto wyloguj"); // debug
        localStorage.removeItem('token');
        sessionStorage.clear();
        window.location.href = 'index.html';
    });
});
