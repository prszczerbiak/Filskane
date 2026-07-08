document.addEventListener("DOMContentLoaded", () => {
    const accountMenu = document.getElementById("accountMenu");
    const accountMenuToggle = document.getElementById("accountMenuToggle");
    const logoutBtn = document.getElementById("logoutBtn");

    function closeAccountMenu() {
        if (!accountMenu || !accountMenuToggle) return;
        accountMenu.classList.remove("open");
        accountMenuToggle.setAttribute("aria-expanded", "false");
    }

    if (accountMenu && accountMenuToggle) {
        accountMenuToggle.addEventListener("click", (event) => {
            event.stopPropagation();
            const isOpen = accountMenu.classList.toggle("open");
            accountMenuToggle.setAttribute("aria-expanded", String(isOpen));
        });

        document.addEventListener("click", (event) => {
            if (!accountMenu.contains(event.target)) {
                closeAccountMenu();
            }
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                closeAccountMenu();
            }
        });
    }

    if (logoutBtn) {
        logoutBtn.addEventListener("click", () => {
            localStorage.clear();
            sessionStorage.clear();
            window.location.href = "login.html";
        });
    }
});
