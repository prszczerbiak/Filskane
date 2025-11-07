const token = localStorage.getItem("token");
if (!token) {
    window.location.href = 'index.html';
}

fetch("/api/userinfo", {
    headers: { "Authorization": "Bearer " + token }
})
    .then(res => {
        if (!res.ok) throw new Error("Błąd autoryzacji");
        return res.json();
    })
    .then(data => {
        document.getElementById("welcome").innerText = `Witaj, ${data.name} 😊`;
    })
    .catch(err => {
        console.error("Nie udało się pobrać danych użytkownika:", err);
        window.location.href = 'index.html';
    });

document.addEventListener("DOMContentLoaded", () => {
    const logoutBtn = document.getElementById('logoutBtn');

    logoutBtn?.addEventListener('click', () => {
        console.log("Kliknięto wyloguj"); // debug
        localStorage.removeItem('token');
        sessionStorage.clear();
        window.location.href = 'index.html';
    });
});
