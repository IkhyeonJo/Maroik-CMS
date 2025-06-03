# Maroik [https://www.maroik.com]

# Contact: admin@maroik.com

## Introduction
Maroik is a modern web application built with ASP.NET Core MVC, featuring a comprehensive set of tools for personal and business management. It includes features such as expense tracking, calendar management, bulletin boards, and role-based user management.

## How to run this project

### Ubuntu Server 22.04.3
1. Run ./Deploy.sh script
2. Go to https://localhost/ [Default setting]
   
## How to set this project

### Ubuntu Server 22.04.3
1. Configure ./Maroik.WebSite/appsettings.DockerComposeLocalDebug.json with domain name and mail settings
![screenshot 2025-06-03 22-10-50](https://github.com/user-attachments/assets/69693b80-6219-4e74-a64b-c3b217448343)

## Default Accounts

### Admin Account
- ID: admin@maroik.com
- Password: Pa$$w0rd

### User Account
- ID: demo@maroik.com
- Password: Pa$$w0rd

## Key Features

### 1. Personal Finance Management
- Expense tracking and categorization
- Income recording
- Budget management
- Financial reports and analytics
- Export to CSV/Excel

### 2. Calendar and Schedule Management
- Daily/Weekly/Monthly calendar view
- Event creation and management
- Recurring events support
- Calendar sharing between users
- Event reminders

### 3. Bulletin Board System
- Multiple board categories
- Post creation, editing, and deletion
- Comment system
- File attachments
- Search functionality

### 4. User Management
- Role-based access control (Admin/User)
- User profile management
- Session-based authentication
- Password management
- Login activity tracking

### 5. Admin Dashboard
- User management
- System settings
- Log monitoring
- Analytics dashboard

## Technologies Used

### Backend
- ASP.NET Core MVC 9.0
  - Minimal APIs
  - Hot Reload
  - Improved performance
  - Enhanced routing
  - Better dependency injection
- PostgreSQL 17
- Entity Framework Core 9.0
  - Improved query performance
  - Better async support
  - Enhanced change tracking
- RESTful API
  - OpenAPI/Swagger integration
  - API versioning
  - Rate limiting
  - Request validation
- Docker
  - Multi-stage builds
  - Container orchestration
  - Environment isolation
- Authentication & Authorization
  - Session Authentication
  - JWT Authentication
  - Role-based access control
  - OAuth2/OpenID Connect
- Cross-Origin Resource Sharing (CORS)
- API Documentation
  - Swagger/OpenAPI
  - API Explorer
  - API versioning support

### Frontend
- AdminLTE 3
- Bootstrap 5.3
- jQuery 3.7
- HTML5/CSS3
- JavaScript ES2024
- NonfactorGrid
- Chart.js 4.4
- Font Awesome 6
- DataTables
- SweetAlert2

### Development Tools
- Visual Studio 2022
- JetBrains Rider
- Git
- Docker Compose
- Postman

## Project Structure
```
Maroik/
├── Maroik.AI/              # AI-related features
├── Maroik.Common/          # Common data access and utilities
├── Maroik.Crontab/         # Scheduled tasks management
├── Maroik.DB/             # Database configuration
├── Maroik.DeployOps/      # Deployment scripts
├── Maroik.FileStorage/    # File storage management
├── Maroik.Log/            # Logging system
├── Maroik.SSL/            # SSL/TLS certificate management
├── Maroik.WebAPI/         # RESTful API endpoints
└── Maroik.WebSite/        # Frontend website
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK or later
- Docker and Docker Compose
- Visual Studio 2022 (Windows) or JetBrains Rider (Linux/Mac)

### Running the Project

#### Windows
1. Install Visual Studio 2022 or higher
2. Open Maroik.sln
3. Set docker-compose as startup project
4. Press F5 to run in debug mode

#### Linux/Mac
1. Install JetBrains Rider
2. Start debug mode with docker-compose.debug.yml
3. Configure mail settings in appsettings files

## Screenshots

### Login
![Login](https://user-images.githubusercontent.com/20404991/132020270-488a1ab7-448c-44d9-938a-40ce32d6d364.jpg)

### User Dashboard
![User-DashBoard](https://user-images.githubusercontent.com/20404991/132020299-e5adb366-9041-44f9-ad56-f2bb606028d5.jpg)

### NonfactorGrid
![NonfactorGrid](https://user-images.githubusercontent.com/20404991/132020455-e66897ef-ece8-4e71-b323-6ebb72f6b110.jpg)

### User Profile
![UserProfile](https://user-images.githubusercontent.com/20404991/132020484-4b633287-a1b1-48b0-8340-ae3ead83235a.jpg)

### WebAPI
![WebAPI](https://user-images.githubusercontent.com/20404991/132020514-13951172-3bcd-48a5-bfe0-a8328cdb766a.jpg)

## Contributing
1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Support
For issues and questions, please use the GitHub issue tracker.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Special Notes
- All backup files are automatically sent via email and can overwrite upload folders and SQL script folders when needed.
- Mail account configuration is required for deployment.
