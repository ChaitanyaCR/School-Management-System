# School Management System — Functional & Non-Functional Requirements

| | |
|---|---|
| **Status** | v1.0 — baselined for Release 1 |
| **Owner** | Chaitanya Raj |
| **Last updated** | 2026-10-29 |
| **Related** | [02-PROJECT-PLAN.md](02-PROJECT-PLAN.md) |

---

## 1. Vision

A production-grade, multi-tenant School Management System (SMS), offered as SaaS. Release 1 targets **school groups/chains** (one organisation with several branches) and must also work for a single standalone school (an organisation with one branch).

Release 1 (MVP) digitises the **academic core** of a school: structure, students, staff, timetable, attendance, exams and report cards.

### 1.1 Goals

- Replace registers, spreadsheets and hand-made report cards with one system of record.
- Give group management a consolidated view across branches.
- Be secure and compliant from the start, since the system holds children's personal data.
- Have an architecture that can grow into a multi-school SaaS without a rewrite.

### 1.2 Non-goals for Release 1

Fees and payments, parent/student logins, SMS/WhatsApp messaging, HR/payroll, library, transport, hostel, LMS, mobile apps. These are planned for later releases (see §4).

---

## 2. Organisational Model (Tenancy)

```text
Platform (SaaS operator — you)
├── Board profiles  (CBSE in R1; ICSE, State boards … added as data packs)
└── Organisation  (tenant — a school group, or a single school)
    ├── Shared academic setup  (one copy, used by every branch)
    │   ├── Board profile applied  (e.g. CBSE)
    │   ├── Academic Year  (e.g. 2026-27, Apr–Mar) → Terms
    │   ├── Academic calendar  (holidays, events)
    │   ├── Classes / Grades  (Nursery … XII) and Subjects
    │   └── Assessment schemes, grading scales, report-card templates
    └── Branch / Campus  (one physical school)
        └── Section  (class + year, e.g. "VII-A, 2026-27")
            ├── Teacher assignments, timetable
            └── Enrolment  (student ↔ section, with roll no.)
                └── Attendance, marks, results
```

- **Organisation** is the tenant and the boundary for data isolation. No data is ever visible across organisations.
- **Academic setup is shared**: the academic year, calendar, classes, subjects, assessment schemes and grading belong to the organisation and apply to all its branches.
- **Branch** is the operating unit. Sections, teachers, timetables, students, attendance and marks belong to a branch. Most users work inside one or more branches.
- **Board profile** packages a board's rules (classes, subjects, assessment patterns, grading, report-card formats) as configuration data. Supporting a new board means adding a profile, not changing core code (see §5.13).

---

## 3. Actors & Roles

| Role | Scope | Typical responsibilities (Release 1) |
|---|---|---|
| **Platform Super Admin** | Platform | Onboard/suspend organisations, platform settings, support access (audited) |
| **Organisation Admin** | Organisation | Create branches, org-wide settings, shared academic setup (year, calendar, classes, subjects, assessment schemes), cross-branch reports, manage branch admins |
| **Branch Admin / Principal** | Branch(es) | Sections, staff, teacher allocation, approve/lock attendance and results, branch reports |
| **Office Staff / Registrar** | Branch(es) | Admissions, student records, certificates, bulk imports |
| **Exam Coordinator** | Branch(es) | Date sheets, marks verification, report card generation (schemes are defined at org level) |
| **Class Teacher** | Section(s) | Daily attendance, co-scholastic grades, remarks, view class records |
| **Subject Teacher** | Subject × Section(s) | Marks entry for assigned subjects, period attendance |
| **Parent / Guardian** | Own children | *(Release 2 — portal)* stored as records only in R1 |
| **Student** | Self | *(Release 2 — portal)* |

- Roles are **permission bundles**. A user can hold different roles in different branches, for example Teacher in Branch A and Exam Coordinator in Branch B.
- Organisations can create **custom roles** from the permission catalogue (Should).

---

## 4. Module Catalogue & Release Mapping

