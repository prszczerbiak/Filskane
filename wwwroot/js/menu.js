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
    'Ustawienia': {
        title: 'Opcja 2',
        text: 'Zawartość przypisana do opcji 2.'
    },
    'Opcja 1': {
        title: 'Opcja 3',
        text: 'Zawartość przypisana do opcji 3.'
    }
};

options.forEach(option => {
    option.addEventListener('click', () => {
        options.forEach(opt => opt.classList.remove('active'));
        option.classList.add('active');

        const key = option.textContent.trim();
        description.innerHTML = `
        ${content[key].title ? `<h2>${content[key].title}</h2>` : ''}
        <p>${content[key].text}</p>
    `;

        // jeśli wybrano "Twoje dane" – pobierz dane użytkownika
        if (key === "Twoje dane") {
            loadUserProfile();
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
        const response = await fetch("https://localhost:7273/api/userinfo", {
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
            document.getElementById("phone").value = data.telephone ?? "";
        });

    } catch (err) {
        console.error("Błąd podczas pobierania profilu:", err);
    }
}

function enableEdit(fieldId) {
    const input = document.getElementById(fieldId);
    if (input) {
        input.removeAttribute("readonly");
        input.focus();
    }
}
