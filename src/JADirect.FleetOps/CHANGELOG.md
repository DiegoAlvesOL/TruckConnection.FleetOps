# Changelog — JADirect FleetOps

All notable changes to this project are documented in this file.
---

## [3.3.1] - 2026-07-05

### Added
- AvailabilityPeriodStatus enum (Active, Expired, Canceled) tracking the
  lifecycle of an availability period. Replaces hard deletion so historical
  periods are preserved for audit and future reporting (Card 8).
- status column added to driver_availability_periods (migration required
  before deploy, see Migration Notes below).
- AvailabilityRepository: GetAllActiveByDriver(), GetById(),
  GetOverlappingActivePeriod() and UpdateDates() supporting the new manager
  UI for viewing, editing and canceling availability periods.
- AvailabilityService: GetAllActiveByDriver(), UpdateLeave() (validates
  date ordering and overlap before persisting an edit), CancelLeave()
  (marks a period as canceled and reactivates the driver immediately if
  the period was currently in effect), and DetermineEffectiveStatus()
  (decides whether a requested status change takes effect immediately or
  should wait for the scheduled start date).
- UsersController: EditLeave() and CancelLeave() AJAX endpoints consumed by
  the new modals on the Manage view. Manage() now always loads
  ActivePeriods for the driver being viewed, including future scheduled
  periods, not only when the driver is currently On Leave or Sick.
- UserManagementViewModel.ActivePeriods, listing a driver's active
  (ongoing or scheduled) availability periods for the Manage view.
- Users/Manage.cshtml: new "Active Availability Periods" table below the
  existing two-column layout, listing ongoing and scheduled periods with
  Edit Dates and Cancel Leave actions. Both actions use Alpine.js modals
  and AJAX POST requests, without a full page reload.
- Alpine.js and x-cloak styling added to _Layout.cshtml, required by the
  new modals.

### Fixed
- UsersController.UpdateStaff() was applying the requested status
  (OnLeave/Sick) to the driver immediately upon saving, regardless of
  whether the leave's start date was in the future. A driver scheduled
  for leave starting tomorrow was incorrectly marked as On Leave today.
  UpdateStaff() now calls AvailabilityService.DetermineEffectiveStatus()
  to decide the status to persist: if the start date is in the future,
  the driver's current status is preserved and the change is left to the
  existing daily background job (AvailabilityAutoReactivationJob /
  ProcessStartingAvailabilityPeriods), which already had this
  responsibility but was never being reached due to this bug.

### Migration Notes
- Requires running the Card 7.1 migration script (adds the status column
  to driver_availability_periods) in production BEFORE deploying this
  version. Deploying the code first will break any query filtering by
  status = 'active'.

---

## [3.3.0] - 2026-07-03

### Added
- Driver Availability System for automated driver status management based on
  scheduled unavailability periods (vacation, medical leave, sick days). Core
  feature for fleet compliance and audit trails.
- AvailabilityPeriod entity and driver_availability_periods table with columns:
  tenant_id, driver_id, status_during_period (OnLeave=4, Sick=5),
  availability_from_date, availability_to_date, reason, auto_reactivate.
  Supports multi-tenant isolation and historical audit of all periods.
- AvailabilityRepository with full CRUD and specialized queries:
  GetActiveByDriver() for point-in-time availability checks,
  GetAllExpiredToday() for auto-reactivation triggers,
  GetAllStartingToday() for auto-deactivation triggers.
- AvailabilityService encapsulating business logic for availability periods:
  MarkUnavailable() validates dates and creates period with defense-in-depth
  (date ordering, past date prevention, status validation).
  RemoveUnavailability() for immediate reactivation.
  IsAvailableForAlerts() filters drivers from WhatsApp compliance alerts.
  ProcessExpiredAvailabilityPeriods() reactivates drivers automatically.
  ProcessStartingAvailabilityPeriods() marks drivers unavailable when period
  begins. All user-facing error messages in English; internal logging in
  Portuguese.
