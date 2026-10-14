# دليل المستخدم — نظام إدارة العيادة
# User Guide — Clinic Management System

> هذا الدليل موجّه للأطباء والسكرتارية والمرضى. كل قسم مكتوب بالعربية والإنجليزية.
> This guide is for doctors, secretaries and patients. Every section is written in Arabic and English.

---

## 1. تسجيل الدخول | Signing in

**عربي**
1. افتح عنوان العيادة في المتصفح.
2. أدخل البريد الإلكتروني وكلمة المرور، ثم اضغط **تسجيل الدخول**.
3. إذا كان حسابك مفعّلًا عليه التحقق بخطوتين (MFA)، أدخل الرمز المكوّن من 6 أرقام من تطبيق المصادقة.
4. للتبديل بين العربية والإنجليزية اضغط زر اللغة في الأعلى، ولتبديل الوضع الليلي/النهاري اضغط زر **🌙/☀**.
5. لتسجيل الخروج، افتح قائمتك في الأعلى واختر **تسجيل الخروج**.

**English**
1. Open the clinic URL in a browser.
2. Enter your email and password, then click **Sign in**.
3. If two-factor authentication (MFA) is enabled on your account, enter the 6-digit code from your authenticator app.
4. Switch between Arabic and English with the language button, and toggle dark/light mode with the **🌙/☀** button.
5. To sign out, open your menu at the top and choose **Sign out**.

---

## 2. الطبيب | Doctor

**عربي**
- **لوحة المعلومات (`/`)**: تعرض إحصاءات مختصرة ومواعيد اليوم وروابط سريعة.
- **ساعات العمل (`/doctors/schedule`)**: حدّد أيام العمل وفترة كل يوم ومدة الموعد (بالدقائق). تُستخدم هذه الساعات لحساب المواعيد المتاحة للمرضى.
- **المواعيد (`/appointments`)**: عرض تقويم الأسبوع. افتح موعدًا لتأكيده، أو إنهائه (Done)، أو تسجيل عدم الحضور (No-show)، أو إلغائه. أنشئ موعدًا جديدًا من **موعد جديد**.
- **الزيارات (`/visits`)**: سجّل زيارة جديدة (الشكوى/الملاحظات/التشخيص) واربطها بالموعد. النصوص الطبية مشفّرة عند الحفظ.
- **الوصفات (`/prescriptions`)**: أصدر وصفة من داخل زيارة. يتحقق النظام تلقائيًا من الحساسية المسجّلة للمريض.
- **المرضى (`/patients`)**: ابحث، واعرض السجل الطبي والزيارات والوصفات السابقة، وأضف حساسية أو حالة مرضية.

**English**
- **Dashboard (`/`)**: shows quick statistics, today's appointments and shortcuts.
- **Working hours (`/doctors/schedule`)**: define working days, hours per day and slot length (minutes). These hours drive the slots patients can book.
- **Appointments (`/appointments`)**: week calendar. Open an appointment to confirm, complete (Done), mark No-show or cancel. Create one from **New appointment**.
- **Visits (`/visits`)**: record a visit (chief complaint / notes / diagnosis) and link it to an appointment. Clinical text is encrypted at rest.
- **Prescriptions (`/prescriptions`)**: issue a prescription from within a visit. The system automatically checks the patient's recorded allergies.
- **Patients (`/patients`)**: search, review medical history, past visits and prescriptions, and add allergies or conditions.

---

## 3. السكرتارية | Secretary

**عربي**
- **المرضى (`/patients`)**: إنشاء وتعديل بيانات المرضى، والبحث بالاسم أو رقم الملف.
- **المواعيد (`/appointments`)**: حجز المواعيد للعملاء، وتأكيدها أو إلغائها. اختر المريض والطبيب والفترة المتاحة.
- **الفواتير (`/invoices`)**: أنشئ فاتورة من زيارة (بنود متعددة)، ثم أصدرها. سجّل الدفعات النقدية أو بالبطاقة أو التحويل، وتابع الحالة (مسودة → صادرة → مدفوعة جزئيًا → مدفوعة → ملغاة).
- **التقارير (`/reports`)**: تقارير الإيرادات والمبالغ المستحقة والزيارات حسب الطبيب، مع تصدير CSV.