| # | Module | R1 (MVP) | R2 | R3 | R4 (SaaS scale) |
|---|---|:-:|:-:|:-:|:-:|
| 1 | Platform & Tenancy | ✅ core | | | Self-serve signup, plans, billing, white-label |
| 2 | Identity & Access (IAM) | ✅ | OTP login, parent/student accounts | SSO | |
| 3 | Academic Structure | ✅ | | | |
| 4 | Student Information (SIS) | ✅ | | Admissions enquiry CRM | |
| 5 | Staff Directory | ✅ | | HR, payroll, staff attendance & leave | |
| 6 | Timetable | ✅ | | Auto-generator | |
| 7 | Attendance (students) | ✅ | Leave requests, absence alerts | Biometric/RFID integration | |
| 8 | Exams & Assessment | ✅ | Online tests | | |
| 9 | Report Cards | ✅ | Publish to portal | | |
| 10 | Dashboards & Reports | ✅ basic | | Advanced analytics | |
| 11 | Audit & Data Governance | ✅ | | | |
| 12 | Fees & Payments | | ✅ | | |
| 13 | Parent & Student Portal | | ✅ | Mobile app | |
| 14 | Communication | | ✅ Announcements, email/SMS/WhatsApp | | |
| 15 | Homework & Assignments | | ✅ | LMS | |
| 16 | Library, Transport, Inventory, Hostel | | | ✅ | |

The rest of this document gives detailed requirements for **R1 only**. R2+ modules get their own requirements when we reach them.

---

## 5. Functional Requirements — Release 1

Priority uses **MoSCoW**: **M** = Must, **S** = Should, **C** = Could. Every requirement has an ID, so code, tests and PRs can refer to it.

### 5.1 Platform & Tenancy (`PLT`)

| ID | Requirement | Pri |
|---|---|:-:|
| PLT-01 | Super Admin can create an organisation with name, unique code, subdomain, primary contact, status. | M |
| PLT-02 | Each organisation is reachable at its own subdomain (`<org>.<domain>`). The tenant is resolved from the subdomain on every request. | M |
| PLT-03 | Data of one organisation is never readable or writable by another organisation's users, enforced in both the application and the database. | M |
| PLT-04 | Super Admin can suspend or reactivate an organisation. Suspended tenants cannot log in. | M |
| PLT-05 | Org Admin can create, edit and deactivate branches (name, code, address, affiliation board & number, UDISE code, logo, contact). | M |
| PLT-06 | Organisation settings: logo, timezone (default IST), date format, admission-number format, default grading scheme. | M |
| PLT-07 | Branch settings can override org defaults where allowed. | S |
| PLT-08 | Per-tenant module toggles (feature flags) so modules can be enabled per organisation. | S |
| PLT-09 | Super Admin "support access" to a tenant needs a reason, is time-boxed, and is fully audited. | S |

### 5.2 Identity & Access (`IAM`)

| ID | Requirement | Pri |
|---|---|:-:|
| IAM-01 | Users log in with email (or mobile number) and password. | M |
| IAM-02 | Admins invite users by email. The invite link expires (72h) and the user sets their own password. | M |
| IAM-03 | Forgot-password flow sends a single-use, time-limited reset link. | M |
| IAM-04 | Password policy: min 10 chars, breached-password check, no forced periodic rotation (NIST 800-63B). | M |
| IAM-05 | Account lockout / throttling after repeated failed logins. | M |
| IAM-06 | Role-based access control: permissions checked on the server for every API call and scoped to organisation → branch → section/subject where relevant. | M |
| IAM-07 | A user can hold multiple role assignments across branches, and the UI provides a branch switcher. | M |
| IAM-08 | Built-in roles as in §3, seeded per organisation. | M |
| IAM-09 | Custom roles built from the permission catalogue. | S |
| IAM-10 | Mandatory MFA (TOTP) for Super Admin, optional and enforceable for Org/Branch Admins. | S |
| IAM-11 | Users can see and revoke their active sessions. Admins can force-logout or deactivate a user. | S |
| IAM-12 | Login, logout, failed login and permission changes are recorded in the audit log. | M |

### 5.3 Academic Structure (`ACD`)

