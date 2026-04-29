/* =========================================================================
   Nigerian Quarry Management System - site.js
   Global helpers.  Currently: Naira currency formatting for inputs & displays.
   ========================================================================= */
(function ($) {
    'use strict';

    /* -----------------------------------------------------------------------
       NairaFormat - public API
       Usage:
         NairaFormat.format(1250000)          => "₦1,250,000.00"
         NairaFormat.format(1250000, {symbol:false})          => "1,250,000.00"
         NairaFormat.format(1250000, {decimals:0})            => "₦1,250,000"
         NairaFormat.parse("₦1,250,000.00")   => 1250000
    ----------------------------------------------------------------------- */
    window.NairaFormat = {
        SYMBOL: '\u20A6', // ₦

        format: function (value, opts) {
            opts = opts || {};
            var withSymbol = opts.symbol !== false;                    // default true
            var decimals   = typeof opts.decimals === 'number' ? opts.decimals : 2;

            if (value === null || value === undefined || value === '') return '';
            var num = typeof value === 'number' ? value : parseFloat(String(value).replace(/[^0-9.\-]/g, ''));
            if (isNaN(num)) return '';

            var s = num.toLocaleString('en-NG', {
                minimumFractionDigits: decimals,
                maximumFractionDigits: decimals
            });
            return withSymbol ? this.SYMBOL + s : s;
        },

        parse: function (str) {
            if (str === null || str === undefined || str === '') return 0;
            if (typeof str === 'number') return str;
            var cleaned = String(str).replace(/[^0-9.\-]/g, ''); // strip ₦, commas, spaces
            var n = parseFloat(cleaned);
            return isNaN(n) ? 0 : n;
        }
    };

    /* -----------------------------------------------------------------------
       Auto-wiring
       Any <input class="money-input"> is converted to a live-formatted field:
         - User sees:    ₦1,250,000.00  with thousands separators as they type
         - Server gets:  1250000.00     (raw decimal, bound to original asp-for)
       Works by switching the real input to type="hidden" and inserting a
       visible text proxy in front of it.
    ----------------------------------------------------------------------- */
    function wireMoneyInput($real) {
        if ($real.data('moneyWired')) return;
        $real.data('moneyWired', true);

        // Read options off data-attrs (all optional)
        var decimals    = $real.data('decimals');
        if (typeof decimals !== 'number') decimals = 2;
        var showSymbol  = $real.data('symbol') !== false;  // default true
        var placeholder = $real.attr('placeholder') || '0.00';

        // Preserve the initial server-rendered value
        var initialVal = $real.val();
        var initialNum = NairaFormat.parse(initialVal);

        // Copy classes (minus money-input) onto the proxy so Bootstrap styling stays
        var classes = ($real.attr('class') || '')
            .split(/\s+/)
            .filter(function (c) { return c && c !== 'money-input'; })
            .join(' ');

        // Build proxy
        var $proxy = $('<input>', {
            type: 'text',
            class: classes + ' money-input-display',
            inputmode: 'decimal',
            autocomplete: 'off',
            placeholder: placeholder
        });

        // Attach the ₦ prefix if requested AND the real input isn't already in an input-group
        // (avoids stacking two ₦ prefixes when the view already wraps it with <span>₦</span>)
        var alreadyInInputGroup = $real.closest('.input-group').length > 0;
        var renderWithSymbol    = showSymbol && !alreadyInInputGroup;

        if (initialNum) {
            $proxy.val(NairaFormat.format(initialNum, { symbol: renderWithSymbol, decimals: decimals }));
        }

        // Hide the real input, keep its name/id/value for model binding
        $real.attr('type', 'hidden');
        // If the real input had min/max/step, those don't matter on a hidden field.
        // Server-side validation still runs off the submitted value.

        $real.before($proxy);

        // Live formatting while typing: keep raw number in hidden input,
        // re-format the proxy preserving caret position roughly.
        $proxy.on('input', function () {
            var el         = this;
            var caret      = el.selectionStart;
            var before     = el.value;
            var digitsBefore = before.substr(0, caret).replace(/[^0-9]/g, '').length;

            var raw = NairaFormat.parse(before);

            // While typing, don't force decimal padding - let the user type freely
            // Only apply thousands separators to the integer part
            var negative = before.trim().charAt(0) === '-';
            var cleaned  = before.replace(/[^0-9.]/g, '');
            var parts    = cleaned.split('.');
            var intPart  = parts[0] || '';
            var fracPart = parts.length > 1 ? parts.slice(1).join('').slice(0, decimals) : null;

            var intFmt = intPart ? Number(intPart).toLocaleString('en-NG') : '';
            var display = intFmt;
            if (fracPart !== null) display += '.' + fracPart;
            if (negative && display) display = '-' + display;
            if (renderWithSymbol && display) display = NairaFormat.SYMBOL + display;

            el.value = display;

            // Restore caret based on digit count
            var newCaret = 0, digits = 0;
            while (newCaret < display.length && digits < digitsBefore) {
                if (/[0-9]/.test(display.charAt(newCaret))) digits++;
                newCaret++;
            }
            try { el.setSelectionRange(newCaret, newCaret); } catch (e) { /* noop */ }

            // Update hidden input with raw decimal
            $real.val(isNaN(raw) ? '' : raw);
            $real.trigger('change'); // so downstream totals/scripts still fire
        });

        $proxy.on('blur', function () {
            var raw = NairaFormat.parse($(this).val());
            if (raw || $(this).val() !== '') {
                $(this).val(NairaFormat.format(raw, { symbol: renderWithSymbol, decimals: decimals }));
                $real.val(raw.toFixed(decimals));
            } else {
                $real.val('');
            }
        });

        $proxy.on('focus', function () {
            // On focus, drop the symbol so selection/typing feels natural
            var raw = NairaFormat.parse($(this).val());
            if (raw) {
                $(this).val(NairaFormat.format(raw, { symbol: false, decimals: decimals }));
            }
        });

        // Keep proxy in sync if something else (e.g. a calculate-totals script) writes to the real input
        $real.on('change.moneySync', function () {
            if (document.activeElement === $proxy[0]) return; // user is typing, leave alone
            var raw = NairaFormat.parse($real.val());
            $proxy.val(raw ? NairaFormat.format(raw, { symbol: renderWithSymbol, decimals: decimals }) : '');
        });
    }

    /* -----------------------------------------------------------------------
       Auto-format read-only displays: <span class="money-display">1250000</span>
       => ₦1,250,000.00
    ----------------------------------------------------------------------- */
    function wireMoneyDisplay($el) {
        if ($el.data('moneyWired')) return;
        $el.data('moneyWired', true);
        var decimals   = typeof $el.data('decimals') === 'number' ? $el.data('decimals') : 2;
        var showSymbol = $el.data('symbol') !== false;
        var raw        = NairaFormat.parse($el.text());
        $el.text(NairaFormat.format(raw, { symbol: showSymbol, decimals: decimals }));
    }

    /* -----------------------------------------------------------------------
       Init on ready + expose a re-scan hook for dynamically injected content
    ----------------------------------------------------------------------- */
    function scan(root) {
        var $root = root ? $(root) : $(document);
        $root.find('input.money-input').each(function () { wireMoneyInput($(this)); });
        $root.find('.money-display').each(function ()   { wireMoneyDisplay($(this)); });
    }

    window.NairaFormat.scan = scan;

    $(function () { scan(); });

})(jQuery);
