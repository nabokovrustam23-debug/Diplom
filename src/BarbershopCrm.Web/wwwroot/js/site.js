// Глобальные UX-помощники: подтверждения, авто-скрытие уведомлений, доступность.
(function () {
    'use strict';

    // 1. Подтверждение действий по data-confirm на формах и ссылках.
    //    Эвристика Нильсена №5 «Предотвращение ошибок»: критические действия
    //    (удалить, отменить, не пришёл) требуют явного подтверждения.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement)) return;
        var msg = form.getAttribute('data-confirm');
        if (msg && !window.confirm(msg)) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);

    document.addEventListener('click', function (e) {
        var el = e.target;
        while (el && el !== document.body && !(el.matches && el.matches('[data-confirm]'))) {
            el = el.parentElement;
        }
        if (!el || el === document.body) return;
        if (el.tagName === 'A' || (el.tagName === 'BUTTON' && el.type !== 'submit')) {
            var msg = el.getAttribute('data-confirm');
            if (msg && !window.confirm(msg)) {
                e.preventDefault();
                e.stopImmediatePropagation();
            }
        }
    }, true);

    // 2. Авто-скрытие flash-уведомлений через 6 секунд + закрытие по кнопке.
    //    Эвристика №1 «Видимость статуса»: уведомление видно, но не мешает.
    function bindFlash() {
        document.querySelectorAll('[data-flash]').forEach(function (el) {
            var hide = function () { el.classList.add('flash--hidden'); };
            var closeBtn = el.querySelector('[data-flash-close]');
            if (closeBtn) closeBtn.addEventListener('click', hide);
            setTimeout(hide, 6000);
        });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindFlash);
    } else {
        bindFlash();
    }

    // 3. Маска телефона +7 (XXX) XXX-XX-XX для input[type="tel"].
    //    Эвристика №2 «Соответствие реальному миру»: формат привычен пользователю.
    //    Эвристика №5 «Предотвращение ошибок»: формат подсказывается визуально.
    function maskPhone(input) {
        var raw = input.value.replace(/\D+/g, '');
        if (!raw) { input.value = ''; return; }
        if (raw[0] === '8') raw = '7' + raw.slice(1);
        if (raw[0] !== '7') raw = '7' + raw;
        raw = raw.slice(0, 11);
        var out = '+7';
        if (raw.length > 1) out += ' (' + raw.slice(1, 4);
        if (raw.length >= 4) out += ') ' + raw.slice(4, 7);
        if (raw.length >= 7) out += '-' + raw.slice(7, 9);
        if (raw.length >= 9) out += '-' + raw.slice(9, 11);
        input.value = out;
    }
    document.querySelectorAll('input[type="tel"]').forEach(function (input) {
        input.addEventListener('input', function () { maskPhone(input); });
        input.addEventListener('focus', function () {
            if (!input.value) input.value = '+7 (';
        });
    });
})();