| ID | Requirement | Pri |
|---|---|:-:|
| ACD-01 | Create academic years **at organisation level**, shared by all branches, with start/end dates. Exactly one is *current*. Past years become read-only after closure. | M |
| ACD-02 | Define terms within an academic year (e.g. Term 1, Term 2), shared by all branches. | M |
| ACD-03 | Maintain classes/grades (ordered: Nursery, LKG, UKG, I … XII) at organisation level. Each branch chooses which classes it offers. | M |
| ACD-04 | Branches create sections per class per academic year, with capacity and an assigned class teacher. | M |
| ACD-05 | Organisation-level subject catalogue: code, name, type (scholastic / co-scholastic), components (theory / practical / internal). | M |
| ACD-06 | Map subjects to classes at organisation level, including **optional/elective groups** (e.g. XI Science: choose Maths or Biology; choose one language). | M |
| ACD-07 | Branches assign subject teachers to subject × section. | M |
| ACD-08 | **Shared academic calendar** at organisation level: holidays, events, working days, applied to all branches. | M |
| ACD-09 | Streams for senior secondary (Science / Commerce / Humanities). | S |
| ACD-10 | Rooms / labs master per branch (for timetable). | C |
| ACD-11 | When creating a new year, copy the previous year's structure (org-level subject mappings, branch-level sections). | S |
| ACD-12 | Branch-local holidays (e.g. a state holiday or local closure) added on top of the shared calendar, and audited. | S |

### 5.4 Student Information System (`SIS`)

| ID | Requirement | Pri |
|---|---|:-:|
| SIS-01 | Register a student: name, DOB, gender, photo, blood group, category, religion (optional), mother tongue, nationality, address, previous school, medical notes. | M |
| SIS-02 | Auto-generate a unique admission number per branch using a configurable pattern (e.g. `{BRANCH}/{YYYY}/{SEQ:5}`). Manual override is allowed but must stay unique. | M |
| SIS-03 | Optional government identifiers: **APAAR ID**, **PEN** (UDISE+). Aadhaar is **not stored in R1** (see §6.2). | S |
| SIS-04 | Guardians: multiple per student (father, mother, legal guardian), with contact details and occupation. One is marked *primary contact*. | M |
| SIS-05 | Siblings are linked automatically through shared guardians and can also be linked manually. | S |
| SIS-06 | Enrol a student into academic year → class → section with a roll number. Roll numbers can be auto-assigned (alphabetical) or entered manually. | M |
| SIS-07 | Student lifecycle statuses: *Active, Promoted, Detained, Transferred-out, Withdrawn, Alumni*, with full history kept. | M |
| SIS-08 | Bulk import of students and guardians from CSV/Excel, with a downloadable template, row-level validation and an error report. Nothing is imported until all rows pass validation (or the user can choose "import valid rows"). | M |
| SIS-09 | Search and filter students (name, admission no., class, section, status, guardian phone), with export to Excel/CSV. Exports are permission-gated and audited. | M |
| SIS-10 | Student profile page: personal info, guardians, enrolment history, attendance summary, exam results. | M |
| SIS-11 | Document uploads (birth certificate, previous TC, photos), stored privately with type and size validation. | S |
| SIS-12 | **Year-end promotion wizard**: bulk promote a section to the next class, mark detained students, and reassign sections for the new year. | M |
| SIS-13 | Transfer a student between branches of the same organisation, carrying their history. | S |
| SIS-14 | Generate Transfer Certificate (TC) and Bonafide Certificate PDFs from templates, with serial numbers. | S |
| SIS-15 | Record guardian **consent** for processing the child's data (DPDP), with timestamp, version of the notice and the method of capture. | M |

### 5.5 Staff Directory (`STF`)

| ID | Requirement | Pri |
|---|---|:-:|
| STF-01 | Staff profile: employee ID, name, photo, designation, department, qualification, date of joining, contact, branch assignments. | M |
| STF-02 | A staff profile can be linked to a user account (login). Not all staff need logins. | M |
| STF-03 | Staff status: active, on leave, relieved. Relieved staff lose access automatically. | M |
| STF-04 | Bulk import staff from CSV/Excel. | S |
| STF-05 | Teacher workload view: subjects × sections × periods per week. | S |

### 5.6 Timetable (`TT`)

