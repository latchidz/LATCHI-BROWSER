namespace LatchiBrowser.App.Theme;

/// <summary>
/// LATCHI UI strings — Arabic + English (§77). Website language is a separate concern
/// (§79): we never force page languages, only the app chrome speaks these.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, (string Ar, string En)> Table = new()
    {
        ["appName"]        = ("LATCHI Browser", "LATCHI Browser"),
        ["newTab"]         = ("تبويب جديد", "New tab"),
        ["tabClose"]       = ("إغلاق التبويب", "Close tab"),
        ["reopenTab"]      = ("إعادة فتح التبويب المغلق (Ctrl+Shift+T)", "Reopen closed tab (Ctrl+Shift+T)"),
        ["addressHint"]    = ("ابحث في Google أو اكتب عنوانًا", "Search Google or type a URL"),
        ["startingEngine"] = ("جارٍ تشغيل محرك التصفح…", "Starting the browser engine…"),

        ["navBack"]    = ("رجوع", "Back"),
        ["navForward"] = ("تقدّم", "Forward"),
        ["navReload"]  = ("تحديث", "Reload"),
        ["navStop"]    = ("إيقاف", "Stop"),
        ["navHome"]    = ("الصفحة الرئيسية", "Home"),

        ["newTabBtn"] = ("تبويب جديد (Ctrl+T)", "New tab (Ctrl+T)"),
        ["fullscreen"] = ("ملء الشاشة (F11)", "Fullscreen (F11)"),
        ["langSwitch"] = ("English", "العربية"), // shows the language you switch TO

        ["winMin"]     = ("تصغير", "Minimize"),
        ["winMax"]     = ("تكبير", "Maximize"),
        ["winRestore"] = ("استعادة", "Restore"),
        ["winClose"]   = ("إغلاق", "Close"),

        ["menuAbout"]  = ("حول LATCHI Browser", "About LATCHI Browser"),
        ["menuExit"]   = ("خروج", "Exit"),

        ["errRuntimeTitle"] = ("مطلوب Microsoft Edge WebView2 Runtime", "Microsoft Edge WebView2 Runtime is required"),
        ["errRuntimeDetail"] = (
            "محرّك العرض في LATCHI Browser يحتاج Microsoft Edge WebView2 Runtime وهو غير مثبّت على هذا الجهاز.\nحمّله من الموقع الرسمي ثم أعد تشغيل التطبيق.",
            "The LATCHI Browser engine needs Microsoft Edge WebView2 Runtime, which is not installed on this machine.\nGet it from the official site, then restart the app."),
        ["openDownload"] = ("فتح صفحة التثبيت الرسمية", "Open the official download page"),
        ["closeApp"]     = ("إغلاق التطبيق", "Close the app"),
    };

    /// <summary>Looks up a localized string. Unknown keys return themselves (visible, testable).</summary>
    public static string S(string lang, string key)
        => Table.TryGetValue(key, out var v) ? (lang == "en" ? v.En : v.Ar) : key;
}