**English**
- **Patients (`/patients`)**: create and edit patients, search by name or MRN.
- **Appointments (`/appointments`)**: book, confirm or cancel appointments. Choose the patient, doctor and an available slot.
- **Invoices (`/invoices`)**: create an invoice from a visit (multiple line items), then issue it. Record cash/card/transfer payments and track status (Draft → Issued → Partially paid → Paid → Void).
- **Reports (`/reports`)**: revenue, outstanding balances and visits by doctor, with CSV export.

---

## 4. المريض | Patient

**عربي**
- **سجلي (`/me`)**: بياناتك، الحساسيات، والتاريخ المرضي (للقراءة فقط).
- **حجز موعد (`/appointments/new`)**: اختر الطبيب، ثم الفترة المتاحة، وأدخل سبب الزيارة، وأكّد الحجز.
- **مواعيدي (`/appointments/me`)**: كل مواعيدك القادمة والسابقة، مع إمكانية إلغاء الموعد القادم.
- **زياراتي (`/visits/me`)** و**وصفاتي (`/prescriptions/me`)**: اطّلع على تفاصيل الزيارات والوصفات، ويمكن طباعة الوصفة.
- **فواتيري (`/invoices/me`)**: حالة فواتيرك والمبالغ المستحقة، مع نسخة قابلة للطباعة.

**English**
- **My record (`/me`)**: your details, allergies and medical history (read-only).
- **Book an appointment (`/appointments/new`)**: pick a doctor, choose an available slot, add a reason and confirm.
- **My appointments (`/appointments/me`)**: upcoming and past appointments, with the option to cancel an upcoming one.
- **My visits (`/visits/me`)** and **My prescriptions (`/prescriptions/me`)**: view visit and prescription details; prescriptions are printable.
- **My invoices (`/invoices/me`)**: invoice status and outstanding amounts, with a printable copy.

---

## 5. تلميحات وأسئلة شائعة | Tips & troubleshooting

**عربي**
- **طباعة**: افتح الفاتورة أو الوصفة واضغط `Ctrl+P` — التنسيق مهيّأ للطباعة.
- **الوضع الليلي**: يُحفظ تلقائيًا في المتصفح ويمكن اختياره: فاتح / داكن / حسب النظام.
- **انتهت الجلسة**: يعيد النظام تسجيل الدخول تلقائيًا مرة واحدة؛ إذا ظهرت شاشة الدخول مجددًا فالجلسة انتهت لأمان الحساب.
- **كلمة المرور**: 12 حرفًا على الأقل مع حرف كبير وحرف صغير ورقم ورمز. خمس محاولات خاطئة تؤدي إلى قفل مؤقت لمدة 5 دقائق.
- **حسابات العرض (بيئة التطوير فقط)**: الطبيب `doctor@clinic.local`، السكرتيرة `secretary@clinic.local`، المريض `patient@clinic.local`.

**English**
- **Printing**: open an invoice or prescription and press `Ctrl+P` — print layouts are optimised.
- **Dark mode**: remembered in your browser; choose Light / Dark / System.
- **Session expired**: the app refreshes your session automatically once; if the login screen reappears, the session ended for security.
- **Passwords**: at least 12 characters with upper, lower, digit and symbol. Five failed attempts trigger a 5-minute lockout.
- **Demo logins (Development only)**: doctor `doctor@clinic.local`, secretary `secretary@clinic.local`, patient `patient@clinic.local`.

---

## 6. أسئلة للمشغّل | Operator notes

- راجع `README.md` لتشغيل النظام، والنشر الإنتاجي خلف HTTPS، والنسخ الاحتياطي.
- See `README.md` for running the system, production deployment behind HTTPS, and backups.