| ID | Requirement | Pri |
|---|---|:-:|
| TT-01 | Define bell schedules per branch (periods, breaks, start/end times). Different schedules can apply to different class groups (e.g. primary vs senior). | M |
| TT-02 | Build a weekly timetable per section: day × period → subject + teacher (+ room). | M |
| TT-03 | **Clash detection**: block or warn when a teacher (or room) is double-booked or a teacher exceeds their configured max periods per day. | M |
| TT-04 | Views: section timetable, teacher timetable, branch-wide grid. | M |
| TT-05 | Print/export timetable as PDF. | M |
| TT-06 | Substitution: mark a teacher absent for a day, see their periods, assign free teachers as substitutes. | S |
| TT-07 | Timetable versions with effective-from dates (mid-year changes keep history). | S |
| TT-08 | Automatic timetable generation. | *R3* |

### 5.7 Attendance (`ATT`)

| ID | Requirement | Pri |
|---|---|:-:|
| ATT-01 | **Period-wise attendance** is the primary mode: the teacher taking each timetable period marks attendance for that period. Classes can be set to **daily** mode instead (e.g. pre-primary), marked once a day by the class teacher. | M |
| ATT-02 | Statuses: Present, Absent, Late, Leave (excused). Day-level also has Half-day. | M |
| ATT-03 | Fast marking UI: students start as Present, or carry over the previous period's status on the same day, and the teacher marks only exceptions. Works well on a phone browser. | M |
| ATT-04 | Marking is blocked on holidays and non-working days, and for students not enrolled on that date. | M |
| ATT-05 | Attendance becomes **locked** after a configurable window (e.g. same day 23:59). Later edits need Branch Admin permission and a reason, and are audited. | M |
| ATT-06 | Reports: daily branch summary, monthly register per section (printable), student-wise attendance %, defaulters below a threshold (e.g. 75%). | M |
| ATT-07 | Dashboard tile: today's attendance %, with periods/sections not yet marked. | M |
| ATT-08 | Attendance totals feed report cards (working days, days present). | M |
| ATT-09 | Offline-tolerant marking (queue on weak network, sync later). | C |
| ATT-10 | **Day status is derived** from period attendance by a configurable rule (e.g. Present if ≥ 75% of periods attended, Half-day if 40–75%, else Absent). The derived day status is what registers and report cards use. | M |
| ATT-11 | Substitute teachers (TT-06) mark attendance for the periods they cover. | M |
| ATT-12 | **Attendance analytics & awards**: rank students by attendance % (period-level and day-level) over any date range or the full academic year, within a section, class, branch or the whole organisation. Includes a perfect-attendance list. Exportable. | M |
| ATT-13 | Award criteria are configurable: minimum %, whether *Late* or *Leave* counts against the student, and tie-break rules (e.g. fewer Lates wins). | S |
| ATT-14 | Subject-wise attendance per student (e.g. "missed 30% of Maths periods"). | S |
| ATT-15 | Attendance award certificates (PDF) for selected students. | C |

### 5.8 Exams & Assessment (`EXM`)

| ID | Requirement | Pri |
|---|---|:-:|
| EXM-01 | Configure an **assessment scheme** per class group and academic year **at organisation level**, shared by all branches: terms → assessments (e.g. Periodic Test 1, Half-Yearly, Annual), each with max marks and **weightage** in the final result. | M |
| EXM-02 | Schemes come pre-filled from the board profile (e.g. CBSE IX–X: PT 5 + Notebook 5 + Subject Enrichment 5 + Exam 80) and remain fully editable. | M |
| EXM-03 | Subject components per assessment (Theory / Practical / Internal) with separate max marks and pass marks. | M |
| EXM-04 | **Grading scales**: configurable marks→grade bands with grade points (e.g. CBSE A1 91–100 … E ≤32), assignable per class range. Separate scales for co-scholastic areas (A/B/C). | M |
| EXM-05 | Exam schedule (date sheet): subject, date, time, room. Printable. | M |
| EXM-06 | **Marks entry grid**: subject teachers enter marks only for their assigned subject × section. Spreadsheet-like keyboard navigation, validation against max marks, special codes (AB = absent, ML = medical, EX = exempt). | M |
| EXM-07 | Marks workflow: *Draft → Submitted (teacher) → Verified (coordinator) → Locked*. Unlocking needs permission and a reason, and is audited. | M |
| EXM-08 | Co-scholastic grades (Art, Health & PE, Work Education, Discipline) and class-teacher remarks per term. | M |
| EXM-09 | **Result computation**: subject totals, weighted term/final scores, percentage, grade, grade point, overall result (Pass / Fail / Compartment) by configurable rules (e.g. min 33% per subject). | M |
| EXM-10 | Rank/position in section and class. Can be disabled. | S |
| EXM-11 | Grace marks / moderation rules applied by the coordinator, recorded separately from raw marks. | C |
| EXM-12 | Result analysis: subject-wise average/highest/lowest, pass %, grade distribution, toppers, teacher-wise comparison, branch comparison (org level). | M |
| EXM-13 | Bulk marks import from Excel template (for teachers who prefer offline entry). | S |
| EXM-14 | Admit card generation. | C |

