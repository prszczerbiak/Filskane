const options = document.querySelectorAll('.menu-option');
const description = document.querySelector('.description');

const content = {
    'Twoje dane': {
        text: `
            <form>
                <label for="name">Imię:</label><br>
                <input type="text" id="name" name="name"><br><br>

                <label for="email">Email:</label><br>
                <input type="email" id="email" name="email"><br><br>

                <label for="phone">Telefon:</label><br>
                <input type="tel" id="phone" name="phone"><br><br>
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
            <h2>${content[key].title}</h2>
            <p>${content[key].text}</p>
        `;
    });
});