- AvailabilityAutoReactivationJob as BackgroundService executing daily at
  00:01 UTC. Processes all active tenants independently with resilient error
  handling (partial failure doesn't stop job). CalculateTimeUntilMidnight()
  computes next execution window asynchronously. ProcessAvailabilityPeriods()
  orchestrates starting and expiring periods. Comprehensive structured logging
  for auditability and debugging.
- Users/Manage.cshtml with two-column responsive layout:
  LEFT: Personal Information (FirstName, Surname, Email, PhoneNumber, Role).
  RIGHT: Availability Status (current status dropdown + unavailable period
  entry with From/To dates and optional Reason). Green "Currently Active"
  panel when no period is set. Security Actions section with Suspend/Restore
  and Reset Password buttons. Tailwind CSS styling with brand teal (#008080).
  JavaScript toggleAvailabilityPanel() to show/hide dates based on status
  selection.
- UserManagementViewModel extended with availability fields:
  AvailabilityPeriodId, AvailabilityFromDate, AvailabilityToDate,
  AvailabilityReason. Computed properties: IsOnLeaveOrSick (for UI logic),
  IsActive (security action routing), DisplayName, StatusLabel, StatusClasses
  (for badge styling). Supports two-column Manage view.
- UserRepository.UpdateStatus(int userId, UserStatus status) for programmatic
  status changes. Used by AvailabilityAutoReactivationJob to activate/deactivate
  drivers based on period schedule. Accepts any valid UserStatus (Active,
  OnLeave, Sick, Suspended, Canceled).
- UsersController.Manage() and UpdateStaff() endpoints for staff availability
  management. Manage() loads driver availability period if on leave/sick.
  UpdateStaff() validates availability dates (required when status is
  OnLeave/Sick, start date cannot be past), creates/updates period via
  AvailabilityService.MarkUnavailable().
- Integration with WhatsApp compliance alerts: AlertRepository and
  DailyLogComplianceService now filter by availability. Drivers in active
  unavailability periods are excluded from daily log compliance alerts.
  WhatsAppNotificationJob passes tenantId to ensure multi-tenant isolation.

### Changed
- AlertRepository.GetDriversPendingDailyLog() signature expanded to accept
  tenantId parameter. Enables multi-tenant availability filtering.
- DailyLogComplianceService.GetPendingDrivers() now passes tenantId when
  querying AlertRepository. Drivers on leave are no longer included in
  WhatsApp alert payloads.
- WhatsAppNotificationJob passes TenantId to DailyLogComplianceService calls
  for consistent multi-tenant isolation.
- ManagerController passes TenantId when invoking compliance service methods.
  Prepares for future TenantId extraction from authenticated user context.
- UserRepository.UpdateUserAsManager() now includes phone_number and status_id
  in UPDATE statement. Previously only updated name, email, and role.
  status_id is critical for availability status changes from Manage view.
  phone_number required for WhatsApp messaging routing.
- Program.cs registers new services: AddScoped<AvailabilityRepository>,
  AddScoped<AvailabilityService>,
  AddHostedService<AvailabilityAutoReactivationJob>. Order preserved per
  layering conventions.

### Removed
- Frontend validation of availability dates removed from Manage.cshtml
  JavaScript. All validation consolidated in backend (AvailabilityService)
  for single source of truth. Reasons: consistency across API calls, security
  (backend is authoritative), simplification.

### Technical Debt
- DailyLogRepository.FillComplianceExceptions() still uses CURDATE() hardcoded.
  Needs signature change to FillComplianceExceptions(report, DateTime? filterDate)
  to support retrospective compliance queries. Scheduled for Card 8.
- ManagerController.Index() does not pass filterDate to FillComplianceExceptions().
  When Card 8 implements dashboard date picker, controller must be updated to
  pass user-selected date instead of implicitly using today.
- Dashboard view (Manager/Index.cshtml) has no date filter input for
  retrospective compliance queries. Card 8 will add date picker and wire to
  ManagerController. Currently dashboard only shows today's pending drivers.
- TenantId = 1 still hardcoded in ManagerController. To be replaced by
  tenant_id from User claim when users table is extended for second client
  onboarding (planned for v3.4).
- Timezone awareness: AvailabilityAutoReactivationJob calculates meia-noite
  using DateTime.UtcNow (UTC). For multi-timezone deployments, should use
  tenant.Timezone to calculate meia-noite local time. Deferred to v3.4.
- Holiday support: driver_availability_periods has no integration with public
  holidays. Drivers on leave during a holiday still show as unavailable. No
  separate holiday alert rules. Public holidays table and integration planned
  for v3.4.


---

## [3.2.0] - 2026-05-25

### Added
- WhatsApp Business Cloud API integration for daily log compliance alerts.
  Direct Meta API integration without third-party intermediaries. Cost: €0.04
  per template message. Webhook verified and secured with HMAC-SHA256 signature
  validation using AppSecret.
- Three new database tables: tenants (multi-tenant anchor with timezone, alert
  hours and manager phone), whatsapp_message_logs (full audit trail of every
  send attempt with status, meta_message_id and error details),
  whatsapp_sessions (24h free-tier session tracking, placeholder for future
  implementation).
- Two Meta-approved message templates: driver_daily_log_reminder (individual
  driver alert with system link) and manager_compliance_summary (daily pending
  drivers list sent to the fleet manager).
- WhatsAppNotificationJob as a BackgroundService. Calculates the next alert
  time across all active tenants using their configured timezone
  (Europe/Dublin), sleeps until one minute before, and fires within a 3-minute
  window. Skips weekends. Prevents duplicate sends per day using in-memory
  dictionaries keyed by tenant.
- DailyLogComplianceService responsible solely for determining which drivers
  have not submitted a daily log. Populates HasActiveSession per driver for
  free-tier message routing logic.
- WhatsAppAlertService responsible solely for sending all WhatsApp messages.
  Routes to free-form text when a 24h session is active or to a paid template
  otherwise. Logs every send attempt regardless of success or failure.
- WhatsAppWebhookController at route /webhook/whatsapp. GET verifies the Meta
  hub challenge. POST validates HMAC-SHA256 signature and processes payload in
  background via IServiceScopeFactory. Decorated with [AllowAnonymous].
- TriggerComplianceAlerts endpoint in ManagerController for manual dispatch of
  alerts to all pending van drivers. Bypasses weekend rules as the manager is
  making a conscious decision.
- RemindDriver POST endpoint in ManagerController for individual driver alerts
  triggered from the dashboard. Receives driverId, resolves from pending list
  and dispatches a single message.
- REMIND button per driver in the Pending Daily Logs card on the manager
  dashboard. Opens a confirmation modal displaying driver name and phone number
  before sending. Modal implemented with Bootstrap and vanilla fetch POST.

### Changed
- AlertRepository.GetDriversPendingDailyLog rewritten to query all active
  VanDrivers (role_id = 2, status_id = 1) without requiring a driver_assignments
  record. Drivers who never logged in are now included in the alert list.
- DailyLogRepository.FillComplianceExceptions corrected to filter role_id = 2
  only (previously included TruckDrivers via role_id IN (2, 3)). Now exposes
  UserId and PhoneNumber to support the REMIND button.
- ComplianceExceptionViewModel extended with UserId and PhoneNumber fields
  required by the individual REMIND flow.
- Pending Daily Logs card on the manager dashboard now displays only active
  VanDrivers (role_id = 2, status_id = 1), consistent with the alert rules.
  Previously included TruckDrivers and used a different data source.

### Removed
- Remind all pending drivers placeholder button removed from the manager
  dashboard pending full implementation in a future release.

### Technical Debt
- WhatsAppWebhookService.ProcessAsync is a placeholder. WhatsApp session
  tracking (whatsapp_sessions table) is not yet populated. Free-tier routing
  via HasActiveSession will always return false until implemented.
- TenantId = 1 hardcoded in ManagerController and WalkaroundController.
  To be replaced by tenant_id from the authenticated user when the column
  is added to the users table for the second client onboarding.
- New alert rule pending implementation: active VanDrivers who have not
  created a driver_assignment by a configurable hour should receive a separate
  alert. Rule must respect weekday filter and future holiday/sick status.
- Individual RemindDriver button does not send a manager summary. To be
  evaluated whether a summary should follow manual individual sends.

---

## [3.1.0] - 2026-05-13

### Added
- Walkaround check printable PDF generated server-side via QuestPDF. Document
  includes cover page with vehicle and driver metadata, inspection summary
  counters, full checklist table with photo thumbnails in a dedicated PIC. column,
  legal notice, and a numbered annex page per flagged item photo. Accessible by
  managers via the Audit History screen. QuestPDF Community License declared in
  Program.cs before builder.Build().
- WalkaroundDocument view created at Walkaround/WalkaroundDocument with dedicated
  _DocumentLayout.cshtml layout. Displays the walkaround check as a clean digital
  document without system navigation, designed for presentation to road authorities.
  Accessible by managers via the Audit History screen. Photos served via the
  existing Photo endpoint.
- Unique reference number added to every walkaround PDF and WalkaroundDocument.
  Format: WLK-{year}-{id:D6} (example: WLK-2026-000181). Derived from the
  walkaround check primary key and check date. Appears in the document header,
  photo annex headers and page footer to prevent documents from being considered
  reused or duplicated.
- Photo annex pages added to the walkaround PDF. Each item with a photo receives
  a dedicated A4 page labelled ANNEX A{n} of {total} with the item name, category,
  status, observations, full-resolution photo and driver signature line.
- WalkaroundPdfService created in JADirect.Application/Services implementing
  IWalkaroundPdfService. Downloads all walkaround photos from Railway Bucket in
  parallel using Task.WhenAll and GetPhotoStreamAsync, reducing total download
  time from the sum of individual latencies to approximately the latency of the
  slowest single download. Individual photo failures are captured and logged
  without aborting PDF generation.
- GetPhotoStreamAsync added to PhotoService. Async counterpart to the existing
  GetPhotoStream, required for non-blocking parallel S3 downloads. Existing
  synchronous method preserved for WalkaroundController.Photo endpoint.
- WalkaroundDetailViewModel added to JADirect.Domain/Models. Carries complete
  vehicle data (manufacturer, model, vehicle_type_id as readable label) needed
  for document generation. Separated from WalkaroundHistoryViewModel to preserve
  each model's contract without coupling.
- GetWalkaroundById added to InspectionRepository. Returns WalkaroundDetailViewModel
  with a JOIN across walkaround_checks, users and vehicles. Filters status =
  'Completed' to prevent draft records from generating official documents.
- PDF download and WalkaroundDocument view buttons added to Audit History per
  inspection card. Accessible to managers only. event.stopPropagation() prevents
  card toggle on button click. WalkaroundDocument opens in new tab.
- _DocumentLayout.cshtml created in Views/Shared. Minimal layout for document
  presentation views: no navigation, no system links. Provides identity bar with
  JADirect logo and uses local Bootstrap and site.css assets from wwwroot.
- Favicon link added to _Layout.cshtml referencing existing wwwroot/favicon.ico.

### Fixed
- Vehicle registration search failed when input contained dashes or leading and
  trailing spaces. Typing 231D12345 did not find a vehicle stored as 231-D-12345
  and vice versa. Fix: AssignmentController normalises input by trimming spaces,
  removing dashes and converting to uppercase before calling the repository.
  VehicleRepository.GetByRegistrationNo updated to use REPLACE(registration_no,
  '-', '') in the WHERE clause, making the search format-independent in both
  development and production environments regardless of how the plate was
  originally registered.
- Unused printer button and Actions column removed from Manage Vehicle walkaround
  history table. The button had no action bound and the functionality is covered
  by the PDF and View buttons in Audit History.

---

## [3.0.2] - 2026-05-04

### Fixed
- Clicking the JA Direct logo redirected authenticated drivers to the
  login page instead of the driver dashboard. Root cause: the layout
  evaluated User.IsInRole("Driver") before checking IsAuthenticated
  only, and the role claim value did not match the string "Driver",
  causing all driver sessions to fall through to the unauthenticated
  else branch. Fix: logo link now checks Manager role first, then falls
  back to Driver dashboard for any other authenticated user, eliminating
  the dependency on the exact role string.
- Clicking the JA Direct logo redirected to the legacy Home/Index view
  (Operating Vehicle screen) regardless of user role. Fix: HomeController
  Index action simplified to pure redirect logic — Manager goes to
  Manager/Index, Driver goes to Driver/SelectVehicle, unauthenticated
  users go to Account/Login. Legacy view removed.
- Legacy Home/Index.cshtml (Operating Vehicle screen) and
  Home/Privacy.cshtml removed. Both views are unreachable in the current
  navigation flow and their presence created risk of accidental access
  via direct URL.

---

## [3.0.1] - 2026-05-04

### Fixed
- Walkaround odometer always saved as 0 and GPS coordinates always null
  in Audit History after completing an inspection. Root cause: odometer,
  latitude and longitude were received by WalkaroundController POST but
  never forwarded to WalkaroundService.FinalizeDraft nor to
  InspectionRepository.CompleteDraft. The draft was created with zeros
  and nulls and the CompleteDraft UPDATE statement did not include those
  columns, so the initial values were never overwritten.
  Fix: three parameters added to FinalizeDraft and CompleteDraft
  signatures and included in the UPDATE that promotes Draft to Completed.

---

## [3.0.0] - 2026-05-04

### Added
- Driver Assignment system (Fase 1): drivers must assume a vehicle each day
  before starting any operational activity. One driver per vehicle per day
  enforced at database level via UNIQUE constraints on driver_assignments table.
- DriverAssignment entity created in JADirect.Domain/Entities with DateOnly
  for assignment_date, reflecting that no time component is associated with
  a journey day.
- AssignmentRepository created in JADirect.Data/Repositories with
  CreateAssignment, GetTodayAssignmentByDriver, ExistsActiveAssignmentForVehicle
  and DeleteTodayAssignment. Uses typed AddParameter pattern throughout.
- AssignmentService created in JADirect.Application/Services with
  AssignVehicleToDriver, UnassignVehicle and GetTodayJourneyState.
  Business rules: one driver per day, one vehicle per day. Vehicle return
  permitted at any time regardless of walkaround status.
- AssignmentController created in JADirect.Web/Controllers with GET/POST
  Assume, POST Unassign and GET State endpoints. VehicleId always resolved
  from active assignment, never from user input.
- JourneyStep enum created in JADirect.Domain/Enums with three values:
  NeedsVehicle, NeedsWalkaround and Ready.
- DriverDashboardModel extended with ActiveAssignment, HasWalkaroundToday,
  JourneyStep and VehicleCompliance properties without removing existing
  AvailableVehicles and RecentActivities.
- Driver dashboard redesigned with 3-step journey stepper and contextual
  hero card. Card color follows fleet compliance semaphore: amber for
  NeedsVehicle, red/amber/green for NeedsWalkaround based on FleetService,
  teal for Ready. Vehicle name and registration displayed in Ready and
  NeedsWalkaround cards.
- Assume Vehicle view created at Assignment/Assume.cshtml with registration
  plate input and QR code scanner via jsQR library loaded locally.
  capture="environment" attribute opens rear camera directly on mobile.
- QR code scanner implemented in wwwroot/js/scanner.js using Canvas API
  frame analysis. Auto-fills registration field on detection. Graceful
  fallback when getUserMedia is unavailable.
- Daily log confirmation flow (Fase 3): Create POST redirects to Review
  view before saving. TempData transports DailyLog between actions.
  Confirm POST performs the actual database write.
- Frontend outlier detection on Review.cshtml: returns above 15 trigger
  amber warning banner. Non-blocking, driver can confirm anyway.
- Backend audit logging in DailyLogService: LogWarning emitted when
  confirmedByDriver is true and returns exceed threshold of 15.
  AssignmentId included in log context for audit traceability.
- Walkaround draft flow (Fase 4): GET /Walkaround/Create now creates a
  Draft record in walkaround_checks before rendering the form. Draft id
  stored in session. POST finalizes the draft via CompleteDraft.
- WalkaroundCheckStatus enum created in JADirect.Domain/Enums with
  Draft and Completed values.
- CreateDraft and CompleteDraft methods added to InspectionRepository.
  CreateDraft returns generated id. CompleteDraft updates status,
  checklist_json and has_defect in a single transaction that also updates
  vehicle last_walkaround_at. Only updates records where status=Draft to
  prevent double-submit.
- StartDraft and FinalizeDraft methods added to WalkaroundService.
  StartDraft populates assignment_id from active assignment automatically.
  SubmitInspection preserved for backward compatibility.
- Camera capture, Canvas compression and incremental photo upload added to
  walkaround checklist. handlePhotoCapture compresses to max 800px width
  at JPEG quality 0.75 before upload. Upload via fetch without blocking
  form submission. Inline preview shown after successful upload.
- Take Photo button added to _CheckItem.cshtml appearing only when item
  state is Attention or Defect. capture="environment" targets rear camera.
- assignment_id INT NULL column added to walkaround_checks and daily_logs
  via migration_assignment_fk_v3.sql. FK references driver_assignments
  with ON DELETE SET NULL to preserve audit history.
- status VARCHAR(20) NOT NULL DEFAULT 'Completed' column added to
  walkaround_checks via migration_walkaround_status_v2.sql. Historical
  records receive Completed automatically.
- driver_assignments table created via migration_driver_assignments_v1.sql
  with UNIQUE constraints per driver per day and per vehicle per day.
- assignment_id automatically populated in both WalkaroundService and
  DailyLogService by querying AssignmentService before persistence.
  Null stored for historical records without active assignment.
- DraftCleanupService created in JADirect.Application/Services implementing
  BackgroundService. Runs daily at 02:00 UTC removing walkaround_checks
  records with status=Draft older than 24 hours. Uses IServiceScopeFactory
  to resolve InspectionRepository with correct DI lifetime.
- DeleteAbandonedDrafts method added to InspectionRepository.
- Microsoft.Extensions.Hosting package added to JADirect.Application.
- Microsoft.Extensions.Logging.Abstractions package added to
  JADirect.Application.
- Photo thumbnail and lightbox added to Audit History view. Photos display
  as 56x56px thumbnails inline in the item row. Clicking opens fullscreen
  lightbox with caption. Closes on X click, overlay click or Escape key.

### Changed
- WalkaroundController GET Create now resolves vehicleId from active
  assignment when SelectedVehicleId session is empty, fixing navigation
  from the new dashboard flow. Session key written here so POST and
  UploadPhoto continue to work.
- UploadPhoto endpoint now wraps PhotoService call in try-catch returning
  JSON error instead of HTML 500 page, fixing JSON.parse failure in frontend.
- UploadPhoto storageKey URL changed from route segment to query string
  (/Walkaround/Photo?storageKey=...) to prevent ASP.NET routing from
  splitting the path on forward slashes.
- PutObjectRequest in PhotoService extended with DisablePayloadSigning=true
  to fix AmazonS3Exception caused by Transfer-Encoding: chunked header
  rejection by Railway Bucket S3-compatible endpoint.
- DailyLogController GET Create now reads vehicleId from active assignment
  instead of session. Vehicle name and registration passed via ViewBag.
- Create.cshtml (DailyLog) shows read-only teal card with assigned vehicle
  at top of form. VehicleId sent as hidden input from assignment, never
  from user input.
- DailyLogRepository fully migrated from AddWithValue to typed AddParameter
  with explicit DbType. AddNullableParameter helper introduced for nullable
  columns.
- InspectionRepository fully migrated from AddWithValue to typed AddParameter.
  GetHistoryByVehicleId and GetAllHistory now filter WHERE status=Completed.
- GetVehicleCompliance method in FleetService: Van Yellow threshold corrected
  to daysSince==6 (expires tomorrow). Previously triggered at daysSince==5.
- DriverController now queries AssignmentService and FleetService on every
  dashboard request. JourneyStep calculated from database, no session
  dependency. VehicleCompliance populated for semaphore color rendering.
- Program.cs updated with AssignmentRepository, AssignmentService,
  DailyLogService and DraftCleanupService registrations.
- WalkaroundService constructor extended with AssignmentService dependency.
- DailyLogService constructor extended with AssignmentService and ILogger
  dependencies.

### Fixed
- walkaround_photos table script corrected: INT UNSIGNED replaced with INT
  to match walkaround_checks.id type, resolving FK incompatibility error
  on Railway MySQL production database.
- NeedsWalkaround hero card now displays vehicle manufacturer, model and
  registration plate, matching the Ready card layout.

### Database migrations (production — execute in order)
1. migration_walkaround_photos_v1.sql: creates walkaround_photos table
2. migration_driver_assignments_v1.sql: creates driver_assignments table
3. migration_walkaround_status_v2.sql: adds status column to walkaround_checks
4. migration_assignment_fk_v3.sql: adds assignment_id to walkaround_checks and daily_logs

---

## [2.1.0] - 2026-04-28

### Added
- WalkaroundPhoto entity created in JADirect.Domain/Entities as the central
  domain contract for photo metadata. Follows the same pattern established
  by WalkaroundCheck: any new property related to a photo must be added here
  first, before any other layer is modified.
- PhotoRepository created in JADirect.Data/Repositories with three methods:
  InsertPhoto, GetPhotosByWalkaroundId and GetPhotosByWalkaroundIds.
  Follows the ADO.NET pure pattern with AddParameter helper already
  established by UserRepository and VehicleRepository.
- PhotoService created in JADirect.Application/Services orchestrating
  Railway Bucket upload via AWSSDK.S3 and metadata persistence via
  PhotoRepository. The controller mounts the entity with known data;
  the service fills StorageKey, FileSizeKb and TakenAt internally.
- Migration script migration_walkaround_photos_v1.sql created in
  JADirect.Data/Scripts creating the walkaround_photos table with
  foreign keys to walkaround_checks (ON DELETE CASCADE) and checklist_items.
- POST /Walkaround/UploadPhoto endpoint added to WalkaroundController.
  Validates MIME type (image/jpeg and image/png only) and file size (5 MB
  maximum) before delegating to PhotoService. Returns JSON with storageKey.
- GET /Walkaround/Photo/{storageKey} endpoint added to WalkaroundController
  as an authenticated proxy. Verifies record existence in the database before
  accessing the bucket. Returns FileResult. No public bucket URL is ever
  exposed.
- Railway Bucket provisioned in project poetic-endurance as private S3-
  compatible object storage. Credentials injected as environment variables:
  RAILWAY_BUCKET_ENDPOINT, RAILWAY_BUCKET_ACCESS_KEY,
  RAILWAY_BUCKET_SECRET_KEY, RAILWAY_BUCKET_NAME.
- AWSSDK.S3 3.7.x added to JADirect.Application.
- Kestrel MaxRequestBodySize set to 6 MB in Program.cs to accommodate
  multipart overhead above the 5 MB file limit validated in the controller.
- WalkaroundId and PhotosByItemId properties added to
  WalkaroundHistoryViewModel. PhotosByItemId is a Dictionary<int, string>
  mapping ChecklistItemId to StorageKey for efficient lookup in the view.
- GetPhotosByWalkaroundIds added to PhotoRepository. Accepts a list of IDs
  and returns all photos in a single query, avoiding the N+1 problem when
  loading the full inspection history.
- History.cshtml updated to render photos inline per checklist item for
  items with state Attention or Defect. Images are served via the
  authenticated proxy endpoint, never from a direct bucket URL.

### Changed
- WalkaroundController constructor extended with PhotoService and
  PhotoRepository dependencies.
- InspectionRepository GetHistoryByVehicleId and GetAllHistory updated
  to include wc.id in SELECT. MapWalkaroundHistoryFromReader updated to
  map WalkaroundId from the result.
- History method in WalkaroundController updated to load and attach photos
  to each inspection after fetching history data.
- Program.cs updated with IAmazonS3 singleton registration using factory
  pattern reading credentials from environment variables, PhotoRepository
  scoped registration and PhotoService scoped registration with factory
  to inject bucketName as string.
- launchSettings.json added to .gitignore and removed from Git tracking
  after real bucket credentials were added locally.
  Repository visibility changed to private on GitHub.

### Pending
- walkaround_photos table not yet created in Railway production database.
  Script migration_walkaround_photos_v1.sql must be executed before deploy.
- Card 3 (photo capture in driver flow) intentionally deferred. Requires
  the Draft pattern on walkaround_checks and driver UX redesign to be
  implemented together in the next delivery.

---

## [2.0.1] - 2026-04-27

### Fixed
- WalkaroundCheck entity connected to the actual inspection flow.
  Previously the entity existed in the Domain layer but was never
  instantiated or used by any other layer.
- InspectionRepository.Add refactored to receive a WalkaroundCheck object
  and a vehicleStatusId instead of seven loose parameters.
  The hasDefect inference logic (vehicleStatusId == 4) was removed from
  the repository and replaced by the HasDefect property on the entity.
- WalkaroundService.SubmitInspection updated to instantiate WalkaroundCheck
  with all calculated data before delegating persistence to the repository.
  No business rules were changed.

### Architecture
- WalkaroundCheck entity is now the central contract for the walkaround flow.
  Any new property related to a walkaround inspection must be added to this
  entity first, before any other layer is modified.

---

## [2.0.0] - 2026-04

### Added
- Walkaround check rebuilt with three states per item: Good, Attention, Defect.
- Each flagged item requires the driver to describe what was found and select
  an action: Resolved in field or Needs garage.
- Support for three vehicle types: Van (18 items), RigidTruck (25 items),
  ArticulatedTruck (30 items). Correct checklist loaded automatically per vehicle.
- Configurable vehicle blocking policy per tenant via walkaround_blocking_rules table.
  JADirect default: Defect/Attention + RequiresGarage = blocked.
  Defect/Attention + Resolved = operational.
- ChecklistItemRepository: loads checklist items dynamically from database.
- BlockingRuleRepository: loads blocking rules per tenant from database.
- WalkaroundService: centralises all walkaround business logic.
- ChecklistItem entity and BlockingRule entity in Domain layer.
- ChecklistItemResult ViewModel as central transit object across all layers.
- Real-time status bar in walkaround form showing vehicle status before submit.
- Audit History rebuilt: each inspection now shows full item detail with state,
  action and driver note, grouped by category.
- PRODUCTION_RUNBOOK.md documenting step-by-step production deployment procedure.

### Changed
- WalkaroundController refactored to delegate all logic to WalkaroundService.
- InspectionRepository.Add updated: removed hasDefect and defectNotes parameters,
  now receives vehicleStatusId calculated by WalkaroundService.
- WalkaroundHistoryViewModel updated: removed hasDefect and DefectNotes globals,
  added Items list with VehicleWasBlocked and IsPassed calculated properties.
- InspectionRepository history methods updated to deserialise checklist JSON by item.
- VehicleType enum: Truck renamed to RigidTruck (value 2 preserved),
  ArticulatedTruck = 3 added.
- History.cshtml layout normalised to match the rest of the system visual standard.

### Fixed
- checklist_json column type changed from TEXT to MEDIUMTEXT to support
  large inspection notes without database errors.

### Database
- New table: checklist_items with 73 items for Van, RigidTruck and ArticulatedTruck.
- New table: walkaround_blocking_rules with JADirect default policy (4 rules).
- Migration script migration_walkaround_v1.sql converts all historical records
  from Pass/Fail format to new state/action/note format per item.
- Backup table walkaround_checks_backup_pre_migration created before migration.

---

## [1.2.0] - 2026-04-18

### Fixed
- Duplicate daily log submissions are now blocked at both application
  and database level.
- A driver can only submit one log per day, regardless of which vehicle was used.
- Corrected the unique constraint from (user_id + vehicle_id + date) to
  (user_id + date), closing a gap where a driver could submit logs for
  different vehicles on the same day.

### Added
- Date picker field on the Daily Log form. Defaults to today.
- Drivers can submit late entries for up to 7 days in the past.
- Future dates are blocked at both UI and service layer.

### Database
- Removed 7 duplicate records from 17 April 2026 (first operational day).
- Removed 1 test record from 18 April 2026 (Dave Tew, id 28).
- Backup table daily_logs_backup_20260417 created before any deletions.
- Replaced unique constraint uq_daily_log_user_vehicle_date
  with uq_daily_log_user_date.
- Added index idx_daily_logs_vehicle_id to maintain foreign key integrity.

### Improved
- DailyLogService created in Application layer to centralise all
  daily log business rules.
- DailyLogController refactored to delegate all business logic to the service.
- 16 CS8618 compiler warnings resolved across the Domain layer.
- Setup.sql consolidated: schema now defined inline without ALTER TABLE statements.

---

## [1.0.0] - 2026-04-17

### Added
- First production deployment.
- Driver daily log submission (deliveries, collections, returns, odometer, notes).
- Walkaround check with 27-item checklist, defect reporting and GPS location.
- Manager dashboard with performance report, compliance exceptions and audit log.
- Vehicle and user management.
- Excel export for audit log.
- Role-based access: Manager, Driver, Supervisor.
- Walkaround compliance traffic light system (Green, Yellow, Red) per vehicle type.