### 5.9 Report Cards (`RC`)

| ID | Requirement | Pri |
|---|---|:-:|
| RC-01 | Report card **templates** per class group, defined at organisation level: header (logo, school name, affiliation no.), student details, scholastic table, co-scholastic table, attendance, remarks, result, signatures. Branch details (name, address, affiliation no.) are filled in automatically. | M |
| RC-02 | Generate a report card PDF for one student, or in **bulk** for a section/class (async, downloadable as ZIP or one merged PDF for printing). | M |
| RC-03 | Report cards can only be generated from **locked** results. Each generation is versioned and stored. | M |
| RC-04 | Term report and final (annual) report formats. | M |
| RC-05 | Template designer with drag/drop. | *Later* — R1 ships a set of well-designed, configurable templates. |

### 5.10 Dashboards & Reports (`DSH`)

| ID | Requirement | Pri |
|---|---|:-:|
| DSH-01 | Branch dashboard: student strength by class/section/gender, today's attendance, pending marks entry, upcoming exams/holidays. | M |
| DSH-02 | Organisation dashboard: the same metrics compared across branches. | M |
| DSH-03 | Teacher home: my timetable today, attendance to mark, marks entry pending. | M |
| DSH-04 | All list/report screens can be exported to Excel/CSV/PDF, subject to permission. | M |

### 5.11 Audit & Data Governance (`AUD`)

| ID | Requirement | Pri |
|---|---|:-:|
| AUD-01 | Audit log for create/update/delete on student, guardian, staff, enrolment, attendance, marks, results and settings. Records actor, timestamp, IP, entity, before/after values. | M |
| AUD-02 | Audit log is append-only and searchable by admins with permission. | M |
| AUD-03 | Records are soft-deleted (restorable), and hard-deleted only through the data-erasure process. | M |
| AUD-04 | Data principal requests (DPDP): export a student's data, correct it, and erase it subject to legal-retention rules. | S |
| AUD-05 | Tenant-level full data export (for offboarding / portability). | S |

### 5.12 Notifications (`NTF`) — R1 minimum

| ID | Requirement | Pri |
|---|---|:-:|
| NTF-01 | Transactional emails: user invite, password reset, bulk-job completion (imports, report cards). | M |
| NTF-02 | In-app notification centre. | S |

### 5.13 Board Support (`BRD`)

| ID | Requirement | Pri |
|---|---|:-:|
| BRD-01 | Boards are a configurable reference list (CBSE, CISCE, State boards, IB, Cambridge…), not hard-coded. Each branch records its board and affiliation number. | M |
| BRD-02 | A **board profile** is a versioned data pack: standard class list, subject catalogue, assessment-scheme presets, grading scales, pass/result rules, report-card and certificate templates. Adding a board means adding a profile, with no change to core code. | M |
| BRD-03 | R1 ships the **CBSE profile**: classes Nursery–XII, CBSE subjects, CBSE IX–X and XI–XII assessment patterns, CBSE grading scales (A1–E, co-scholastic A–C), CBSE-style report cards. | M |
| BRD-04 | Applying a profile to an organisation creates editable defaults. Later profile updates never silently overwrite the organisation's customisations. | M |
| BRD-05 | Result rules that can't be expressed as configuration (e.g. a board's compartment logic) are implemented as **pluggable rule strategies** registered per board and covered by tests. | S |
| BRD-06 | Additional profiles (CISCE, state boards) are added in R2+ as customers need them. | *R2+* |

---

## 6. Non-Functional Requirements

### 6.1 Security

