$obsoleteControllerFiles = @(
    "DARAK.Api\Controllers\Admin\AdminBuildingsController.cs",
    "DARAK.Api\Controllers\Admin\AdminFloorsController.cs",
    "DARAK.Api\Controllers\Admin\AdminPropertyUnitsController.cs",
    "DARAK.Api\Controllers\Admin\AdminParkingSpotsController.cs",
    "DARAK.Api\Controllers\Admin\AdminCompoundServicesController.cs",
    "DARAK.Api\Controllers\Admin\AdminBillingCyclesController.cs",
    "DARAK.Api\Controllers\Admin\AdminUtilityBillsController.cs",
    "DARAK.Api\Controllers\Admin\AdminMeterReadingsController.cs",
    "DARAK.Api\Controllers\Admin\AdminViolationFinesController.cs",
    "DARAK.Api\Controllers\AnalyticsController.cs"
)

foreach ($file in $obsoleteControllerFiles) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "Removed $file"
    }
}
