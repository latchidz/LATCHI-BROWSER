namespace LatchiBrowser.App.Theme;

/// <summary>
/// LATCHI UI strings — Arabic + English (§77). Website language is a separate concern
/// (§79): we never force page languages, only the app chrome speaks these.
/// Unknown keys return themselves (visible in UI + covered by tests — no silent misses).
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, (string Ar, string En)> Table = new()
    {
        ["appName"]        = ("LATCHI Browser", "LATCHI Browser"),
        ["newTab"]         = ("تبويب جديد", "New tab"),
        ["privateTab"]     = ("تبويب خاص", "InPrivate tab"),
        ["privateSuffix"]  = (" — خاص", " — InPrivate"),
        ["tabClose"]       = ("إغلاق التبويب", "Close tab"),
        ["reopenTab"]      = ("إعادة فتح التبويب المغلق (Ctrl+Shift+T)", "Reopen closed tab (Ctrl+Shift+T)"),
        ["addressHint"]    = ("ابحث أو اكتب عنوانًا", "Search or type a URL"),
        ["startingEngine"] = ("جارٍ تشغيل محرك التصفح…", "Starting the browser engine…"),

        ["navBack"]    = ("رجوع", "Back"),
        ["navForward"] = ("تقدّم", "Forward"),
        ["navReload"]  = ("تحديث", "Reload"),
        ["navStop"]    = ("إيقاف", "Stop"),
        ["navHome"]    = ("الصفحة الرئيسية", "Home"),

        ["newTabBtn"]        = ("تبويب جديد (Ctrl+T)", "New tab (Ctrl+T)"),
        ["newWindow"]        = ("نافذة جديدة (Ctrl+N)", "New window (Ctrl+N)"),
        ["newPrivateWindow"] = ("نافذة خاصة (Ctrl+Shift+N)", "New InPrivate window (Ctrl+Shift+N)"),
        ["fullscreen"]       = ("ملء الشاشة (F11)", "Fullscreen (F11)"),
        ["langSwitch"]       = ("English", "العربية"), // shows the language you switch TO

        ["winMin"]     = ("تصغير", "Minimize"),
        ["winMax"]     = ("تكبير", "Maximize"),
        ["winRestore"] = ("استعادة", "Restore"),
        ["winClose"]   = ("إغلاق", "Close"),

        ["menuAbout"]      = ("حول LATCHI Browser", "About LATCHI Browser"),
        ["menuExit"]       = ("خروج", "Exit"),
        ["menuSettings"]   = ("الإعدادات", "Settings"),
        ["menuHistory"]    = ("السجل (Ctrl+H)", "History (Ctrl+H)"),
        ["menuDownloads"]  = ("التنزيلات (Ctrl+J)", "Downloads (Ctrl+J)"),
        ["menuExtensions"] = ("الإضافات", "Extensions"),
        ["menuBookmarks"]  = ("إدارة المفضلة", "Manage bookmarks"),
        ["menuZoomIn"]     = ("تكبير (Ctrl++)", "Zoom in (Ctrl++)"),
        ["menuZoomOut"]    = ("تصغير (Ctrl+-)", "Zoom out (Ctrl+-)"),
        ["menuZoomReset"]  = ("الحجم العادي (Ctrl+0)", "Actual size (Ctrl+0)"),
        ["menuBar"]        = ("شريط المفضلة (Ctrl+Shift+B)", "Bookmarks bar (Ctrl+Shift+B)"),
        ["menuAi"]         = ("مساعد LATCHI AI", "LATCHI AI assistant"),

        // ── profiles (§14-§20) ──
        ["profileBtn"]            = ("الحسابات", "Accounts"),
        ["profileAdd"]            = ("إضافة حساب", "Add account"),
        ["profileAddNote"]        = ("يُنشأ حساب جديد بجلسات معزولة تمامًا.", "Creates a new account with fully isolated sessions."),
        ["profileRename"]         = ("إعادة تسمية الحساب", "Rename account"),
        ["profileSignInGoogle"]   = ("تسجيل الدخول إلى Google", "Sign in to Google"),
        ["profileRemove"]         = ("حذف هذا الحساب", "Remove this account"),
        ["profileRemoveConfirm"]  = ("سيُحذف هذا الحساب وتبويباته المفتوحة وسجلّه وبيانات جلساته من LATCHI.\nهل أنت متأكد؟", "This account, its open tabs, history and session data will be removed from LATCHI.\nAre you sure?"),
        ["profileDefault"]        = ("الحساب الشخصي", "Personal"),
        ["profileEmptySlots"]     = ("حساباتك", "Your accounts"),

        // ── bookmarks (§24/§25) ──
        ["starAdd"]           = ("إضافة إلى المفضلة (Ctrl+D)", "Add bookmark (Ctrl+D)"),
        ["starRemove"]        = ("إزالة من المفضلة (Ctrl+D)", "Remove bookmark (Ctrl+D)"),
        ["bookmarkOpen"]      = ("فتح", "Open"),
        ["bookmarkRemove"]    = ("حذف من المفضلة", "Remove bookmark"),
        ["bookmarksBarEmpty"] = ("اضغط ☆ بجانب العنوان لإضافة الصفحة الحالية", "Press ★ next to the address bar to bookmark this page"),
        ["bookmarksClear"]    = ("مسح كل المفضلة", "Clear all bookmarks"),
        ["bookmarksConfirm"]  = ("سيُمسح كل المفضلة. هل أنت متأكد؟", "All bookmarks will be cleared. Are you sure?"),
        ["bookmarksTitle"]    = ("المفضلة", "Bookmarks"),

        // ── history (§26) ──
        ["historyTitle"]        = ("سجل التصفح", "Browsing history"),
        ["historySearch"]       = ("ابحث في السجل…", "Search history…"),
        ["historyEmpty"]        = ("لا سجل بعد", "No history yet"),
        ["historyClear"]        = ("مسح السجل", "Clear history"),
        ["historyConfirmClear"] = ("سيُمسح سجل هذا الحساب بالكامل. هل أنت متأكد؟", "This account's entire history will be cleared. Are you sure?"),
        ["historyDelete"]       = ("حذف", "Delete"),
        ["historyOpen"]         = ("فتح", "Open"),

        // ── downloads (§27) ──
        ["downloadsTitle"]   = ("التنزيلات", "Downloads"),
        ["downloadsEmpty"]   = ("لا تنزيلات بعد", "No downloads yet"),
        ["downloadOpen"]     = ("فتح الملف", "Open file"),
        ["downloadShow"]     = ("إظهار في المجلد", "Show in folder"),
        ["downloadPause"]    = ("إيقاف مؤقت", "Pause"),
        ["downloadResume"]   = ("متابعة", "Resume"),
        ["downloadCancel"]   = ("إلغاء", "Cancel"),
        ["downloadsClear"]   = ("إزالة المنتهية من القائمة", "Clear finished"),
        ["dlRunning"]        = ("جارٍ التنزيل", "Downloading"),
        ["dlPaused"]         = ("متوقف مؤقتًا", "Paused"),
        ["dlDone"]           = ("اكتمل", "Completed"),
        ["dlInterrupted"]    = ("انقطع", "Interrupted"),

        // ── extensions (§28-§34) ──
        ["extensionsTitle"]       = ("الإضافات", "Extensions"),
        ["extEmpty"]              = ("لا إضافات مثبّتة", "No extensions installed"),
        ["extInstall"]            = ("تثبيت من مجلد…", "Install from folder…"),
        ["extNote"]               = ("يدعم LATCHI الإضافات غير المعبّأة (مجلد يحوي manifest.json) عبر واجهات WebView2 الرسمية فقط. لا يوجد تثبيت من متجر Chrome حاليًا — وهذا قيد معلن لا نخفيه.", "LATCHI supports unpacked extensions (a folder containing manifest.json) via official WebView2 APIs only. Chrome Web Store installs are not supported today — an honest, stated limitation."),
        ["extRemove"]             = ("إزالة", "Remove"),
        ["extEnable"]             = ("تفعيل", "Enable"),
        ["extDisable"]            = ("تعطيل", "Disable"),
        ["extRemoveConfirm"]      = ("إزالة هذه الإضافة؟", "Remove this extension?"),
        ["extInstallOk"]          = ("تم تثبيت الإضافة بنجاح", "Extension installed"),
        ["extInstallFail"]        = ("فشل تثبيت الإضافة", "Extension installation failed"),
        ["extEnabledState"]       = ("مفعّلة", "Enabled"),
        ["extDisabledState"]      = ("معطّلة", "Disabled"),

        // ── settings (§51-§53) ──
        ["settingsTitle"]     = ("الإعدادات", "Settings"),
        ["setGeneral"]        = ("عام", "General"),
        ["setLanguage"]       = ("لغة الواجهة (منفصلة عن لغة المواقع)", "UI language (separate from website language)"),
        ["setHome"]           = ("صفحة البداية", "Home page"),
        ["setSearch"]         = ("محرك البحث", "Search engine"),
        ["setAi"]             = ("LATCHI AI — مساعد Gemini", "LATCHI AI — Gemini assistant"),
        ["setAiEnable"]       = ("تفعيل المساعد", "Enable the assistant"),
        ["setModel"]          = ("الموديل", "Model"),
        ["setKey"]            = ("مفتاح API", "API key"),
        ["setKeyPlaceholder"] = ("أدخل المفتاح ليُحفظ مشفّرًا…", "Paste the key to store it encrypted…"),
        ["setKeyNote"]        = ("يُخزَّن المفتاح مشفّرًا على هذا الجهاز فقط (Windows DPAPI) ولا يُرسل إلا إلى Google. لا يظهر أبدًا في الإعدادات أو السجلات.", "The key is stored encrypted on this machine only (Windows DPAPI) and is sent to Google alone. It is never shown in settings or logs."),
        ["setKeyStored"]      = ("مفتاح محفوظ — اترك الحقل فارغًا للإبقاء عليه", "A key is stored — leave the field empty to keep it"),
        ["setKeyRemove"]      = ("حذف المفتاح المحفوظ", "Delete stored key"),
        ["setSave"]           = ("حفظ", "Save"),
        ["setCancel"]         = ("إلغاء", "Cancel"),
        ["setAbout"]          = ("حول", "About"),

        // ── AI sidebar (§54-§59) ──
        ["aiTitle"]      = ("LATCHI AI", "LATCHI AI"),
        ["aiGreeting"]   = ("مرحبًا! أنا LATCHI AI، مساعدك داخل المتصفح — اسألني عن أي شيء.", "Hi! I'm LATCHI AI, your in-browser assistant — ask me anything."),
        ["aiThinking"]   = ("…يفكّر", "Thinking…"),
        ["aiInputHint"]  = ("اسأل أي شيء…", "Ask anything…"),
        ["aiSend"]       = ("إرسال", "Send"),
        ["aiNoKey"]      = ("لم يُضبط مفتاح Gemini بعد.\nافتح قائمة ⋮ ثم الإعدادات ← LATCHI AI وأدخل مفتاحك — يُحفظ مشفّرًا على جهازك فقط.", "No Gemini key configured yet.\nOpen the ⋮ menu → Settings → LATCHI AI and enter your key — stored encrypted on this device only."),
        ["aiDisabled"]   = ("المساعد معطّل. فعّله من الإعدادات.", "The assistant is disabled. Enable it in Settings."),
        ["aiErrorPrefix"] = ("خطأ: ", "Error: "),

        // ── errors ──
        ["errRuntimeTitle"] = ("مطلوب Microsoft Edge WebView2 Runtime", "Microsoft Edge WebView2 Runtime is required"),
        ["errRuntimeDetail"] = (
            "محرّك العرض في LATCHI Browser يحتاج Microsoft Edge WebView2 Runtime وهو غير مثبّت على هذا الجهاز.\nحمّله من الموقع الرسمي ثم أعد تشغيل التطبيق.",
            "The LATCHI Browser engine needs Microsoft Edge WebView2 Runtime, which is not installed on this machine.\nGet it from the official site, then restart the app."),
        ["openDownload"] = ("فتح صفحة التثبيت الرسمية", "Open the official download page"),
        ["closeApp"]     = ("إغلاق التطبيق", "Close the app"),

        ["confirmYes"] = ("نعم", "Yes"),
        ["confirmNo"]  = ("لا", "No"),

        // ── first-run wizard (user request 2026-10-06) ──
        ["welcomeTitle"]        = ("مرحبًا بك في LATCHI Browser", "Welcome to LATCHI Browser"),
        ["welcomeNote"]         = ("سجّل الدخول بحسابك في Google لتجهيز حسابك الخاص — بجلسة معزولة تمامًا تبقى محفوظة على جهازك.", "Sign in with your Google account to set up your own space — a fully isolated session that stays saved on this device."),
        ["continueWithGoogle"]  = ("المتابعة باستخدام Google", "Continue with Google"),
        ["googleSecurityNote"]  = ("يفتح التطبيق صفحة Google الرسمية للتسجيل. لا يطلب LATCHI كلمة السر ولا يراها ولا يخزّنها أبدًا.", "Opens the official Google sign-in page. LATCHI never asks for, sees or stores your password."),
        ["googleFooterNote"]    = ("تسجيل الدخول يتم عبر accounts.google.com الرسمي · جميع الحقوق لمالكيها", "Sign-in happens on the official accounts.google.com · all rights belong to their owners"),
        ["signedInOk"]          = ("تم تسجيل الدخول بنجاح", "Signed in successfully"),
        ["mustSignIn"]          = ("أغلقت نافذة تسجيل الدخول قبل إكماله.\nشغّل التطبيق من جديد وسجّل دخولك بحساب Google لفتح المتصفح.", "You closed the sign-in window before finishing.\nRun the app again and sign in with your Google account to open the browser."),
        ["startSearchHint"]     = ("ابحث في Google أو اكتب عنوانًا", "Search Google or type a URL"),
    };

    /// <summary>Looks up a localized string. Unknown keys return themselves (visible, testable).</summary>
    public static string S(string lang, string key)
        => Table.TryGetValue(key, out var v) ? (lang == "en" ? v.En : v.Ar) : key;
}
