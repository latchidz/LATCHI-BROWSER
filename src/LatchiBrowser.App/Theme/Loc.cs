namespace LatchiBrowser.App.Theme;

/// <summary>
/// LATCHI UI strings — Arabic + English (§77). Website language is a separate concern
/// (§79): we never force page languages, only the app chrome speaks these.
/// Plain, friendly wording only — no technical jargon in the UI (round 10).
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

        // ── tab context menu (round 10) ──
        ["duplicateTab"]   = ("تكرار التبويب", "Duplicate tab"),
        ["closeOtherTabs"] = ("إغلاق التبويبات الأخرى", "Close other tabs"),
        ["closeTabsRight"] = ("إغلاق التبويبات على اليمين", "Close tabs to the right"),

        // ── profiles (§14-§20) — plain words, no technical terms ──
        ["profileBtn"]           = ("الحسابات", "Accounts"),
        ["profileAdd"]           = ("إضافة حساب Google", "Add Google account"),
        ["profileAddNote"]       = ("أضف حسابًا للتنقل بسرعة بين حساباتك المختلفة.", "Add an account to quickly switch between your accounts."),
        ["profileRename"]        = ("إعادة تسمية الحساب", "Rename account"),
        ["profileRemove"]        = ("حذف هذا الحساب", "Remove this account"),
        ["profileRemoveConfirm"] = ("سيُحذف هذا الحساب وتبويباته المفتوحة وسجلّه من LATCHI.\nهل أنت متأكد؟", "This account, its open tabs and history will be removed from LATCHI.\nAre you sure?"),
        ["profileDefault"]       = ("الحساب الشخصي", "Personal"),

        // ── start page (round 10) ──
        ["startSearchHint"] = ("ابحث في Google أو اكتب عنوانًا", "Search Google or type a URL"),
        ["quickAccess"]     = ("وصول سريع", "Quick access"),
        ["favoritesRow"]    = ("المفضلة", "Favorites"),
        ["recentRow"]       = ("الزيارات الأخيرة", "Recently visited"),
        ["customizeHome"]   = ("تخصيص الصفحة", "Customize this page"),
        ["addShortcut"]     = ("إضافة اختصار", "Add shortcut"),
        ["editShortcut"]    = ("تعديل الاختصار", "Edit shortcut"),
        ["removeShortcut"]  = ("حذف الاختصار", "Remove shortcut"),
        ["shortcutName"]    = ("الاسم", "Name"),
        ["shortcutUrl"]     = ("العنوان (URL)", "Address (URL)"),
        ["shortcutInvalidUrl"] = ("العنوان غير صالح — اكتب مثلًا: youtube.com", "Invalid address — try for example: youtube.com"),

        // ── home background (round 10) ──
        ["bgDark"]   = ("داكن", "Dark"),
        ["bgColor"]  = ("لون", "Color"),
        ["bgImage"]  = ("صورة", "Image"),
        ["bgRemove"] = ("إزالة الصورة", "Remove image"),
        ["bgNavy"]   = ("أزرق ليلي", "Night blue"),
        ["bgViolet"] = ("بنفسجي", "Violet"),
        ["bgGreen"]  = ("أخضر", "Green"),
        ["bgGray"]   = ("رمادي", "Gray"),
        ["bgWine"]   = ("نبيذي", "Wine"),

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
        ["extensionsTitle"]  = ("الإضافات", "Extensions"),
        ["extEmpty"]         = ("لا إضافات مثبّتة", "No extensions installed"),
        ["extInstall"]       = ("تثبيت من مجلد…", "Install from folder…"),
        ["extNote"]          = ("تُثبَّت الإضافات من مجلد يحوي ملف manifest.json. التثبيت من متجر Chrome غير مدعوم حاليًا.", "Install extensions from a folder containing a manifest.json file. Chrome Web Store installs aren't supported yet."),
        ["extRemove"]        = ("إزالة", "Remove"),
        ["extEnable"]        = ("تفعيل", "Enable"),
        ["extDisable"]       = ("تعطيل", "Disable"),
        ["extRemoveConfirm"] = ("إزالة هذه الإضافة؟", "Remove this extension?"),
        ["extInstallOk"]     = ("تم تثبيت الإضافة بنجاح", "Extension installed"),
        ["extInstallFail"]   = ("فشل تثبيت الإضافة", "Extension installation failed"),
        ["extEnabledState"]  = ("مفعّلة", "Enabled"),
        ["extDisabledState"] = ("معطّلة", "Disabled"),

        // ── settings (§51-§53 + round 10 sections) ──
        ["settingsTitle"]  = ("الإعدادات", "Settings"),
        ["setGeneral"]     = ("عام", "General"),
        ["setLanguage"]    = ("لغة الواجهة (منفصلة عن لغة المواقع)", "UI language (separate from website language)"),
        ["setHome"]        = ("صفحة البداية", "Home page"),
        ["setSearch"]      = ("محرك البحث", "Search engine"),
        ["setAppearance"]  = ("المظهر", "Appearance"),
        ["setShowBar"]     = ("إظهار شريط المفضلة", "Show the bookmarks bar"),
        ["setBg"]          = ("خلفية الصفحة الرئيسية", "Home page background"),
        ["setTabs"]        = ("التبويبات", "Tabs"),
        ["restoreTabs"]    = ("استعادة التبويبات المفتوحة عند بدء التشغيل", "Restore open tabs on startup"),
        ["setPrivacy"]     = ("الخصوصية", "Privacy"),
        ["privacyHistory"] = ("مسح سجل التصفح", "Clear browsing history"),
        ["privacyCache"]   = ("مسح الملفات المؤقتة (الكاش)", "Clear cached files"),
        ["privacyCookies"] = ("مسح ملفات تعريف الارتباط (Cookies)", "Clear cookies"),
        ["privacyTitle"]   = ("الخصوصية", "Privacy"),
        ["privacyDone"]    = ("تم المسح بنجاح", "Cleared successfully"),
        ["privacyNeedTab"] = ("افتح أي موقع أولًا ثم أعد المحاولة.", "Open any website first, then try again."),
        ["setAi"]          = ("LATCHI AI — مساعد Gemini", "LATCHI AI — Gemini assistant"),
        ["setAiEnable"]    = ("تفعيل المساعد", "Enable the assistant"),
        ["setModel"]       = ("الموديل", "Model"),
        ["setKey"]         = ("مفتاح API", "API key"),
        ["setKeyNote"]     = ("يُحفظ المفتاح مشفّرًا على هذا الجهاز فقط، ولا يُرسل إلا إلى Google.", "The key is stored encrypted on this device only, and is sent to Google alone."),
        ["setKeyStored"]   = ("مفتاح محفوظ — اترك الحقل فارغًا للإبقاء عليه", "A key is stored — leave the field empty to keep it"),
        ["setKeyRemove"]   = ("حذف المفتاح المحفوظ", "Delete stored key"),
        ["setSave"]        = ("حفظ", "Save"),
        ["setCancel"]      = ("إلغاء", "Cancel"),
        ["setAbout"]       = ("حول", "About"),

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
    };

    /// <summary>Looks up a localized string. Unknown keys return themselves (visible, testable).</summary>
    public static string S(string lang, string key)
        => Table.TryGetValue(key, out var v) ? (lang == "en" ? v.En : v.Ar) : key;
}
