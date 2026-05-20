// Tiny persistence layer for the dark-mode toggle.
// Stored in localStorage so it survives across visits.
window.valioTheme = {
    get: function() {
        try { return localStorage.getItem('valio-theme') || 'light'; }
        catch { return 'light'; }
    },
    set: function(v) {
        try { localStorage.setItem('valio-theme', v); }
        catch { /* ignored */ }
    }
};
