using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ServiceVendorService(ApplicationDbContext dbContext)
    : IServiceVendorService
{
    private const int MaxNameLength = 150;
    private const int MaxPhoneLength = 30;
    private const int MaxEmailLength = 256;
    private const int MaxAddressLength = 300;
    private const int MaxNoteLength = 1000;

    public async Task<PagedResult<ServiceVendorResponse>> SearchServiceVendorsAsync(
        ServiceVendorQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var vendors = ApplyVendorFilters(dbContext.ServiceVendors.AsNoTracking(), query);
        var totalCount = await vendors.CountAsync(cancellationToken);
        var items = await vendors
            .OrderBy(vendor => vendor.Name)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(vendor => new ServiceVendorResponse(
                vendor.Id,
                vendor.Name,
                vendor.ContactPersonName,
                vendor.PhoneNumber,
                vendor.Email,
                vendor.ServiceType,
                vendor.Status,
                vendor.Address,
                vendor.Notes,
                vendor.CreatedAtUtc,
                vendor.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ServiceVendorResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<ServiceResult<ServiceVendorResponse>> GetServiceVendorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var vendor = await dbContext.ServiceVendors
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return vendor is null
            ? ServiceResult<ServiceVendorResponse>.NotFound("Service vendor was not found.")
            : ServiceResult<ServiceVendorResponse>.Success(ToServiceVendorResponse(vendor));
    }

    public async Task<ServiceResult<ServiceVendorResponse>> CreateServiceVendorAsync(
        CreateServiceVendorRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateServiceVendorRequest(
            request.Name,
            request.ContactPersonName,
            request.PhoneNumber,
            request.Email,
            request.ServiceType,
            request.Status,
            request.Address,
            request.Notes);
        if (validation is not null)
        {
            return ToResult<ServiceVendorResponse>(validation);
        }

        var vendor = new ServiceVendor
        {
            Name = request.Name.Trim(),
            ContactPersonName = TrimOrNull(request.ContactPersonName),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = TrimOrNull(request.Email),
            ServiceType = request.ServiceType,
            Status = request.Status,
            Address = TrimOrNull(request.Address),
            Notes = TrimOrNull(request.Notes)
        };

        dbContext.ServiceVendors.Add(vendor);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ServiceVendorResponse>.Success(ToServiceVendorResponse(vendor));
    }

    public async Task<ServiceResult<ServiceVendorResponse>> UpdateServiceVendorAsync(
        Guid id,
        UpdateServiceVendorRequest request,
        CancellationToken cancellationToken = default)
    {
        var vendor = await dbContext.ServiceVendors
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (vendor is null)
        {
            return ServiceResult<ServiceVendorResponse>.NotFound("Service vendor was not found.");
        }

        var validation = ValidateServiceVendorRequest(
            request.Name,
            request.ContactPersonName,
            request.PhoneNumber,
            request.Email,
            request.ServiceType,
            request.Status,
            request.Address,
            request.Notes);
        if (validation is not null)
        {
            return ToResult<ServiceVendorResponse>(validation);
        }

        vendor.Name = request.Name.Trim();
        vendor.ContactPersonName = TrimOrNull(request.ContactPersonName);
        vendor.PhoneNumber = request.PhoneNumber.Trim();
        vendor.Email = TrimOrNull(request.Email);
        vendor.ServiceType = request.ServiceType;
        vendor.Status = request.Status;
        vendor.Address = TrimOrNull(request.Address);
        vendor.Notes = TrimOrNull(request.Notes);
        vendor.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ServiceVendorResponse>.Success(ToServiceVendorResponse(vendor));
    }

    public async Task<ServiceResult<ServiceVendorResponse>> SetServiceVendorStatusAsync(
        Guid id,
        VendorStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            return ServiceResult<ServiceVendorResponse>.BadRequest("Vendor status is invalid.");
        }

        var vendor = await dbContext.ServiceVendors
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (vendor is null)
        {
            return ServiceResult<ServiceVendorResponse>.NotFound("Service vendor was not found.");
        }

        vendor.Status = status;
        vendor.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ServiceVendorResponse>.Success(ToServiceVendorResponse(vendor));
    }

    private static IQueryable<ServiceVendor> ApplyVendorFilters(
        IQueryable<ServiceVendor> query,
        ServiceVendorQueryRequest request)
    {
        if (request.ServiceType.HasValue)
        {
            query = query.Where(item => item.ServiceType == request.ServiceType.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Status == request.Status.Value);
        }

        var searchTerm = TrimOrNull(request.SearchTerm);
        if (searchTerm is not null)
        {
            query = query.Where(item => item.Name.Contains(searchTerm)
                || item.PhoneNumber.Contains(searchTerm)
                || (item.ContactPersonName != null && item.ContactPersonName.Contains(searchTerm))
                || (item.Email != null && item.Email.Contains(searchTerm))
                || (item.Address != null && item.Address.Contains(searchTerm)));
        }

        return query;
    }

    private static ValidationFailure? ValidateServiceVendorRequest(
        string name,
        string? contactPersonName,
        string phoneNumber,
        string? email,
        VendorServiceType serviceType,
        VendorStatus status,
        string? address,
        string? notes)
    {
        if (TrimOrNull(name) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor name is too long.");
        }

        if (TrimOrNull(phoneNumber) is null)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor phone number is required.");
        }

        if (phoneNumber.Trim().Length > MaxPhoneLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor phone number is too long.");
        }

        if (!Enum.IsDefined(serviceType))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor service type is invalid.");
        }

        if (!Enum.IsDefined(status))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor status is invalid.");
        }

        if (TrimOrNull(contactPersonName)?.Length > MaxNameLength
            || TrimOrNull(email)?.Length > MaxEmailLength
            || TrimOrNull(address)?.Length > MaxAddressLength
            || TrimOrNull(notes)?.Length > MaxNoteLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Vendor metadata contains a value that is too long.");
        }

        return null;
    }

    private static ServiceVendorResponse ToServiceVendorResponse(ServiceVendor vendor)
    {
        return new ServiceVendorResponse(
            vendor.Id,
            vendor.Name,
            vendor.ContactPersonName,
            vendor.PhoneNumber,
            vendor.Email,
            vendor.ServiceType,
            vendor.Status,
            vendor.Address,
            vendor.Notes,
            vendor.CreatedAtUtc,
            vendor.UpdatedAtUtc);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validationFailure)
    {
        return validationFailure.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validationFailure.Message),
            _ => ServiceResult<T>.BadRequest(validationFailure.Message)
        };
    }

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
