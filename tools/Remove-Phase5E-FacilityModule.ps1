$obsoletePaths = @(
  ".\DARAK.Api\Controllers\Admin\AdminFacilitiesController.cs",
  ".\DARAK.Api\Controllers\Facilities",
  ".\DARAK.Api\DTOs\Facilities",
  ".\DARAK.Api\DTOs\Structure\CreateFacilityRequest.cs",
  ".\DARAK.Api\DTOs\Structure\FacilityResponse.cs",
  ".\DARAK.Api\DTOs\Structure\FacilitySearchQuery.cs",
  ".\DARAK.Api\DTOs\Structure\UpdateFacilityRequest.cs",
  ".\DARAK.Api\Entities\Facilities",
  ".\DARAK.Api\Entities\Structure\Facility.cs",
  ".\DARAK.Api\Enums\Facilities",
  ".\DARAK.Api\Enums\Structure\FacilityType.cs",
  ".\DARAK.Api\Interfaces\Facilities",
  ".\DARAK.Api\Services\Facilities",
  ".\DARAK.Tests\FacilityPaymentServiceTests.cs",
  ".\DARAK.Tests\FacilityReservationDeepTests.cs"
)

foreach ($path in $obsoletePaths) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
        Write-Host "Deleted: $path"
    } else {
        Write-Host "Already missing: $path"
    }
}
