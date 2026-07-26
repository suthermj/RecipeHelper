if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').then(function (registration) {
        function promptReload(worker) {
            showUpdateBanner(function () {
                worker.postMessage({ type: 'SKIP_WAITING' });
            });
        }

        // A worker was already waiting when this page loaded (e.g. a deploy
        // happened while the tab was open).
        if (registration.waiting) {
            promptReload(registration.waiting);
        }

        // A new worker just finished installing during this page's lifetime.
        registration.addEventListener('updatefound', function () {
            var newWorker = registration.installing;
            if (!newWorker) return;
            newWorker.addEventListener('statechange', function () {
                if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                    promptReload(newWorker);
                }
            });
        });
    });

    // Reload once the new worker takes control (after SKIP_WAITING is posted),
    // so the page picks up the fresh HTML/JS/CSS it now controls.
    var reloadedForUpdate = false;
    navigator.serviceWorker.addEventListener('controllerchange', function () {
        if (reloadedForUpdate) return;
        reloadedForUpdate = true;
        window.location.reload();
    });
}

function showUpdateBanner(onReload) {
    if (document.getElementById('swUpdateBanner')) return;

    var banner = document.createElement('button');
    banner.id = 'swUpdateBanner';
    banner.type = 'button';
    banner.textContent = 'Update available — tap to refresh';
    banner.style.cssText =
        'position:fixed; left:50%; transform:translateX(-50%) translateY(-8px); ' +
        'top:calc(env(safe-area-inset-top) + 8px); z-index:300; ' +
        'max-width:24rem; width:calc(100% - 2rem); ' +
        'border-radius:16px; padding:12px 16px; ' +
        'font-size:15px; font-weight:500; font-family:inherit; text-align:center; ' +
        'color:#fff; background:rgba(255,59,48,0.95); border:none; ' +
        'box-shadow:0 8px 24px rgba(0,0,0,0.25); ' +
        'backdrop-filter:blur(20px); -webkit-backdrop-filter:blur(20px); ' +
        'opacity:0; transition:opacity .25s ease, transform .25s ease;';

    document.body.appendChild(banner);
    requestAnimationFrame(function () {
        banner.style.opacity = '1';
        banner.style.transform = 'translateX(-50%) translateY(0)';
    });

    banner.addEventListener('click', function () {
        banner.disabled = true;
        banner.textContent = 'Updating…';
        onReload();
    });
}

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