| ID | Requirement |
|---|---|
| NFR-SEC-01 | Follow **OWASP ASVS Level 2** as the baseline security standard. |
| NFR-SEC-02 | TLS 1.2+ everywhere, with HSTS. |
| NFR-SEC-03 | Passwords hashed with Argon2id. |
| NFR-SEC-04 | **Tenant isolation in depth**: app-level tenant scoping **plus** PostgreSQL Row-Level Security, with automated tests proving cross-tenant access fails. |
| NFR-SEC-05 | Sensitive personal fields (e.g. medical notes, government IDs) encrypted at field level. Disk/DB encrypted at rest. |
| NFR-SEC-06 | Secrets live only in a secret manager or environment, never in the repo. Secret scanning runs in CI. |
| NFR-SEC-07 | Rate limiting on auth and expensive endpoints. CSRF protection and secure, HttpOnly, SameSite cookies. |
| NFR-SEC-08 | Uploaded files: type/size validation, private storage, and short-lived signed URLs for access. |
| NFR-SEC-09 | Dependency and container vulnerability scanning in CI. A security review before each release. |

### 6.2 Privacy & Regulatory (India)

| ID | Requirement |
|---|---|
| NFR-PRV-01 | **DPDP Act 2023**: students are children (<18), so **verifiable parental consent** is required to process their data (see SIS-15). Collect only data needed for the stated purpose. No tracking, behavioural monitoring or targeted advertising directed at children. |
| NFR-PRV-02 | Privacy notice per organisation (the school is the *Data Fiduciary*; we are the *Data Processor*). The SaaS contract includes a Data Processing Agreement. |
| NFR-PRV-03 | Personal-data breach notification process (to the Data Protection Board and affected principals). **CERT-In**: report cyber incidents within 6 hours and retain ICT logs for 180 days. |
| NFR-PRV-04 | Host data (database, files, backups) in an India region on whichever provider is chosen. This is expected by schools and simplifies compliance. |
| NFR-PRV-05 | **Do not store Aadhaar numbers** in R1. If needed later, store only masked or last-4 digits, per UIDAI rules. |
| NFR-PRV-06 | Defined retention periods per data category, with automated purge/anonymisation after expiry. |
| NFR-PRV-07 | *(R2, Fees)* Use an RBI-authorised payment aggregator. Never store card data (PCI DSS scope stays with the PA). |

### 6.3 Performance & Scale

| ID | Requirement |
|---|---|
| NFR-PRF-01 | API p95 latency < 300 ms for standard reads/writes at design load. |
| NFR-PRF-02 | Marks grid (60 students × 10 columns) loads and saves in < 2 s. |
| NFR-PRF-03 | Bulk report cards: 1,000 PDFs generated asynchronously in < 10 min, with progress feedback. |
| NFR-PRF-04 | Design load for R1: 1 org, 10 branches, 15,000 students, 1,000 staff. Architecture headroom: 500 orgs / 1M students without redesign. |
| NFR-PRF-05 | Peak concurrency: ~1,000 teachers marking attendance within the first 5 minutes of each period. |
| NFR-PRF-06 | Period attendance volume at design load: ~15,000 students × 8 periods × 220 days ≈ **26M entries per year**. Year-long attendance rankings (ATT-12) return in < 3 s for a whole organisation. |

### 6.4 Reliability & Operations

| ID | Requirement |
|---|---|
| NFR-OPS-01 | Availability target 99.5% (R1), 99.9% (SaaS GA). |
| NFR-OPS-02 | Automated daily backups plus point-in-time recovery. **RPO ≤ 15 min, RTO ≤ 4 h**. Restore is tested quarterly. |
| NFR-OPS-03 | Separate environments: local, staging and production. Infrastructure is defined as code. |
| NFR-OPS-04 | Observability: structured logs with request & tenant IDs, metrics, error tracking, uptime alerts. |
| NFR-OPS-05 | Zero-downtime deployments and backward-compatible DB migrations. |
| NFR-OPS-06 | **Hosting portability**: the system runs on any Linux host with Docker. It depends only on open standards and open-source containers (PostgreSQL, S3-compatible object storage, SMTP, optional Redis), with no proprietary cloud service in the core path. The same container images deploy to a single VPS (e.g. Hostinger VPS) or to managed cloud (AWS, GCP, Azure). |
| NFR-OPS-07 | Product name, base domain and branding are configuration values, not hard-coded. |

