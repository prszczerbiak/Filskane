const token = localStorage.getItem("token");

fetch("/api/userinfo", {
    headers: {
        "Authorization": "Bearer " + token
    }
})
.then(res => res.json())
.then(data => {
    document.getElementById("welcome").innerText = `Witaj, ${data.name} 😊`;
    // Możesz też użyć data.email, data.telephone itd.
});