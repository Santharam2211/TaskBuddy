# TimeBuddy – Collaborative Task Scheduling & Notification System

A full-stack web application designed for personal and team task organization, collaborative schedule management, deadline tracking, and automated email notifications.

---

## 🛠️ Technology Stack

- **Frontend:** Angular 20, TypeScript, HTML5, CSS3, Bootstrap 5, Bootstrap Icons
- **Backend:** C#, ASP.NET Core 8 Web API, Entity Framework Core 8, Pomelo MySQL Provider, JWT Bearer Authentication
- **Database:** MySQL 8.3 (WampServer / phpMyAdmin compatible)
- **Email Service:** Brevo API (Transactional HTTP API v3) + MailKit fallback
- **Background Processing:** ASP.NET Core `BackgroundService` (`ScheduleMonitorService`) running every 60 seconds

---

## 📂 Project Structure

```text
TaskBuddy/
├── TimeBuddy.Backend/
│   ├── BackgroundServices/
│   │   └── ScheduleMonitorService.cs    # Periodic background task monitor (every 1 min)
│   ├── Controllers/
│   │   ├── AuthController.cs            # Register, Login, Me
│   │   ├── FriendsController.cs         # Friends CRUD & Requests
│   │   ├── SchedulesController.cs       # Schedules & Shared tasks CRUD
│   │   ├── NotificationsController.cs   # In-app notifications
│   │   └── DashboardController.cs       # Metrics & summaries
│   ├── Data/
│   │   └── ApplicationDbContext.cs      # EF Core DbContext with constraints & indexes
│   ├── DTOs/
│   │   ├── RegisterDto.cs & LoginDto.cs
│   │   ├── ScheduleDto.cs & ParticipantDto.cs
│   │   └── DashboardDto.cs & FriendRequestDto.cs
│   ├── Middleware/
│   │   └── ExceptionHandlingMiddleware.cs
│   ├── Migrations/                      # EF Core MySQL migrations
│   ├── Models/
│   │   ├── User.cs
│   │   ├── FriendRequest.cs
│   │   ├── Schedule.cs
│   │   ├── ScheduleParticipant.cs
│   │   ├── Notification.cs
│   │   └── EmailLog.cs
│   ├── Services/
│   │   ├── AuthService.cs               # BCrypt + JWT generation
│   │   ├── FriendService.cs             # Friend requests & search
│   │   ├── ScheduleService.cs           # Task management & participant logic
│   │   ├── NotificationService.cs       # In-app notifications
│   │   └── EmailService.cs              # Brevo transactional emails + HTML templates
│   ├── Program.cs                       # Dependency injection, CORS, Swagger JWT
│   └── appsettings.json                 # Connection strings & Brevo credentials
│
├── timebuddy-frontend/
│   ├── src/
│   │   ├── app/
│   │   │   ├── auth/                    # Login & Register components
│   │   │   ├── dashboard/               # Metric cards, upcoming deadlines, activity
│   │   │   ├── schedules/               # Schedules CRUD, filters, tabs, status badges
│   │   │   ├── calendar/                # Month, Week, and Day views
│   │   │   ├── friends/                 # Search users, friend requests, accepted list
│   │   │   ├── notifications/           # In-app alerts history & mark as read
│   │   │   ├── shared/components/       # Reusable Quick Task Modal
│   │   │   ├── core/
│   │   │   │   ├── guards/              # auth.guard & guest.guard
│   │   │   │   ├── interceptors/        # auth.interceptor (JWT injection)
│   │   │   │   ├── models/              # TypeScript interfaces
│   │   │   │   └── services/            # Angular HTTP API services
│   │   │   ├── app.ts & app.html        # App layout with Sidebar & Topbar
│   │   │   └── app.routes.ts            # Angular client routes
│   │   └── styles.css                   # Custom theme & responsive styling
│
├── start-all.bat                        # Starts both Backend and Frontend
├── start-backend.bat                    # Starts ASP.NET Core API on port 5000
└── start-frontend.bat                   # Starts Angular dev server on port 4200
```

