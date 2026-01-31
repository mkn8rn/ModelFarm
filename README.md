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

## Usage

### 1. Create a Dataset
1. Go to **Datasets** page
2. Select data source (e.g., Binance)
3. Enter symbol (e.g., BTCUSDT), interval, and date range
4. Click "Create Dataset" to start downloading

### 2. Create a Configuration
1. Go to **Configurations** page
2. Select "Quant Strategy" type
3. Choose your dataset and model type
4. Configure training parameters, checkpoint settings, and retry behavior
5. Set performance requirements
6. Click "Create Configuration"

### 3. Start Training
1. Go to **Jobs** page
2. Select a configuration
3. Click "Start Training"
4. Monitor progress in real-time

### 4. View Results
- Click the chart icon on completed jobs to view detailed results
- See training metrics, performance metrics, and trade statistics
- Check if the model meets your performance requirements

## Model Checkpoints

When enabled, model checkpoints are saved to the `checkpoints/` directory:
- `checkpoints/{jobId}/current_model.bin` - Current model weights
- `checkpoints/{jobId}/best_model.bin` - Best model weights (lowest validation loss)
- `checkpoints/{jobId}/checkpoint.json` - Metadata (epoch, loss, normalization stats)