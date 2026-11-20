// Light/dark colour theme, ported from A1's wwwroot/js/theme.js.
//
// Loaded in <head> before the stylesheets so a saved preference applies before the first paint
// (no white flash in dark mode). Unlike A1, it never edits the toggle button's DOM itself: Blazor
// owns that markup, so the ThemeToggle component reads and sets the theme through window.memsTheme.
// The preference is a per-browser convenience in localStorage; it is not election data.
(function () {
    "use strict";

    var storageKey = "municipal-elections-theme";
    var root = document.documentElement;
    var media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;
    var listeners = {};
    var nextId = 1;
    var preference = read();

    function read() {
        try {
            var value = window.localStorage.getItem(storageKey);
            return value === "light" || value === "dark" ? value : null;
        } catch (e) {
            return null; // storage blocked: follow the system setting
        }
    }

    function effective() {
        return preference || (media && media.matches ? "dark" : "light");
    }

    function apply() {
        var theme = effective();
        root.setAttribute("data-bs-theme", theme);
        root.style.colorScheme = theme;
        Object.keys(listeners).forEach(function (id) {
            listeners[id].invokeMethodAsync("OnThemeChanged", theme).catch(function () { });
        });
    }

    apply();

    if (media && media.addEventListener) {
        media.addEventListener("change", function () { if (!preference) apply(); });
    }
    // Another tab changed the preference.
    window.addEventListener("storage", function (event) {
        if (event.key === storageKey || event.key === null) {
            preference = read();
            apply();
        }
    });

    window.memsTheme = {
        current: effective,
        set: function (theme) {
            if (theme !== "light" && theme !== "dark") return effective();
            preference = theme;
            try { window.localStorage.setItem(storageKey, theme); } catch (e) { /* still applies for this page */ }
            apply();
            return theme;
        },
        // dotNetRef: DotNetObjectReference with an [JSInvokable] OnThemeChanged(string) method.
        subscribe: function (dotNetRef) {
            var id = nextId++;
            listeners[id] = dotNetRef;
            return id;
        },
        unsubscribe: function (id) { delete listeners[id]; }
    };
}());
