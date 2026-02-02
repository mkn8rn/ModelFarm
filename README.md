## Prerequisites

- .NET 10 SDK
- PostgreSQL database
- (Optional) CUDA-capable GPU for faster training

## Getting Started

### 1. Database Setup

Create a PostgreSQL database and update the connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=modelfarm;Username=postgres;Password=yourpassword"
  }
}
```

### 2. Apply Migrations

```bash
cd ModelFarm.Infrastructure
dotnet ef database update --context ApplicationDbContext --startup-project ../ModelFarm.Web
dotnet ef database update --context MarketDataDbContext --startup-project ../ModelFarm.Web
```

### 3. Run the Application

```bash
cd ModelFarm.Web
dotnet run
```

Navigate to `https://localhost:7030` (or the configured port).