---

## 💾 Database Design (`timebuddy_db` in MySQL / phpMyAdmin)

The database contains the following relational tables configured with foreign keys and unique constraints:

1. **`users`**
   - `UserId` (PK, Auto-Increment)
   - `FullName`, `Email` (Unique index), `PasswordHash` (BCrypt), `CreatedAt`
2. **`friendrequests`**
   - `RequestId` (PK)
   - `SenderId` (FK), `ReceiverId` (FK), `Status` (`Pending`, `Accepted`, `Rejected`), `CreatedAt`, `UpdatedAt`
3. **`schedules`**
   - `ScheduleId` (PK)
   - `CreatedBy` (FK to Users), `TaskTitle`, `Description`, `StartTime`, `EndTime`, `Priority` (`Low`, `Medium`, `High`)
   - `ScheduleType` (`Personal`, `Shared`), `Status` (`Scheduled`, `InProgress`, `Overdue`, `Completed`, `Cancelled`)
   - `CompletedBy` (Nullable FK to Users), `CompletedAt`, `CreatedAt`, `UpdatedAt`
4. **`scheduleparticipants`**
   - `ParticipantId` (PK)
   - `ScheduleId` (FK), `UserId` (FK), `InvitationStatus`, `JoinedAt`
   - Unique composite index on `(ScheduleId, UserId)` to prevent duplicates.
5. **`notifications`**
   - `NotificationId` (PK)
   - `UserId` (FK), `ScheduleId` (Nullable FK), `NotificationType`, `Message`, `IsRead`, `CreatedAt`
6. **`emaillogs`**
   - `EmailLogId` (PK)
   - `ScheduleId` (Nullable FK), `UserId` (FK), `NotificationType`, `DeliveryStatus` (`Sent`/`Failed`), `SentAt`, `ErrorMessage`
   - Unique composite index on `(ScheduleId, UserId, NotificationType)` for **idempotency** (never duplicate emails).

---

## 🚀 Running the Application

### Option A: Using the One-Click Scripts
Double-click:
```bash
start-all.bat
```
This automatically launches:
- **Backend API:** `http://localhost:5000` (Swagger UI: `http://localhost:5000/swagger`)
- **Frontend App:** `http://localhost:4200`

### Option B: Running Manually

1. **Start the Backend API:**
   ```powershell
   cd d:\MiniProject\TaskBuddy\TimeBuddy.Backend
   dotnet run --launch-profile http
   ```
2. **Start the Angular Frontend:**
   ```powershell
   cd d:\MiniProject\TaskBuddy\timebuddy-frontend
   npm start
   ```
3. Open your browser and navigate to:
   **[http://localhost:4200](http://localhost:4200)**

---

## 🧪 Verification & Features Checklist

| Feature | Module | Status | Verification Detail |
|---|---|---|---|
| User Registration & Login | Module 1 | Verified | BCrypt password hashing, duplicate email rejection, JWT Bearer |
| Friend Management | Module 2 | Verified | Email search, requests list, accept/reject, self-request prevention |
| Personal Schedule CRUD | Module 3 | Verified | Create/Read/Update/Delete, priority, time validation, status badges |
| Shared Collaboration | Module 4 | Verified | Only accepted friends invited, unauthorized users blocked, participant completion |
| Automatic Email Notifications | Module 5 | Verified | Brevo API dispatch for Deadlines & Completions, logged in `emaillogs` |
| Background Schedule Monitoring | Module 6 | Verified | ASP.NET Core `BackgroundService` runs every 60s, updates past tasks to `Overdue` |
| Interactive Dashboard | Module 7 | Verified | Metric summary cards, upcoming deadlines, quick create modal |
| Visual Calendar | Module 8 | Verified | Month, Week, and Day views with status-colored indicators |
| In-App Notification Center | Module 9 | Verified | Unread count badge, filter by type, mark as read / mark all as read |
