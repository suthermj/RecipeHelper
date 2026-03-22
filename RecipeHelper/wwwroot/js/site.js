// Loading overlay for forms with class "loading-form"
// Optionally set data-loading-text="Custom message..." on the form
(function () {
    var overlay = document.getElementById('loadingOverlay');
    var loadingText = document.getElementById('loadingText');
    if (!overlay) return;

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form.classList.contains('loading-form')) return;

        var text = form.getAttribute('data-loading-text') || 'Loading...';
        if (loadingText) loadingText.textContent = text;

        overlay.classList.remove('hidden');
        overlay.classList.add('flex');

        // Disable all submit buttons in the form to prevent double-submit
        form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (btn) {
            btn.disabled = true;
        });
    });
})();