### 6.5 Usability & Accessibility

| ID | Requirement |
|---|---|
| NFR-UX-01 | Responsive web app. Teacher workflows (attendance, marks) are fully usable on a low-end Android phone browser. |
| NFR-UX-02 | WCAG 2.1 AA. |
| NFR-UX-03 | English UI in R1. All strings externalised (i18n-ready for Hindi and regional languages). |
| NFR-UX-04 | Indian conventions: DD/MM/YYYY, IST, Indian numbering where relevant, Apr–Mar academic year default. |

### 6.6 Maintainability & Quality

| ID | Requirement |
|---|---|
| NFR-MNT-01 | Modular monolith with clear module boundaries (see plan). |
| NFR-MNT-02 | Automated tests: ≥ 80% coverage on domain/business logic. Tenant-isolation and permission tests for every endpoint. E2E tests for critical flows. |
| NFR-MNT-03 | CI must pass (lint, type-check, tests, security scans) before merge to `main`. |
| NFR-MNT-04 | API documented via OpenAPI. Architecture decisions recorded as ADRs. |

---

## 7. Key Business Rules (R1)

1. A student has **at most one active enrolment** per academic year.
2. Attendance and marks can only be recorded for students **enrolled** in that section on that date.
3. A teacher can enter marks only for **subject × section pairs assigned to them**. Class teachers additionally own co-scholastic grades and remarks.
4. Results must be **Locked** before report cards are generated. Any change after locking requires an unlock with a reason, which is audited.
5. Closed academic years are **read-only**.
6. Admission numbers are **unique within a branch** and never reused.
7. Deleting a student with attendance/marks history is not allowed. Use status changes instead.

---

## 8. Glossary

| Term | Meaning |
|---|---|
| **Tenant / Organisation** | A customer of the SaaS: a school group or a single school |
| **Branch** | One physical school/campus within an organisation |
| **Enrolment** | A student's placement in a section for one academic year |
| **Scholastic / Co-scholastic** | Academic subjects vs. activities like art, sports, discipline |
| **Assessment scheme** | The structure of exams in a year and how they combine into a final result |
| **APAAR ID / PEN** | Government student identifiers under the National Education Policy / UDISE+ |
| **TC** | Transfer Certificate, issued when a student leaves a school |
| **UDISE code** | Government-issued unique school code |

---

## 9. Assumptions & Decisions

### Assumptions

- Parents and students do not log in during R1. Report cards are printed or shared as PDFs by the school.
- One currency (INR) and one country (India) until SaaS expansion.
- All branches of one organisation follow the same board. The data model still allows a branch-level override later, for groups with mixed boards.

### Decisions (resolved 2026-10-29)

| # | Question | Decision | Reflected in |
|---|---|---|---|
| 1 | Boards at launch | CBSE first. Other boards supported through board profiles. | §2, §5.13, EXM-02 |
| 2 | Shared vs per-branch setup | Branches share one academic calendar and the same assessment schemes. | §2, ACD-01…08, EXM-01, RC-01 |
| 3 | Attendance mode | Period-wise is required (for analytics and attendance awards). Day status is derived from periods. | ATT-01, ATT-10…15, NFR-PRF-06 |
| 4 | Product name & domain | Deferred until before launch. A working name is used meanwhile, and name/domain are configuration values. | NFR-OPS-07, Plan §2.5 |
| 5 | Hosting | Provider-agnostic: Docker-based, deployable to a VPS (e.g. Hostinger) or AWS/GCP/Azure. Start small and scale up. | NFR-OPS-06, Plan §2.5 |
| 6 | Pilot school | A school is available. They will commit to testing after a demo, so a demo milestone is planned early. | Plan §4 (Demo milestone) |
| 7 | Branch-local holidays | Confirmed: branches can add local holidays on top of the shared calendar. | ACD-12 |
| 8 | Tech stack | .NET 10 (ASP.NET Core) backend, React + TypeScript frontend, PostgreSQL. | Plan §1, ADR-001 |

### Still open

- Product name and domain: decide before Phase 8.
