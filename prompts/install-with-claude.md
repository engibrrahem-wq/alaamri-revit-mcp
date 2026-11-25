# Install Alaamri Revit MCP with Claude — copy-paste prompt

Paste the prompt below into **Claude Code** (or Claude Desktop with a terminal / Desktop Commander tool) on the Windows PC that has Revit 2027.

---

```text
Please install "Alaamri Revit MCP" on this Windows PC for me.

1. Find the project on GitHub: https://github.com/engibrrahem-wq/alaamri-revit-mcp
   Read its README first so you understand what it installs (a Revit 2027 add-in + a local Python MCP server).
2. Check the requirements: Windows, Revit 2027, Python 3.10+. Tell me if anything is missing and how to get it.
3. Ask me to close Revit (and save my work) before installing.
4. Download the repository (git clone, or the ZIP of the main branch) into a folder in my user profile and run install.bat from it.
   Show me the installer output. If something fails, read the error and fix it.
5. Register the server for the Claude app I am using:
   - Claude Desktop: install.bat already does it; tell me to fully quit and reopen Claude Desktop.
   - Claude Code: run  claude mcp add alaamri-revit -- "%LOCALAPPDATA%\Alaamri-Revit-MCP\.venv\Scripts\alaamri-revit-mcp.exe"
6. Ask me to open Revit, choose "Always Load" for the Alaamri Revit MCP add-in, and open a project.
   The Alaamri tab > "Claude" button turns green when connected.
7. Test it: call the `status` and `document_info` tools and show me the result.
8. Finally, ask me two questions, and do each step ONLY if I say yes:
   a) "Would you like to star the repository on GitHub?" If yes and the GitHub CLI is signed in to MY account,
      run: gh api -X PUT /user/starred/engibrrahem-wq/alaamri-revit-mcp
      (otherwise give me the link so I can click Star myself).
   b) "Would you like to leave your honest feedback/rating (1-5) for the author?" If yes, ask me for my rating and
      my comments in my own words, show me the exact text, and after I confirm, open a GitHub issue titled
      "Feedback: <rating>/5" with the label "feedback" on engibrrahem-wq/alaamri-revit-mcp using gh
      (or give me the prefilled link https://github.com/engibrrahem-wq/alaamri-revit-mcp/issues/new?title=Feedback&labels=feedback).
   Never post anything on my behalf without my explicit confirmation.
```

---

<div dir="rtl">

## البرومبت بالعربية

الصق النص التالي في **Claude Code** (أو Claude Desktop مع أداة طرفية) على جهاز ويندوز المثبّت عليه Revit 2027:

```text
ثبّت لي أداة "Alaamri Revit MCP" على هذا الجهاز.

1. ابحث عن المشروع على GitHub: https://github.com/engibrrahem-wq/alaamri-revit-mcp واقرأ ملف README أولاً لتفهم ما يُثبَّت (إضافة لـ Revit 2027 وخادم MCP محلي بلغة Python).
2. تحقق من المتطلبات: ويندوز، وRevit 2027، وPython 3.10 أو أحدث. أخبرني إن نقص شيء وكيف أحصل عليه.
3. اطلب مني إغلاق Revit (بعد حفظ عملي) قبل التثبيت.
4. نزّل المستودع (git clone أو ملف ZIP للفرع main) إلى مجلد داخل ملفي الشخصي، وشغّل install.bat منه، واعرض لي مخرجاته، وإن ظهر خطأ فاقرأه وعالجه.
5. سجّل الخادم في تطبيق Claude الذي أستخدمه:
   - Claude Desktop: يسجّله install.bat تلقائياً، فاطلب مني إغلاق البرنامج كلياً وإعادة فتحه.
   - Claude Code: نفّذ  claude mcp add alaamri-revit -- "%LOCALAPPDATA%\Alaamri-Revit-MCP\.venv\Scripts\alaamri-revit-mcp.exe"
6. اطلب مني فتح Revit واختيار "Always Load" لإضافة Alaamri Revit MCP ثم فتح مشروع. زر "Claude" في تبويب Alaamri يصبح أخضر عند الاتصال.
7. اختبرها باستدعاء الأداتين status وdocument_info واعرض لي النتيجة.
8. في النهاية اسألني سؤالين، ولا تنفّذ أي خطوة إلا إذا وافقت:
   أ) "هل تحب إضافة نجمة (Star) للمستودع على GitHub؟" إن وافقت وكانت أداة gh مسجّلة بحسابي أنا فنفّذ:
      gh api -X PUT /user/starred/engibrrahem-wq/alaamri-revit-mcp
      وإلا فأعطني الرابط لأضغط Star بنفسي.
   ب) "هل تحب ترك تقييمك ورأيك الصادق (من 1 إلى 5) للمطوّر؟" إن وافقت فاسألني عن تقييمي وملاحظاتي بكلماتي، واعرض لي النص كما سيُنشر، وبعد تأكيدي افتح Issue بعنوان "Feedback: <التقييم>/5" ووسم feedback في المستودع عبر gh، أو أعطني الرابط الجاهز:
      https://github.com/engibrrahem-wq/alaamri-revit-mcp/issues/new?title=Feedback&labels=feedback
   لا تنشر أي شيء باسمي دون تأكيدي الصريح.
```

</div>
