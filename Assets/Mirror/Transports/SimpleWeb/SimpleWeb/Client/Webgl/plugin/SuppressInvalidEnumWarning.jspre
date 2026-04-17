// Workaround for Chrome bug: https://issues.chromium.org/issues/454273251
// Unity bug: UUM-93245
// Chrome incorrectly emits INVALID_ENUM warnings when Unity probes these 6
// internal formats. Intercept the call and return null for the known-bad enums.
(function ()
{
    const _getInternalformatParameter = WebGL2RenderingContext.prototype.getInternalformatParameter;
    const invalidFormats = new Set([36756, 36757, 36759, 36760, 36761, 36763]);

    WebGL2RenderingContext.prototype.getInternalformatParameter = function (target, internalformat, pname)
    {
        if (invalidFormats.has(internalformat)) return null;
        return _getInternalformatParameter.call(this, target, internalformat, pname);
    };
})();